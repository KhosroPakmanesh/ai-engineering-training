using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;

public static class ProfileValidator
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static CandidateProfile ParseAndValidate(string json, string resume, string sourceFile)
    {
        // Shape comes first: malformed JSON, duplicate keys, missing fields, and wrong types fail here.
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "profile.schema.json")));
        EvaluationResults evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!evaluation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, SchemaErrors(evaluation)));

        CandidateProfile profile = JsonSerializer.Deserialize<CandidateProfile>(json, JsonOptions)!;
        var errors = new List<string>();

        // A schema checks structure; these checks concern the extracted data.
        CheckEvidence(profile.Evidence, "", profile.Name, profile.Email, profile.Phone);
        for (int i = 0; i < profile.Experience.Length; i++)
        {
            Experience item = profile.Experience[i];
            string path = $"/experience/{i}";
            CheckEvidence(item.Evidence, path, item.Employer, item.Role, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
            if (item.DurationMonths is int months && !Regex.IsMatch(item.Evidence.Quote, $@"\b{months}\s+months?\b", RegexOptions.IgnoreCase))
                errors.Add($"{path}/durationMonths: duration must be explicitly stated in months, or null.");
        }
        for (int i = 0; i < profile.Education.Length; i++)
        {
            Education item = profile.Education[i];
            string path = $"/education/{i}";
            CheckEvidence(item.Evidence, path, item.Institution, item.Degree, item.FieldOfStudy, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
        }
        for (int i = 0; i < profile.Skills.Length; i++)
            CheckEvidence(profile.Skills[i].Evidence, $"/skills/{i}", profile.Skills[i].Name, profile.Skills[i].Proficiency);

        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return profile with
        {
            Skills = profile.Skills.Select(skill => skill with { Name = skill.Name.Trim() })
                .DistinctBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToArray()
        };

        void CheckEvidence(Evidence evidence, string path, params string?[] facts)
        {
            if (evidence.Source != sourceFile)
                errors.Add($"{path}/evidence/source: expected the supplied CV filename.");
            if (string.IsNullOrWhiteSpace(evidence.Quote) || !resume.Contains(evidence.Quote, StringComparison.Ordinal))
                errors.Add($"{path}/evidence/quote: expected an exact, nonempty span of the CV.");
            foreach (string? fact in facts)
                if (fact is not null && (string.IsNullOrWhiteSpace(fact) || !evidence.Quote.Contains(fact.Trim(), StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"{path}/evidence/quote: an extracted fact is absent from its evidence.");
        }

        void CheckDates(string? start, string? end, string path)
        {
            var startRange = DateRange(start, path + "/startDate");
            var endRange = DateRange(end, path + "/endDate");
            if (startRange.HasValue && endRange.HasValue && endRange.Value.Max < startRange.Value.Min)
                errors.Add($"{path}/endDate: end date precedes start date.");
        }

        (DateOnly Min, DateOnly Max)? DateRange(string? date, string path)
        {
            if (date is null) return null;
            string fullDate = date.Length switch { 4 => date + "-01-01", 7 => date + "-01", _ => date };
            if (!Regex.IsMatch(date, @"^\d{4}(-\d{2})?(-\d{2})?$") ||
                !DateOnly.TryParseExact(fullDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly min))
            {
                errors.Add($"{path}: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.");
                return null;
            }
            // Bounds compare chronology without adding guessed date parts to the profile.
            DateOnly max = date.Length switch
            {
                4 => new(min.Year, 12, 31),
                7 => new(min.Year, min.Month, DateTime.DaysInMonth(min.Year, min.Month)),
                _ => min
            };
            return (min, max);
        }
    }

    private static IEnumerable<string> SchemaErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (var error in result.Errors)
                yield return $"{(result.InstanceLocation.ToString() == "" ? "$" : result.InstanceLocation)}: {error.Value}";
        if (result.Details is not null)
            foreach (EvaluationResults detail in result.Details)
                foreach (string error in SchemaErrors(detail)) yield return error;
    }
}
