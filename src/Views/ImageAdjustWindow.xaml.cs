using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Markup;
using System.Globalization;

using Skatech.Components.Presentation;
using System.Windows.Media;

namespace Skatech.Euphoria;

partial class ImageAdjustWindow : Window {
    readonly ImageAdjustWindowController Controller;

    internal ImageAdjustWindow(Window owner, ImageGroupController igc) {
        InitializeComponent();
        Owner = owner;
        DataContext = Controller = new ImageAdjustWindowController(igc);
    }

    void OnResetChanges(object sender, RoutedEventArgs e) {
        Controller.ResetChanges();
    }

    private void OnKeyDownUp(object sender, KeyEventArgs e) {
        if (e.Handled = e.Key == Key.Escape) {
            Close();
        }
        else if (e.IsDown) {
            int step = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;

            if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)) {
                if (e.Key == Key.Up) {
                    Controller.ChangeScale(+step * 0.001);
                }
                else if (e.Key == Key.Down) {
                    Controller.ChangeScale(-step * 0.001);
                }
                else if (e.Key == Key.Right) {
                    Controller.ChangeWidth(+step);
                }
                else if (e.Key == Key.Left) {
                    Controller.ChangeWidth(-step);
                }
            }
            else {
                if (e.Key == Key.Up) {
                    Controller.ChangeShiftY(-step);
                }
                else if (e.Key == Key.Down) {
                    Controller.ChangeShiftY(+step);
                }
                else if (e.Key == Key.Right) {
                    Controller.ChangeShiftX(+step);
                }
                else if (e.Key == Key.Left) {
                    Controller.ChangeShiftX(-step);
                }
            }
        }
    }
}

class ImageAdjustWindowController : ControllerBase {
    public ImageGroupController Image { get; }
    ImageGroupData _orig;

    public ImageAdjustWindowController(ImageGroupController igc) {
        _orig = ImageGroupController.GetData(Image = igc).Copy();
    }

    public double DeltaScale => Image.ScaleY - _orig.ScaleY;
    public void ChangeScale(double step) {
        if (Image.ChangeScale(Image.ScaleY + step, Image.IsFlipped))
            OnPropertyChanged(nameof(DeltaScale));
    }
    
    public int DeltaWidth => Image.Width - _orig.Width;
    public void ChangeWidth(int step) {
        Image.Width += step;
        OnPropertyChanged(nameof(DeltaWidth));
    }

    public int DeltaShiftX => Image.ShiftX - _orig.ShiftX;
    public void ChangeShiftX(int step) {
        if (Image.ChangeShiftX(Image.ShiftX + step))
            OnPropertyChanged(nameof(DeltaShiftX));
    }
    
    public int DeltaShiftY => Image.ShiftY - _orig.ShiftY;
    public void ChangeShiftY(int step) {
        if (Image.ChangeShiftY(Image.ShiftY + step))
            OnPropertyChanged(nameof(DeltaShiftY));
    }

    public bool IsPreFlipped {
        get => Image.IsFlipped;
        set {
            if (Image.ChangeScale(Image.ScaleY, value))
                OnPropertyChanged(nameof(IsPreFlipped));
        }
    }

    public void ResetChanges() {
        if(Image.ChangeScale(_orig.ScaleY, _orig.IsFlipped)) 
            OnPropertiesChanged(nameof(DeltaScale), nameof(IsPreFlipped));
        if (Image.ChangeWidth(_orig.Width))
            OnPropertyChanged(nameof(DeltaWidth));
        if(Image.ChangeShiftX(_orig.ShiftX))
            OnPropertyChanged(nameof(DeltaShiftX));
        if (Image.ChangeShiftY(_orig.ShiftY))
            OnPropertyChanged(nameof(DeltaShiftY));
    }
}

///<summary>Converts delta double values to string and Brush
class DeltaValueConverter : MarkupExtension, IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is double dv || (value is int && double.IsInteger(dv = (int)value))) {
            if(parameter is string ps && Int32.TryParse(ps, out int precesion))
                dv = Math.Round(dv, precesion, MidpointRounding.AwayFromZero);
            if (targetType == typeof(string))
                return dv > 0 ? '+' + dv.ToString() : dv.ToString();
            if (targetType == typeof(System.Windows.Media.Brush)){
                return dv < 0 ? Brushes.Blue : dv > 0 ? Brushes.Red : Brushes.LightGray;
            }
        }
        throw new NotSupportedException(
            "Supported conversion of double values to string and Brush only");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotSupportedException("Back conversion of delta value not supported");
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public static DeltaValueConverter Instance { get; } = new ();
}