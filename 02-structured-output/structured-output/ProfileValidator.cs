using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

public static class ProfileValidator
{
    private static string _rawStringResume = string.Empty;
    private static string _resumeFileName = string.Empty;
    private static readonly List<string> errors = [];

    // Check the profile against the resume and report all failed checks together.
    public static void ValidateProfile(CandidateProfile profile, string rawStringResume, string resumeFileName)
    {
        errors.Clear();

        _rawStringResume = rawStringResume;
        _resumeFileName = resumeFileName;

        CheckEvidence(profile.Evidence, "", profile.Name, profile.Email, profile.Phone);
        CheckExperience(profile.Experience);
        CheckEducation(profile.Education);
        CheckSkills(profile.Skills);

        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
    // Check job dates against their quotes; a duration must be stated in months, not calculated.
    private static void CheckExperience(List<Experience> experience)
    {
        for (int index = 0; index < experience.Count; index++)
        {
            Experience item = experience[index];
            string path = $"/experience/{index}";
            CheckEvidence(item.Evidence, path, item.Employer, item.Role, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
            if (item.DurationMonths is int months && !Regex.IsMatch(item.Evidence.Quote, $@"\b{months}\s+months?\b", RegexOptions.IgnoreCase))
                errors.Add($"{path}/durationMonths: duration must be explicitly stated in months, or null.");
        }
    }
    // Check each education entry's facts and dates against its own resume quote.
    private static void CheckEducation(List<Education> education)
    {
        for (int index = 0; index < education.Count; index++)
        {
            Education item = education[index];
            string path = $"/education/{index}";
            CheckEvidence(item.Evidence, path, item.Institution, item.Degree, item.FieldOfStudy, item.StartDate, item.EndDate);
            CheckDates(item.StartDate, item.EndDate, path);
        }
    }
    // A skill level must appear in the quote; otherwise it must stay null.
    private static void CheckSkills(List<Skill> skills)
    {
        for (int index = 0; index < skills.Count; index++)
            CheckEvidence(skills[index].Evidence, $"/skills/{index}", skills[index].Name, skills[index].Proficiency);
    }
    // Each item needs an exact resume quote that contains all of its known facts.
    private static void CheckEvidence(Evidence evidence, string path, params List<string?> facts)
    {
        if (evidence.Source != _resumeFileName)
            errors.Add($"{path}/evidence/source: expected the supplied CV filename.");
        if (string.IsNullOrWhiteSpace(evidence.Quote) || !_rawStringResume.Contains(evidence.Quote, StringComparison.Ordinal))
            errors.Add($"{path}/evidence/quote: expected an exact, nonempty span of the CV.");
        foreach (string? fact in facts)
            if (fact is not null && (string.IsNullOrWhiteSpace(fact) || !evidence.Quote.Contains(fact.Trim(), StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{path}/evidence/quote: an extracted fact is absent from its evidence.");
    }
    // Reject an end date only when it is earlier than every possible start date.
    private static void CheckDates(string? start, string? end, string path)
    {
        var startRange = DateRange(start, path + "/startDate");
        var endRange = DateRange(end, path + "/endDate");
        if (startRange.HasValue && endRange.HasValue && endRange.Value.Max < startRange.Value.Min)
            errors.Add($"{path}/endDate: end date precedes start date.");
    }
    // Treat a year or month as a date range so missing date parts stay unknown in the profile.
    private static (DateOnly Min, DateOnly Max)? DateRange(string? date, string path)
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
