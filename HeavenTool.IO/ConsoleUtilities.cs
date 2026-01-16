namespace HeavenTool.IO;

public static class ConsoleUtilities
{
    public static void WriteLine(string message, ConsoleColor color) {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static bool YesOrNo()
    {
        var key = Console.ReadKey(true).Key;

        if (key == ConsoleKey.Y || key == ConsoleKey.N)
            return key == ConsoleKey.Y;
        else return YesOrNo();
    }
}
