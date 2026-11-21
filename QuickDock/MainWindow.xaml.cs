using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database = new();
    private readonly Dictionary<string, int> _lastCopiedTextRows = new();
    private readonly Dictionary<string, (DateTime LastClickUtc, int PreviousRowNumber)> _doubleClickStates = new();
    private readonly Dictionary<char, int> _lastColumnRows = new();
    private readonly Stack<UndoSnapshot> _undoStack = new();
    private FloatingWindow? _floatingWindow;
    private string _currentSheet = "TXT1";
    private string? _lastActiveTextSheet;
    private bool _ready;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    public AppSettings Settings { get; }
    public ObservableCollection<TextRow> CurrentTextRows { get; } = new();
    public ObservableCollection<SheetRow> MainRows { get; } = new();

    private sealed record UndoSnapshot(
        string SheetName,
        List<TextRow>? TextRows = null,
        List<SheetRow>? MainRows = null);

    public MainWindow()
    {
        InitializeComponent();

        _database.Initialize();
        Settings = _database.LoadSettings();
        DataContext = this;

        LoadTextSheet(_currentSheet);
        LoadMainSheet();
        LoadProgressState();
        UpdateTextCopyButtonLabels();
        UpdateColumnActionButtonLabels();

        _ready = true;
        ApplyFloatingSettings();
    }


    private void Txt1_Main_Click(object sender, RoutedEventArgs e) => CopyNextText("TXT1");
    private void Txt2_Main_Click(object sender, RoutedEventArgs e) => CopyNextText("TXT2");
    private void Txt3_Main_Click(object sender, RoutedEventArgs e) => CopyNextText("TXT3");
    private void Txt4_Main_Click(object sender, RoutedEventArgs e) => CopyNextText("TXT4");

    private void MainA_Click(object sender, RoutedEventArgs e) => PasteClipboardIntoColumn('A');
    private void MainB_Click(object sender, RoutedEventArgs e) => PasteClipboardIntoColumn('B');
    private void MainC_Click(object sender, RoutedEventArgs e) => PasteClipboardIntoColumn('C');
    private void MainD_Click(object sender, RoutedEventArgs e) => PasteClipboardIntoColumn('D');
    private void MainE_Click(object sender, RoutedEventArgs e) => PasteClipboardIntoColumn('E');

    private void LoadProgressState()
    {
        foreach (var sheet in new[] { "TXT1", "TXT2", "TXT3", "TXT4" })
        {
            var rowNumber = _database.LoadActionState($"Text:{sheet}");
            if (rowNumber > 0)
                _lastCopiedTextRows[sheet] = rowNumber;
        }

        var activeSheetNumber = _database.LoadActionState("LastTextSheet");
        if (activeSheetNumber is >= 1 and <= 4)
            _lastActiveTextSheet = $"TXT{activeSheetNumber}";

        foreach (var column in "ABCDE")
        {
            var savedRow = _database.LoadActionState($"Column:{column}");
            var savedCellStillExists = savedRow > 0 && MainRows.Any(row =>
                row.RowNumber == savedRow &&
                !string.IsNullOrWhiteSpace(GetColumnValue(row, column)));

            var rowNumber = savedCellStillExists
                ? savedRow
                : MainRows.Where(row => !string.IsNullOrWhiteSpace(GetColumnValue(row, column)))
                    .Select(row => row.RowNumber)
                    .DefaultIfEmpty(0)
                    .Max();

            _lastColumnRows[column] = rowNumber;
            _database.SaveActionState($"Column:{column}", rowNumber);
        }
    }

    private void UpdateTextCopyButtonLabels()
    {
        SetTextCopyButton(Txt1CopyButton, "TXT 1", "TXT1");
        SetTextCopyButton(Txt2CopyButton, "TXT 2", "TXT2");
        SetTextCopyButton(Txt3CopyButton, "TXT 3", "TXT3");
        SetTextCopyButton(Txt4CopyButton, "TXT 4", "TXT4");

        if (_floatingWindow is null)
            return;

        foreach (var sheet in new[] { "TXT1", "TXT2", "TXT3", "TXT4" })
        {
            var rowNumber = _lastCopiedTextRows.TryGetValue(sheet, out var row) ? row : 0;
            _floatingWindow.UpdateTextProgress(sheet, rowNumber);
        }

        if (_lastActiveTextSheet is not null)
        {
            var row = _lastCopiedTextRows.TryGetValue(_lastActiveTextSheet, out var value) ? value : 0;
            var sheetNumber = _lastActiveTextSheet[3..];
            _floatingWindow.SetBubbleStatus(row > 0 ? $"T{sheetNumber} · #{row}" : $"T{sheetNumber} · —");
        }
        else
        {
            _floatingWindow.SetBubbleStatus("TXT: —");
        }
    }

    private void SetTextCopyButton(Button button, string title, string sheetName)
    {
        var row = _lastCopiedTextRows.TryGetValue(sheetName, out var value) ? value : 0;
        button.Content = row > 0
            ? $"{title}{Environment.NewLine}#{row}"
            : $"{title}{Environment.NewLine}—";
    }

    private void RefreshColumnProgress()
    {
        foreach (var column in "ABCDE")
        {
            var lastRow = MainRows
                .Where(row => !string.IsNullOrWhiteSpace(GetColumnValue(row, column)))
                .Select(row => row.RowNumber)
                .DefaultIfEmpty(0)
                .Max();

            _lastColumnRows[column] = lastRow;
            _database.SaveActionState($"Column:{column}", lastRow);
        }

        UpdateColumnActionButtonLabels();
    }

    private void UpdateColumnActionButtonLabels()
    {
        MainAButton.Content = FormatColumnButton('A');
        MainBButton.Content = FormatColumnButton('B');
        MainCButton.Content = FormatColumnButton('C');
        MainDButton.Content = FormatColumnButton('D');
        MainEButton.Content = FormatColumnButton('E');

        _floatingWindow?.UpdateColumnProgress('A', _lastColumnRows.TryGetValue('A', out var a) ? a : 0);
        _floatingWindow?.UpdateColumnProgress('B', _lastColumnRows.TryGetValue('B', out var b) ? b : 0);
        _floatingWindow?.UpdateColumnProgress('C', _lastColumnRows.TryGetValue('C', out var c) ? c : 0);
        _floatingWindow?.UpdateColumnProgress('D', _lastColumnRows.TryGetValue('D', out var d) ? d : 0);
        _floatingWindow?.UpdateColumnProgress('E', _lastColumnRows.TryGetValue('E', out var e) ? e : 0);
    }

    private string FormatColumnButton(char column)
    {
        var row = _lastColumnRows.TryGetValue(column, out var value) ? value : 0;
        return row > 0 ? $"{column}{Environment.NewLine}{column}{row}" : $"{column}{Environment.NewLine}—";
    }

    private void LoadTextSheet(string sheetName)
    {
        CurrentTextRows.Clear();
        var rows = _database.LoadTextRows(sheetName);

        if (rows.Count == 0)
        {
            CurrentTextRows.Add(new TextRow { RowNumber = 1 });
            return;
        }

        foreach (var row in rows)
            CurrentTextRows.Add(row);
    }

    private void LoadMainSheet()
    {
        MainRows.Clear();
        var rows = _database.LoadMainRows();

        if (rows.Count == 0)
        {
            for (var i = 1; i <= 40; i++)
                MainRows.Add(new SheetRow { RowNumber = i });

            _database.SaveMainRows(MainRows);
            return;
        }

        foreach (var row in rows)
            MainRows.Add(row);
    }

    private string GetSelectedSheetName()
    {
        if (TxtSheetSelector.SelectedItem is ComboBoxItem item &&
            item.Tag is string sheetName)
            return sheetName;

        return "TXT1";
    }

    private void TxtSheetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
            return;

        TextGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        TextGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveCurrentTextRows();

        _currentSheet = GetSelectedSheetName();
        LoadTextSheet(_currentSheet);
    }

    private void SaveCurrentTextRows()
    {
        if (!_ready)
            return;

        for (var i = 0; i < CurrentTextRows.Count; i++)
            CurrentTextRows[i].RowNumber = i + 1;

        _database.SaveTextRows(_currentSheet, CurrentTextRows);
        TextGrid.Items.Refresh();
    }

    private void TextGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        var editedSheet = _currentSheet;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (editedSheet == _currentSheet)
                SaveCurrentTextRows();
        }), DispatcherPriority.Background);
    }

    private void AddTextRow_Click(object sender, RoutedEventArgs e)
    {
        TextGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        TextGrid.CommitEdit(DataGridEditingUnit.Row, true);

        CurrentTextRows.Add(new TextRow { RowNumber = CurrentTextRows.Count + 1 });
        SaveCurrentTextRows();

        TextGrid.SelectedItem = CurrentTextRows[^1];
        TextGrid.ScrollIntoView(TextGrid.SelectedItem);
        ShowStatus($"{_currentSheet}: new row added.");
    }

    private void TextGrid_SelectAll_Click(object sender, RoutedEventArgs e)
    {
        TextGrid.SelectAll();
        TextGrid.Focus();
    }

    private void TextGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
        {
            TextGrid.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && e.OriginalSource is not TextBox)
        {
            DeleteSelectedTextRows();
            e.Handled = true;
        }
    }

    private void DeleteTextRow_Click(object sender, RoutedEventArgs e) => DeleteSelectedTextRows();

    private void DeleteSelectedTextRows()
    {
        TextGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        TextGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveCurrentTextRows();

        var selected = TextGrid.SelectedItems.OfType<TextRow>().ToList();
        if (selected.Count == 0)
        {
            ShowStatus("Select one or more TXT rows first. Use Ctrl or Shift for multi-select.");
            return;
        }

        var answer = MessageBox.Show(
            $"Delete {selected.Count} selected row(s) from {_currentSheet}?",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        PushTextUndoSnapshot(_currentSheet);

        foreach (var row in selected)
            CurrentTextRows.Remove(row);

        if (CurrentTextRows.Count == 0)
            CurrentTextRows.Add(new TextRow { RowNumber = 1 });

        SaveCurrentTextRows();
        TextGrid.UnselectAll();
        ShowStatus($"{selected.Count} row(s) deleted. Use Undo to restore.");
    }

    private void PushTextUndoSnapshot(string sheetName)
    {
        var rows = _database.LoadTextRows(sheetName)
            .Select(row => new TextRow { RowNumber = row.RowNumber, Text = row.Text })
            .ToList();

        _undoStack.Push(new UndoSnapshot(sheetName, TextRows: rows));
    }

    private void PushMainUndoSnapshot()
    {
        var rows = MainRows
            .Select(row => new SheetRow
            {
                RowNumber = row.RowNumber,
                A = row.A,
                B = row.B,
                C = row.C,
                D = row.D,
                E = row.E
            })
            .ToList();

        _undoStack.Push(new UndoSnapshot("MAIN", MainRows: rows));
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0)
        {
            ShowStatus("There is no delete or clear action to undo.");
            return;
        }

        var snapshot = _undoStack.Pop();

        if (snapshot.TextRows is not null)
        {
            _database.SaveTextRows(snapshot.SheetName, snapshot.TextRows);

            if (_currentSheet == snapshot.SheetName)
            {
                LoadTextSheet(_currentSheet);
                TextGrid.UnselectAll();
            }

            ShowStatus($"Undo completed for {snapshot.SheetName}.");
            return;
        }

        if (snapshot.MainRows is not null)
        {
            MainRows.Clear();
            foreach (var row in snapshot.MainRows)
                MainRows.Add(row);

            SaveMainSheet();
            RefreshColumnProgress();
            MainGrid.Items.Refresh();
            MainGrid.UnselectAll();
            ShowStatus("Undo completed for the A–E sheet.");
        }
    }

    private void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SaveMainSheet();
            RefreshColumnProgress();
        }), DispatcherPriority.Background);
    }

    private void MainGrid_SelectAll_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.SelectAll();
        MainGrid.Focus();
    }

    private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
        {
            MainGrid.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && e.OriginalSource is not TextBox)
        {
            ClearSelectedCells();
            e.Handled = true;
        }
    }

    private void AddMainRows_Click(object sender, RoutedEventArgs e)
    {
        var next = MainRows.Count == 0 ? 1 : MainRows.Max(row => row.RowNumber) + 1;
        for (var i = 0; i < 20; i++)
            MainRows.Add(new SheetRow { RowNumber = next + i });

        SaveMainSheet();
        ShowStatus("20 rows added to the A–E sheet.");
    }

    private void SaveMainRows_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveMainSheet();
        RefreshColumnProgress();
        ShowStatus("A–E sheet saved.");
    }

    private void ClearSelectedCells_Click(object sender, RoutedEventArgs e) => ClearSelectedCells();

    private void ClearSelectedCells()
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveMainSheet();

        var targets = new List<(SheetRow Row, char Column)>();
        var seen = new HashSet<(int RowNumber, char Column)>();

        foreach (var cell in MainGrid.SelectedCells)
        {
            if (!cell.IsValid || cell.Item is not SheetRow row)
                continue;

            if (TryGetEditableColumn(cell.Column, out var column) &&
                seen.Add((row.RowNumber, column)))
            {
                targets.Add((row, column));
            }
        }

        if (targets.Count == 0 && MainGrid.SelectedItems.Count > 0)
        {
            foreach (var row in MainGrid.SelectedItems.OfType<SheetRow>())
            {
                foreach (var column in "ABCDE")
                {
                    if (seen.Add((row.RowNumber, column)))
                        targets.Add((row, column));
                }
            }
        }

        if (targets.Count == 0)
        {
            ShowStatus("Select one or more A–E cells to clear.");
            return;
        }

        PushMainUndoSnapshot();

        foreach (var target in targets)
            SetColumnValue(target.Row, target.Column, string.Empty);

        SaveMainSheet();
        RefreshColumnProgress();
        MainGrid.Items.Refresh();
        ShowStatus($"{targets.Count} cell(s) cleared. Use Undo to restore.");
    }

    private void DeleteMainRows_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveMainSheet();

        var selected = new List<SheetRow>();

        foreach (var row in MainGrid.SelectedItems.OfType<SheetRow>())
        {
            if (!selected.Contains(row))
                selected.Add(row);
        }

        foreach (var cell in MainGrid.SelectedCells)
        {
            if (cell.IsValid && cell.Item is SheetRow row && !selected.Contains(row))
                selected.Add(row);
        }

        if (selected.Count == 0)
        {
            ShowStatus("Select one or more A–E rows first.");
            return;
        }

        var answer = MessageBox.Show(
            $"Delete {selected.Count} selected row(s) from the A–E sheet?",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        PushMainUndoSnapshot();

        foreach (var row in selected)
            MainRows.Remove(row);

        if (MainRows.Count == 0)
        {
            for (var i = 1; i <= 20; i++)
                MainRows.Add(new SheetRow { RowNumber = i });
        }
        else
        {
            RenumberMainRows();
        }

        SaveMainSheet();
        RefreshColumnProgress();
        MainGrid.Items.Refresh();
        MainGrid.UnselectAll();
        ShowStatus($"{selected.Count} row(s) deleted. Use Undo to restore.");
    }

    private void RenumberMainRows()
    {
        for (var i = 0; i < MainRows.Count; i++)
            MainRows[i].RowNumber = i + 1;
    }

    private static bool TryGetEditableColumn(DataGridColumn column, out char result)
    {
        var header = column.Header?.ToString();
        if (!string.IsNullOrEmpty(header) && header.Length == 1 &&
            "ABCDE".Contains(header[0]))
        {
            result = header[0];
            return true;
        }

        result = default;
        return false;
    }

    private void SaveMainSheet()
    {
        if (!_ready)
            return;

        _database.SaveMainRows(MainRows);
    }

    public void CopyNextText(string sheetName)
    {
        try
        {
            var values = LoadCopyableText(sheetName);
            if (values.Count == 0)
            {
                ShowStatus($"{sheetName} has no text rows yet.");
                return;
            }

            var now = DateTime.UtcNow;
            var doubleClickInterval = TimeSpan.FromMilliseconds(
                Math.Clamp((int)GetDoubleClickTime() + 50, 300, 700));

            if (_doubleClickStates.TryGetValue(sheetName, out var clickState) &&
                now - clickState.LastClickUtc <= doubleClickInterval)
            {
                _doubleClickStates.Remove(sheetName);

                var previousIndex = values.FindIndex(value =>
                    value.RowNumber == clickState.PreviousRowNumber);

                if (previousIndex < 0)
                    previousIndex = 0;

                CopyTextAt(sheetName, values[previousIndex], isPrevious: true);
                return;
            }

            var currentIndex = GetLastCopiedIndex(sheetName, values);
            var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % values.Count;
            var previousRowIndex = currentIndex < 0
                ? 0
                : (currentIndex - 1 + values.Count) % values.Count;

            _doubleClickStates[sheetName] = (now, values[previousRowIndex].RowNumber);
            CopyTextAt(sheetName, values[nextIndex], isPrevious: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Copy failed: {ex.Message}");
        }
    }

    private List<(int RowNumber, string Text)> LoadCopyableText(string sheetName)
    {
        if (sheetName == _currentSheet)
        {
            TextGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TextGrid.CommitEdit(DataGridEditingUnit.Row, true);
            SaveCurrentTextRows();
        }

        return _database.LoadTextRows(sheetName)
            .Where(row => !string.IsNullOrWhiteSpace(row.Text))
            .Select(row => (row.RowNumber, row.Text))
            .ToList();
    }

    private int GetLastCopiedIndex(string sheetName, List<(int RowNumber, string Text)> values)
    {
        var lastRow = _lastCopiedTextRows.TryGetValue(sheetName, out var storedRow)
            ? storedRow
            : _database.LoadActionState($"Text:{sheetName}");

        if (lastRow <= 0)
            return -1;

        var index = values.FindIndex(value => value.RowNumber == lastRow);
        if (index >= 0)
            return index;

        return values.FindLastIndex(value => value.RowNumber < lastRow);
    }

    private void CopyTextAt(string sheetName, (int RowNumber, string Text) value, bool isPrevious)
    {
        ClipboardService.WriteText(value.Text);
        _lastCopiedTextRows[sheetName] = value.RowNumber;
        _database.SaveActionState($"Text:{sheetName}", value.RowNumber);
        _lastActiveTextSheet = sheetName;
        _database.SaveActionState("LastTextSheet", int.Parse(sheetName[3..],
            System.Globalization.CultureInfo.InvariantCulture));
        UpdateTextCopyButtonLabels();

        var direction = isPrevious ? "previous row" : "next row";
        ShowStatus($"{sheetName} · Row {value.RowNumber} copied ({direction}).");
    }

    public void PasteClipboardIntoColumn(char column)
    {
        try
        {
            var clipboardText = ClipboardService.ReadText();
            if (string.IsNullOrEmpty(clipboardText))
            {
                ShowStatus("Clipboard is empty. Copy some text first.");
                return;
            }

            var targetRow = MainRows.FirstOrDefault(row =>
                string.IsNullOrWhiteSpace(GetColumnValue(row, column)));

            if (targetRow is null)
            {
                var nextNumber = MainRows.Count == 0 ? 1 : MainRows.Max(row => row.RowNumber) + 1;
                targetRow = new SheetRow { RowNumber = nextNumber };
                MainRows.Add(targetRow);
            }

            SetColumnValue(targetRow, column, clipboardText);
            _lastColumnRows[column] = targetRow.RowNumber;
            _database.SaveActionState($"Column:{column}", targetRow.RowNumber);
            SaveMainSheet();
            UpdateColumnActionButtonLabels();
            MainGrid.Items.Refresh();

            ShowStatus($"Pasted clipboard text into {column}{targetRow.RowNumber}. Last used row: {column}{targetRow.RowNumber}.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Paste failed: {ex.Message}");
        }
    }

    private static string GetColumnValue(SheetRow row, char column) => column switch
    {
        'A' => row.A,
        'B' => row.B,
        'C' => row.C,
        'D' => row.D,
        'E' => row.E,
        _ => string.Empty
    };

    private static void SetColumnValue(SheetRow row, char column, string value)
    {
        switch (column)
        {
            case 'A': row.A = value; break;
            case 'B': row.B = value; break;
            case 'C': row.C = value; break;
            case 'D': row.D = value; break;
            case 'E': row.E = value; break;
        }
    }


    private void ExportTextCsv_Click(object sender, RoutedEventArgs e)
    {
        TextGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        TextGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveCurrentTextRows();

        var dialog = new SaveFileDialog
        {
            Title = $"Export {_currentSheet} as CSV",
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"{_currentSheet}.csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var lines = new List<string> { CsvLine("#", "TEXT") };
        lines.AddRange(CurrentTextRows.Select(row =>
            CsvLine(row.RowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Text)));

        WriteCsv(dialog.FileName, lines);
        ShowStatus($"{_currentSheet} exported as UTF-8 CSV for Google Sheets.");
    }

    private void ExportMainCsv_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveMainSheet();

        var dialog = new SaveFileDialog
        {
            Title = "Export A–E sheet as CSV",
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = "QuickDock-AE.csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var lines = new List<string> { CsvLine("ROW", "A", "B", "C", "D", "E") };
        lines.AddRange(MainRows.Select(row => CsvLine(
            row.RowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            row.A, row.B, row.C, row.D, row.E)));

        WriteCsv(dialog.FileName, lines);
        ShowStatus("A–E sheet exported as UTF-8 CSV for Google Sheets.");
    }

    private static string CsvLine(params string?[] fields) =>
        string.Join(",", fields.Select(CsvEscape));

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') ||
            value.Contains('\r') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static void WriteCsv(string path, IEnumerable<string> lines)
    {
        var content = string.Join("\r\n", lines) + "\r\n";
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public void GenerateTotpFromClipboard()
    {
        try
        {
            var secret = ClipboardService.ReadText();
            var code = TotpService.GenerateFromSecret(secret);
            ClipboardService.WriteText(code);
            ShowStatus("2FA code generated and copied. It refreshes every 30 seconds.");
        }
        catch (Exception ex)
        {
            ShowStatus($"2FA: {ex.Message}");
        }
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _database.SaveSettings(Settings);
        ApplyFloatingSettings();
    }

    private void ApplyFloatingSettings()
    {
        if (Settings.FloatingEnabled)
        {
            if (_floatingWindow is null)
            {
                _floatingWindow = new FloatingWindow(this);
                var createdWindow = _floatingWindow;
                createdWindow.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_floatingWindow, createdWindow))
                        _floatingWindow = null;
                };
            }

            _floatingWindow.UpdateButtonVisibility(Settings);
            UpdateTextCopyButtonLabels();
            UpdateColumnActionButtonLabels();
            if (!_floatingWindow.IsVisible)
                _floatingWindow.Show();
        }
        else if (_floatingWindow is not null)
        {
            var window = _floatingWindow;
            _floatingWindow = null;
            window.Close();
        }
    }

    public void ShowStatus(string message)
    {
        StatusLabel.Text = message;
    }

    protected override void OnClosed(EventArgs e)
    {
        _doubleClickStates.Clear();

        _floatingWindow?.Close();
        _floatingWindow = null;
        base.OnClosed(e);
    }
}
