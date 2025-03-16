using System;
using System.Windows.Data;
using System.Windows.Markup;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Skatech.Presentation.MarkupConverters;

///<summary>Converts values to Boolean.
///Boolean true and other non-null values ​​are considered true. Supports value inversion.
///Usage: IsEnabled="{Binding MyProp, Mode=OneWay, Converter={conv:BooleanConverter Inverted=True}}"</summary>
class BooleanConverter : MarkupExtension, IValueConverter {
    bool _inverted = false;
    public object Inverted {
        get => _inverted;
        set => _inverted = System.Convert.ToBoolean(value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (targetType == typeof(bool)) {
            return _inverted != (value is null ? false : value is bool bval ? bval : true);
        }
        throw new InvalidOperationException("Supported conversion to Boolean only");
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

///<summary>Converts values to Visibility.
///Boolean true and other non-null values ​​are considered true.
///Supports false and true values substitution. By default OnFalse: Hidden, OnTrue: Visible.
///Usage: Visibility="{Binding MyProp, Mode=OneWay,
///Converter={conv:VisibilityConverter OnFalse=Visible, OnTrue=Collapsed}}"</summary>
class VisibilityConverter : MarkupExtension, IValueConverter {
    Visibility _onfalse = Visibility.Hidden;
    public object OnFalse {
        get => _onfalse;
        set => _onfalse = ConvertValue<Visibility>(value);
    }

    Visibility _ontrue = Visibility.Visible;
    public object OnTrue {
        get => _ontrue;
        set => _ontrue = ConvertValue<Visibility>(value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (targetType == typeof(Visibility)) {
            return (value is null ? false : value is bool bval ? bval : true)
                ? _ontrue : _onfalse;
        }
        throw new InvalidOperationException("Supported conversion to Visibility only");
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;  

    public static TEnum ConvertValue<TEnum>(object value) where TEnum : struct {
        return value is string str
            ? Enum.Parse<TEnum>(str, true)
            : (TEnum)System.Convert.ChangeType(value, typeof(TEnum));
    }
}

///<summary>Converts values to ScrollBarVisibility.
///Boolean true and other non-null values ​​are considered true.
///Supports false and true values substitution. By default OnFalse: Disabled, OnTrue: Auto.
///Usage: HorizontalScrollBarVisibility="{Binding MyProp, Mode=OneWay,
///Converter={conv:ScrollBarVisibilityConverter OnFalse=Hidden, OnTrue=Visible}}"</summary>
class ScrollBarVisibilityConverter : MarkupExtension, IValueConverter {
    ScrollBarVisibility _onfalse = ScrollBarVisibility.Disabled;
    public object OnFalse {
        get => _onfalse;
        set => _onfalse = VisibilityConverter.ConvertValue<ScrollBarVisibility>(value);
    }

    ScrollBarVisibility _ontrue = ScrollBarVisibility.Auto;
    public object OnTrue {
        get => _ontrue;
        set => _ontrue = VisibilityConverter.ConvertValue<ScrollBarVisibility>(value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (targetType == typeof(ScrollBarVisibility)) {
            return (value is null ? false : value is bool bval ? bval : true)
                ? _ontrue : _onfalse;
        }
        throw new InvalidOperationException("Supported conversion to ScrollBarVisibility only");
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}


///<summary>Converts int and double values to GridLength and vice versa.
class GridLengthConverter : MarkupExtension, IValueConverter {
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
            "Supported conversion of int and double values to GridLength type only");
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
            "Supported conversion of GridLength values to int and double types only");
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}