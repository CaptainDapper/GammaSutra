using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfContextMenu = System.Windows.Controls.ContextMenu;

namespace GammaControl;

public partial class App
{
    private TaskbarIcon? _trayIcon;
    internal MainWindow? MainWindowInstance { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var minimized = e.Args.Contains("--minimized");
        MainWindowInstance = new MainWindow();
        MainWindowInstance.Icon = IconGenerator.CreateImageSource();

        if (!minimized)
            MainWindowInstance.Show();

        SetupTrayIcon();
    }

    private static System.Drawing.Icon LoadEmbeddedIcon()
    {
        var uri    = new Uri("pack://application:,,,/Resources/icon.ico");
        var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
        return stream != null
            ? new System.Drawing.Icon(stream)
            : IconGenerator.CreateIcon(); // fallback
    }

    private void SetupTrayIcon()
    {
        var menu = new WpfContextMenu();
        menu.Opened += (_, _) => RefreshTrayMenu(menu);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Gamma Sutra",
            ContextMenu = menu,
            Icon        = LoadEmbeddedIcon()
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => MainWindowInstance?.ShowWindow();
    }

    private void RefreshTrayMenu(WpfContextMenu menu)
    {
        menu.Items.Clear();

        // Per-monitor status
        if (MainWindowInstance != null)
        {
            foreach (var dev in MainWindowInstance.MonitorDeviceNames)
            {
                var profileName = MainWindowInstance.MonitorProfileNames.TryGetValue(dev, out var n) && !string.IsNullOrEmpty(n) ? n : "(none)";
                var shortName = dev.Replace(@"\\.\", "");
                menu.Items.Add(new WpfMenuItem { Header = $"{shortName}: {profileName}", IsEnabled = false });
            }
            menu.Items.Add(new WpfSeparator());
        }

        var showItem = new WpfMenuItem { Header = "Show" };
        showItem.Click += (_, _) => MainWindowInstance?.ShowWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new WpfSeparator());

        // Profile quick-launch items
        if (MainWindowInstance != null)
        {
            foreach (var profile in MainWindowInstance.ProfileService.Profiles)
            {
                var p = profile;
                var profileItem = new WpfMenuItem { Header = p.Name };
                profileItem.Click += (_, _) => MainWindowInstance.ApplyProfileByName(p.Name);
                menu.Items.Add(profileItem);
            }
        }

        menu.Items.Add(new WpfSeparator());

        var startupItem = new WpfMenuItem
        {
            Header      = "Start with Windows",
            IsCheckable = true,
            IsChecked   = StartupHelper.IsEnabled()
        };
        startupItem.Click += (_, _) => StartupHelper.SetEnabled(startupItem.IsChecked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new WpfSeparator());

        var resetItem = new WpfMenuItem { Header = "Reset All" };
        resetItem.Click += (_, _) => MonitorService.ResetAll();
        menu.Items.Add(resetItem);

        menu.Items.Add(new WpfSeparator());

        var quitItem = new WpfMenuItem { Header = "Quit" };
        quitItem.Click += (_, _) =>
        {
            MainWindowInstance?.Cleanup();
            Shutdown();
        };
        menu.Items.Add(quitItem);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
