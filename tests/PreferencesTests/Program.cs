using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ExplorerTabUtility;
using ExplorerTabUtility.Helpers;
using ExplorerTabUtility.Managers;
using ExplorerTabUtility.UI.Views;

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS " + name);
    }
    [STAThread]
    public static int Main(string[] args)
    {

        var directory = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "ETU-Verification", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        AppContext.SetData("ExplorerTabUtility.SettingsPath", Path.Combine(directory, "isolated-settings.json"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "isolated-settings.json"), "{\"ExternalPreference\":{\"keep\":1}}");
            Persistence(directory);
            Startup();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var theme in new[] { "Colors", "DefaultStyles", "ThemeResources" }) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ExplorerTabUtility;component/UI/Themes/" + theme + ".xaml") });
            UiText.Current.Apply("zh-CN");
            Console.WriteLine("SYSTEM FONT " + SystemFonts.MessageFontFamily.Source);
            SettingsManager.IsFirstRun = false;
            SettingsManager.HotKeyProfiles = "user profiles stay untouched 用户名称";
            Parallel.For(0, 30, i => SettingsManager.ReuseTabs = i % 2 == 0);
            SettingsManager.Language = "zh-CN";
            SettingsManager.FormSize = new Size(900, 720);
            SettingsManager.SaveSettings();
            var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "isolated-settings.json")));
            Check(saved.RootElement.GetProperty("ExternalPreference").GetProperty("keep").GetInt32() == 1, "settings preserve unknown extension fields");
            Check(saved.RootElement.GetProperty("HotKeyProfiles").GetString() == "user profiles stay untouched 用户名称", "settings preserve user text during concurrent saves");
            Check(saved.RootElement.GetProperty("ReuseTabs").ValueKind is JsonValueKind.True or JsonValueKind.False, "settings concurrent writes remain valid");
            var window = new MainWindow(preview: true);
            ((ListBox)window.FindName("NavigationList")).SelectedIndex = 1;
            Render(window, directory, "preferences-zh", 900, 870, 96);
            Check(((CheckBox)window.FindName("CbStartup")).Content?.ToString() == "开机自动启动", "Chinese resources resolved");
            UiText.Current.Apply("en");
            Render(window, directory, "preferences-en", 900, 870, 96);
            Check(((CheckBox)window.FindName("CbStartup")).Content?.ToString() == "Start with Windows", "language changes on existing controls without recreation");
            UiText.Current.Apply("zh-CN");
            Render(window, directory, "preferences-small-150dpi", 720, 500, 144);
            var family = ((FontFamily)app.Resources["DefaultFontFamily"]).Source;
            Check(family.StartsWith(SystemFonts.MessageFontFamily.Source, StringComparison.Ordinal), "font follows Windows UI font");
            Check(!family.Contains("MDL2") && !family.Contains("Roboto"), "body font excludes symbol families");
            Check(ExplorerTabUtility.UI.Converters.EnumLabelConverter.Label(ExplorerTabUtility.Models.HotKeyAction.Duplicate) == "复制标签页", "localized enum labels");
            UiText.Current.Apply("en");
            Check(ExplorerTabUtility.UI.Converters.EnumLabelConverter.Label(ExplorerTabUtility.Models.HotKeyAction.Duplicate) == "Duplicate", "English enum label preserves enum identity");
            Check(UiText.Translate("简体中文") == "简体中文", "language native names preserved");
            Check(SettingsManager.HotKeyProfiles == "user profiles stay untouched 用户名称", "language changes preserve profiles");
            window.Close();
            Console.WriteLine($"COMPLETED {_checks} checks; previews: {directory}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Persistence(string directory)
    {
        var path = Path.Combine(directory, "atomic.json");
        AtomicJsonFile.Save(path, "{\"revision\":1,\"unknown\":\"keep\"}");
        AtomicJsonFile.Save(path, "{\"revision\":2}");
        Check(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("revision").GetInt32() == 2, "atomic replacement saves latest revision");
        Check(JsonDocument.Parse(File.ReadAllText(path + ".bak")).RootElement.GetProperty("revision").GetInt32() == 1, "atomic replacement retains previous good revision");
        File.WriteAllText(path, "truncated {");
        var data = AtomicJsonFile.Load<Dictionary<string, JsonElement>>(path, out var recovery);
        Check(recovery == "backup" && data["revision"].GetInt32() == 1 && data["unknown"].GetString() == "keep", "corrupt main file recovers backup and unknown fields");
        Check(Directory.GetFiles(directory, "atomic.json.corrupt-*").Length > 0, "corrupt evidence is retained");
        AtomicJsonFile.Save(path, "{\"revision\":3}");
        Check(JsonDocument.Parse(File.ReadAllText(path + ".bak")).RootElement.GetProperty("revision").GetInt32() == 1, "recovery does not replace backup with corrupt data");
        File.WriteAllText(path + ".bak", "bad"); File.WriteAllText(path, "bad");
        try { AtomicJsonFile.Load<Dictionary<string, string>>(path, out _); Check(false, "both corrupt rejected"); }
        catch (IOException) { Check(File.ReadAllText(path) == "bad", "both corrupt fail without overwriting settings"); }
        Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "atomic writes leave no temporary files");
    }
    private static void Startup()
    {
        var prefix = @"Software\ExplorerTabUtility-Verification\" + Guid.NewGuid().ToString("N");
        try
        {
            var state = new StartupRegistration(Registry.CurrentUser, prefix + @"\Run", prefix + @"\Approved", "ETUTest", @"C:\Test Path\ETU.exe");
            Check(!state.IsEnabled, "missing startup keys read disabled");
            state.SetEnabled(true);
            Check(state.IsEnabled, "startup creates missing keys and reads back");
            using (var run = Registry.CurrentUser.OpenSubKey(prefix + @"\Run", true)!)
            {
                Check((string)run.GetValue("ETUTest")! == "\"C:\\Test Path\\ETU.exe\"", "startup path is quoted");
                run.SetValue("ETUTest", @"C:\Test Path\ETU.exe");
                run.SetValue("Unrelated", "keep");
            }
            Check(state.IsEnabled, "legacy unquoted startup recognized");
            using (var approved = Registry.CurrentUser.OpenSubKey(prefix + @"\Approved", true)!) approved.SetValue("ETUTest", new byte[] { 3 }, RegistryValueKind.Binary);
            Check(!state.IsEnabled, "Task Manager disabled startup recognized");
            state.SetEnabled(true); Check(state.IsEnabled, "explicit startup enable reconciles disabled approval");
            state.SetEnabled(false); Check(!state.IsEnabled, "startup disabled and read back");
            using var check = Registry.CurrentUser.OpenSubKey(prefix + @"\Run")!;
            Check((string)check.GetValue("Unrelated")! == "keep", "startup only changes own entry");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(prefix, false); }
    }
    private static void Render(Window window, string directory, string name, double width, double height, double dpi)
    {
        var content = (FrameworkElement)window.Content;
        window.Width = width; window.Height = height;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new RenderTargetBitmap((int)(width * dpi / 96), (int)(height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
        Check(bitmap.PixelWidth > 0, "render " + name);
    }
}
