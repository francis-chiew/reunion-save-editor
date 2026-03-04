using System.Text;

namespace ReunionSaveEditor;

/// <summary>
/// Reads and writes Reunion (1994) save files.
///
/// Save files are named SPIDYSAV.1 through SPIDYSAV.9 and are always
/// exactly 41,670 bytes. All multi-byte integers are little-endian.
///
/// Key offsets discovered by reverse-engineering REUCHT.EXE (the original
/// DOS cheat tool by H. Hansen, 1995) and by binary comparison of saves:
///
///   0x0000        : 1 byte  — length of save name
///   0x0001–0x000F : 15 bytes — save name string (null/dash padded)
///   0x388C        : uint32  — credits
///   0x3890–0x38A4 : uint32 × 6 — minerals (types 1–6)
///
/// Invention records (35 total):
///   base  = 0x3140
///   stride = 0x35 (53 bytes)
///   record[N].status    = base + N * stride + 0   (1 byte)
///   record[N].inventory = base + N * stride + 10  (uint16 LE, 0–65535)
///
/// Eight inventions are not purchasable (research/building unlocks only).
/// Their inventory field exists in the file but has no meaningful in-game
/// value as a stockpile count; the editor does not expose it for editing.
/// See NoBuyIndices for the full list.
///
/// Tech-tree unlock flags (35 bytes, one per invention):
///   flag[N] = 0x30AF + N
///   0x01 = researched/unlocked, 0xFE = locked, etc.
///   Written alongside status when unlocking (REUCHT did not update this,
///   but we do for safety).
/// </summary>
public class SaveFile
{
    // ── File constants ────────────────────────────────────────────────────
    public const int ExpectedSize = 41_670;

    // ── Offsets ───────────────────────────────────────────────────────────
    private const int OffSaveNameLen  = 0x0000;
    private const int OffSaveName     = 0x0001; // 15 bytes
    private const int OffCredits      = 0x388C;
    private const int OffMinerals     = 0x3890; // 6 × uint32
    private const int OffInventions   = 0x3140; // base; stride = InventionStride
    private const int InventionStride = 0x35;   // 53 bytes per record
    private const int StatusOffset    = 0;      // byte within record
    private const int InventoryOffset = 10;     // uint16 LE within record
    private const int OffTechFlags    = 0x30AF; // 35-byte flag array

    // ── Invention status values ───────────────────────────────────────────
    public const byte StatusNone       = 0x00;
    public const byte StatusStarted    = 0x01;
    public const byte StatusInProgress = 0x03;
    public const byte StatusComplete   = 0x05;

    // ── Tech flag values ──────────────────────────────────────────────────
    private const byte FlagResearched = 0x01;

    /// <summary>
    /// Indices of inventions that cannot be purchased — they are unlocked
    /// through research or are buildings/features, not buyable units.
    /// Their inventory field is not exposed for editing.
    ///   0  Nuclear gen       — power building
    ///   6  Control Centre    — planet building
    ///   7  Communicator      — research unlock
    ///  13  Hyperspace drv    — research unlock
    ///  17  Tractor Beam      — research unlock
    ///  27  Meteor disint     — research unlock
    ///  28  ION cannon        — research unlock
    ///  29  Anti rad shield   — research unlock
    /// </summary>
    public static readonly HashSet<int> NoBuyIndices = [0, 6, 7, 13, 17, 27, 28, 29];

    public static bool IsNoBuy(int index) => NoBuyIndices.Contains(index);

    // ── Invention names (index 0–34) ──────────────────────────────────────
    public static readonly string[] InventionNames =
    [
        "Nuclear gen",       //  0
        "Miner droid",       //  1
        "Satellite",         //  2
        "Sat. carrier",      //  3
        "Miner station",     //  4
        "Transfer ship",     //  5
        "Control Centre",    //  6
        "Communicator",      //  7
        "Trade ship",        //  8
        "Hunter",            //  9
        "Laser cannon",      // 10
        "Radio",             // 11
        "Twin laser gun",    // 12
        "Hyperspace drv",    // 13
        "Galleon",           // 14
        "Starfighter",       // 15
        "Trooper",           // 16
        "Tractor Beam",      // 17
        "Pirate ship",       // 18
        "Spy satellite",     // 19
        "Battle tank",       // 20
        "Missile",           // 21
        "Destroyer",         // 22
        "Space station",     // 23
        "Spy ship",          // 24
        "Aircraft",          // 25
        "Solar plant",       // 26
        "Meteor disint",     // 27
        "ION cannon",        // 28
        "Anti rad shield",   // 29
        "Plasma gun",        // 30
        "Missile launcher",  // 31
        "Cruiser",           // 32
        "Mental radar",      // 33
        "Energy shield",     // 34
    ];

    public const int InventionCount = 35;

    // ── Mineral names ─────────────────────────────────────────────────────
    public static readonly string[] MineralNames =
    [
        "Mineral 1",
        "Mineral 2",
        "Mineral 3",
        "Mineral 4",
        "Mineral 5",
        "Mineral 6",
    ];

    // ── Data ──────────────────────────────────────────────────────────────
    public string FilePath { get; }

    private byte[] _data;

    public const int SaveNameMaxLen = 15;

    public string SaveName
    {
        get
        {
            int len = Math.Min((int)_data[OffSaveNameLen], 15);
            return Encoding.ASCII.GetString(_data, OffSaveName, len).TrimEnd('\0', '-', ' ');
        }
    }

