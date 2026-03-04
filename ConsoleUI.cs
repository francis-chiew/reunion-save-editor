namespace ReunionSaveEditor;

/// <summary>
/// All console interaction lives here — display, menus, and prompts.
/// SaveFile is never written unless the user explicitly confirms a change.
/// </summary>
public static class ConsoleUI
{
    // ── Colour palette ────────────────────────────────────────────────────
    private static readonly ConsoleColor ColourHeader  = ConsoleColor.Cyan;
    private static readonly ConsoleColor ColourLabel   = ConsoleColor.Gray;
    private static readonly ConsoleColor ColourValue   = ConsoleColor.White;
    private static readonly ConsoleColor ColourGood    = ConsoleColor.Green;
    private static readonly ConsoleColor ColourWarn    = ConsoleColor.Yellow;
    private static readonly ConsoleColor ColourPrompt  = ConsoleColor.DarkCyan;
    private static readonly ConsoleColor ColourError   = ConsoleColor.Red;

    // ═════════════════════════════════════════════════════════════════════
    // Top-level entry point
    // ═════════════════════════════════════════════════════════════════════
    public static void Run(SaveFile save, IReadOnlyList<string>? allPaths = null)
    {
        while (true)
        {
            Console.Clear();
            PrintBanner();
            PrintSummary(save);

            Console.WriteLine();
            WriteColour("  [1]", ColourPrompt); Console.Write(" Credits");
            WriteColour("    [2]", ColourPrompt); Console.Write(" Minerals");
            WriteColour("    [3]", ColourPrompt); Console.Write(" Inventions");
            WriteColour("    [4]", ColourPrompt); Console.Write(" Inventory");
            WriteColour("    [N]", ColourPrompt); Console.Write(" Name");
            if (allPaths is { Count: > 1 })
            {
                WriteColour("    [S]", ColourPrompt); Console.Write(" Switch save");
            }
            WriteColour("    [X]", ColourPrompt); Console.WriteLine(" Exit");
            Console.WriteLine();

            char choice = Prompt("Choice");

            switch (char.ToUpper(choice))
            {
                case '1': EditCredits(save);     break;
                case '2': EditMinerals(save);    break;
                case '3': EditInventions(save);  break;
                case '4': EditInventory(save);   break;
                case 'N': EditSaveName(save);    break;
                case 'S' when allPaths is { Count: > 1 }:
                    SaveFile? switched = PickSave(allPaths, save.FilePath);
                    if (switched != null) save = switched;
                    break;
                case 'X': return;
                default:
                    WriteLineColour("  Unknown option.", ColourWarn);
                    Pause();
                    break;
            }
        }
    }

