using System;
using ExplorerTabUtility.Managers;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using ExplorerTabUtility.UI.Views;
using ExplorerTabUtility.Helpers;

namespace ExplorerTabUtility;

// ReSharper disable once RedundantExtendsListEntry
public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, Constants.MutexId, out var createdNew);

        if (createdNew)
        {
            base.OnStartup(e);
            SetupTooltipBehavior();

            try
            {
                UiText.Current.Apply(SettingsManager.Language);
                UiText.Current.StartSystemTracking();
                _ = new MainWindow();
            }
            catch (Exception error)
            {
                MessageBox.Show("无法加载 Explorer Tab Utility 设置 / Unable to load settings.\n" + error.GetBaseException().Message,
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
            return;
        }

        UiText.Current.Apply(SettingsManager.Language);
        CustomMessageBox.Show(UiText.Translate("程序已经在运行。请在系统托盘中找到程序图标。"), Constants.AppName, icon: MessageBoxImage.Information);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UiText.Current.StopSystemTracking();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static void SetupTooltipBehavior()
    {
        ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(3500));
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(1700));
        ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(150));
        ToolTipService.ShowsToolTipOnKeyboardFocusProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(false));
    }
}