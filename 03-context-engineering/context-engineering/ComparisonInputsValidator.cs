public static class ComparisonInputsValidator
{
    // Console runs and acceptance tests share the same question boundary before preparing prompts.
    public static void ValidateQuestion(string questionString)
    {
        if (string.IsNullOrWhiteSpace(questionString) || questionString.Length > 500)
        {
            throw new ArgumentException(
                "The question must contain between 1 and 500 characters.",
                nameof(questionString));
        }
    }
}
