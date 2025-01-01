using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Cache;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
// using System.Windows.Shapes;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

using Skatech.Components.Presentation;
using System.IO;

namespace Skatech.Euphoria;

public partial class MainWindow : Window {
    MainWindowController Controller => (MainWindowController)DataContext;

    public MainWindow() {
        InitializeComponent();
        Components.Presentation.WindowBoundsKeeper.Register(this, "MainWindowBounds");
    }

    private void SwitchFullScreen() {
        if (WindowState == WindowState.Maximized) {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
        }
        else {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
    }

    private void OnKeyDownUp(object sender, KeyEventArgs e) {
        switch (e.Key) {
            case Key.Escape:
                if (e.IsDown) {
                    if (WindowState == WindowState.Maximized) {
                        SwitchFullScreen();
                    } else Close();
                }
                break;
            case Key.Enter:
                if (e.IsDown)
                    SwitchFullScreen();
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                if (e.IsRepeat is false)
                    Controller.ShowScrollBar(e.IsDown);
                break;
        }
    }

    void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e) {
        if (sender is ScrollViewer scv) {
            scv.ScrollToHorizontalOffset(scv.ScrollableWidth * 0.5);
            scv.ScrollToVerticalOffset(scv.ScrollableHeight * 0.5);
        }
    }
    
    private bool FindTaggedObject<T>(object src, [NotNullWhen(true)] out T? val) {
        while (true) {
            if (src is FrameworkElement fel){
                if (fel.Tag is T obj) {
                    val = obj;
                    return true;
                }
                src = fel.Parent;
            }
            else if (src is FrameworkContentElement fcl){
                if (fcl.Tag is T obj) {
                    val = obj;
                    return true;
                }
                src = fcl.Parent;
            }
            else {
                val = default;
                return false;
            }
        }
    }

    private void OnShiftImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShiftImageGroup(igc, mi.Header.Equals("_Right"));
    }

    private void OnShowImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, true);
    }

    private void OnHideImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, false);
    }

    private void OnSelectAnotherGroupImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.Header is string name
                && sender is MenuItem mip && mip.Tag is ImageGroupController igc)
            igc.SelectVariant(name);
    }
}

class MainWindowController : ControllerBase {
    public List<ImageGroupController> ImageGroups { get; } = new();
    public ObservableCollection<ImageGroupController> ShownImageGroups { get; } = new();
    public IEnumerable<ImageGroupController> CanShowImageGroups =>
            ImageGroups.Where(g => ShownImageGroups.Contains(g) is false);
    
    public ScrollBarVisibility ImagesScrollBarVisibility { get; set; } = ScrollBarVisibility.Disabled;
    public void ShowScrollBar(bool show) {
        var value = show ? ScrollBarVisibility.Visible : ScrollBarVisibility.Disabled;
        if (value != ImagesScrollBarVisibility) {
            ImagesScrollBarVisibility = value;
            OnPropertyChanged(nameof(ImagesScrollBarVisibility));
        }
    }

    public MainWindowController() {
        var service = new ImageDataService(App.AppdataDirectory);
        
        // service.LoadLegacy(s => {
        //     var igc = new ImageGroupController(s);
        //     ImageGroups.Add(igc);
        //     return igc; });

        service.Load(s => {
            var igc = new ImageGroupController(s, service);
            ImageGroups.Add(igc);
            return igc; });

        // service.Save(ImageGroups.OrderBy(e => e.Root));
    }

    public void ShowImageGroup(ImageGroupController igc, bool show) {
        if (show) {
            igc.LoadResources();
            ShownImageGroups.Add(igc);
        }
        else ShownImageGroups.Remove(igc);
        OnPropertyChanged(nameof(CanShowImageGroups));
    }

    public void ShiftImageGroup(ImageGroupController igc, bool right) {
        int cnt = ShownImageGroups.Count, pos = ShownImageGroups.IndexOf(igc);
        if (cnt > 1 && pos >= 0) {
            ShownImageGroups.Move(pos, ((right ? pos + 1 : pos - 1) + cnt) % cnt);
        }
    }
}

class ImageGroupController : IImageGroup, INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Root { get; init; }
    public int Width { get; set; }
    public int ShiftX { get; set; }
    public int ShiftY { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }

    // public string[] Variants { get; private set; } = Array.Empty<string>();


    public Dictionary<string, string> GroupImages = new(StringComparer.OrdinalIgnoreCase);
    // public IEnumerable<MenuItem> Variants =>
    //     GroupImages.Keys.Where(s => s.Equals(Name, StringComparison.OrdinalIgnoreCase) is false)
    //         .Select(s => new MenuItem { Header = s, Tag = this });

    public IEnumerable<string> Variants => GroupImages.Keys
        .Where(s => s.Equals(Name, StringComparison.OrdinalIgnoreCase) is false);

    public string? Name { get; private set; }
    public BitmapFrame? Image { get; private set; }

    readonly ImageDataService _service;
    public ImageGroupController(string root, ImageDataService service) {
        Root = root; _service = service;
    }

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void SelectVariant(string name) {
        if (!name.Equals(Name) && GroupImages.Keys.Contains(name)) {
            Name = name;
            OnPropertyChanged(nameof(Name));
            Image = _service.TryLoadImage(GroupImages[Name], Name);
            OnPropertyChanged(nameof(Image));
        }
    } 

    public void LoadResources() {
        if (Image is null && GroupImages.Count < 1) {
            GroupImages = _service.GetGroupImages(Root);
            OnPropertyChanged(nameof(GroupImages));
            if (GroupImages.Count > 0)
                SelectVariant(GroupImages.Keys.First());
        }
    }

    public override string ToString() {
        return Root;
    }
}





