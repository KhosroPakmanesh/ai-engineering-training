public static class ProfileNormalizer
{
    // Preserve the first supported spelling and evidence for each skill.
    public static CandidateProfile NormalizeProfile(CandidateProfile profile) => profile with
    {
        Skills = profile.Skills.Select(skill => skill with { Name = skill.Name.Trim() })
            .DistinctBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToList()
    };
}
