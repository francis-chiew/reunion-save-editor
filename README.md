
# Reunion Save Editor

A save game editor for **[Reunion](https://en.wikipedia.org/wiki/Reunion_(video_game))** (1994, DOS), written in C#.
You can play it for free in your browser via [Internet Archive](https://archive.org/details/msdos_Reunion_1994). 

## Why?

Because I could. I loved playing this game as a kid, but it frustrated me no end. I had a save game editor way back when, but lost it over a few moves of house and equipment. Back your stuff up!

## Features

- View save name, credits, minerals, invention statuses, and stockpile counts
- Edit credits and minerals
- Set individual invention research status (None / Started / In Progress / Complete)
- Unlock all inventions at once
- Set individual or all unit stockpile counts (0–255)
- Automatically backs up the save file before writing (`SPIDYSAV.N.bak`)

## Usage

Place `ReunionSaveEditor.exe` in the game's root directory (next to the `SAVE\` folder).
It will auto-detect saves 1–9 and let you choose one.

```
ReunionSaveEditor.exe
```

Or pass a save file path directly:

```
ReunionSaveEditor.exe "C:\Games\Reunion\SAVE\SPIDYSAV.1"
```

## Save File Format Notes

All offsets are in the 41,670-byte `SPIDYSAV.N` save files:

| Offset | Size | Field |
|--------|------|-------|
| `0x0000` | 1 byte | Save name length |
| `0x0001` | 15 bytes | Save name string |
| `0x388C` | uint32 LE | Credits |
| `0x3890` | uint32 LE × 6 | Minerals 1–6 |
| `0x3140 + N*0x35 + 0` | 1 byte | Invention N status (0=none, 1=started, 3=in progress, 5=complete) |
| `0x3140 + N*0x35 + 10` | 1 byte | Invention N stockpile count (0–255) |
| `0x30AF + N` | 1 byte | Invention N tech-tree flag (0x01=researched) |

Offsets were confirmed by disassembling `REUCHT.EXE` (the original DOS cheat tool by H. Hansen, 1995) and by binary comparison of save files.

## Building

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
```

To publish a self-contained single executable for Windows:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Compatibility

Tested against saves from the DOS version of Reunion (1994) running under DOSBox.
Save files are always exactly 41,670 bytes; the editor will refuse to open files of a different size.
