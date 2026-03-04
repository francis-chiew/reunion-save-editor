using ReunionSaveEditor;

// ── Save file discovery ───────────────────────────────────────────────────────

string? savePath = null;

if (args.Length > 0)
{
    // Path provided on command line
    savePath = args[0];
}
else
{
    // Auto-discover: look in .\SAVE\ then the current directory
    string[] searchDirs =
    [
        Path.Combine(Directory.GetCurrentDirectory(), "SAVE"),
        Directory.GetCurrentDirectory(),
    ];

    List<string> found = [];
    foreach (string dir in searchDirs)
    {
        if (!Directory.Exists(dir)) continue;
        for (int n = 1; n <= 9; n++)
        {
            string candidate = Path.Combine(dir, $"SPIDYSAV.{n}");
            if (File.Exists(candidate))
                found.Add(candidate);
        }
    }

    if (found.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("No save files found.");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Usage: ReunionSaveEditor <path\\to\\SPIDYSAV.1>");
        Console.WriteLine();
        Console.WriteLine("Or place this tool in the game's directory (next to the SAVE\\ folder)");
        Console.WriteLine("and it will auto-detect your saves.");
        return 1;
    }

    if (found.Count == 1)
    {
        savePath = found[0];
    }
    else
    {
        // Let the user pick
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Reunion Save Editor — select a save file");
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < found.Count; i++)
        {
            // Peek at the save name without fully loading
            string label = TryReadSaveName(found[i]) ?? Path.GetFileName(found[i]);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();
            Console.Write($"{Path.GetFileName(found[i]),-15} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(label);
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  Choice> ");
        Console.ResetColor();

        string? choice = Console.ReadLine();
        if (!int.TryParse(choice?.Trim(), out int pick) || pick < 1 || pick > found.Count)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Cancelled.");
            Console.ResetColor();
            return 0;
        }

        savePath = found[pick - 1];
    }
}

// ── Load and run ──────────────────────────────────────────────────────────────

SaveFile save;
try
{
    save = SaveFile.Load(savePath);
}
catch (FileNotFoundException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 1;
}
catch (InvalidDataException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 1;
}

ConsoleUI.Run(save);
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static string? TryReadSaveName(string path)
{
    try
    {
        using FileStream fs = File.OpenRead(path);
        byte[] buf = new byte[16];
        if (fs.Read(buf, 0, 16) < 16) return null;
        int len = Math.Min((int)buf[0], 15);
        return System.Text.Encoding.ASCII.GetString(buf, 1, len).TrimEnd('\0', '-', ' ');
    }
    catch
    {
        return null;
    }
}
