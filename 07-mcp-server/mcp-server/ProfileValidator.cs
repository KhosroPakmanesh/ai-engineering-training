namespace ResumeMcpServer;

public static class ProfileValidator
{
    // Collect independent source errors so the learner can fix the file in one pass.
    public static void Validate(List<ResumeProfile> resumeProfiles)
    {
        var errorStrings = new List<string>();
        ValidateProfiles(
            resumeProfiles,
            errorStrings);
        if (errorStrings.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                errorStrings));
        }
    }

    // IDs select records; blank facts or skills would make the demonstration misleading.
    private static void ValidateProfiles(
        List<ResumeProfile> resumeProfiles,
        List<string> errorStrings)
    {
        if (resumeProfiles.Count == 0)
        {
            errorStrings.Add("profiles: provide at least one fictional profile.");
        }

        var idStringsHashSet = new HashSet<string>(StringComparer.Ordinal);
        for (int indexInt = 0; indexInt < resumeProfiles.Count; indexInt++)
        {
            ResumeProfile? resumeProfile = resumeProfiles[indexInt];
            string pathString = $"profiles[{indexInt}]";
            if (resumeProfile is null)
            {
                errorStrings.Add($"{pathString}: a profile is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(resumeProfile.IdString) || !idStringsHashSet.Add(resumeProfile.IdString))
            {
                errorStrings.Add($"{pathString}.id: provide a nonempty, unique ID.");
            }

            if (string.IsNullOrWhiteSpace(resumeProfile.NameString))
            {
                errorStrings.Add($"{pathString}.name: provide a name.");
            }

            if (string.IsNullOrWhiteSpace(resumeProfile.SummaryString))
            {
                errorStrings.Add($"{pathString}.summary: provide a summary.");
            }

            if (resumeProfile.SkillStrings is null || resumeProfile.SkillStrings.Count == 0 ||
                resumeProfile.SkillStrings.Any(skillString => string.IsNullOrWhiteSpace(skillString)))
            {
                errorStrings.Add($"{pathString}.skills: provide nonempty skill names.");
            }
        }
    }
}
