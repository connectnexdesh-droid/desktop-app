using System.Windows;

namespace QuickDock.Services;

public static class ClipboardService
{
    public static string ReadText()
    {
        return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
    }

    public static void WriteText(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Clipboard text cannot be empty.", nameof(value));

        System.Windows.Clipboard.SetText(value);
    }
}
