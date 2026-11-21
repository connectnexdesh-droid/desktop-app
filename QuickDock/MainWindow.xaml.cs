using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickDock.Models;
using QuickDock.Services;

namespace QuickDock;

public partial class MainWindow : Window
{
    private readonly DatabaseService _database = new();
    private readonly Dictionary<string, (int Index, DateTime LastClickUtc)> _copyStates = new();
    private FloatingWindow? _floatingWindow;
    private string _currentSheet = "TXT1";
    private bool _ready;

    public AppSettings Settings { get; }
    public ObservableCollection<TextRow> CurrentTextRows { get; } = new();
    public ObservableCollection<SheetRow> MainRows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _database.Initialize();
        Settings = _database.LoadSettings();
        DataContext = this;

        LoadTextSheet(_currentSheet);
        LoadMainSheet();

        _ready = true;
        ApplyFloatingSettings();
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

        SaveCurrentTextRows();
        _currentSheet = GetSelectedSheetName();
        LoadTextSheet(_currentSheet);
    }

    private void SaveCurrentTextRows()
    {
        if (!_ready)
            return;

        var rowNumber = 1;
        foreach (var row in CurrentTextRows)
        {
            if (!string.IsNullOrWhiteSpace(row.Text))
                row.RowNumber = rowNumber++;
        }

        _database.SaveTextRows(_currentSheet, CurrentTextRows);
    }

    private void TextGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(SaveCurrentTextRows), DispatcherPriority.Background);
    }

    private void AddTextRow_Click(object sender, RoutedEventArgs e)
    {
        CurrentTextRows.Add(new TextRow { RowNumber = CurrentTextRows.Count + 1 });
        TextGrid.SelectedItem = CurrentTextRows[^1];
        TextGrid.ScrollIntoView(TextGrid.SelectedItem);
        ShowStatus($"{_currentSheet}: new row added.");
    }

    private void DeleteTextRow_Click(object sender, RoutedEventArgs e)
    {
        if (TextGrid.SelectedItem is not TextRow selected)
        {
            ShowStatus("Select a row to delete.");
            return;
        }

        CurrentTextRows.Remove(selected);
        if (CurrentTextRows.Count == 0)
            CurrentTextRows.Add(new TextRow { RowNumber = 1 });

        SaveCurrentTextRows();
        ShowStatus($"{_currentSheet}: selected row deleted.");
    }

    private void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(SaveMainSheet), DispatcherPriority.Background);
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
        SaveMainSheet();
        ShowStatus("A–E sheet saved.");
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
            if (sheetName == _currentSheet)
                SaveCurrentTextRows();

            var values = _database.LoadTextRows(sheetName)
                .Select(row => row.Text)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
            {
                ShowStatus($"{sheetName} has no text rows yet.");
                return;
            }

            var now = DateTime.UtcNow;
            var nextIndex = 0;

            if (_copyStates.TryGetValue(sheetName, out var previous))
            {
                var clickedWithinTwoSeconds = (now - previous.LastClickUtc).TotalSeconds <= 2;
                nextIndex = clickedWithinTwoSeconds
                    ? previous.Index
                    : (previous.Index + 1) % values.Count;
            }

            nextIndex %= values.Count;
            ClipboardService.WriteText(values[nextIndex]);
            _copyStates[sheetName] = (nextIndex, now);

            ShowStatus($"{sheetName} · Row {nextIndex + 1} copied.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Copy failed: {ex.Message}");
        }
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
            SaveMainSheet();
            MainGrid.Items.Refresh();

            ShowStatus($"Pasted clipboard text into {column}{targetRow.RowNumber}.");
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
        _floatingWindow?.Close();
        _floatingWindow = null;
        base.OnClosed(e);
    }
}
