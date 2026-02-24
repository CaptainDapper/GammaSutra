using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using GammaControl.Controls;
using GammaControl.Models;

namespace GammaControl;

public partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private readonly Dictionary<string, MonitorSettings> _monitorSettings = new();
    private readonly Dictionary<string, MonitorSettings> _loadedMonitorSettings = new();
    private string _currentProfileName = string.Empty;
    private bool _suppressEvents = false;
    private bool _isDrawMode = false;
    private bool _isInitialized = false;

    internal ProfileService ProfileService { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        _isInitialized = true;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeyManager = new HotkeyManager(hwnd);

        ProfileService.Load();
        PopulateMonitors();

        if (ProfileService.LastUsedProfileName is { } lastName)
        {
            var profile = ProfileService.GetProfile(lastName);
            if (profile != null)
                ApplyProfile(profile);
        }

        ReRegisterHotkeys();
    }

    // ── Monitor management ──────────────────────────────────────────────────

    private void PopulateMonitors()
    {
        MonitorComboBox.Items.Clear();
        foreach (var screen in Screen.AllScreens)
        {
            MonitorComboBox.Items.Add(screen.DeviceName);
            if (!_monitorSettings.ContainsKey(screen.DeviceName))
                _monitorSettings[screen.DeviceName] = new MonitorSettings { DeviceName = screen.DeviceName };
        }
        if (MonitorComboBox.Items.Count > 0)
            MonitorComboBox.SelectedIndex = 0;
    }

    private string? SelectedDevice => MonitorComboBox.SelectedItem as string;

    private MonitorSettings? CurrentSettings =>
        SelectedDevice is { } dev && _monitorSettings.TryGetValue(dev, out var s) ? s : null;

    private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadUIFromCurrentSettings();

    private void LoadUIFromCurrentSettings()
    {
        var s = CurrentSettings;
        if (s == null) return;

        _suppressEvents = true;
        GammaSlider.Value      = s.Gamma;
        BrightnessSlider.Value = s.Brightness;
        ContrastSlider.Value   = s.Contrast;
        SCurveSlider.Value     = s.SCurve;
        HighlightsSlider.Value = s.Highlights;
        ShadowsSlider.Value    = s.Shadows;
        GammaLabel.Text        = s.Gamma.ToString("F2");
        BrightnessLabel.Text   = s.Brightness.ToString("F2");
        ContrastLabel.Text     = s.Contrast.ToString("F2");
        SCurveLabel.Text       = s.SCurve.ToString("F2");
        HighlightsLabel.Text   = s.Highlights.ToString("F2");
        ShadowsLabel.Text      = s.Shadows.ToString("F2");
        _suppressEvents = false;

        RefreshCurve();
    }

    // ── Sliders ─────────────────────────────────────────────────────────────

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _suppressEvents || _isDrawMode) return;

        GammaLabel.Text      = GammaSlider.Value.ToString("F2");
        BrightnessLabel.Text = BrightnessSlider.Value.ToString("F2");
        ContrastLabel.Text   = ContrastSlider.Value.ToString("F2");
        SCurveLabel.Text     = SCurveSlider.Value.ToString("F2");
        HighlightsLabel.Text = HighlightsSlider.Value.ToString("F2");
        ShadowsLabel.Text    = ShadowsSlider.Value.ToString("F2");

        var s = CurrentSettings;
        if (s != null)
        {
            s.Gamma      = GammaSlider.Value;
            s.Brightness = BrightnessSlider.Value;
            s.Contrast   = ContrastSlider.Value;
            s.SCurve     = SCurveSlider.Value;
            s.Highlights = HighlightsSlider.Value;
            s.Shadows    = ShadowsSlider.Value;
            s.UseDrawnCurve = false;
        }

        ApplyCurrentToMonitor();
        RefreshCurve();
        MarkDirty();
    }

    // ── Draw mode ────────────────────────────────────────────────────────────

    private void DrawModeButton_Checked(object sender, RoutedEventArgs e)
    {
        _isDrawMode = true;
        SetSlidersEnabled(false);

        var s = CurrentSettings;
        if (s != null && !s.UseDrawnCurve)
        {
            s.DrawnRamp = GammaCalculator.BuildRamp(
                s.Gamma, s.Brightness, s.Contrast, s.SCurve, s.Highlights, s.Shadows);
            s.UseDrawnCurve = true;
        }

        if (s?.DrawnRamp != null)
            CurveControl.UpdateRamp(s.DrawnRamp);

        CurveControl.SetDrawMode(true);
    }

    private void DrawModeButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isDrawMode = false;
        SetSlidersEnabled(true);

        var s = CurrentSettings;
        if (s != null)
            s.UseDrawnCurve = false;

        CurveControl.SetDrawMode(false);
        RefreshCurve();
        ApplyCurrentToMonitor();
    }

    private void SetSlidersEnabled(bool enabled)
    {
        GammaSlider.IsEnabled      = enabled;
        BrightnessSlider.IsEnabled = enabled;
        ContrastSlider.IsEnabled   = enabled;
        SCurveSlider.IsEnabled     = enabled;
        HighlightsSlider.IsEnabled = enabled;
        ShadowsSlider.IsEnabled    = enabled;
    }

    private void CurveControl_DrawnRampChanged(object sender, ushort[] ramp)
    {
        var s = CurrentSettings;
        if (s == null) return;
        s.DrawnRamp = ramp;
        s.UseDrawnCurve = true;
        ApplyCurrentToMonitor();
        MarkDirty();
    }

    // ── Apply / Reset ────────────────────────────────────────────────────────

    private NativeMethods.RAMP BuildRampForSettings(MonitorSettings s)
    {
        return s.UseDrawnCurve && s.DrawnRamp != null
            ? GammaCalculator.BuildFullRampFromArray(s.DrawnRamp)
            : GammaCalculator.BuildFullRamp(s.Gamma, s.Brightness, s.Contrast,
                                            s.SCurve, s.Highlights, s.Shadows);
    }

    private void ApplyCurrentToMonitor()
    {
        var s = CurrentSettings;
        if (s == null || SelectedDevice == null) return;
        MonitorService.ApplyRamp(SelectedDevice, BuildRampForSettings(s));
    }

    private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
    {
        var s = CurrentSettings;
        if (s == null) return;
        var ramp = BuildRampForSettings(s);
        foreach (var screen in Screen.AllScreens)
            MonitorService.ApplyRamp(screen.DeviceName, ramp);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        MonitorService.ResetAll();

        var s = CurrentSettings;
        if (s != null)
        {
            s.Gamma = 1.0; s.Brightness = 0.0; s.Contrast = 1.0;
            s.SCurve = 0.0; s.Highlights = 0.0; s.Shadows = 0.0;
            s.UseDrawnCurve = false;
            s.DrawnRamp = null;
        }

        _suppressEvents = true;
        GammaSlider.Value = 1.0;      BrightnessSlider.Value = 0.0; ContrastSlider.Value = 1.0;
        SCurveSlider.Value = 0.0;     HighlightsSlider.Value = 0.0; ShadowsSlider.Value = 0.0;
        GammaLabel.Text = "1.00";     BrightnessLabel.Text = "0.00"; ContrastLabel.Text = "1.00";
        SCurveLabel.Text = "0.00";    HighlightsLabel.Text = "0.00"; ShadowsLabel.Text = "0.00";
        _suppressEvents = false;

        RefreshCurve();
        MarkDirty();
    }

    // ── Curve refresh ────────────────────────────────────────────────────────

    private void RefreshCurve()
    {
        var s = CurrentSettings;
        if (s == null) return;

        var ramp = s.UseDrawnCurve && s.DrawnRamp != null
            ? s.DrawnRamp
            : GammaCalculator.BuildRamp(s.Gamma, s.Brightness, s.Contrast,
                                        s.SCurve, s.Highlights, s.Shadows);
        CurveControl.UpdateRamp(ramp);
    }

    // ── Profile management ───────────────────────────────────────────────────

    private void MarkDirty()
    {
        if (!_isInitialized || string.IsNullOrEmpty(_currentProfileName)) return;
        SaveProfileButton.Visibility = Visibility.Visible;
    }

    private void ClearDirty()
    {
        if (_isInitialized)
            SaveProfileButton.Visibility = Visibility.Collapsed;
    }

    private void UpdateCurrentProfileLabel()
    {
        CurrentProfileLabel.Text = string.IsNullOrEmpty(_currentProfileName)
            ? "(none)"
            : _currentProfileName;
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileName)) return;

        var existing = ProfileService.GetProfile(_currentProfileName);
        var profile = new Profile
        {
            Name            = _currentProfileName,
            HotkeyModifiers = existing?.HotkeyModifiers ?? 0,
            HotkeyVKey      = existing?.HotkeyVKey      ?? 0,
            // Clone values so the saved profile is independent of live _monitorSettings
            MonitorSettings = _monitorSettings.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())
        };

        ProfileService.AddOrUpdate(profile);
        ProfileService.LastUsedProfileName = _currentProfileName;
        ProfileService.Save();

        // Advance the loaded snapshot so Revert returns to this saved state
        _loadedMonitorSettings.Clear();
        foreach (var kv in _monitorSettings)
            _loadedMonitorSettings[kv.Key] = kv.Value.Clone();

        ClearDirty();
    }

    private void RevertProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_loadedMonitorSettings.Any()) return;

        foreach (var kv in _loadedMonitorSettings)
            _monitorSettings[kv.Key] = kv.Value.Clone();

        LoadUIFromCurrentSettings();
        ApplyCurrentToMonitor();
        ClearDirty();
    }

    private void ManageProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        new ProfilesWindow(this).ShowDialog();
    }

    internal void ApplyProfile(Profile profile)
    {
        _currentProfileName = profile.Name;
        ProfileService.LastUsedProfileName = profile.Name;

        foreach (var kv in profile.MonitorSettings)
            _monitorSettings[kv.Key] = kv.Value.Clone();

        // Snapshot for Revert
        _loadedMonitorSettings.Clear();
        foreach (var kv in profile.MonitorSettings)
            _loadedMonitorSettings[kv.Key] = kv.Value.Clone();

        foreach (var kv in profile.MonitorSettings)
            MonitorService.ApplyRamp(kv.Key, BuildRampForSettings(kv.Value));

        LoadUIFromCurrentSettings();
        UpdateCurrentProfileLabel();
        ClearDirty();
    }

    internal void ApplyProfileByName(string name)
    {
        var profile = ProfileService.GetProfile(name);
        if (profile != null)
            Dispatcher.Invoke(() => ApplyProfile(profile));
    }

    internal void ReRegisterHotkeys()
    {
        _hotkeyManager?.UnregisterAll();
        foreach (var p in ProfileService.Profiles)
        {
            if (p.HotkeyVKey == 0) continue;
            var captured = p;
            _hotkeyManager?.Register(captured.HotkeyModifiers, captured.HotkeyVKey, () =>
                Dispatcher.Invoke(() => ApplyProfile(captured)));
        }
    }

    internal Dictionary<string, MonitorSettings> CurrentMonitorSettings()
        => _monitorSettings.ToDictionary(kv => kv.Key, kv => kv.Value);

    internal void OnProfileDeleted(string name)
    {
        if (_currentProfileName == name)
        {
            _currentProfileName = string.Empty;
            UpdateCurrentProfileLabel();
            ClearDirty();
        }
    }

    // ── Tray / close ─────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    internal void Cleanup()
    {
        _hotkeyManager?.Dispose();
        ProfileService.Save();
    }
}
