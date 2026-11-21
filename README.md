# QuickDock

QuickDock is a Windows desktop utility for sequential text copying, internal spreadsheet filling, and clipboard-based TOTP generation.

## Features

- Four independent text sheets: TXT 1, TXT 2, TXT 3 and TXT 4.
- TXT buttons are available in both the main window and the floating panel. Each button displays the last copied row.
- Single-click TXT action copies the next non-empty row. Double-click within the Windows double-click interval copies the previous non-empty row. The single-click action waits briefly so a double-click can be recognized without copying an unwanted row first.
- The floating bubble displays the most recently used TXT sheet and row number.
- One combined A–E sheet. Each A–E action button fills the next empty cell in its own column using the current Windows clipboard text.
- A–E buttons remain separate and show the last occupied cell for each column (for example, A1, B4 or C2) in both the main window and floating panel.
- Multi-select TXT rows with Ctrl/Shift, Select All, Delete Selected and Undo.
- Multi-select A–E cells, clear selected cells, select rows from row headers, delete selected rows and Undo.
- Delete key clears selected A–E cells; Delete key on selected TXT rows deletes those rows.
- Export the active TXT sheet to UTF-8 CSV or export the complete A–E sheet to UTF-8 CSV. CSV values are escaped correctly and include a UTF-8 BOM for spreadsheet compatibility.
- Clipboard-first 2FA: copy a Base32 secret or otpauth:// URI, then press 2FA to generate and copy a 6-digit RFC 6238 TOTP.
- Always-on-top floating bubble. Click to open; drag to move using screen-coordinate tracking. Drag the panel title bar to move the expanded panel.
- Master Floating Bubble ON/OFF setting.
- Independent ON/OFF settings for TXT 1–4, 2FA and A–E. Disabled buttons are hidden from the floating panel.
- Dark theme with coordinated text, selection, edit-field, button, hover, dropdown and DataGrid corner colours.
- Local SQLite storage under %LOCALAPPDATA%\QuickDock\quickdock.db.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK (only required for development; the published self-contained build does not need a separate .NET installation)

## Build and run

Open a terminal in the repository root:

    dotnet restore QuickDock.sln
    dotnet build QuickDock.sln -c Release
    dotnet run --project QuickDock/QuickDock.csproj

To publish a self-contained Windows x64 build:

    dotnet publish QuickDock/QuickDock.csproj -c Release -r win-x64 --self-contained true

## Usage

1. Open QuickDock and add text rows under TXT SHEETS.
2. Use TXT 1–4 in the main window or floating panel to copy sequentially. Double-click a TXT button to copy the previous non-empty row.
3. Open SETTINGS to enable or disable the master floating bubble and each action independently.
4. Click the QD bubble once to expand the floating panel. Drag the bubble to move it without opening it.
5. Copy any text in Windows, then press A, B, C, D or E to place it into the next empty cell in that column of the internal A–E sheet.
6. The A–E button labels show the last used cell for each column.
7. Copy a Base32 2FA secret (or an otpauth:// URI) and press 2FA. The generated code replaces the clipboard contents.
8. In TXT SHEETS, use Ctrl/Shift to select multiple rows. Use Select all, Delete selected and Undo.
9. In A–E SHEET, Ctrl/drag selects multiple cells; row headers select rows. Use Clear selected cells, Delete selected rows and Undo.
10. Use Export TXT CSV to export the selected TXT sheet, or Export A–E CSV to export the full A–E sheet. Import the resulting CSV into Google Sheets.

## Security notes

- Text sheets and A–E data are stored locally in SQLite.
- TXT copy progress and last-used A–E row indicators are stored locally so the labels remain meaningful after restarting.
- The current clipboard-first 2FA flow does not persist or upload the secret key.
- Clipboard contents are read only when the user presses an action button.
- Database encryption is not currently enabled. Avoid storing highly sensitive credentials in the local sheets until encryption is added.
