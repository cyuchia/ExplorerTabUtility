using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace ExplorerTabUtility.Helpers;

public sealed class UiText : INotifyPropertyChanged
{
    public static UiText Current { get; } = new();
    private readonly Dictionary<string, string[]> _strings;
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;
    private string _choice = "auto";
    public int Version { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Chinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    private UiText()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExplorerTabUtility.UI.Localization.Strings.json")!;
        _strings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
        foreach (var pair in _strings)
            foreach (var value in pair.Value) _keys[value] = pair.Key;
    }

    public static string Translate(string text) => Current._keys.TryGetValue(text, out var key)
        ? Current._strings[key][Current.Chinese ? 0 : 1] : text;

    public void Apply(string choice)
    {
        _choice = choice;
        var culture = choice == "auto" ? _systemCulture : CultureInfo.GetCultureInfo(choice);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        var app = Application.Current;
        foreach (var pair in _strings) app.Resources[pair.Key] = pair.Value[Chinese ? 0 : 1];
        // The composite family supplies locale-appropriate glyphs when the Windows UI font lacks them.
        app.Resources["DefaultFontFamily"] = new FontFamily(SystemFonts.MessageFontFamily.Source + ", Global User Interface");
        var language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
        app.Resources["UiLanguage"] = language;
        foreach (Window window in app.Windows) window.Language = language;
        Version++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
    }

    public void StartSystemTracking() => SystemEvents.UserPreferenceChanged += OnSystemChanged;
    public void StopSystemTracking() => SystemEvents.UserPreferenceChanged -= OnSystemChanged;
    private void OnSystemChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.Window or UserPreferenceCategory.General or UserPreferenceCategory.Locale)) return;
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(_choice)));
    }
}
