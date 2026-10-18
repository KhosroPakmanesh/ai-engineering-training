using Xunit;

public class ProfileNormalizerTests
{
    // Remove duplicate skills while keeping the first entry and its resume quote.
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
