using System.Globalization;
using System.Text.RegularExpressions;

// One validation pass shares its source text and error list across the profile's items.
internal sealed class ProfileChecks(string resume, string sourceFile)
{
    private readonly List<string> errors = [];

    // A schema checks structure; these checks concern the extracted facts.
    public void Validate(CandidateProfile profile)
    {
        CheckEvidence(profile.Evidence, "", profile.Name, profile.Email, profile.Phone);
        CheckExperience(profile.Experience);
        CheckEducation(profile.Education);
        CheckSkills(profile.Skills);

        // Collect errors in source order rather than stopping at the first invalid fact.
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }

    // Employment facts need supporting quotes, valid chronology, and explicitly stated durations.
    private void CheckExperience(Experience[] experience)
    {
        for (int index = 0; index < experience.Length; index++)
        {
            Experience item = experience[index];
            string path = $"/experience/{index}";
            CheckEvidence(item.Evidence, path, item.Employer, item.Role, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
            if (item.DurationMonths is int months && !Regex.IsMatch(item.Evidence.Quote, $@"\b{months}\s+months?\b", RegexOptions.IgnoreCase))
                errors.Add($"{path}/durationMonths: duration must be explicitly stated in months, or null.");
        }
    }

    // Education has its own source evidence and date range.
    private void CheckEducation(Education[] education)
    {
        for (int index = 0; index < education.Length; index++)
        {
            Education item = education[index];
            string path = $"/education/{index}";
            CheckEvidence(item.Evidence, path, item.Institution, item.Degree, item.FieldOfStudy, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
        }
    }

    // Skill names and any stated proficiency must occur in the item's quote.
    private void CheckSkills(Skill[] skills)
    {
        for (int index = 0; index < skills.Length; index++)
            CheckEvidence(skills[index].Evidence, $"/skills/{index}", skills[index].Name, skills[index].Proficiency);
    }

    // Exact source spans prevent invented quotations; text matching still needs manual meaning checks.
    private void CheckEvidence(Evidence evidence, string path, params string?[] facts)
    {
        if (evidence.Source != sourceFile)
            errors.Add($"{path}/evidence/source: expected the supplied CV filename.");
        if (string.IsNullOrWhiteSpace(evidence.Quote) || !resume.Contains(evidence.Quote, StringComparison.Ordinal))
            errors.Add($"{path}/evidence/quote: expected an exact, nonempty span of the CV.");
        foreach (string? fact in facts)
            if (fact is not null && (string.IsNullOrWhiteSpace(fact) || !evidence.Quote.Contains(fact.Trim(), StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{path}/evidence/quote: an extracted fact is absent from its evidence.");
    }

    // Compare the possible date ranges without inventing missing months or days.
    private void CheckDates(string? start, string? end, string path)
    {
        var startRange = DateRange(start, path + "/startDate");
        var endRange = DateRange(end, path + "/endDate");
        if (startRange.HasValue && endRange.HasValue && endRange.Value.Max < startRange.Value.Min)
            errors.Add($"{path}/endDate: end date precedes start date.");
    }

    // A partial ISO date describes a range; those bounds never replace the original profile value.
    private (DateOnly Min, DateOnly Max)? DateRange(string? date, string path)
    {
        if (date is null) return null;
        string fullDate = date.Length switch { 4 => date + "-01-01", 7 => date + "-01", _ => date };
        if (!Regex.IsMatch(date, @"^\d{4}(-\d{2})?(-\d{2})?$") ||
            !DateOnly.TryParseExact(fullDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly min))
        {
            errors.Add($"{path}: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.");
            return null;
        }
        DateOnly max = date.Length switch
        {
            4 => new(min.Year, 12, 31),
            7 => new(min.Year, min.Month, DateTime.DaysInMonth(min.Year, min.Month)),
            _ => min
        };
        return (min, max);
    }
}
