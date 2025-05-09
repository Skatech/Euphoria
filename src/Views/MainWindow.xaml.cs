﻿using System;
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
using System.Text;
using System.Data;

namespace Skatech.Euphoria;

partial class MainWindow : Window {
    MainWindowController Controller => (MainWindowController)DataContext;

    public MainWindow() {
        InitializeComponent();
        Components.Presentation.WindowBoundsKeeper.Register(this, "MainWindowBounds");
    }

    private void SwitchFullScreen() {
        if (WindowStyle == WindowStyle.None) {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
        }
        else {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
    }

    // Func<Key, bool> KeyDoubleEventChecker = WindowHelpers.CreateKeyDoubleEventChecker();
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
        else if (imagesItemsControl.HandleKeyDownUp(sender, e)) {
            e.Handled = true;
        }
        else switch (e.Key) {
            case Key.L:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == ModifierKeys.None) {
                    if (WindowStyle == WindowStyle.None)
                        SwitchFullScreen();
                    Controller.LockApp();
                }
                break;
            case Key.R:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == (ModifierKeys.Control|ModifierKeys.Shift))
                    Controller.ResetCaches();
                break;
            case Key.X:
                if (e.IsDown && e.IsRepeat is false && e.KeyboardDevice.Modifiers == (ModifierKeys.Control|ModifierKeys.Shift))
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
                // if (e.IsToggled && KeyDoubleEventChecker(e.Key)) {
                //     Debug.WriteLine($"Double tap {e.Key}");
                //     Controller.SwitchControlMode(Controller.IsControlMode is false);
                // }
                if (e.IsRepeat is false)
                    Controller.SwitchControlMode(e.IsDown);
                break;

            case Key.D0: case Key.D1: case Key.D2: case Key.D3: case Key.D4:
            case Key.D5: case Key.D6: case Key.D7: case Key.D8: case Key.D9:
                if (e.IsDown && e.IsRepeat is false) {
                    if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                        Controller.SaveImageSet(e.Key - Key.D0);
                    else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        Controller.LoadImageSet(e.Key - Key.D0);
                }
            break;
        }
    }
    
    private void OnHideAllImagesMenuItemClick(object sender, RoutedEventArgs e) {
        Controller.HideAllImages();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadData();
        // #if !DEBUG
            Controller.LockApp();
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

    private void OnResetCachesMenuItemClick(object sender, RoutedEventArgs? e) {
        Controller.ResetCaches();
    }
}

class MainWindowController : LockableControllerBase {
    public ObservableCollection<ImageGroupController> ImageGroups { get; private set; } = new();
    public ObservableCollection<ImageGroupController> ShownImageGroups { get; } = new();    
    public ImageGroupController? MouseOverGroup { get; set; }

    public bool IsControlMode { get; private set; }
    public void SwitchControlMode(bool enable) {
        if (IsControlMode != enable) {
            IsControlMode = enable;
            OnPropertyChanged(nameof(IsControlMode));
        }
    }

