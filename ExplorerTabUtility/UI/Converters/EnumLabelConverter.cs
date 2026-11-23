using ExplorerTabUtility.Helpers;
using System;
using System.Globalization;
using System.Windows.Data;
namespace ExplorerTabUtility.UI.Converters;
public class EnumLabelConverter : IValueConverter, IMultiValueConverter
{
    public static string Label(object? value) => UiText.Translate(value?.ToString() switch
    {
        "Global" => "全局", "FileExplorer" => "资源管理器",
        "Open" => "打开位置", "Duplicate" => "复制标签页",
        "ReopenClosed" => "重新打开已关闭", "TabSearch" => "搜索标签页",
        "NavigateBack" => "后退", "NavigateUp" => "上一级", "NavigateForward" => "前进",
        "SetTargetWindow" => "设为目标窗口", "ToggleWinHook" => "切换窗口转标签页",
        "ToggleReuseTabs" => "切换标签页复用", "ToggleVisibility" => "显示或隐藏设置",
        "DetachTab" => "分离标签页", "SnapRight" => "窗口靠右", "SnapLeft" => "窗口靠左",
        "SnapUp" => "窗口靠上", "SnapDown" => "窗口靠下", _ => value?.ToString() ?? ""
    });
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Label(value);
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => Label(values[0]);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
