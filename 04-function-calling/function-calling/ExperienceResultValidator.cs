public static class ExperienceResultValidator
{
    // A deliberately small answer format lets code check every claimed number and its source.
    public static void ValidateAnswer(string answer, ExperienceResult result)
    {
        string expectedAnswer = $"{result.TotalMonths} full calendar months [CalculateExperience.totalMonths].";
        if (!string.Equals(answer.Trim(), expectedAnswer, StringComparison.Ordinal))
            throw new InvalidDataException("The answer must report and cite the exact totalMonths returned by CalculateExperience.");
    }
}
