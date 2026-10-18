public static class ProfileNormalizer
{
    // Keep the first entry for each skill, with its spelling and supporting quote.
    public static CandidateProfile NormalizeProfile(CandidateProfile profile) => profile with
    {
        Skills = profile.Skills.Select(skill => skill with { Name = skill.Name.Trim() })
            .DistinctBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToList()
    };
}
