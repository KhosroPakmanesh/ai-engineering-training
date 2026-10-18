public sealed record Evidence(string Source, string Quote);

public sealed record CandidateProfile(
    string? Name, string? Email, string? Phone, Evidence Evidence,
    List<Experience> Experience, List<Education> Education, List<Skill> Skills);

public sealed record Experience(
    string? Employer, string? Role, string? StartDate, string? EndDate,
    int? DurationMonths, Evidence Evidence);

public sealed record Education(
    string? Institution, string? Degree, string? FieldOfStudy,
    string? StartDate, string? EndDate, Evidence Evidence);

public sealed record Skill(string Name, string? Proficiency, Evidence Evidence);
