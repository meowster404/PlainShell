namespace PlainShell.Core;

public static class ConsoleHelper
{
    public static void SafeClear()
    {
        try
        {
            if (!Console.IsOutputRedirected && !Console.IsInputRedirected)
            {
                Console.Clear();
            }
        }
        catch
        {
            // Ignore handle errors in non-interactive/redirected environments
        }
    }
}
