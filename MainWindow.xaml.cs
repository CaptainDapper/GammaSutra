using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using GammaControl.Controls;
using GammaControl.Models;

namespace GammaControl;

public partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private readonly Dictionary<string, MonitorSettings> _monitorSettings = new();
    private readonly Dictionary<string, MonitorSettings> _loadedSettings = new();
    private readonly Dictionary<string, string> _monitorProfileNames = new();
    private bool _suppressEvents = false;
    private bool _isDrawMode = false;
    private bool _isBezierMode = false;
    private bool _isInitialized = false;
    private int _activeChannel = -1; // -1 = All, 0 = R, 1 = G, 2 = B

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

    private string CurrentProfileName
    {
        get => SelectedDevice is { } dev && _monitorProfileNames.TryGetValue(dev, out var n) ? n : string.Empty;
        set { if (SelectedDevice is { } dev) _monitorProfileNames[dev] = value; }
    }

    private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadUIFromCurrentSettings();
        UpdateCurrentProfileLabel();
        ClearDirty();
    }

    // Helper: which channel index to read from for UI display
    // In "All" mode, read from channel 0 (all 3 are kept in sync)
    private int ReadChannel => _activeChannel < 0 ? 0 : _activeChannel;

    private void LoadUIFromCurrentSettings()
    {
        var s = CurrentSettings;
        if (s == null) return;

        int ch = ReadChannel;
        _suppressEvents = true;
        GammaSlider.Value      = s.Gamma[ch];
        BrightnessSlider.Value = s.Brightness[ch];
        ContrastSlider.Value   = s.Contrast[ch];
        SCurveSlider.Value     = s.SCurve[ch];
        HighlightsSlider.Value = s.Highlights[ch];
        ShadowsSlider.Value    = s.Shadows[ch];
        GammaLabel.Text        = s.Gamma[ch].ToString("F2");
        BrightnessLabel.Text   = s.Brightness[ch].ToString("F2");
        ContrastLabel.Text     = s.Contrast[ch].ToString("F2");
        SCurveLabel.Text       = s.SCurve[ch].ToString("F2");
        HighlightsLabel.Text   = s.Highlights[ch].ToString("F2");
        ShadowsLabel.Text      = s.Shadows[ch].ToString("F2");

        // Posterize
        PosterizeStepsSlider.Value    = s.PosterizeSteps[ch];
        PosterizeRangeMinSlider.Value = s.PosterizeRangeMin[ch];
        PosterizeRangeMaxSlider.Value = s.PosterizeRangeMax[ch];
        PosterizeFeatherSlider.Value  = s.PosterizeFeather[ch];
        PosterizeStepsLabel.Text      = s.PosterizeSteps[ch].ToString();
        PosterizeRangeMinLabel.Text   = s.PosterizeRangeMin[ch].ToString("F2");
        PosterizeRangeMaxLabel.Text   = s.PosterizeRangeMax[ch].ToString("F2");
        PosterizeFeatherLabel.Text    = s.PosterizeFeather[ch].ToString("F2");
        _suppressEvents = false;

        RefreshCurve();
    }

    // ── Channel selector ──────────────────────────────────────────────────────

    private void ChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender == ChannelAllButton) _activeChannel = -1;
        else if (sender == ChannelRButton) _activeChannel = 0;
        else if (sender == ChannelGButton) _activeChannel = 1;
        else if (sender == ChannelBButton) _activeChannel = 2;

        ChannelAllButton.IsChecked = _activeChannel == -1;
        ChannelRButton.IsChecked   = _activeChannel == 0;
        ChannelGButton.IsChecked   = _activeChannel == 1;
        ChannelBButton.IsChecked   = _activeChannel == 2;

        CurveControl.ActiveChannel = _activeChannel;
        LoadUIFromCurrentSettings();
    }

    // ── Sliders ─────────────────────────────────────────────────────────────

    private void WriteToChannels(double[] arr, double value)
    {
        if (_activeChannel < 0)
            arr[0] = arr[1] = arr[2] = value;
        else
            arr[_activeChannel] = value;
    }

    private void WriteToChannels(int[] arr, int value)
    {
        if (_activeChannel < 0)
            arr[0] = arr[1] = arr[2] = value;
        else
            arr[_activeChannel] = value;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _suppressEvents || _isDrawMode || _isBezierMode) return;

        GammaLabel.Text      = GammaSlider.Value.ToString("F2");
        BrightnessLabel.Text = BrightnessSlider.Value.ToString("F2");
        ContrastLabel.Text   = ContrastSlider.Value.ToString("F2");
        SCurveLabel.Text     = SCurveSlider.Value.ToString("F2");
        HighlightsLabel.Text = HighlightsSlider.Value.ToString("F2");
        ShadowsLabel.Text    = ShadowsSlider.Value.ToString("F2");

        var s = CurrentSettings;
        if (s != null)
        {
            WriteToChannels(s.Gamma, GammaSlider.Value);
            WriteToChannels(s.Brightness, BrightnessSlider.Value);
            WriteToChannels(s.Contrast, ContrastSlider.Value);
            WriteToChannels(s.SCurve, SCurveSlider.Value);
            WriteToChannels(s.Highlights, HighlightsSlider.Value);
            WriteToChannels(s.Shadows, ShadowsSlider.Value);
            if (_activeChannel < 0)
                s.UseDrawnCurve[0] = s.UseDrawnCurve[1] = s.UseDrawnCurve[2] = false;
            else
                s.UseDrawnCurve[_activeChannel] = false;
        }

        ApplyCurrentToMonitor();
        RefreshCurve();
        MarkDirty();
    }

    // ── Posterize sliders ─────────────────────────────────────────────────────

    private void PosterizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _suppressEvents) return;

        PosterizeStepsLabel.Text    = ((int)PosterizeStepsSlider.Value).ToString();
        PosterizeRangeMinLabel.Text = PosterizeRangeMinSlider.Value.ToString("F2");
        PosterizeRangeMaxLabel.Text = PosterizeRangeMaxSlider.Value.ToString("F2");
        PosterizeFeatherLabel.Text  = PosterizeFeatherSlider.Value.ToString("F2");

        var s = CurrentSettings;
        if (s != null)
        {
            WriteToChannels(s.PosterizeSteps, (int)PosterizeStepsSlider.Value);
            WriteToChannels(s.PosterizeRangeMin, PosterizeRangeMinSlider.Value);
            WriteToChannels(s.PosterizeRangeMax, PosterizeRangeMaxSlider.Value);
            WriteToChannels(s.PosterizeFeather, PosterizeFeatherSlider.Value);
        }

        ApplyCurrentToMonitor();
        RefreshCurve();
        MarkDirty();
    }

    // ── Draw mode ────────────────────────────────────────────────────────────

    private void DrawModeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isBezierMode)
        {
            _isBezierMode = false;
            BezierModeButton.IsChecked = false;
            CurveControl.SetBezierMode(false);
        }

        _isDrawMode = true;
        SetSlidersEnabled(false);

        var s = CurrentSettings;
        if (s != null)
        {
            s.CurveMode = 1;
            // Initialize drawn ramps for channels that don't have one yet
            for (int ch = 0; ch < 3; ch++)
            {
                if (!s.UseDrawnCurve[ch])
                {
                    s.DrawnRamp[ch] = GammaCalculator.BuildRamp(
                        s.Gamma[ch], s.Brightness[ch], s.Contrast[ch],
                        s.SCurve[ch], s.Highlights[ch], s.Shadows[ch]);
                    s.UseDrawnCurve[ch] = true;
                }
            }
        }

        CurveControl.UpdateRamps(
            s?.DrawnRamp[0] ?? new ushort[256],
            s?.DrawnRamp[1] ?? new ushort[256],
            s?.DrawnRamp[2] ?? new ushort[256],
            _activeChannel);
        CurveControl.SetDrawMode(true);
    }

    private void DrawModeButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isDrawMode = false;
        if (!_isBezierMode)
            SetSlidersEnabled(true);

        var s = CurrentSettings;
        if (s != null && !_isBezierMode)
        {
            s.CurveMode = 0;
            s.UseDrawnCurve[0] = s.UseDrawnCurve[1] = s.UseDrawnCurve[2] = false;
        }

        CurveControl.SetDrawMode(false);
        if (!_isBezierMode)
        {
            RefreshCurve();
            ApplyCurrentToMonitor();
        }
    }

    // ── Bezier mode ───────────────────────────────────────────────────────────

    private void BezierModeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isDrawMode)
        {
            _isDrawMode = false;
            DrawModeButton.IsChecked = false;
            CurveControl.SetDrawMode(false);
        }

        _isBezierMode = true;
        SetSlidersEnabled(false);

        var s = CurrentSettings;
        if (s != null)
        {
            s.CurveMode = 2;
            CurveControl.SetBezierMode(true, s.BezierPoints);

            for (int ch = 0; ch < 3; ch++)
            {
                s.DrawnRamp[ch] = BezierEvaluator.Evaluate(
                    s.BezierPoints[ch] ?? BezierEvaluator.DefaultPoints());
                s.UseDrawnCurve[ch] = true;
                s.BezierPoints[ch] ??= CurveControl.GetBezierPoints(ch);
            }
            ApplyCurrentToMonitor();
        }
        else
        {
            CurveControl.SetBezierMode(true);
        }
    }

    private void BezierModeButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isBezierMode = false;
        CurveControl.SetBezierMode(false);

        if (!_isDrawMode)
        {
            SetSlidersEnabled(true);
            var s = CurrentSettings;
            if (s != null)
            {
                s.CurveMode = 0;
                s.UseDrawnCurve[0] = s.UseDrawnCurve[1] = s.UseDrawnCurve[2] = false;
            }
            RefreshCurve();
            ApplyCurrentToMonitor();
        }
    }

    private void CurveControl_BezierPointsChanged(object sender, BezierPointsChangedEventArgs args)
    {
        var s = CurrentSettings;
        if (s == null) return;
        s.BezierPoints[args.Channel] = args.Points;
        MarkDirty();
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

    private void CurveControl_DrawnRampChanged(object sender, DrawnRampChangedEventArgs args)
    {
        var s = CurrentSettings;
        if (s == null) return;
        s.DrawnRamp[args.Channel] = args.Ramp;
        s.UseDrawnCurve[args.Channel] = true;
        ApplyCurrentToMonitor();
        MarkDirty();
    }

    // ── Apply / Reset ────────────────────────────────────────────────────────

    private ushort[] BuildChannelRamp(MonitorSettings s, int ch)
    {
        ushort[] channel;
        if (s.UseDrawnCurve[ch] && s.DrawnRamp[ch] != null)
            channel = (ushort[])s.DrawnRamp[ch]!.Clone();
        else
            channel = GammaCalculator.BuildRamp(s.Gamma[ch], s.Brightness[ch], s.Contrast[ch],
                                                s.SCurve[ch], s.Highlights[ch], s.Shadows[ch]);

        if (s.PosterizeSteps[ch] >= 2)
            channel = GammaCalculator.ApplyPosterize(channel, s.PosterizeSteps[ch],
                s.PosterizeRangeMin[ch], s.PosterizeRangeMax[ch],
                s.PosterizeFeather[ch], s.PosterizeFeatherCurve[ch]);

        return channel;
    }

    private NativeMethods.RAMP BuildRampForSettings(MonitorSettings s)
    {
        return new NativeMethods.RAMP
        {
            Red   = BuildChannelRamp(s, 0),
            Green = BuildChannelRamp(s, 1),
            Blue  = BuildChannelRamp(s, 2),
        };
    }

    private void ApplyCurrentToMonitor()
    {
        var s = CurrentSettings;
        if (s == null || SelectedDevice == null) return;
        MonitorService.ApplyRamp(SelectedDevice, BuildRampForSettings(s));
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        MonitorService.ResetAll();

        if (_isDrawMode)
        {
            _isDrawMode = false;
            DrawModeButton.IsChecked = false;
            CurveControl.SetDrawMode(false);
        }
        if (_isBezierMode)
        {
            _isBezierMode = false;
            BezierModeButton.IsChecked = false;
            CurveControl.SetBezierMode(false);
        }
        SetSlidersEnabled(true);

        var s = CurrentSettings;
        if (s != null)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                s.Gamma[ch] = 1.0; s.Brightness[ch] = 0.0; s.Contrast[ch] = 1.0;
                s.SCurve[ch] = 0.0; s.Highlights[ch] = 0.0; s.Shadows[ch] = 0.0;
                s.UseDrawnCurve[ch] = false;
                s.DrawnRamp[ch] = null;
                s.BezierPoints[ch] = null;
                s.PosterizeSteps[ch] = 0;
                s.PosterizeRangeMin[ch] = 0.0;
                s.PosterizeRangeMax[ch] = 1.0;
                s.PosterizeFeather[ch] = 0.1;
                s.PosterizeFeatherCurve[ch] = 1.0;
            }
        }

        _suppressEvents = true;
        GammaSlider.Value = 1.0;      BrightnessSlider.Value = 0.0; ContrastSlider.Value = 1.0;
        SCurveSlider.Value = 0.0;     HighlightsSlider.Value = 0.0; ShadowsSlider.Value = 0.0;
        GammaLabel.Text = "1.00";     BrightnessLabel.Text = "0.00"; ContrastLabel.Text = "1.00";
        SCurveLabel.Text = "0.00";    HighlightsLabel.Text = "0.00"; ShadowsLabel.Text = "0.00";
        PosterizeStepsSlider.Value = 0; PosterizeRangeMinSlider.Value = 0.0;
        PosterizeRangeMaxSlider.Value = 1.0; PosterizeFeatherSlider.Value = 0.1;
        PosterizeStepsLabel.Text = "0"; PosterizeRangeMinLabel.Text = "0.00";
        PosterizeRangeMaxLabel.Text = "1.00"; PosterizeFeatherLabel.Text = "0.10";
        _suppressEvents = false;

        RefreshCurve();
        MarkDirty();
    }

    // ── Curve refresh ────────────────────────────────────────────────────────

    private void RefreshCurve()
    {
        var s = CurrentSettings;
        if (s == null) return;

        var ramps = new ushort[3][];
        for (int ch = 0; ch < 3; ch++)
            ramps[ch] = BuildChannelRamp(s, ch);

        CurveControl.UpdateRamps(ramps[0], ramps[1], ramps[2], _activeChannel);
    }

    // ── Profile management ───────────────────────────────────────────────────

    private void MarkDirty()
    {
        if (!_isInitialized || string.IsNullOrEmpty(CurrentProfileName)) return;
        SaveProfileButton.Visibility = Visibility.Visible;
    }

    private void ClearDirty()
    {
        if (_isInitialized)
            SaveProfileButton.Visibility = Visibility.Collapsed;
    }

    private void UpdateCurrentProfileLabel()
    {
        CurrentProfileLabel.Text = string.IsNullOrEmpty(CurrentProfileName)
            ? "(none)"
            : CurrentProfileName;
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CurrentProfileName) || CurrentSettings == null || SelectedDevice == null) return;

        var existing = ProfileService.GetProfile(CurrentProfileName);
        var profile = new Profile
        {
            Name            = CurrentProfileName,
            HotkeyModifiers = existing?.HotkeyModifiers ?? 0,
            HotkeyVKey      = existing?.HotkeyVKey      ?? 0,
            Settings        = CurrentSettings.Clone()
        };

        ProfileService.AddOrUpdate(profile);
        ProfileService.LastUsedProfileName = CurrentProfileName;
        ProfileService.Save();

        _loadedSettings[SelectedDevice] = CurrentSettings.Clone();

        ClearDirty();
    }

    private void RevertProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice == null || !_loadedSettings.TryGetValue(SelectedDevice, out var loaded)) return;

        _monitorSettings[SelectedDevice] = loaded.Clone();

        RestoreCurveMode();
        LoadUIFromCurrentSettings();
        ApplyCurrentToMonitor();
        ClearDirty();
    }

    private void ManageProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        new ProfilesWindow(this).ShowDialog();
    }

    private void RestoreCurveMode()
    {
        // Reset current mode state
        if (_isDrawMode)
        {
            _isDrawMode = false;
            DrawModeButton.IsChecked = false;
            CurveControl.SetDrawMode(false);
        }
        if (_isBezierMode)
        {
            _isBezierMode = false;
            BezierModeButton.IsChecked = false;
            CurveControl.SetBezierMode(false);
        }
        SetSlidersEnabled(true);

        var cs = CurrentSettings;
        if (cs == null) return;

        switch (cs.CurveMode)
        {
            case 2: // Bezier
                _isBezierMode = true;
                BezierModeButton.IsChecked = true;
                SetSlidersEnabled(false);
                CurveControl.SetBezierMode(true, cs.BezierPoints);
                break;
            case 1: // Draw
                _isDrawMode = true;
                DrawModeButton.IsChecked = true;
                SetSlidersEnabled(false);
                CurveControl.UpdateRamps(
                    cs.DrawnRamp[0] ?? new ushort[256],
                    cs.DrawnRamp[1] ?? new ushort[256],
                    cs.DrawnRamp[2] ?? new ushort[256],
                    _activeChannel);
                CurveControl.SetDrawMode(true);
                break;
        }
    }

    internal void ApplyProfile(Profile profile)
    {
        if (SelectedDevice == null) return;

        CurrentProfileName = profile.Name;
        ProfileService.LastUsedProfileName = profile.Name;

        _monitorSettings[SelectedDevice] = profile.Settings.Clone();
        _loadedSettings[SelectedDevice] = profile.Settings.Clone();

        MonitorService.ApplyRamp(SelectedDevice, BuildRampForSettings(_monitorSettings[SelectedDevice]));

        RestoreCurveMode();
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

    internal MonitorSettings? CurrentSettingsClone()
        => CurrentSettings?.Clone();

    internal void OnProfileDeleted(string name)
    {
        // Clear the profile name from any monitor that had it loaded
        foreach (var key in _monitorProfileNames.Keys.ToList())
        {
            if (_monitorProfileNames[key] == name)
                _monitorProfileNames[key] = string.Empty;
        }
        UpdateCurrentProfileLabel();
        ClearDirty();
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
