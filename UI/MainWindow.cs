using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FullRoomSniper.Core;

namespace FullRoomSniper.UI;

public class MainWindow : Window
{
    private readonly Extension ext;
    private readonly FlatButton btnStartStop;
    private readonly RichTextBox rtbLog;
    private string lastMessage = "";
    private bool _dragging;
    private Point _dragStart;

    public MainWindow(Extension extension)
    {
        ext = extension;

        Title = "Full Room Sniper";

        Width = 400;
        Height = 260;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Background = Theme.BgDark;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = BuildHeader(out btnStartStop);
        Grid.SetRow(header, 0);

        var logArea = BuildLog(out rtbLog);
        Grid.SetRow(logArea, 1);

        root.Children.Add(header);
        root.Children.Add(logArea);
        Content = root;

        ext.LogMessage += OnLog;
        Closed += (_, _) => ext.LogMessage -= OnLog;
    }

    private Border BuildHeader(out FlatButton btnAction)
    {
        var header = new Border
        {
            Background = Theme.BgHeader,
            Padding = new Thickness(12, 0, 6, 0),
            Height = 38,
        };

        header.MouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(this);
            _dragging = true;
            header.CaptureMouse();
        };
        header.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            var pos = e.GetPosition(this);
            Left += pos.X - _dragStart.X;
            Top += pos.Y - _dragStart.Y;
        };
        header.MouseLeftButtonUp += (_, _) => { _dragging = false; header.ReleaseMouseCapture(); };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/UI/icon.ico")),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        var title = new TextBlock
        {
            Text = "Full Room Sniper",
            Foreground = Theme.TextMain,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 1);

        btnAction = new FlatButton("Start", Theme.Green, Theme.GreenHov, width: 72);

        bool pinned = false;
        var btnPin = new FlatButton("📌", Theme.BgHeader, Theme.BgSurface, width: 26, fontSize: 10, fontFamily: new FontFamily("Segoe UI Symbol"))
        {
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Always on top"
        };
        btnPin.SetForeground(Theme.TextMuted);
        btnPin.Clicked += () =>
        {
            pinned = !pinned;
            Topmost = pinned;
            btnPin.SetForeground(pinned ? Theme.TextMain : Theme.TextMuted);
        };

        var btnMinimize = new FlatButton("─", Theme.BgHeader, Theme.BgSurface, width: 32, fontSize: 18)
        {
            Margin = new Thickness(4, 0, 0, 0)
        };
        btnMinimize.Clicked += () => WindowState = WindowState.Minimized;

        var btnClose = new FlatButton("✕", Theme.BgHeader, Theme.CloseHov, width: 32, fontSize: 13)
        {
            Margin = new Thickness(4, 0, 0, 0)
        };
        btnClose.Clicked += () => Close();

        btnAction.Clicked += () => OnStartStop();

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        btnRow.Children.Add(btnAction);
        btnRow.Children.Add(btnPin);
        btnRow.Children.Add(btnMinimize);
        btnRow.Children.Add(btnClose);
        Grid.SetColumn(btnRow, 2);

        grid.Children.Add(icon);
        grid.Children.Add(title);
        grid.Children.Add(btnRow);
        header.Child = grid;
        return header;
    }

    private static Border BuildLog(out RichTextBox rtb)
    {
        rtb = new RichTextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Background = Brushes.Transparent,
            Foreground = Theme.TextMain,
            Padding = new Thickness(10, 8, 10, 8),
            CaretBrush = Brushes.Transparent,
        };
        return new Border { Background = Theme.BgDark, Child = rtb };
    }

    private void SetRunning(bool running)
    {
        btnStartStop.SetLabel(running ? "Stop" : "Start");
        btnStartStop.SetColors(
            running ? Theme.Red : Theme.Green,
            running ? Theme.RedHov : Theme.GreenHov);
    }

    private void OnStartStop()
    {
        if (ext.RoomSniper.IsMonitoring || ext.RoomSniper.StartMonitor)
        {
            ext.RoomSniper.Stop();
            return;
        }

        ClearLog();
        ext.RoomSniper.StartMonitor = true;
        SetRunning(true);
        AppendLine("Try to enter the room you want to target...", Theme.TextMain);
    }

    private void OnLog(string message)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => OnLog(message)); return; }

        if (message == "\x00clear") { ClearLog(); SetRunning(false); return; }
        if (message == "\x00stop") { SetRunning(false); return; }
        if (message == lastMessage) return;

        lastMessage = message;

        AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}", Theme.TextMain);
    }

    private void AppendLine(string text, SolidColorBrush? color = null)
    {
        var run = new Run(text) { Foreground = color ?? Theme.TextMain };
        rtbLog.Document.Blocks.Add(new Paragraph(run) { Margin = new Thickness(0) });
        rtbLog.ScrollToEnd();
    }

    private void ClearLog()
    {
        rtbLog.Document.Blocks.Clear();
        lastMessage = "";
    }
}
