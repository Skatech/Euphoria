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
using System.Threading;

namespace Skatech.Euphoria;

class ActorGroupController {
    public string Name { get; private set; }
    public ObservableCollection<ImageGroupController> ImageGroups { get; private set; }

    public ActorGroupController(string name, IEnumerable<ImageGroupController> groups) {
        Name = name;
        ImageGroups = new ObservableCollection<ImageGroupController>(groups);
    }
}

class ImageGroupController : ControllerBase {
    readonly MainWindowController _ctrl;
    readonly ImageGroupData _data;

    public ActorGroupController Actor { get; init; }    
    public MainWindowController Controller => _ctrl;
    public string Base => _data.Base;
    public int DisplayIndex => _ctrl.ShownImageGroups.IndexOf(this);
    public override string ToString() => Base;
    public static ImageGroupData GetData(ImageGroupController igc) => igc._data;

    public static ImageGroupController Create(MainWindowController controller, string baseName) {
        return new(controller, new ImageGroupData(baseName) {
            Width = 270, ShiftX = 0, ShiftY = 0, Rotation = 0.0, ScaleX = 1.0, ScaleY = 1.0 });
    }

    public ImageGroupController(MainWindowController ctrl, ImageGroupData data) {
        _ctrl = ctrl; _data = data;
        Actor = _ctrl.GetOrAddActorGroup(new ImageLocator(_data.Base).Actr.ToString());
        Actor.ImageGroups.Add(this);
    }

    public int Width {
        get => _data.Width;
        set => TryUpdateField(ref _data.Width, value);
    }

    bool _flip;
    public bool IsFlipped {
        get => _flip;
        set { 
            if (TryUpdateField(ref _flip, value))
                OnPropertyChanged(nameof(ScaleX));
        }
    }
    public bool IsPreFlipped => _data.IsFlipped;
    public double ScaleY => _data.ScaleY;
    public double ScaleX => _flip ? -_data.ScaleX : _data.ScaleX;
    public bool ChangeScale(double value, bool isFlipped) {
        bool oldfl = IsPreFlipped;
        bool updsy = TryUpdateField(ref _data.ScaleY, value, nameof(ScaleY));
        bool updsx = TryUpdateField(ref _data.ScaleX, isFlipped ? -value : value, nameof(ScaleX));
        if (oldfl != IsPreFlipped)
            OnPropertyChanged(nameof(IsPreFlipped));
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

    public bool IsShown {
        get => _ctrl.ShownImageGroups.Contains(this);
        set {
            if (value != IsShown) {
                if (value is false) {
                    _ctrl.ShownImageGroups.Remove(this);
                    OnPropertyChanged();
                    Image = null;
                    Name = null;
                }
                else SelectVariant(Base, 0).DoNotAwait();
            }
        }
    }

    public async ValueTask<bool> SelectVariant(string name, int insertPosition = -1) {
        if (FilePath.Equals(name, Name))
            return true;
        if (await GetImageAsync(name) is BitmapFrame image) {
            Name = name;
            Image = image;
            OnPropertiesChanged(nameof(Name), nameof(Image), nameof(Variants));
            if (IsShown is false) {
                _ctrl.ShownImageGroups.Insert(
                    insertPosition < 0 ? _ctrl.ShownImageGroups.Count : insertPosition, this);
                OnPropertyChanged(nameof(IsShown));
            }
            return true;
        }
        Debug.WriteLine($"Image variant missing: {name}");
        return false;
    }

    Dictionary<string, Record>? _cache;
    public IEnumerable<string> Variants => (_cache is not null)
        ? _cache.Keys.Where(s => !String.Equals(s, Name, StringComparison.OrdinalIgnoreCase))
        : Enumerable.Empty<string>();

    public async ValueTask<BitmapFrame?> GetImageAsync(string name) {
        if (_cache is null) {
            var data = await _ctrl.LoadImageGroupDataAsync(this);
            _cache = data?.ToDictionary(r => r.Key, r => new Record(r.Key, r.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        if (_cache is not null && _cache.TryGetValue(name, out Record? rec)) {
            if (rec.Image is null) {
                Debug.WriteLine(name + " - loading from network...");
                rec.Image = await _ctrl.LoadImageAsync(rec.File, name);
            }
            return rec.Image;
        }
        return null;
    }

    BitmapFrame? _icon;
    public BitmapFrame Icon {
        get {
            if (_icon is null) {
                _icon = Images.LoadIcon;
                LoadIconAsync().DoNotAwait();
            }
            return _icon;
        }
    }

    private async ValueTask LoadIconAsync() {
        var icon = await _ctrl.UnCacheIconAsync(Base);
        if (icon is null) {
            var image = await GetImageAsync(Base);
            if (image is not null) {
                icon = ImageHelpers.CreateMiniature(image, 32);
                _ctrl.CacheIconAsync(Base, icon).DoNotAwait();
            }
        }
        if (icon is not null) {
            TryUpdateField(ref _icon, icon, nameof(Icon));
        }
        else TryUpdateField(ref _icon, Images.DeadIcon, nameof(Icon));
    }

    public void ResetCache() {
        IsShown = false;
        _cache?.Clear();
        _cache = null;
    }

    public async ValueTask ReloadImages() {
        var name = Name;
        var indx = DisplayIndex;
        ResetCache();
        if (name is not null) {
            if (await SelectVariant(name, indx)) {
                IconCache.DropIcon(Base);
                TryUpdateField(ref _icon, null, nameof(Icon));
            }
        }
    }

    record Record(string Name, string File) {
        public BitmapFrame? Image { get; set; }
    }
}
