public static class ConsoleOutput
{
    // Give every lesson the same heading style while keeping its teaching text in the application.
    public static void PrintHeading(string title) =>
        PrintColoredLine($"{title}\n──────────────────────────────────────", ConsoleColor.Green);

    // Restore the terminal's colour after each message; redirected output stays plain text.
    public static void PrintColoredLine(string text, ConsoleColor color)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(text);
            return;
        }

        ConsoleColor originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
}
