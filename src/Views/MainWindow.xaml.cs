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
using System.Text;
using System.Data;
using System.Windows.Media;

using Skatech.Components;
using Skatech.Concurrency;
using Skatech.Components.Presentation;
using Skatech.Imaging;
using Skatech.IO;

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
                        Controller.SaveImageSet(e.Key - Key.D0).DoNotAwait();
                    else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        Controller.LoadImageSet(e.Key - Key.D0).DoNotAwait();
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

    private async void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
        e.Cancel = await Controller.OnClosingWindow();
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
    readonly Skatech.Concurrency.Semaphore _semaphore = new(Environment.ProcessorCount);
    readonly IImageDataService _idsvc;
    readonly byte[] _iv, _key;

    public ObservableCollection<ImageGroupController> ImageGroups { get; private set; } = new();
    public ObservableCollection<ActorGroupController> ActorGroups { get; private set; } = new();
    public ObservableCollection<ImageGroupController> ShownImageGroups { get; } = new();
    public ImageGroupController? MouseOverGroup { get; set; }

    public MainWindowController() {
        _idsvc = ServiceLocator.Resolve<IImageDataService>();
        _key = AesHelpers.GenerateKey("test-value-please-ignore", 999999, Convert.FromHexString("2A6654FF76210284"));
        _iv = Convert.FromHexString("01C90EBFBF4B7606D64314235886C1FB");
        IconCache.CreateDirectory();
    }

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
        if (ShownImageGroups.Count > 0)
            await SaveImageSet(0);
        await LockUntilComplete(Lock(), LockAppMessage, InfoLockBackground);
        await Task.Delay(2000);
        await LockedDriveCheck(ServiceLocator.Resolve<IImageDataService>().Root);
    }

    public void LoadData() {
        async void Load() {
            try {
                var service = ServiceLocator.Resolve<IImageDataService>();
                await LockedDriveCheck(service.Root, 0);
                var imgdata = await LockUntilComplete(service.LoadAsync(), "Loading data...");
                // if (imgdata is null is bool restoring && restoring) {
                //     Debug.WriteLine("Data lost, restoring from legacy...");
                //     imgdata = await LockUntilComplete(service.LoadLegacyAsync(), "Restoring data from legacy...");
                // }
                if (imgdata is not null) {
                    ImageGroups = new(imgdata.OrderBy(e => e.Base).Select(e => new ImageGroupController(this, e)));
                    OnPropertyChanged(nameof(ImageGroups));
                    // if (restoring)
                    //     await LockUntilComplete(SaveDataAsync(), "Saving restored data...", "#88008800");

                    // foreach (var igc in ImageGroups) {
                    //     var loc = new ImageLocator(igc.Base);
                    //     var grp = GetOrAddActorGroup(loc.Actr.ToString());
                    //     grp.ImageGroups.Add(igc);
                    // }
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

    public ActorGroupController GetOrAddActorGroup(string name) {
        var group = ActorGroups.FirstOrDefault(g => name.Equals(g.Name, StringComparison.OrdinalIgnoreCase));
        if (group is null) 
            ActorGroups.Add(group = new ActorGroupController(name, Enumerable.Empty<ImageGroupController>()));        
        return group;
    }

    public Task SaveDataAsync() {
        var service = ServiceLocator.Resolve<IImageDataService>();
        return service.SaveAsync(ImageGroups.Select(ImageGroupController.GetData).OrderBy(e => e.Base));
    }

    public void SaveData() {
        if (LockMessage is null)
            LockUntilComplete(SaveDataAsync(),"Saving data...");
    }

    public async ValueTask<Dictionary<string, string>?> LoadImageGroupDataAsync(ImageGroupController igc) {
        await Task.Yield();
        using var lck = await _semaphore.WaitAsync();
        return await LockedDriveCheck(_idsvc.Root) ? await _idsvc.LoadImageGroupDataAsync(igc.Base) : null;
    }


    public async ValueTask<BitmapFrame?> LoadImageAsync(string path, string fileNameNoExt) {
        await Task.Yield();
        using var lck = await _semaphore.WaitAsync();
        return await LockedDriveCheck(_idsvc.Root) ? await _idsvc.LoadImageAsync(path, fileNameNoExt) : null;
    }

    public async ValueTask CacheIconAsync(string name, BitmapFrame image) {
        await Task.Yield();
        using var lck = await _semaphore.WaitAsync();
        IconCache.SaveIcon(name, image, _key, _iv);
    }

    public async Task<BitmapFrame?> UnCacheIconAsync(string name) {
        await Task.Yield();
        using var lck = await _semaphore.WaitAsync();
        return IconCache.LoadIcon(name, _key, _iv);
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
        var errors = new List<String>();
        int position = 0;
        foreach (var name in imageNames) {
            if (FindImageGroup(name) is ImageGroupController igc) {
                igc.IsShown = false;
                if (await igc.SelectVariant(name, position++) is false)
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

    ImageGroupController? FindImageGroup(string name) {
        var loc = new ImageLocator(name);
        return ImageGroups.FirstOrDefault(g => FilePath.Equals(g.Base, loc.Base));
    }

    public IEnumerable<string> GetShownImageNames() =>
        ShownImageGroups.Select(i => i.Name
            ?? throw new NoNullAllowedException("Shown image group name must have value"));

    public async ValueTask SaveImageSet(int index) {
        if (LockMessage is null && StoredSets.Default.UpdateSet(index, GetShownImageNames())) {
            await LockWithMessage(ShownImageGroups.Count < 1
                ? $"Clearing set #{index}" : $"Storing set #{index}", 750);
            SetWindowTitle("#" + index);
        }
    }

    public async ValueTask LoadImageSet(int index) {
        if (LockMessage is null && StoredSets.Default[index] is string[] names) {
            if (names.Length > 0) {
                HideAllImages();
                await OpenImageGroupAsync(names, false);
                SetWindowTitle("#" + index);
            }
            else await LockWithErrorMessage($"Set #{index} is not defined", 1500);
        }
    }

    public async ValueTask<bool> OnClosingWindow() {
        if (ShownImageGroups.Count > 0)
            await SaveImageSet(0);
        return false;
    }
}
