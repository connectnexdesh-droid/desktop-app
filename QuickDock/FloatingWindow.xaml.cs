using System.Windows;
using System.Windows.Input;
using QuickDock.Models;

namespace QuickDock;

public partial class FloatingWindow : Window
{
    private readonly MainWindow _owner;
    private bool _pointerPressed;
    private bool _dragging;
    private Point _dragStart;
    private double _dragWindowLeft;
    private double _dragWindowTop;
    private double _bubbleLeft;
    private double _bubbleTop;

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
        _bubbleLeft = Left;
        _bubbleTop = Top;
    }

    private void ExpandPanel()
    {
        if (PanelRoot.Visibility == Visibility.Visible)
            return;

        _bubbleLeft = Left;
        _bubbleTop = Top;

        Width = 350;
        Height = 470;

        var workArea = SystemParameters.WorkArea;
        var maxLeft = Math.Max(workArea.Left + 8, workArea.Right - Width - 8);
        var maxTop = Math.Max(workArea.Top + 8, workArea.Bottom - Height - 8);

        Left = Math.Clamp(_bubbleLeft, workArea.Left + 8, maxLeft);
        Top = Math.Clamp(_bubbleTop, workArea.Top + 8, maxTop);

        BubbleRoot.Visibility = Visibility.Collapsed;
        PanelRoot.Visibility = Visibility.Visible;
    }

    private void CollapsePanel()
    {
        PanelRoot.Visibility = Visibility.Collapsed;
        Width = 68;
        Height = 68;
        Left = _bubbleLeft;
        Top = _bubbleTop;
        BubbleRoot.Visibility = Visibility.Visible;
    }

    private void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        _pointerPressed = true;
        _dragging = false;
        _dragStart = e.GetPosition(null);
        _dragWindowLeft = Left;
        _dragWindowTop = Top;

        BubbleRoot.CaptureMouse();
        e.Handled = true;
    }

    private void Bubble_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pointerPressed || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(null);
        var deltaX = current.X - _dragStart.X;
        var deltaY = current.Y - _dragStart.Y;

        if (!_dragging &&
            (Math.Abs(deltaX) >= SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(deltaY) >= SystemParameters.MinimumVerticalDragDistance))
        {
            _dragging = true;
        }

        if (!_dragging)
            return;

        var workArea = SystemParameters.WorkArea;
        var maxLeft = Math.Max(workArea.Left, workArea.Right - Width);
        var maxTop = Math.Max(workArea.Top, workArea.Bottom - Height);

        Left = Math.Clamp(_dragWindowLeft + deltaX, workArea.Left, maxLeft);
        Top = Math.Clamp(_dragWindowTop + deltaY, workArea.Top, maxTop);

        _bubbleLeft = Left;
        _bubbleTop = Top;
    }

    private void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pointerPressed)
            return;

        _pointerPressed = false;
        if (BubbleRoot.IsMouseCaptured)
            BubbleRoot.ReleaseMouseCapture();

        if (!_dragging)
            ExpandPanel();

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
