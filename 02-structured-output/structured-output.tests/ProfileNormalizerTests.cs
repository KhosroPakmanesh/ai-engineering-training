using Xunit;

public class ProfileNormalizerTests
{
    // Remove duplicate skills while keeping the first entry and its resume quote.
    [Fact]
    public void Skill_normalization_trims_names_and_keeps_the_first_duplicate()
    {
        CandidateProfile candidateProfile = ProfileTestData.ReadExample();
        Skill originalSkill = candidateProfile.Skills[0];
        candidateProfile = candidateProfile with
        {
            Skills = [originalSkill with { NameString = " " + originalSkill.NameString + " " }, originalSkill with { NameString = originalSkill.NameString.ToLowerInvariant() }]
        };
        Skill acceptedSkill = Assert.Single(ProfileNormalizer.NormalizeProfile(candidateProfile).Skills);
        Assert.Equal(originalSkill, acceptedSkill);
    }
}
