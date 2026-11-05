using System.Globalization;
using System.Text.RegularExpressions;

public class ProfileValidator
{
    private readonly string rawStringResume;
    private readonly string resumeFileNameString;
    private readonly List<string> errorStrings = [];

    // Store the source once so every check uses the same resume and filename.
    public ProfileValidator(
        string rawStringResume,
        string resumeFileNameString)
    {
        this.rawStringResume = rawStringResume;
        this.resumeFileNameString = resumeFileNameString;
    }

    // Report all failed checks together before the profile can reach normalization.
    public void Validate(CandidateProfile candidateProfile)
    {
        CheckEvidence(
            candidateProfile.Evidence,
            "",
            candidateProfile.NameString,
            candidateProfile.EmailString,
            candidateProfile.PhoneString);
        CheckExperience(candidateProfile.Experiences);
        CheckEducation(candidateProfile.Educations);
        CheckSkills(candidateProfile.Skills);

        if (errorStrings.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                errorStrings));
        }
    }

    // Keep each call's source and errors separate so different profiles can be checked at the same time.
    public static void ValidateProfile(
        CandidateProfile candidateProfile,
        string rawStringResume,
        string resumeFileNameString)
    {
        new ProfileValidator(
            rawStringResume,
            resumeFileNameString).Validate(candidateProfile);
    }

    // Check job dates against their quotes; a duration must be stated in months, not calculated.
    private void CheckExperience(List<Experience> experiences)
    {
        for (int indexInt = 0; indexInt < experiences.Count; indexInt++)
        {
            Experience itemExperience = experiences[indexInt];
            string pathString = $"/experience/{indexInt}";

            CheckEvidence(
                itemExperience.Evidence,
                pathString,
                itemExperience.EmployerString,
                itemExperience.RoleString,
                itemExperience.StartDateString,
                itemExperience.EndDateString);

            CheckDates(
                itemExperience.StartDateString,
                itemExperience.EndDateString,
                pathString);

            if (itemExperience.DurationMonthsInt is int monthsInt && !Regex.IsMatch(
                itemExperience.Evidence.QuoteString,
                $@"\b{monthsInt}\s+months?\b",
                RegexOptions.IgnoreCase))
            {
                errorStrings.Add(
                    $"{pathString}/durationMonths: duration must be explicitly stated in months, or null.");
            }
        }
    }

    // Check each education entry's facts and dates against its own resume quote.
    private void CheckEducation(List<Education> educations)
    {
        for (int indexInt = 0; indexInt < educations.Count; indexInt++)
        {
            Education itemEducation = educations[indexInt];
            string pathString = $"/education/{indexInt}";

            CheckEvidence(
                itemEducation.Evidence,
                pathString,
                itemEducation.InstitutionString,
                itemEducation.DegreeString,
                itemEducation.FieldOfStudyString,
                itemEducation.StartDateString,
                itemEducation.EndDateString);

            CheckDates(
                itemEducation.StartDateString,
                itemEducation.EndDateString,
                pathString);
        }
    }

    // A skill level must appear in the quote; otherwise it must stay null.
    private void CheckSkills(List<Skill> skills)
    {
        for (int indexInt = 0; indexInt < skills.Count; indexInt++)
        {
            CheckEvidence(
                skills[indexInt].Evidence,
                $"/skills/{indexInt}",
                skills[indexInt].NameString,
                skills[indexInt].ProficiencyString);
        }
    }

    // Each item needs an exact resume quote that contains all of its known facts.
    private void CheckEvidence(
        Evidence evidence,
        string pathString,
        params List<string?> factStrings)
    {
        if (evidence.SourceString != resumeFileNameString)
        {
            errorStrings.Add($"{pathString}/evidence/source: expected the supplied CV filename.");
        }

        if (string.IsNullOrWhiteSpace(evidence.QuoteString) ||
            !rawStringResume.Contains(
                evidence.QuoteString,
                StringComparison.Ordinal))
        {
            errorStrings.Add($"{pathString}/evidence/quote: expected an exact, nonempty span of the CV.");
        }

        foreach (string? factString in factStrings)
        {
            if (factString is not null && (string.IsNullOrWhiteSpace(factString) ||
                        !evidence.QuoteString.Contains(
                            factString.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
            {
                errorStrings.Add($"{pathString}/evidence/quote: an extracted fact is absent from its evidence.");
            }
        }
    }

    // Reject an end date only when it is earlier than every possible start date.
    private void CheckDates(
        string? startString,
        string? endString,
        string pathString)
    {
        var startRangeValueTuple = DateRange(
            startString,
            pathString + "/startDate");
        var endRangeValueTuple = DateRange(
            endString,
            pathString + "/endDate");
        if (startRangeValueTuple.HasValue && endRangeValueTuple.HasValue
            && endRangeValueTuple.Value.MaximumDateOnly < startRangeValueTuple.Value.MinimumDateOnly)
        {
            errorStrings.Add($"{pathString}/endDate: end date precedes start date.");
        }
    }

    // Treat a year or month as a date range so missing date parts stay unknown in the profile.
    private (DateOnly MinimumDateOnly, DateOnly MaximumDateOnly)? DateRange(
        string? dateString,
        string pathString)
    {
        if (dateString is null)
        {
            return null;
        }

        string fullDateString = dateString.Length switch
        {
            4 => dateString + "-01-01",
            7 => dateString + "-01",
            _ => dateString
        };

        if (!Regex.IsMatch(
            dateString,
            @"^\d{4}(-\d{2})?(-\d{2})?$") ||
            !DateOnly.TryParseExact(
                fullDateString,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly minDateOnly))
        {
            errorStrings.Add($"{pathString}: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.");
            return null;
        }

        DateOnly maxDateOnly = dateString.Length switch
        {
            4 => new(
                minDateOnly.Year,
                12,
                31),
            7 => new(
                minDateOnly.Year,
                minDateOnly.Month,
                DateTime.DaysInMonth(
                    minDateOnly.Year,
                    minDateOnly.Month)),
            _ => minDateOnly
        };

        return (minDateOnly, maxDateOnly);
    }
}
