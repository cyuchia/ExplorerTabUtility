namespace ExplorerTabUtility.Helpers;

internal static class Constants
{
    internal const string AppName = "ExplorerTabUtility";
    internal const string MutexId = $"__{AppName}Hook__Mutex";
    internal static string NotifyIconText => UiText.Translate("Explorer Tab Utility：将新窗口自动转为标签页");
    internal const string SettingsFileName = "settings.json";
    internal const string HotKeyProfilesFileName = "HotKeyProfiles.json";
    internal static string JsonFileFilter => UiText.Translate("JSON 文件 (*.json)|*.json|所有文件|*.*");
    internal const string UpdateUrl = "https://api.github.com/repos/w4po/ExplorerTabUtility/releases/latest";
    internal const string DefaultHotKeyProfiles = "[{\"Name\":\"Home\",\"HotKeys\":[91,69],\"Scope\":0,\"Action\":0,\"Path\":\"\",\"IsHandled\":true,\"IsEnabled\":true,\"Delay\":0},{\"Name\":\"Duplicate\",\"HotKeys\":[17,68],\"Scope\":1,\"Action\":1,\"Path\":null,\"IsHandled\":true,\"IsEnabled\":true,\"Delay\":0},{\"Name\":\"ReopenClosed\",\"HotKeys\":[16,17,84],\"Scope\":1,\"Action\":2,\"Path\":null,\"IsHandled\":true,\"IsEnabled\":true,\"Delay\":0}]";
}