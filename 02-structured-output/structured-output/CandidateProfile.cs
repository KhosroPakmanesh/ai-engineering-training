// Each quote is an exact span of the original CV, with its source filename.
public sealed record Evidence(string Source, string Quote);

public sealed record CandidateProfile(
    string? Name, string? Email, string? Phone, Evidence Evidence,
    Experience[] Experience, Education[] Education, Skill[] Skills);

public sealed record Experience(
    string? Employer, string? Role, string? StartDate, string? EndDate,
    int? DurationMonths, Evidence Evidence);

public sealed record Education(
    string? Institution, string? Degree, string? FieldOfStudy,
    string? StartDate, string? EndDate, Evidence Evidence);

public sealed record Skill(string Name, string? Proficiency, Evidence Evidence);
