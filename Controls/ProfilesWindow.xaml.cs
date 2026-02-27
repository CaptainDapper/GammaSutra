using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GammaControl.Models;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace GammaControl.Controls;

public partial class ProfilesWindow : Window
{
    private readonly MainWindow _mainWindow;
    private Profile? _captureTarget;
    private uint _capturedMods;
    private uint _capturedVKey;

    public ProfilesWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        Owner = mainWindow;
        RefreshList();
    }

    // ── Profile list ─────────────────────────────────────────────────────────

    private void RefreshList()
    {
        ProfilesStack.Children.Clear();
        foreach (var profile in _mainWindow.ProfileService.Profiles)
            ProfilesStack.Children.Add(CreateRow(profile));
    }

    private UIElement CreateRow(Profile profile)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBlock = new TextBlock
        {
            Text = profile.Name,
            VerticalAlignment = VerticalAlignment.Center
        };

        var hotkeyBlock = new TextBlock
        {
            Text = HotkeyHelper.Format(profile.HotkeyModifiers, profile.HotkeyVKey),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };

        var loadBtn = new Button
        {
            Content = "Load",
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(4, 0, 0, 0)
        };
        loadBtn.Click += (_, _) => { _mainWindow.ApplyProfile(profile); Close(); };

        var hotkeyBtn = new Button
        {
            Content = "Hotkey",
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(4, 0, 0, 0)
        };
        hotkeyBtn.Click += (_, _) => BeginCapture(profile);

        var deleteBtn = new Button
        {
            Content = "Delete",
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(4, 0, 0, 0)
        };
        deleteBtn.Click += (_, _) => DeleteProfile(profile);

        Grid.SetColumn(nameBlock,  0);
        Grid.SetColumn(hotkeyBlock, 1);
        Grid.SetColumn(loadBtn,    2);
        Grid.SetColumn(hotkeyBtn,  3);
        Grid.SetColumn(deleteBtn,  4);

        grid.Children.Add(nameBlock);
        grid.Children.Add(hotkeyBlock);
        grid.Children.Add(loadBtn);
        grid.Children.Add(hotkeyBtn);
        grid.Children.Add(deleteBtn);

        return grid;
    }

    private void CreateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var existing = _mainWindow.ProfileService.GetProfile(name);
        var profile = new Profile
        {
            Name            = name,
            HotkeyModifiers = existing?.HotkeyModifiers ?? 0,
            HotkeyVKey      = existing?.HotkeyVKey      ?? 0,
            Settings        = _mainWindow.CurrentSettingsClone() ?? new MonitorSettings()
        };

        _mainWindow.ProfileService.AddOrUpdate(profile);
        _mainWindow.ProfileService.Save();
        NewProfileNameBox.Text = string.Empty;
        _mainWindow.ApplyProfile(profile);
        RefreshList();
    }

    private void NewProfileNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CreateProfileButton_Click(sender, new RoutedEventArgs());
    }

    private void DeleteProfile(Profile profile)
    {
        _mainWindow.ProfileService.Delete(profile.Name);
        _mainWindow.ProfileService.Save();
        _mainWindow.ReRegisterHotkeys();
        _mainWindow.OnProfileDeleted(profile.Name);
        CancelCapture();
        RefreshList();
    }

    // ── Hotkey capture ────────────────────────────────────────────────────────

    private void BeginCapture(Profile profile)
    {
        _captureTarget = profile;
        _capturedMods  = profile.HotkeyModifiers;
        _capturedVKey  = profile.HotkeyVKey;

        CaptureTargetLabel.Text = $"Setting hotkey for \"{profile.Name}\"";
        CaptureBox.Text = _capturedVKey == 0
            ? "Click here, then press your key combination..."
            : HotkeyHelper.Format(_capturedMods, _capturedVKey);
        CaptureConfirmButton.IsEnabled = _capturedVKey != 0;
        CapturePanel.Visibility = Visibility.Visible;
        CaptureBox.Focus();
    }

    private void CaptureBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_capturedVKey == 0)
            CaptureBox.Text = "Press your key combination...";
    }

    private void CaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_captureTarget == null) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore pure modifier keypresses — wait for the main key
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (key == Key.Escape) { CancelCapture(); return; }

        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  mods |= NativeMethods.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   mods |= NativeMethods.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= NativeMethods.MOD_SHIFT;

        _capturedMods = mods;
        _capturedVKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        CaptureBox.Text = HotkeyHelper.Format(_capturedMods, _capturedVKey);
        CaptureConfirmButton.IsEnabled = true;
    }

    private void CaptureConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (_captureTarget == null) return;
        _captureTarget.HotkeyModifiers = _capturedMods;
        _captureTarget.HotkeyVKey      = _capturedVKey;
        CommitHotkey();
    }

    private void CaptureClear_Click(object sender, RoutedEventArgs e)
    {
        if (_captureTarget == null) return;
        _captureTarget.HotkeyModifiers = 0;
        _captureTarget.HotkeyVKey      = 0;
        CommitHotkey();
    }

    private void CaptureCancel_Click(object sender, RoutedEventArgs e) => CancelCapture();

    private void CommitHotkey()
    {
        _mainWindow.ProfileService.Save();
        _mainWindow.ReRegisterHotkeys();
        CancelCapture();
        RefreshList();
    }

    private void CancelCapture()
    {
        _captureTarget = null;
        CapturePanel.Visibility = Visibility.Collapsed;
    }
}
