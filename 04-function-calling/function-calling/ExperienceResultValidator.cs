public static class ExperienceResultValidator
{
    // A deliberately small answer format lets code check every claimed number and its source.
    public static void ValidateAnswer(
        string answerString,
        ExperienceResult experienceResult)
    {
        string expectedAnswerString =
            $"{experienceResult.TotalMonthsInt} full calendar months [CalculateExperience.totalMonths].";
        if (!string.Equals(
            answerString.Trim(),
            expectedAnswerString,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The answer must report and cite the exact totalMonths returned by CalculateExperience.");
        }
    }
}
