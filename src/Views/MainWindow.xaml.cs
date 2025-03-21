using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Skatech.IO;
using Skatech.Components;
using Skatech.Components.Presentation;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

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
                else if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                    Controller.OpenStoriesWindow(this, e.KeyboardDevice);
                break;
            case Key.O:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    Controller.OpenNewImageGroup();
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

    private void OnHideAllImagesMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.HideAllImages();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadData();
    }

    private void OnSaveDataMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.SaveData();
    }

    private void OnOpenFileMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.OpenNewImageGroup();
    }
}

class MainWindowController : LockableControllerBase {
    public ObservableCollection<ImageGroupController> ImageGroups { get; private set; } = new();
    public ObservableCollection<ImageGroupController> ShownImageGroups { get; } = new();

    public bool IsControlMode { get; private set; }
    public void SwitchControlMode(bool enable) {
        if (IsControlMode != enable) {
            IsControlMode = enable;
            OnPropertyChanged(nameof(IsControlMode));
        }
    }

    public void LoadData() {
        async void Load() {
            var service = ServiceLocator.Resolve<IImageDataService>();
            var imgdata = await LockUntilComplete(service.LoadAsync(), "Loading data...");
            ImageGroups = new(imgdata.Select(e => new ImageGroupController(this, e)));

            if (ImageGroups.Count < 1) {
                Debug.WriteLine("Data lost, restoring from legacy...");
                ImageGroups = new(service.LoadLegacy()
                    .OrderBy(e => e.Base).Select(e => new ImageGroupController(this, e)));
                await LockUntilComplete(SaveDataAsync(), "Saving restored data...", "#77770000");
            }
            OnPropertyChanged(nameof(ImageGroups));
        }
        if (LockMessage is null)
            Load();
    }

    public Task<bool> SaveDataAsync() {
        var service = ServiceLocator.Resolve<IImageDataService>();
        return service.SaveAsync(ImageGroups.Select(ImageGroupController.GetData).OrderBy(e => e.Base));
    }

    public void SaveData() {
        if (LockMessage is null)
            LockUntilComplete(SaveDataAsync(),"Saving data...");
    }

    public async ValueTask<Dictionary<string, string>> LoadGroupDataAsync(ImageGroupController igc) {
        return await LockUntilComplete(
            ServiceLocator.Resolve<IImageDataService>().GetGroupImagesAsync(igc.Base),
            $"Loading group {igc.Base}...");
    }

    public async ValueTask<BitmapFrame?> LoadGroupImageAsync(ImageGroupController igc, string file, string name) {
        return await LockUntilComplete(
            ServiceLocator.Resolve<IImageDataService>().TryLoadImageAsync(file, name),
            $"Loading image {name}...");
    }

    public void ShiftImageGroup(ImageGroupController igc, bool right) {
        int cnt = ShownImageGroups.Count, pos = ShownImageGroups.IndexOf(igc);
        if (cnt > 1 && pos >= 0) {
            ShownImageGroups.Move(pos, ((right ? pos + 1 : pos - 1) + cnt) % cnt);
        }
    }

    public void ShiftImageGroupTo(ImageGroupController igc, ImageGroupController igs) {
        int pos = ShownImageGroups.IndexOf(igs), pon = ShownImageGroups.IndexOf(igc);
        if (pos >= 0 && pon >= 0 && pos!=pon)
            ShownImageGroups.Move(pos, pon);
    }

    public void HideAllImages() {
        for (int i = ShownImageGroups.Count; i > 0;)
            ShownImageGroups[--i].IsShown = false;
    }

    public void OpenNewImageGroup() {
        var service = ServiceLocator.Resolve<IImageDataService>();
        var dialog = new Microsoft.Win32.OpenFileDialog() {
            Filter = String.Format(
                "Image or archive files (*{0};*{1})|*{0};*{1}|Image files (*{0})|*{0}|Archive files (*{1})|*{1}",
                ImageLocator.ImageFileExtension, ImageLocator.ArchiveFileExtension),
            InitialDirectory = service.Root };
        if (dialog.ShowDialog() is true && dialog.FileName is string file) {
            var locator = new ImageLocator(Path.GetFileNameWithoutExtension(file));
            if (FilePath.Equals(file, locator.CreateImageFilePath(service.Root)) ||
                    FilePath.Equals(file, locator.CreateArchiveFilePath(service.Root))) {
                if (ImageGroups.FirstOrDefault(g => FilePath.Equals(g.Base, locator.Base)) is not null) {
                    MessageBox.Show("Image group with same name already exists",
                        "Open new image group", MessageBoxButton.OK, MessageBoxImage.Error);   
                }
                else {
                    var igc = ImageGroupController.Create(this, locator.Base.ToString());
                    ImageGroups.Insert(ImageGroups.TakeWhile(e => e.Base.CompareTo(igc.Base) < 0).Count(), igc);
                    igc.IsShown = true;
                }
            }
            else MessageBox.Show("Invalid image or image archive file or location",
                    "Open new image group", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void OpenStoriesWindow(Window window, KeyboardDevice kbd) {
        var dialog = new StoriesWindow(window, this);
        dialog.ShowDialog();
    }

    public async void OpenStoryImages(Story story, bool hidePrevious) {
        if (hidePrevious)
            HideAllImages();
        foreach (var img in story.GetImageNames()) {
            var loc = new ImageLocator(img);
            if (ImageGroups.FirstOrDefault(g => FilePath.Equals(g.Base, loc.Base)) is ImageGroupController igc) {
                while (LockMessage is not null)
                    await Task.Delay(25);
                igc.SelectVariant(img);
            }
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

    public static ImageGroupController Create(MainWindowController controller, string baseName) {
        return new(controller, new ImageGroupData(baseName) {
            Width = 270, ShiftX = 0, ShiftY = 0, Rotation = 0.0, ScaleX = 1.0, ScaleY = 1.0 });
    }

    public ImageGroupController(MainWindowController controller, ImageGroupData data) {
        Controller = controller; _data = data;
    }

    public bool IsShown {
        get => Controller.ShownImageGroups.Contains(this);
        set {
            if (value != IsShown) {
                if (value is false) {
                    Controller.ShownImageGroups.Remove(this);
                    OnPropertyChanged();
                    Image = null;
                    Name = null;
                }
                else SelectVariant(Base);
            }
        }
    }

    public async void SelectVariant(string name) {
        if (_files is null) {
            _images = new(StringComparer.OrdinalIgnoreCase);
            _files = await Controller.LoadGroupDataAsync(this);
            OnPropertyChanged(nameof(Variants));
        }

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

            if (IsShown is false) {
                Controller.ShownImageGroups.Add(this);
                OnPropertyChanged(nameof(IsShown));
            }
        }
    }

    public override string ToString() => Base;

    public static ImageGroupData GetData(ImageGroupController igc) => igc._data;
}
