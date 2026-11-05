public static class ProfileNormalizer
{
    // Keep the first entry for each skill, with its spelling and supporting quote.
    public static CandidateProfile NormalizeProfile(CandidateProfile candidateProfile)
    {
        return candidateProfile with
        {
            Skills = candidateProfile.Skills
            .Select(skill => skill with
            {
                NameString = skill.NameString.Trim()
            })
            .DistinctBy(
                skill => skill.NameString,
                StringComparer.OrdinalIgnoreCase)
            .ToList()
        };
    }
}
