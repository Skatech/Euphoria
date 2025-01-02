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
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Skatech.IO;
using Skatech.Components;
using Skatech.Components.Presentation;

// using System.Windows.Shapes;

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
                    Controller.SwitchControlMode(e.IsDown);
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

    private void OnShiftAnotherGroupImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (sender is MenuItem mi && mi.DataContext is ImageGroupController igc
            && e.OriginalSource is MenuItem smi && smi.DataContext is ImageGroupController igs)
                igc.Controller.ShiftImageGroups(igc, igs);
    }

    private void OnShowImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, true);
    }

    private void OnHideImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, false);
    }

    private void OnHideAllImageGroupsMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.ShownImageGroups.Clear();
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
    
    public bool IsControlMode { get; private set; }
    public void SwitchControlMode(bool enable) {
        if (IsControlMode != enable) {
            IsControlMode = enable;
            OnPropertyChanged(nameof(IsControlMode));
        }
    }

    public MainWindowController() {
        // var service = new ImageDataService(App.AppdataDirectory);
        var service = ServiceLocator.Resolve<IImageDataService>();
        ImageGroups.AddRange(service.Load().Select(e => new ImageGroupController(this, e)));
        
        // service.LoadLegacy(s => {
        //     var igc = new ImageGroupController(s);
        //     ImageGroups.Add(igc);
        //     return igc; });

        // service.Load(s => {
        //     var igc = new ImageGroupController(s, service);
        //     ImageGroups.Add(igc);
        //     return igc; });

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
    public void ShiftImageGroups(ImageGroupController igc, ImageGroupController igs) {
        int pos = ShownImageGroups.IndexOf(igs), pon = ShownImageGroups.IndexOf(igc);
        if (pos >= 0 && pon >= 0 && pos!=pon)
            ShownImageGroups.Move(pos, pon);
    }
}

class ImageGroupController : ControllerBase {
    public string Base => _data.Base;
    public int Width { get => _data.Width; set => _data.Width = value; }
    public int ShiftX { get => _data.ShiftX; set => _data.ShiftX = value; }
    public int ShiftY { get => _data.ShiftY; set => _data.ShiftY = value; }
    public double Rotation { get => _data.Rotation; set => _data.Rotation = value; }
    public double ScaleX { get => _data.ScaleX; set => _data.ScaleX = value; }
    public double ScaleY { get => _data.ScaleY; set => _data.ScaleY = value; }

    public string? Name { get; private set; }
    public BitmapFrame? Image { get; private set; }

    Dictionary<string, string> GroupImages = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Variants => GroupImages.Keys
        .Where(s => s.Equals(Name, StringComparison.OrdinalIgnoreCase) is false);

    public MainWindowController Controller { get; }
    readonly ImageGroupData _data;
    public ImageGroupController(MainWindowController controller, ImageGroupData data) {
        Controller = controller; _data = data;
    }

    public void SelectVariant(string name) {
        if (GroupImages.Keys.Contains(name) && !FilePath.Equals(name, Name)) {
            Name = name;
            OnPropertyChanged(nameof(Name));
            Image = ServiceLocator.Resolve<IImageDataService>().TryLoadImage(GroupImages[Name], Name);
            OnPropertyChanged(nameof(Image));
        }
    } 

    public void LoadResources() {
        if (Image is null && GroupImages.Count < 1) {
            GroupImages = ServiceLocator.Resolve<IImageDataService>().GetGroupImages(Base);
            OnPropertyChanged(nameof(GroupImages));
            if (GroupImages.Count > 0)
                SelectVariant(GroupImages.Keys.First());
        }
    }

    public override string ToString() {
        return Base;
    }
}





