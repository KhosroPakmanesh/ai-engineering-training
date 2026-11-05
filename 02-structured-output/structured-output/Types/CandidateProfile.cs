using System.Text.Json.Serialization;

// Contact facts and supported resume entries form the accepted candidate profile.
public sealed record CandidateProfile(
    [property: JsonPropertyName("name")] string? NameString,
    [property: JsonPropertyName("email")] string? EmailString,
    [property: JsonPropertyName("phone")] string? PhoneString,
    Evidence Evidence,
    [property: JsonPropertyName("experience")] List<Experience> Experiences,
    [property: JsonPropertyName("education")] List<Education> Educations,
    List<Skill> Skills);
