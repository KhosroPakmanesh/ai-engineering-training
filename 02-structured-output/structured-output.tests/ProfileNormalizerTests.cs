using Xunit;

public class ProfileNormalizerTests
{
    // Cleanup keeps the first supported skill and its evidence while removing duplicates.
    [Fact]
    public void Skill_normalization_trims_names_and_keeps_the_first_duplicate()
    {
        CandidateProfile profile = ProfileTestData.ReadExample();
        Skill original = profile.Skills[0];
        profile = profile with
        {
            Skills = [original with { Name = " " + original.Name + " " }, original with { Name = original.Name.ToLowerInvariant() }]
        };
        Skill accepted = Assert.Single(ProfileNormalizer.NormalizeProfile(profile).Skills);
        Assert.Equal(original, accepted);
    }
}
