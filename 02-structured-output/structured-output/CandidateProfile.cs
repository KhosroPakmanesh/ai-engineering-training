// Each quote is an exact span of the original CV, with its source filename.
public sealed record Evidence(string Source, string Quote);

// Nullable facts represent unknowns; collections describe the supported items.
public sealed record CandidateProfile(
    string? Name, string? Email, string? Phone, Evidence Evidence,
    Experience[] Experience, Education[] Education, Skill[] Skills);

// Preserve stated date precision and durations instead of inferring missing values.
public sealed record Experience(
    string? Employer, string? Role, string? StartDate, string? EndDate,
    int? DurationMonths, Evidence Evidence);

// A missing qualification remains null even when the institution is known.
public sealed record Education(
    string? Institution, string? Degree, string? FieldOfStudy,
    string? StartDate, string? EndDate, Evidence Evidence);

// Evidence supports both the skill name and any stated proficiency.
public sealed record Skill(string Name, string? Proficiency, Evidence Evidence);
