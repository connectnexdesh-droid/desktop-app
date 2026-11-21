using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickDock.Models;

public sealed class SheetRow : INotifyPropertyChanged
{
    private string _a = string.Empty;
    private string _b = string.Empty;
    private string _c = string.Empty;
    private string _d = string.Empty;
    private string _e = string.Empty;

    public int RowNumber { get; set; }

    public string A { get => _a; set => Set(ref _a, value); }
    public string B { get => _b; set => Set(ref _b, value); }
    public string C { get => _c; set => Set(ref _c, value); }
    public string D { get => _d; set => Set(ref _d, value); }
    public string E { get => _e; set => Set(ref _e, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        value ??= string.Empty;
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