    public string WindowTitle { get; private set; } = $"{App.Title}  v{App.Version.ToString(3)}";
    public void SetWindowTitle(string? suffix = null) {
        if (suffix is null) {
            if (WindowTitle != App.Title) {
                WindowTitle = App.Title;
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
        else if ((WindowTitle.Length == suffix.Length + App.Title.Length + 3 &&
                WindowTitle.EndsWith(suffix)) is false) {
            WindowTitle = $"{App.Title} - {suffix}";
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public static string LockAppMessage { get; } = "Awaiting... ";
    public async void LockApp() {
        async Task Lock() {
            while((Keyboard.IsKeyDown(Key.L) && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) is false)
                await Task.Delay(50);
        }
        await LockUntilComplete(Lock(), LockAppMessage, InfoLockBackground);
        await LockedDriveCheck(ServiceLocator.Resolve<IImageDataService>().Root);
    }

    public void LoadData() {
        async void Load() {
            try {
                var service = ServiceLocator.Resolve<IImageDataService>();
                await LockedDriveCheck(service.Root, 0);
                var imgdata = await LockUntilComplete(service.LoadAsync(), "Loading data...");
                if (imgdata is null is bool restoring && restoring) {
                    Debug.WriteLine("Data lost, restoring from legacy...");
                    imgdata = await LockUntilComplete(service.LoadLegacyAsync(), "Restoring data from legacy...");
                }
                if (imgdata is not null) {
                    ImageGroups = new(imgdata.OrderBy(e => e.Base)
                        .Select(e => new ImageGroupController(this, e)));
                    OnPropertyChanged(nameof(ImageGroups));
                    if (restoring)
                        await LockUntilComplete(SaveDataAsync(), "Saving restored data...", "#88008800");
                }
                else await this.LockWithErrorMessage("Unable to load any data", 5000);
            }
            catch (Exception ex) {
                await this.LockWithErrorMessage($"Data loading error: {ex.Message}", int.MaxValue);
            }
        }
        if (LockMessage is null)
            Load();
    }

    public Task SaveDataAsync() {
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
            ? await LockUntilComplete(service.LoadImageGroupDataAsync(igc.Base), $"Loading group data {igc.Base}...")
            : null;
    }

    public async ValueTask<BitmapFrame?> LoadGroupImageAsync(string file, string name) {
        var service = ServiceLocator.Resolve<IImageDataService>();
        return await LockedDriveCheck(service.Root)
            ? await LockUntilComplete(service.LoadImageAsync(file, name), $"Loading image {name}...")
            : null;
    }

    public void ShiftImageGroup(ImageGroupController igc, bool right) {
        int cnt = ShownImageGroups.Count, pos = ShownImageGroups.IndexOf(igc);
        if (cnt > 1 && pos >= 0) {
            ShownImageGroups.Move(pos, ((right ? pos + 1 : pos - 1) + cnt) % cnt);
        }
    }

    public void ShiftImageGroupTo(ImageGroupController igc, int positionTo) {
        int pos = ShownImageGroups.IndexOf(igc);
        if (pos >= 0 && positionTo >= 0 && pos != positionTo)
            ShownImageGroups.Move(pos, positionTo);
    }

    public void ShiftImageGroupTo(ImageGroupController igc, ImageGroupController igcTo) {
        ShiftImageGroupTo(igc, ShownImageGroups.IndexOf(igcTo));
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
                igc.IsShown = false;
                while (LockMessage is not null)
                    await Task.Delay(25);
                if (await igc.SelectVariant(name) is false)
                    errors.Add($"Image variant missing: {name}");
            }
            else errors.Add($"Image group missing: {name}");
        }

        foreach (var error in errors)
            await LockWithErrorMessage(error, errors.Count < 3 ? 2000 : 1000);
    }

    public void ResetCaches() {
        if (LockMessage is null) {
            var shownImageNames = ShownImageGroups.Select(g => g.Name!).ToArray();
            foreach (var igc in ImageGroups)
                igc.ResetCache();
            OpenImageGroupAsync(shownImageNames, false).DoNotAwait();
        }
    }

    ImageGroupController? FindImageController(string name) {
        var loc = new ImageLocator(name);
        return ImageGroups.FirstOrDefault(g => FilePath.Equals(g.Base, loc.Base));
    }

    public IEnumerable<string> GetShownImageNames() =>
        ShownImageGroups.Select(i => i.Name
            ?? throw new NoNullAllowedException("Shown image group name must have value"));

    public void SaveImageSet(int index) {
        if (LockMessage is null && StoredSets.Default.UpdateSet(index, GetShownImageNames())) {
            LockWithMessage($"Storing set #{index}", 750).DoNotAwait();
            SetWindowTitle("#" + index);
        }
    }

    public void LoadImageSet(int index) {
        if (LockMessage is null && StoredSets.Default[index] is string[] names) {
            if (names.Length > 0) {
                HideAllImages();
                OpenImageGroupAsync(names, false).DoNotAwait();
                SetWindowTitle("#" + index);
            }
            else LockWithErrorMessage($"Set #{index} is not defined", 1500).DoNotAwait();
        }
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
        _files?.Keys.Where(s => !FilePath.Equals(s, Name)) ?? Enumerable.Empty<string>();

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
                else SelectVariant(Base).DoNotAwait();
            }
        }
    }

    public async ValueTask<bool> SelectVariant(string name) {
        if (FilePath.Equals(name, Name))
            return true;
        if (await PreloadVariantImage(name) is BitmapFrame image) {
            Name = name;
            Image = image;
            OnPropertiesChanged(nameof(Name), nameof(Image), nameof(Variants));
            if (IsShown is false) {
                Controller.ShownImageGroups.Add(this);
                OnPropertyChanged(nameof(IsShown));
            }
            return true;
        }
        Debug.WriteLine($"Image variant missing: {name}");
        return false;
    }

    public async ValueTask<BitmapFrame?> PreloadVariantImage(string name) {
        if (_files is null && (_files = await Controller.LoadGroupDataAsync(this)) is not null) {
            _images = new(StringComparer.OrdinalIgnoreCase);
            // OnPropertyChanged(nameof(Variants));
        }
        if (_files?.TryGetValue(name, out string? file) ?? false) {
            if (_images!.TryGetValue(name, out BitmapFrame? image))
                return image;
            if ((image = await Controller.LoadGroupImageAsync(file, name))
                    is not null && _images.TryAdd(name, image))
                return image;
        }
        return null;
    }

    public void ResetCache() {
        IsShown = false;
        _images = null;
        _files = null;
    }

    public override string ToString() => Base;

    public static ImageGroupData GetData(ImageGroupController igc) => igc._data;
}

class StoredSets {
    string[][] _sets = Enumerable.Repeat(Array.Empty<string>(), 10).ToArray();

    public static StoredSets Default { get; } = new StoredSets(); 

    public string[] this[int index] => _sets[index];

    public bool UpdateSet(int index, IEnumerable<string> value) {
        if (!Enumerable.SequenceEqual(_sets[index], value)) {
            _sets[index] = value.ToArray();
            var sb = _sets.Aggregate(new StringBuilder(), (a, v) => a.AppendLine(String.Join('|', v)));
            var ss = Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
            ServiceLocator.Resolve<Skatech.Components.Settings.ISettings>().Set("StoredSets", ss);
            return true;
        }
        return false;
    }

    StoredSets() {
        if (ServiceLocator.Resolve<Skatech.Components.Settings.ISettings>().Get("StoredSets") is string s) {
            _sets = Encoding.UTF8.GetString(Convert.FromBase64String(s))
                .Split(Environment.NewLine).Select(s => s.Split('|', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }
    }
}