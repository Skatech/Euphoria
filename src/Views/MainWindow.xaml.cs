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
using Microsoft.VisualBasic;
using System.Windows.Media.Animation;
using System.Diagnostics.Eventing.Reader;

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
            case Key.X:
                if (e.IsDown && e.IsRepeat is false)
                    Controller.HideAllImages();
                break;
            case Key.S:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    Controller.SaveData();
                break;

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

    private void OnShowImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, true);
    }

    private void OnHideAllImagesMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.HideAllImages();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadData();
    }

    private void OnSaveDataMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.SaveData();
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

    // public bool IsBusy => BusyMessage is not null;
    public string? BusyMessage { get; private set; }
    public void SetBusy(string? message = default) {
        if (message is not null && BusyMessage is not null)
            throw new Exception("Controller already busy");
        if (message != BusyMessage) {
            BusyMessage = message;
            OnPropertyChanged(nameof(BusyMessage));
            // OnPropertyChanged(nameof(IsBusy));
        }
    }
    async ValueTask<TResult> LockUntilComplete<TResult>(Task<TResult> task, string message) {
        if (task.IsCompleted) {
            return task.Result;
        }
        try {
            SetBusy(message);
            return await task;
        }
        finally {
            SetBusy();
        }
    }

    public MainWindowController() {}

    public async void LoadData() {
        var service = ServiceLocator.Resolve<IImageDataService>();
        var imgdata = await LockUntilComplete(service.LoadAsync(), "Loading data...");
        ImageGroups.Clear();
        ImageGroups.AddRange(imgdata.Select(e => new ImageGroupController(this, e)));       
        if (ImageGroups.Count < 1) {
            Debug.WriteLine("Data lost, restoring from legacy...");
            ImageGroups.AddRange(service.LoadLegacy()
                .OrderBy(e => e.Base).Select(e => new ImageGroupController(this, e)));
            SaveData();
        }
        OnPropertyChanged(nameof(CanShowImageGroups));
    }

    public async void SaveData() {
        var service = ServiceLocator.Resolve<IImageDataService>();
        await LockUntilComplete(
            service.SaveAsync(ImageGroups.Select(ImageGroupController.GetData).OrderBy(e => e.Base)),
            $"Saving data...");
    }

    public void ShowImageGroup(ImageGroupController igc, bool show) {
        if (show) {
            igc.Show();
            ShownImageGroups.Add(igc);
        }
        else ShownImageGroups.Remove(igc);
        OnPropertyChanged(nameof(CanShowImageGroups));
    }

    public async ValueTask<Dictionary<string, string>> LoadGroupDataAsync(ImageGroupController igc) {
        return await LockUntilComplete(
            ServiceLocator.Resolve<IImageDataService>().GetGroupImagesAsync(igc.Base),
            $"Loading data {igc.Base}...");
    }


    public async ValueTask<BitmapFrame?> LoadGroupImageAsync(ImageGroupController igc, string file, string name) {
        return await LockUntilComplete(
            ServiceLocator.Resolve<IImageDataService>() .TryLoadImageAsync(file, name),
            $"Loading image {name}...");
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

    public void HideAllImages() {
        if (ShownImageGroups.Count > 0) {
            ShownImageGroups.Clear();
            OnPropertiesChanged(nameof(CanShowImageGroups));
        }
    }
}

class ImageGroupController : ControllerBase {
    public string Base => _data.Base;
    public int Width { get => _data.Width; set => _data.Width = value; }
    public int ShiftX { get => _data.ShiftX; set => _data.ShiftX = value; }
    public int ShiftY { get => _data.ShiftY; set => _data.ShiftY = value; }
    public double Rotation { get => _data.Rotation; set => _data.Rotation = value; }
    public double ScaleX {
        get => _flipped ? -_data.ScaleX : _data.ScaleX;
        set => _data.ScaleX = _flipped ? -value : value; }
    public double ScaleY { get => _data.ScaleY; set => _data.ScaleY = value; }

    bool _flipped;
    public bool IsFlipped {
        get => _flipped;
        set {
            if (TryUpdateField(ref _flipped, value))
                OnPropertyChanged(nameof(ScaleX));
        }
    }

    public string? Name { get; private set; }
    public BitmapFrame? Image { get; private set; }

    Dictionary<string, BitmapFrame?>? _images;
    Dictionary<string, string>? _files;
    public IEnumerable<string> Variants => (_files is not null)
        ? _files.Keys.Where(s => !s.Equals(Name, StringComparison.OrdinalIgnoreCase))
        : Enumerable.Empty<string>();

    public MainWindowController Controller { get; }
    readonly ImageGroupData _data;
    public ImageGroupController(MainWindowController controller, ImageGroupData data) {
        Controller = controller; _data = data;
    }

    public async void Show() {
        Name = null; Image = null;
        if (_files is null) {
            _images = new(StringComparer.OrdinalIgnoreCase);
            _files = await Controller.LoadGroupDataAsync(this);
            OnPropertyChanged(nameof(Variants));
        }
        SelectVariant(Base);
    }

    public async void SelectVariant(string name) {
        if (_files is not null && _images is not null &&
                _files.Keys.Contains(name) && !FilePath.Equals(name, Name)) {
            if (!_images.TryGetValue(name, out BitmapFrame? image)) {
                image = await Controller.LoadGroupImageAsync(this, _files[name], name);
                _images[name] = image;
            }
            Image = image;
            Name = name;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Image));
            OnPropertyChanged(nameof(Variants));
        }
    }

    public override string ToString() {
        return Base;
    }

    public static ImageGroupData GetData(ImageGroupController igc) {
        return igc._data;
    }
}





