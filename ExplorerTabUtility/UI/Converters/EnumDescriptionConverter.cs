using ExplorerTabUtility.Helpers;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.ComponentModel;

namespace ExplorerTabUtility.UI.Converters;

public class EnumDescriptionConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return DependencyProperty.UnsetValue;

        var valueStr = value.ToString()!;
        var fieldInfo = value.GetType().GetField(valueStr);
        if (fieldInfo == null) return valueStr;

        var descriptionAttribute = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;

        return UiText.Translate(descriptionAttribute?.Description ?? valueStr);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => Convert(values[0], targetType, parameter, culture);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}