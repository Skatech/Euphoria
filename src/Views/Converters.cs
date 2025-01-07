using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Globalization;

namespace Skatech.Components.Presentation.MarkupConverters;

sealed class GridLengthConverter : MarkupExtension, IValueConverter {
    public override object ProvideValue(IServiceProvider serviceProvider) {
        return this;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (targetType == typeof(GridLength)) {
            if (value is double vd) {
                return new GridLength(vd);
            }
            if (value is int vi) {
                return new GridLength(vi);
            }
        }
        throw new InvalidOperationException(
            $"Can not convert {value ?? "null"} to {targetType.Name}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is GridLength gl) {
            if (targetType == typeof(double)) {
                return gl.Value;
            }
            if (targetType == typeof(int)) {
                return (int)Math.Round(gl.Value);
            }
        }
        throw new InvalidOperationException(
            $"Can not convert {value ?? "null"} to {targetType.Name}");
    }
}
