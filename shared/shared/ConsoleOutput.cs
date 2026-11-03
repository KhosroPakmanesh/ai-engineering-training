public static class ConsoleOutput
{
    // Give every lesson the same heading style while keeping its teaching text in the application.
    public static void PrintHeading(string titleString)
    {
        PrintColoredLine($"{titleString}\n──────────────────────────────────────", ConsoleColor.Green);
    }

    // Restore the terminal's colour after each message; redirected output stays plain text.
    public static void PrintColoredLine(string textString, ConsoleColor consoleColor)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(textString);
            return;
        }

        ConsoleColor originalColorConsoleColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(textString);
        }
        finally
        {
            Console.ForegroundColor = originalColorConsoleColor;
        }
    }
}
