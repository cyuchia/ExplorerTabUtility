using System;
using ExplorerTabUtility.Helpers;
using ExplorerTabUtility.Managers;

namespace ExplorerTabUtility.UI.Views;

public partial class MainWindow
{
    private bool _refreshingPreferences;
    private sealed class LanguageOption(string code, string label)
    {
        public string Code { get; } = code;
        public override string ToString() => UiText.Translate(label);
    }

    private void InitializePreferences(bool preview)
    {
        LanguageChoice.ItemsSource = new[] { new LanguageOption("auto", "跟随系统"), new LanguageOption("zh-CN", "简体中文"), new LanguageOption("en", "English") };
        LanguageChoice.SelectedValuePath = "Code";
        LanguageChoice.SelectedValue = SettingsManager.Language;
        CbStartup.IsChecked = preview ? false : RegistryManager.IsStartupEnabled;
        if (preview) return;
        CbStartup.Click += (_, _) =>
        {
            if (_refreshingPreferences) return;
            try { RegistryManager.SetStartupEnabled(CbStartup.IsChecked == true); PreferencesStatus.Text = ""; }
            catch (Exception ex) { PreferencesStatus.Text = UiText.Translate("无法保存开机启动设置：") + ex.Message; RefreshStartup(); }
        };
        RegistryManager.StartupChanged += RefreshStartup;
        Activated += (_, _) => RefreshStartup();
        LanguageChoice.SelectionChanged += (_, _) =>
        {
            if (LanguageChoice.SelectedItem is not LanguageOption option) return;
            SettingsManager.Language = option.Code;
            UiText.Current.Apply(option.Code);
        };
        SettingsManager.SaveFailed += OnSettingsSaveFailed;
        if (SettingsManager.Recovery != null) PreferencesStatus.Text = UiText.Translate("配置读取失败，已从备份恢复。");
    }

    private void RefreshStartup()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefreshStartup)); return; }
        _refreshingPreferences = true;
        try { CbStartup.IsChecked = RegistryManager.IsStartupEnabled; }
        catch (Exception ex) { PreferencesStatus.Text = ex.Message; }
        finally { _refreshingPreferences = false; }
    }

    private void OnSettingsSaveFailed(string reason) => Dispatcher.BeginInvoke(new Action(() =>
        PreferencesStatus.Text = UiText.Translate("配置未保存，请检查磁盘空间和权限：") + reason));

    private void DisposePreferences()
    {
        RegistryManager.StartupChanged -= RefreshStartup;
        SettingsManager.SaveFailed -= OnSettingsSaveFailed;
    }
}
