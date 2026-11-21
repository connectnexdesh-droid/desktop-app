# QuickDock

QuickDock is a Windows desktop utility for sequential text copying, internal spreadsheet filling, and clipboard-based TOTP generation.

## Features

- Four independent text sheets: TXT 1, TXT 2, TXT 3 and TXT 4.
- Sequential copy: first click copies the current row; another click within 2 seconds repeats that row; a later click advances to the next row.
- One combined A–E sheet. Each floating action button fills the next empty cell in its own column using the current Windows clipboard text.
- Clipboard-first 2FA: copy a Base32 secret or an otpauth URI, then press 2FA to generate and copy a 6-digit RFC 6238 TOTP code.
- Always-on-top floating bubble.
- Master Floating Bubble ON/OFF setting.
- Independent ON/OFF settings for TXT 1–4, 2FA and A–E. Disabled buttons are hidden from the floating panel.
- Local SQLite storage under %LOCALAPPDATA%\QuickDock\quickdock.db.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK

## Build and run

Open a terminal in the repository root:

    dotnet restore QuickDock.sln
    dotnet build QuickDock.sln -c Release
    dotnet run --project QuickDock/QuickDock.csproj

To publish a self-contained Windows x64 build:

    dotnet publish QuickDock/QuickDock.csproj -c Release -r win-x64 --self-contained true

The published application will be under QuickDock/bin/Release/net8.0-windows/win-x64/publish/.

## Usage

1. Open QuickDock and add text rows under TXT SHEETS.
2. Open SETTINGS to enable or disable the master floating bubble and each action independently.
3. Click the QD bubble to expand the floating panel.
4. TXT buttons copy rows in sequence. Re-click within 2 seconds to repeat the same row.
5. Copy any text in Windows, then press A, B, C, D or E to place it into the next empty cell in that column of the internal A–E sheet.
6. Copy a Base32 2FA secret (or an otpauth URI) and press 2FA. The generated code replaces the clipboard contents.

## Security notes

- Text sheets and A–E data are stored locally in SQLite.
- The current clipboard-first 2FA flow does not persist or upload the secret key.
- Clipboard contents are read only when the user presses an action button.
- Treat clipboard contents and locally stored text as sensitive if they contain credentials.