    /// <summary>Presents a numbered list of saves and loads the chosen one.</summary>
    private static SaveFile? PickSave(IReadOnlyList<string> paths, string currentPath)
    {
        Console.Clear();
        WriteLineColour("  ── Switch Save ──────────────────────────────────", ColourHeader);
        Console.WriteLine();

        for (int i = 0; i < paths.Count; i++)
        {
            string label = TryReadSaveName(paths[i]) ?? Path.GetFileName(paths[i]);
            bool   current = string.Equals(paths[i], currentPath, StringComparison.OrdinalIgnoreCase);
            WriteColour($"  [{i + 1}] ", ColourPrompt);
            WriteColour($"{Path.GetFileName(paths[i]),-15} ", ColourLabel);
            WriteColour(label, current ? ColourGood : ColourValue);
            if (current) WriteColour(" (current)", ColourLabel);
            Console.WriteLine();
        }

        Console.WriteLine();
        WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Cancel");
        Console.WriteLine();

        string? input = PromptLine("Choice");
        if (input == null || input.Trim().ToUpper() == "X") return null;

        if (!int.TryParse(input.Trim(), out int pick) || pick < 1 || pick > paths.Count)
        {
            return null;
        }

        string chosen = paths[pick - 1];
        if (string.Equals(chosen, currentPath, StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            return SaveFile.Load(chosen);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            WriteLineColour($"  Failed to load {Path.GetFileName(chosen)}: {ex.Message}", ColourError);
            Pause();
            return null;
        }
    }

    private static string? TryReadSaveName(string path)
    {
        try
        {
            using FileStream fs = File.OpenRead(path);
            byte[] buf = new byte[16];
            if (fs.Read(buf, 0, 16) < 16) return null;
            int len = Math.Min((int)buf[0], 15);
            return System.Text.Encoding.ASCII.GetString(buf, 1, len).TrimEnd('\0', '-', ' ');
        }
        catch { return null; }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Summary view
    // ═════════════════════════════════════════════════════════════════════
    private static void PrintBanner()
    {
        WriteLineColour("  Reunion Save Editor", ColourHeader);
        WriteLineColour("  " + new string('─', 50), ColourLabel);
    }

    private static void PrintSummary(SaveFile save)
    {
        Console.WriteLine();
        WriteColour("  Save : ", ColourLabel);
        WriteLineColour(save.SaveName, ColourValue);
        WriteColour("  File : ", ColourLabel);
        WriteLineColour(save.FilePath, ColourLabel);

        string bakPath = save.FilePath + ".bak";
        WriteColour("  Backup: ", ColourLabel);
        if (File.Exists(bakPath))
        {
            TimeSpan age = DateTime.Now - File.GetLastWriteTime(bakPath);
            string ageStr = age.TotalDays >= 1   ? $"{(int)age.TotalDays}d ago"
                          : age.TotalHours >= 1   ? $"{(int)age.TotalHours}h ago"
                          : age.TotalMinutes >= 1  ? $"{(int)age.TotalMinutes}m ago"
                          : "just now";
            WriteLineColour(ageStr, ColourLabel);
        }
        else
        {
            WriteLineColour("none", ColourWarn);
        }

        Console.WriteLine();
        WriteColour("  Credits  : ", ColourLabel);
        WriteLineColour($"{save.Credits:N0}", save.Credits == 0 ? ColourWarn : ColourGood);

        Console.WriteLine();
        WriteLineColour("  Minerals :", ColourLabel);
        for (int i = 0; i < SaveFile.MineralNames.Length; i++)
        {
            uint m = save.GetMineral(i);
            WriteColour($"    {SaveFile.MineralNames[i],-12}: ", ColourLabel);
            WriteLineColour($"{m:N0}", m == 0 ? ColourError : ColourValue);
        }

        int complete = 0, inProg = 0;
        for (int i = 0; i < SaveFile.InventionCount; i++)
        {
            byte s = save.GetInventionStatus(i);
            if (s == SaveFile.StatusComplete)   complete++;
            else if (s != SaveFile.StatusNone)  inProg++;
        }
        Console.WriteLine();
        WriteColour("  Inventions: ", ColourLabel);
        WriteColour($"{complete}", ColourGood);
        WriteColour($"/{SaveFile.InventionCount} complete", ColourLabel);
        if (inProg > 0) { WriteColour($", {inProg} in progress", ColourWarn); }
        Console.WriteLine();
    }

    // ═════════════════════════════════════════════════════════════════════
    // Credits editor
    // ═════════════════════════════════════════════════════════════════════
    private static void EditCredits(SaveFile save)
    {
        string? status = null;

        while (true)
        {
            Console.Clear();
            WriteLineColour("  ── Credits ──────────────────────────────────────", ColourHeader);
            Console.WriteLine();
            WriteColour("  Current : ", ColourLabel);
            WriteLineColour($"{save.Credits:N0}", ColourValue);
            Console.WriteLine();
            WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Back");
            Console.WriteLine();
            PrintStatus(status);

            string? input = PromptLine("  New value (blank / X to go back)");
            status = null;

            if (string.IsNullOrWhiteSpace(input) || input.Trim().ToUpper() == "X") return;

            if (!TryParseUInt(input, out uint value))
            {
                status = Error("Invalid number.");
                continue;
            }

            save.Credits = value;
            status = CommitSave(save, $"Credits set to {value:N0}.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Save name editor
    // ═════════════════════════════════════════════════════════════════════
    private static void EditSaveName(SaveFile save)
    {
        string? status = null;

        while (true)
        {
            Console.Clear();
            WriteLineColour("  ── Save Name ────────────────────────────────────", ColourHeader);
            Console.WriteLine();
            WriteColour("  Current : ", ColourLabel);
            WriteLineColour(save.SaveName, ColourValue);
            WriteColour("  Max     : ", ColourLabel);
            WriteLineColour($"{SaveFile.SaveNameMaxLen} characters (ASCII printable only)", ColourLabel);
            Console.WriteLine();
            WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Back");
            Console.WriteLine();
            PrintStatus(status);

            string? input = PromptLine("  New name (blank / X to go back)");
            status = null;

            if (string.IsNullOrWhiteSpace(input) || input.Trim().ToUpper() == "X") return;

            save.SetSaveName(input);
            status = CommitSave(save, $"Save name set to \"{save.SaveName}\".");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Minerals editor
    // ═════════════════════════════════════════════════════════════════════
    private static void EditMinerals(SaveFile save)
    {
        string? status = null;

        while (true)
        {
            Console.Clear();
            WriteLineColour("  ── Minerals ─────────────────────────────────────", ColourHeader);
            Console.WriteLine();

            for (int i = 0; i < SaveFile.MineralNames.Length; i++)
            {
                WriteColour($"  [{i + 1}] ", ColourPrompt);
                WriteColour($"{SaveFile.MineralNames[i],-12}: ", ColourLabel);
                WriteLineColour($"{save.GetMineral(i):N0}", ColourValue);
            }

            Console.WriteLine();
            WriteColour("  [A]", ColourPrompt); Console.WriteLine(" Set all minerals to the same value");
            WriteColour("  [M]", ColourPrompt); Console.WriteLine(" Max all minerals (999,999)");
            WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Back");
            Console.WriteLine();
            PrintStatus(status);

            char choice = Prompt("Choice");
            status = null;

            if (char.ToUpper(choice) == 'X') return;

            if (char.ToUpper(choice) == 'A')
            {
                string? input = PromptLine("  Value for all minerals (blank to cancel)");
                if (string.IsNullOrWhiteSpace(input)) continue;
                if (!TryParseUInt(input, out uint val))
                {
                    status = Error("Invalid number.");
                    continue;
                }
                for (int i = 0; i < SaveFile.MineralNames.Length; i++)
                    save.SetMineral(i, val);
                status = CommitSave(save, $"All minerals set to {val:N0}.");
                continue;
            }

            if (char.ToUpper(choice) == 'M')
            {
                const uint maxMineral = 999_999;
                for (int i = 0; i < SaveFile.MineralNames.Length; i++)
                    save.SetMineral(i, maxMineral);
                status = CommitSave(save, $"All minerals set to {maxMineral:N0}.");
                continue;
            }

            if (choice >= '1' && choice <= '6')
            {
                int idx = choice - '1';
                string? input = PromptLine($"  New value for {SaveFile.MineralNames[idx]} (blank to cancel)");
                if (string.IsNullOrWhiteSpace(input)) continue;
                if (!TryParseUInt(input, out uint val))
                {
                    status = Error("Invalid number.");
                    continue;
                }
                save.SetMineral(idx, val);
                status = CommitSave(save, $"{SaveFile.MineralNames[idx]} set to {val:N0}.");
                continue;
            }

            status = Error("Unknown option — enter 1–6, A, M, or X.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Inventions editor
    // ═════════════════════════════════════════════════════════════════════
    private static void EditInventions(SaveFile save)
    {
        string? status = null;
        int?    pending = null;   // index selected, awaiting status key

        while (true)
        {
            Console.Clear();
            WriteLineColour("  ── Inventions ───────────────────────────────────", ColourHeader);
            Console.WriteLine();
            PrintInventionTable(save);
            Console.WriteLine();

            if (pending.HasValue)
            {
                // Second-level prompt: show which invention is selected and ask for new status
                WriteColour("  Selected: ", ColourLabel);
                WriteColour($"{pending.Value} – {SaveFile.InventionNames[pending.Value]}", ColourValue);
                WriteColour("  (current: ", ColourLabel);
                WriteColour(SaveFile.StatusName(save.GetInventionStatus(pending.Value)), ColourValue);
                WriteLineColour(")", ColourLabel);
                Console.WriteLine();
                WriteColour("  [0]", ColourPrompt); Console.WriteLine(" None");
                WriteColour("  [1]", ColourPrompt); Console.WriteLine(" Started");
                WriteColour("  [3]", ColourPrompt); Console.WriteLine(" In progress");
                WriteColour("  [5]", ColourPrompt); Console.WriteLine(" Complete");
                WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Cancel");
                Console.WriteLine();
                PrintStatus(status);

                char key = Prompt("New status");
                status = null;

                byte newStatus = key switch
                {
                    '0'      => SaveFile.StatusNone,
                    '1'      => SaveFile.StatusStarted,
                    '3'      => SaveFile.StatusInProgress,
                    '5'      => SaveFile.StatusComplete,
                    'x' or 'X' => 0xFF,
                    _        => 0xFE,
                };

                if (newStatus == 0xFF) { pending = null; continue; }

                if (newStatus == 0xFE)
                {
                    status = Error("Enter 0, 1, 3, 5, or X.");
                    continue;
                }

                int idx = pending.Value;
                pending = null;
                save.SetInventionStatus(idx, newStatus);
                status = CommitSave(save, $"{SaveFile.InventionNames[idx]} set to {SaveFile.StatusName(newStatus)}.");
            }
            else
            {
                // Top-level prompt
                WriteColour("  [0-34]", ColourPrompt); Console.WriteLine(" Edit invention by index");
                WriteColour("  [S]",    ColourPrompt); Console.WriteLine(" Set all inventions to a specific status");
                WriteColour("  [U]",    ColourPrompt); Console.WriteLine(" Unlock all inventions (set all to Complete)");
                WriteColour("  [X]",    ColourPrompt); Console.WriteLine(" Back");
                Console.WriteLine();
                PrintStatus(status);

                string? input = PromptLine("Choice");
                status = null;

                if (input == null) return;
                if (input.Trim().ToUpper() == "X") return;

                if (input.Trim().ToUpper() == "U")
                {
                    int notComplete = Enumerable.Range(0, SaveFile.InventionCount)
                        .Count(i => save.GetInventionStatus(i) != SaveFile.StatusComplete);
                    if (notComplete == 0)
                    {
                        status = Error("All inventions are already Complete.");
                        continue;
                    }
                    Console.Write($"  Upgrade {notComplete} invention(s) to Complete? [y/N] ");
                    string? confirm = Console.ReadLine();
                    if (confirm?.Trim().ToUpper() == "Y")
                    {
                        save.UnlockAllInventions();
                        status = CommitSave(save, $"All inventions unlocked ({notComplete} upgraded).");
                    }
                    continue;
                }

                if (input.Trim().ToUpper() == "S")
                {
                    WriteColour("  [0]", ColourPrompt); Console.WriteLine(" None");
                    WriteColour("  [1]", ColourPrompt); Console.WriteLine(" Started");
                    WriteColour("  [3]", ColourPrompt); Console.WriteLine(" In progress");
                    WriteColour("  [5]", ColourPrompt); Console.WriteLine(" Complete");
                    char sk = Prompt("Set all to");
                    byte sAll = sk switch
                    {
                        '0' => SaveFile.StatusNone,
                        '1' => SaveFile.StatusStarted,
                        '3' => SaveFile.StatusInProgress,
                        '5' => SaveFile.StatusComplete,
                        _   => 0xFF,
                    };
                    if (sAll == 0xFF) { status = Error("Enter 0, 1, 3, or 5."); continue; }
                    Console.Write($"  Set all 35 inventions to {SaveFile.StatusName(sAll)}? [y/N] ");
                    string? confirmS = Console.ReadLine();
                    if (confirmS?.Trim().ToUpper() == "Y")
                    {
                        save.SetAllInventionStatus(sAll);
                        status = CommitSave(save, $"All inventions set to {SaveFile.StatusName(sAll)}.");
                    }
                    continue;
                }

                if (!int.TryParse(input.Trim(), out int idx) || idx < 0 || idx >= SaveFile.InventionCount)
                {
                    status = Error("Enter an index 0–34, U, or X.");
                    continue;
                }

                pending = idx;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Inventory editor
    // ═════════════════════════════════════════════════════════════════════

    // Buyable invention indices in display order (no-buy items excluded).
    private static readonly int[] BuyableIndices =
        Enumerable.Range(0, SaveFile.InventionCount)
                  .Where(i => !SaveFile.IsNoBuy(i))
                  .ToArray();

    private const int MaxInventory = 9999;

    private static void EditInventory(SaveFile save)
    {
        string? status = null;

        while (true)
        {
            Console.Clear();
            WriteLineColour("  ── Inventory ────────────────────────────────────", ColourHeader);
            Console.WriteLine();
            PrintInventoryTable(save);
            Console.WriteLine();
            WriteColour("  [#]", ColourPrompt); Console.WriteLine($" Edit stockpile by row number above");
            WriteColour("  [A]", ColourPrompt); Console.WriteLine(" Set all stockpiles to the same value");
            WriteColour("  [Z]", ColourPrompt); Console.WriteLine(" Zero all stockpiles");
            WriteColour("  [X]", ColourPrompt); Console.WriteLine(" Back");
            Console.WriteLine();
            PrintStatus(status);

            string? input = PromptLine("Choice");
            status = null;

            if (input == null) return;

            string trimmed = input.Trim().ToUpper();

            if (trimmed == "X") return;

            if (trimmed == "A")
            {
                string? valStr = PromptLine($"  Value for all stockpiles 0–{MaxInventory} (blank to cancel)");
                if (string.IsNullOrWhiteSpace(valStr)) continue;
                if (!TryParseInventoryValue(valStr, out ushort val))
                {
                    status = Error($"Invalid value (must be 0–{MaxInventory}).");
                    continue;
                }
                save.SetAllInventory(val);
                status = CommitSave(save, $"All buyable stockpiles set to {val}.");
                continue;
            }

            if (trimmed == "Z")
            {
                Console.Write("  Zero all buyable stockpiles? [y/N] ");
                string? confirmZ = Console.ReadLine();
                if (confirmZ?.Trim().ToUpper() == "Y")
                {
                    save.SetAllInventory(0);
                    status = CommitSave(save, "All buyable stockpiles zeroed.");
                }
                continue;
            }

            // Row number entered — map back to invention index
            if (!int.TryParse(trimmed, out int row) || row < 1 || row > BuyableIndices.Length)
            {
                status = Error($"Enter a row number 1–{BuyableIndices.Length}, A, or X.");
                continue;
            }

            int idx = BuyableIndices[row - 1];
            string? countStr = PromptLine($"  New value 0–{MaxInventory} for {SaveFile.InventionNames[idx]} (blank to cancel)");
            if (string.IsNullOrWhiteSpace(countStr)) continue;
            if (!TryParseInventoryValue(countStr, out ushort count))
            {
                status = Error($"Invalid value (must be 0–{MaxInventory}).");
                continue;
            }
            save.SetInventory(idx, count);
            status = CommitSave(save, $"{SaveFile.InventionNames[idx]} stockpile set to {count}.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Shared table renderers
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>All 35 inventions in a two-column layout, numbered left-to-right.</summary>
    private static void PrintInventionTable(SaveFile save)
    {
        // Column: idx(3) + "  " + name(16) + "  " + status(11) = 34 chars per column
        // Full row: 2 indent + col(34) + "  |  " + col(34) = 75 chars
        const int nameWidth   = 16;
        const int statusWidth = 11; // length of "In progress"

        string colHeader = $"{"Idx",-3}  {"Invention",-16}  {"Status",-11}";
        WriteLineColour($"  {colHeader}  |  {colHeader}", ColourLabel);
        WriteLineColour("  " + new string('─', 34) + "─|─" + new string('─', 34), ColourLabel);

        int totalRows = (SaveFile.InventionCount + 1) / 2;

        for (int row = 0; row < totalRows; row++)
        {
            int li = row * 2;
            int ri = row * 2 + 1;

            // Left column — always present
            PrintInventionCell(save, li, nameWidth, statusWidth);

            WriteColour("  |  ", ColourLabel);

            // Right column — absent on the last row if count is odd
            if (ri < SaveFile.InventionCount)
                PrintInventionCell(save, ri, nameWidth, statusWidth);

            Console.WriteLine();
        }
    }

    private static void PrintInventionCell(SaveFile save, int idx, int nameWidth, int statusWidth)
    {
        byte   status    = save.GetInventionStatus(idx);
        string statusStr = SaveFile.StatusName(status);

        ConsoleColor statusColour = status switch
        {
            SaveFile.StatusComplete   => ColourGood,
            SaveFile.StatusNone       => ColourLabel,
            _                         => ColourWarn,
        };

        WriteColour($"{idx,-3}  {SaveFile.InventionNames[idx],-16}  ", ColourLabel);
        WriteColour($"{statusStr,-11}", statusColour);
    }

    /// <summary>
    /// Buyable inventions in a two-column layout, numbered left-to-right.
    /// Columns are separated by a pipe. Fits in a standard 80-column terminal.
    /// </summary>
    private static void PrintInventoryTable(SaveFile save)
    {
        // Column layout: "  ##. <name,16>  <stock,4>"  = 2+3+1+16+2+4 = 28 chars per column
        // Full row: col_left(28) + " | " + col_right(28) = 59 chars + 2 leading spaces = 61
        const int stockWidth = 4;

        string colHeader = $"{"#",-3}  {"Invention",-16}  {"Stock",stockWidth}";
        WriteLineColour($"  {colHeader}  |  {colHeader}", ColourLabel);
        WriteLineColour("  " + new string('─', 28) + "─|─" + new string('─', 28), ColourLabel);

        int totalRows = (BuyableIndices.Length + 1) / 2;

        for (int row = 0; row < totalRows; row++)
        {
            int leftItem  = row * 2;
            int rightItem = row * 2 + 1;

            // Left column — always present
            int    leftIdx   = BuyableIndices[leftItem];
            ushort leftStock = save.GetInventory(leftIdx);
            string leftName  = SaveFile.InventionNames[leftIdx];

            WriteColour($"  {leftItem + 1,-3}  {leftName,-16}  ", ColourLabel);
            ConsoleColor leftColour = leftStock > 0 ? ColourValue : ColourLabel;
            WriteColour($"{leftStock,stockWidth}", leftColour);

            WriteColour("  |  ", ColourLabel);

            // Right column — absent on the last row if count is odd
            if (rightItem < BuyableIndices.Length)
            {
                int    rightIdx   = BuyableIndices[rightItem];
                ushort rightStock = save.GetInventory(rightIdx);
                string rightName  = SaveFile.InventionNames[rightIdx];

                WriteColour($"{rightItem + 1,-3}  {rightName,-16}  ", ColourLabel);
                ConsoleColor rightColour = rightStock > 0 ? ColourValue : ColourLabel;
                WriteColour($"{rightStock,stockWidth}", rightColour);
            }

            Console.WriteLine();
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Save helper — returns a status string instead of pausing
    // ═════════════════════════════════════════════════════════════════════
    private static string CommitSave(SaveFile save, string message)
    {
        try
        {
            save.Save();
            return $"[OK] {message}";
        }
        catch (Exception ex)
        {
            return $"[ERR] Write failed: {ex.Message}";
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Status / error line above the prompt
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Prints a status message in green (OK) or red (ERR) above the prompt.</summary>
    private static void PrintStatus(string? status)
    {
        if (status == null) return;

        if (status.StartsWith("[OK]"))
            WriteLineColour("  " + status[5..], ColourGood);
        else if (status.StartsWith("[ERR]"))
            WriteLineColour("  " + status[6..], ColourError);
        else
            WriteLineColour("  " + status, ColourWarn);
    }

    /// <summary>Formats a validation error as an [ERR] status string.</summary>
    private static string Error(string msg) => $"[ERR] {msg}";

    // ═════════════════════════════════════════════════════════════════════
    // Input helpers
    // ═════════════════════════════════════════════════════════════════════
    private static char Prompt(string label)
    {
        WriteColour($"  {label}> ", ColourPrompt);
        string? line = Console.ReadLine();
        return string.IsNullOrEmpty(line) ? '\0' : line[0];
    }

    private static string? PromptLine(string label)
    {
        WriteColour($"  {label}> ", ColourPrompt);
        return Console.ReadLine();
    }

    private static void Pause()
    {
        Console.WriteLine();
        WriteLineColour("  Press Enter to continue...", ColourLabel);
        Console.ReadLine();
    }

    private static bool TryParseUInt(string input, out uint value)
    {
        // Accept plain integers; strip common formatting chars
        string cleaned = input.Replace(",", "").Replace("_", "").Replace(" ", "").Trim();
        return uint.TryParse(cleaned, out value);
    }

    private static bool TryParseInventoryValue(string input, out ushort value)
    {
        string cleaned = input.Replace(",", "").Replace("_", "").Replace(" ", "").Trim();
        if (ushort.TryParse(cleaned, out ushort raw) && raw <= MaxInventory)
        {
            value = raw;
            return true;
        }
        value = 0;
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════
    // Colour wrappers
    // ═════════════════════════════════════════════════════════════════════
    private static void WriteColour(string text, ConsoleColor colour)
    {
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = colour;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }

    private static void WriteLineColour(string text, ConsoleColor colour)
    {
        WriteColour(text, colour);
        Console.WriteLine();
    }
}
