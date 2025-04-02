using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;

using Skatech.IO;
using Skatech.Components;
using Skatech.Components.Presentation;
using System.Data.SqlTypes;
using System.Configuration;

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
        if (e.Key == Key.Escape && e.IsDown) {
            if (WindowState == WindowState.Maximized) {
                SwitchFullScreen();
            } else Close();
        }
        else if (e.Key == Key.Enter && e.IsDown) {
            SwitchFullScreen();
        }
        else if (Controller.LockMessage is not null) {
            e.Handled = true;
        }
        else switch (e.Key) {
            case Key.L:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.None)
                    Controller.LockWindow();
                break;
            case Key.X:
                if (e.IsDown && e.IsRepeat is false)
                    Controller.HideAllImages();
                break;
            case Key.S:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    Controller.SaveData();
                else if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                    OnOpenStoriesWindowMenuItemClick(this, null);
                break;
            case Key.O:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    Controller.OpenNewImageGroup();
                else if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                    OnOpenImageToolsWindowMenuItemClick(this, null);
                break;
            case Key.D:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.None)
                    OnOpenDiceWindowMenuItemClick(this, null);
                break;
            case Key.OemTilde:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.None)
                    Controller.SwitchControlMode(Controller.IsControlMode is false);
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                if (e.IsRepeat is false)
                    Controller.SwitchControlMode(e.IsDown);
                break;
        }
    }
    
    private void OnHideAllImagesMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.HideAllImages();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadData();
        // #if !DEBUG
            Controller.LockWindow();
        // #endif
    }

    private void OnSaveDataMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.SaveData();
    }

    private void OnOpenFileMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.OpenNewImageGroup();
    }

    private void OnOpenStoriesWindowMenuItemClick(object sender, RoutedEventArgs? e) {
        new StoriesWindow(this, Controller).ShowDialog();
    }

    private void OnOpenImageToolsWindowMenuItemClick(object sender, RoutedEventArgs? e) {
        new ImageToolsWindow(this).ShowDialog();
    }

    private void OnOpenDiceWindowMenuItemClick(object sender, RoutedEventArgs? e) {
        new DiceWindow(this).ShowDialog();
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

    public async void LockWindow() {
        async Task Lock() {
            while((Keyboard.IsKeyDown(Key.L) && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) is false)
                await Task.Delay(50);
        }
        await LockUntilComplete(Lock(), ImagesItemsControl.LockActionMessage, InfoLockBackground);
        await LockedDriveCheck(ServiceLocator.Resolve<IImageDataService>().Root);
    }

    public void LoadData() {
        async void Load() {
            var service = ServiceLocator.Resolve<IImageDataService>();
            await LockedDriveCheck(service.Root, 0);
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

    public async ValueTask<Dictionary<string, string>?> LoadGroupDataAsync(ImageGroupController igc) {
        var service = ServiceLocator.Resolve<IImageDataService>();
        return await LockedDriveCheck(service.Root)
            ? await LockUntilComplete(service.GetGroupImagesAsync(igc.Base), $"Loading group {igc.Base}...")
            : null;
    }

    public async ValueTask<BitmapFrame?> LoadGroupImageAsync(ImageGroupController igc, string file, string name) {
        var service = ServiceLocator.Resolve<IImageDataService>();
        return await LockedDriveCheck(service.Root)
            ? await LockUntilComplete(service.TryLoadImageAsync(file, name), $"Loading image {name}...")
            : null;
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

    public async Task OpenImageGroupAsync(IEnumerable<string> imageNames, bool keepOpened) {
        if (keepOpened is false)
            HideAllImages();

        // var loads = imageNames.Select(s => (Name: s, Controller: FindImageController(s)))
        //     .Select(r => (Name: r.Name, Controller: r.Controller,
        //         Task: r.Controller?.PreloadVariant(r.Name).AsTask()
        //             ?? Task.FromResult(false)));

        // await Task.WhenAll(loads.Select(r => r.Task));

        // foreach (var load in loads) {
        //     if (load.Controller is null) {
        //         LockWithErrorMessage($"Image group missing: {load.Name}");
        //     }
        //     else if (load.Task.Result) {
        //         load.Controller.SelectVariant(load.Name);
        //     }
        //     else LockWithErrorMessage($"Image load failed: {load.Name}");
        // }

        var errors = new List<String>();
        foreach (var name in imageNames) {
            if (FindImageController(name) is ImageGroupController igc) {
                while (LockMessage is not null)
                    await Task.Delay(25);
                if (await igc.PreloadVariantImage(name) is not null) {
                    igc.SelectVariant(name);
                }
                else errors.Add($"Image variant missing: {name}");
            }
            else errors.Add($"Image group missing: {name}");
        }

        foreach (var error in errors)
            await LockWithErrorMessage(error, errors.Count < 3 ? 2000 : 1000);
    }

    ImageGroupController? FindImageController(string name) {
        var loc = new ImageLocator(name);
        return ImageGroups.FirstOrDefault(g => FilePath.Equals(g.Base, loc.Base));
    }
}

class ImageGroupController : ControllerBase {
    public string Base => _data.Base;
    public int Width {
        get => _data.Width;
        set => ChangeWidth(value);
    }
    public bool ChangeWidth(int value) {
        return TryUpdateField(ref _data.Width, value, nameof(Width));
    }

    bool _flip;
    public bool IsShowFlipped {
        get => _flip;
        set {
            if (TryUpdateField(ref _flip, value))
                OnPropertyChanged(nameof(ScaleX));
        }
    }
    public bool IsFlipped => _data.IsFlipped;
    public double ScaleY => _data.ScaleY;
    public double ScaleX => _flip ? -_data.ScaleX : _data.ScaleX;
    public bool ChangeScale(double value, bool isFlipped) {
        bool oldfl = IsFlipped;
        bool updsy = TryUpdateField(ref _data.ScaleY, value, nameof(ScaleY));
        bool updsx = TryUpdateField(ref _data.ScaleX, isFlipped ? -value : value, nameof(ScaleX));
        if (oldfl != IsFlipped)
            OnPropertyChanged(nameof(IsFlipped));
        return updsy || updsx;
    }

    public int ShiftX => _data.ShiftX;
    public bool ChangeShiftX(int value) {
        return TryUpdateField(ref _data.ShiftX, value, nameof(ShiftX));
    }

    public int ShiftY => _data.ShiftY;
    public bool ChangeShiftY(int value) {
        return TryUpdateField(ref _data.ShiftY, value, nameof(ShiftY));
    }

    public double Rotation => _data.Rotation;
    
    public string? Name { get; private set; }
    public BitmapFrame? Image { get; private set; }

    Dictionary<string, BitmapFrame?>? _images;
    Dictionary<string, string>? _files;
    public IEnumerable<string> Variants =>
        _files?.Keys.Where(s => !s.Equals(Name, StringComparison.OrdinalIgnoreCase))
            ?? Enumerable.Empty<string>();

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
        if (await PreloadVariantImage(name) is BitmapFrame image) {
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
        else Debug.WriteLine($"Image variant missing: {name}");
    }

    public async ValueTask<BitmapFrame?> PreloadVariantImage(string name) {
        if (_images == null) {
            _images = new(StringComparer.OrdinalIgnoreCase);

            _files = await Controller.LoadGroupDataAsync(this);
            OnPropertyChanged(nameof(Variants));
        }

        if (_files is not null && _files.TryGetValue(name, out string? file) && !FilePath.Equals(name, Name)) {
            if (_images.TryGetValue(name, out BitmapFrame? image))
                return image;
            image = await Controller.LoadGroupImageAsync(this, file, name);
            if (image is not null && _images.TryAdd(name, image))
                return image;
        }
        return null;
    }

    public override string ToString() => Base;

    public static ImageGroupData GetData(ImageGroupController igc) => igc._data;
}
