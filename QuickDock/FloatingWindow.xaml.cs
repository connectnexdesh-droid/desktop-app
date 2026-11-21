using System.Windows;
using System.Windows.Input;
using QuickDock.Models;

namespace QuickDock;

public partial class FloatingWindow : Window
{
    private readonly MainWindow _owner;

    public FloatingWindow(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        Loaded += (_, _) => PositionBottomRight();
    }

    public void UpdateButtonVisibility(AppSettings settings)
    {
        Txt1Button.Visibility = settings.Txt1Enabled ? Visibility.Visible : Visibility.Collapsed;
        Txt2Button.Visibility = settings.Txt2Enabled ? Visibility.Visible : Visibility.Collapsed;
        Txt3Button.Visibility = settings.Txt3Enabled ? Visibility.Visible : Visibility.Collapsed;
        Txt4Button.Visibility = settings.Txt4Enabled ? Visibility.Visible : Visibility.Collapsed;
        TwoFaButton.Visibility = settings.TwoFaEnabled ? Visibility.Visible : Visibility.Collapsed;
        AButton.Visibility = settings.AEnabled ? Visibility.Visible : Visibility.Collapsed;
        BButton.Visibility = settings.BEnabled ? Visibility.Visible : Visibility.Collapsed;
        CButton.Visibility = settings.CEnabled ? Visibility.Visible : Visibility.Collapsed;
        DButton.Visibility = settings.DEnabled ? Visibility.Visible : Visibility.Collapsed;
        EButton.Visibility = settings.EEnabled ? Visibility.Visible : Visibility.Collapsed;

        var anyEnabled = settings.Txt1Enabled || settings.Txt2Enabled ||
                         settings.Txt3Enabled || settings.Txt4Enabled ||
                         settings.TwoFaEnabled || settings.AEnabled ||
                         settings.BEnabled || settings.CEnabled ||
                         settings.DEnabled || settings.EEnabled;

        EmptyState.Visibility = anyEnabled ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 22;
        Top = workArea.Bottom - Height - 70;
    }

    private void ExpandPanel()
    {
        BubbleRoot.Visibility = Visibility.Collapsed;
        PanelRoot.Visibility = Visibility.Visible;
        Width = 326;
        Height = 438;
        PositionBottomRight();
    }

    private void CollapsePanel()
    {
        PanelRoot.Visibility = Visibility.Collapsed;
        BubbleRoot.Visibility = Visibility.Visible;
        Width = 62;
        Height = 62;
        PositionBottomRight();
    }

    private void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ExpandPanel();
        e.Handled = true;
    }

    private void Bubble_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); }
        catch (InvalidOperationException) { }
        e.Handled = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => CollapsePanel();

    private void Txt1_Click(object sender, RoutedEventArgs e) => _owner.CopyNextText("TXT1");
    private void Txt2_Click(object sender, RoutedEventArgs e) => _owner.CopyNextText("TXT2");
    private void Txt3_Click(object sender, RoutedEventArgs e) => _owner.CopyNextText("TXT3");
    private void Txt4_Click(object sender, RoutedEventArgs e) => _owner.CopyNextText("TXT4");

    private void TwoFa_Click(object sender, RoutedEventArgs e) => _owner.GenerateTotpFromClipboard();

    private void A_Click(object sender, RoutedEventArgs e) => _owner.PasteClipboardIntoColumn('A');
    private void B_Click(object sender, RoutedEventArgs e) => _owner.PasteClipboardIntoColumn('B');
    private void C_Click(object sender, RoutedEventArgs e) => _owner.PasteClipboardIntoColumn('C');
    private void D_Click(object sender, RoutedEventArgs e) => _owner.PasteClipboardIntoColumn('D');
    private void E_Click(object sender, RoutedEventArgs e) => _owner.PasteClipboardIntoColumn('E');
}
