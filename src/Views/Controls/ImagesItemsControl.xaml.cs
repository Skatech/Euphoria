using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Globalization;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Skatech.Euphoria;

public partial class ImagesItemsControl : ItemsControl {
    MainWindowController Controller => (MainWindowController)Window.GetWindow(this).DataContext;

    public ImagesItemsControl() {
        InitializeComponent();
    }

    public bool HandleKeyDownUp(object sender, KeyEventArgs e) {
        if (e.IsDown && e.IsRepeat is false && Controller.MouseOverGroup is ImageGroupController igc) {
            if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.None) {
                igc.IsShowFlipped = igc.IsShowFlipped is false;
                return true;
            }
            if (e.Key == Key.X && e.KeyboardDevice.Modifiers == ModifierKeys.None) {
                igc.IsShown = false;
                return true;
            }
            if (e.Key == Key.A && e.KeyboardDevice.Modifiers == ModifierKeys.Shift
                    && Window.GetWindow(this) is MainWindow window) {
                OpenImageAdjustWindow(igc);
                return true;
            }
            if ((e.Key == Key.E || e.Key == Key.G) && e.KeyboardDevice.Modifiers == ModifierKeys.None) {
                if (igc.Name is string name && Controller.LockMessage is null) {
                    string newName = ImageLocator.SwitchAttribute(name, e.Key == Key.E ? 'E' : 'G');
                    igc.SelectVariant(newName).DoNotAwait();
                }
                return true;
            }
        }
        return false;
    }

    void OpenImageAdjustWindow(ImageGroupController igc) {
        if (Window.GetWindow(this) is MainWindow window)
            new ImageAdjustWindow(window, igc).ShowDialog();
    }

    void OnMouseEnterLeave(object sender, MouseEventArgs e) {
        if (e.OriginalSource is Grid grid && grid.Tag is ImageGroupController igc)
            Controller.MouseOverGroup = grid.IsMouseOver ? igc : null;
    }

    void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e) {
        if (sender is ScrollViewer scv) {
            scv.ScrollToHorizontalOffset(scv.ScrollableWidth * 0.5);
            scv.ScrollToVerticalOffset(scv.ScrollableHeight * 0.5);
        }
    }

    private void OnHideImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            igc.IsShown = false;
    }  

    private void OnSelectAnotherGroupImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.Header is string name
                && sender is MenuItem mip && mip.Tag is ImageGroupController igc)
            igc.SelectVariant(name).DoNotAwait();
    }

    private void OnShiftImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShiftImageGroup(igc, mi.Header.Equals("_Right"));
    }

    private void OnShiftAnotherImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (sender is MenuItem mi && mi.DataContext is ImageGroupController igc
            && e.OriginalSource is MenuItem smi && smi.DataContext is ImageGroupController igs)
                igc.Controller.ShiftImageGroupTo(igc, igs);
    }

    private void OnOpenImageAdjustWindowMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            OpenImageAdjustWindow(igc);
    }
}

class BadgeColorConverter : MarkupExtension, IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (targetType == typeof(System.Windows.Media.Brush)) {
            if (parameter is string ribbon && value is string name)
                if (GetBadgeBrush(ribbon, name) is Brush brush)
                    return brush;
            return Brushes.Transparent;
        }
        throw new InvalidOperationException("Supported conversion of strings to System.Windows.Media.Brush only");
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    
    public static BadgeColorConverter Instance { get; } = new ();

    readonly static Dictionary<string, Brush> BrushCache = new ();

    public static Brush? GetBadgeBrush(string ribbon, string name) {
        if (BadgeColors.GetBadgeColor(ribbon, name) is string color) {
            if (BrushCache.TryGetValue(color, out Brush? brush) is false)
                BrushCache.Add(color, brush = new SolidColorBrush(Skatech.Media.ColorHelper.FromHTML(color)));
            return brush;
        }
        return null;
    }
}

class BadgeColors {
    static readonly Tuple<string, string, string>[] Data =
        Encoding.UTF8.GetString(Convert.FromHexString(
            "412D422D23383866666566643520412D442D23363636366364616120412D522D23333336366364616120412D" +
            "4D2D23313830303030303020412D4E2D23353530303030303020412D536861682D2334343030303030302041" +
            "2D436F702D23333331313030383820422D562D23323234653030393920422D5052472D233131666630366262"))
        .Split(' ').Select(s => s.Split('-')).Select(v => Tuple.Create(v[0], v[1], v[2])).ToArray();

    public static string? GetAttributeColor(string ribbon, string attr) {
        foreach (var (rb, at, brush) in Data)
            if (ribbon.Equals(rb) && attr.Equals(at))
                return brush;
        return null;
    }

    public static string? GetBadgeColor(string ribbon, string name) {
        foreach (var (rb, at, brush) in Data)
            if (ribbon.Equals(rb) && ImageLocator.HasAttribute(name, at))
                return brush;
        return null;
    }
}