    /// <summary>
    /// Writes a new save name. Maximum 15 ASCII characters; longer strings are
    /// truncated. The length byte and name field are both updated; any unused
    /// bytes in the name field are zeroed.
    /// </summary>
    public void SetSaveName(string name)
    {
        // Keep only printable ASCII, truncate to field width
        string safe = new string(name.Where(c => c >= 0x20 && c <= 0x7E).ToArray());
        if (safe.Length > SaveNameMaxLen) safe = safe[..SaveNameMaxLen];

        _data[OffSaveNameLen] = (byte)safe.Length;

        byte[] encoded = Encoding.ASCII.GetBytes(safe);
        Array.Clear(_data, OffSaveName, SaveNameMaxLen);
        Array.Copy(encoded, 0, _data, OffSaveName, encoded.Length);
    }

    public uint Credits
    {
        get => ReadUInt32(OffCredits);
        set => WriteUInt32(OffCredits, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────
    private SaveFile(string filePath, byte[] data)
    {
        FilePath = filePath;
        _data = data;
    }

    // ── Factory ───────────────────────────────────────────────────────────
    public static SaveFile Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Save file not found: {filePath}");

        byte[] data = File.ReadAllBytes(filePath);

        if (data.Length != ExpectedSize)
            throw new InvalidDataException(
                $"Unexpected file size: {data.Length} bytes (expected {ExpectedSize}). " +
                "This may not be a valid Reunion save file.");

        return new SaveFile(filePath, data);
    }

    // ── Minerals ──────────────────────────────────────────────────────────
    public uint GetMineral(int index)
    {
        ValidateMineralIndex(index);
        return ReadUInt32(OffMinerals + index * 4);
    }

    public void SetMineral(int index, uint value)
    {
        ValidateMineralIndex(index);
        WriteUInt32(OffMinerals + index * 4, value);
    }

    private static void ValidateMineralIndex(int index)
    {
        if (index < 0 || index >= MineralNames.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Mineral index must be 0–{MineralNames.Length - 1}.");
    }

    // ── Inventions ────────────────────────────────────────────────────────
    public byte GetInventionStatus(int index)
    {
        ValidateInventionIndex(index);
        return _data[OffInventions + index * InventionStride + StatusOffset];
    }

    public void SetInventionStatus(int index, byte status)
    {
        ValidateInventionIndex(index);
        _data[OffInventions + index * InventionStride + StatusOffset] = status;

        // Update the tech-tree unlock flag alongside the status byte.
        // REUCHT.EXE did not do this, but the game sets it when research
        // completes naturally, so we mirror that behaviour for safety.
        _data[OffTechFlags + index] = status == StatusComplete ? FlagResearched : _data[OffTechFlags + index];
    }

    /// <summary>Sets all 35 inventions to StatusComplete and marks their tech flags.</summary>
    public void UnlockAllInventions()
    {
        for (int i = 0; i < InventionCount; i++)
        {
            _data[OffInventions + i * InventionStride + StatusOffset] = StatusComplete;
            _data[OffTechFlags + i] = FlagResearched;
        }
    }

    // ── Inventory ─────────────────────────────────────────────────────────
    public ushort GetInventory(int index)
    {
        ValidateInventionIndex(index);
        return ReadUInt16(OffInventions + index * InventionStride + InventoryOffset);
    }

    public void SetInventory(int index, ushort count)
    {
        ValidateInventionIndex(index);
        WriteUInt16(OffInventions + index * InventionStride + InventoryOffset, count);
    }

    /// <summary>Sets stockpile count for all buyable (non-NoBuy) inventions.</summary>
    public void SetAllInventory(ushort count)
    {
        for (int i = 0; i < InventionCount; i++)
        {
            if (!IsNoBuy(i))
                WriteUInt16(OffInventions + i * InventionStride + InventoryOffset, count);
        }
    }

    // ── Status helpers ────────────────────────────────────────────────────
    public static string StatusName(byte status) => status switch
    {
        StatusNone       => "None",
        StatusStarted    => "Started",
        StatusInProgress => "In progress",
        StatusComplete   => "Complete",
        _                => $"Unknown (0x{status:X2})",
    };

    private static void ValidateInventionIndex(int index)
    {
        if (index < 0 || index >= InventionCount)
            throw new ArgumentOutOfRangeException(nameof(index), $"Invention index must be 0–{InventionCount - 1}.");
    }

    // ── Save ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Writes a .bak backup of the original file (only if one does not already
    /// exist), then saves the modified data back to the original path.
    /// </summary>
    public void Save()
    {
        string backupPath = FilePath + ".bak";
        if (!File.Exists(backupPath))
            File.Copy(FilePath, backupPath);

        File.WriteAllBytes(FilePath, _data);
    }

    // ── Low-level helpers ─────────────────────────────────────────────────
    private ushort ReadUInt16(int offset) =>
        (ushort)(_data[offset] | (_data[offset + 1] << 8));

    private void WriteUInt16(int offset, ushort value)
    {
        _data[offset]     = (byte)(value & 0xFF);
        _data[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private uint ReadUInt32(int offset) =>
        (uint)(_data[offset] | (_data[offset + 1] << 8) | (_data[offset + 2] << 16) | (_data[offset + 3] << 24));

    private void WriteUInt32(int offset, uint value)
    {
        _data[offset]     = (byte)(value & 0xFF);
        _data[offset + 1] = (byte)((value >> 8)  & 0xFF);
        _data[offset + 2] = (byte)((value >> 16) & 0xFF);
        _data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
