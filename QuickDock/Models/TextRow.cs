using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickDock.Models;

public sealed class TextRow : INotifyPropertyChanged
{
    private string _text = string.Empty;

    public int RowNumber { get; set; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
