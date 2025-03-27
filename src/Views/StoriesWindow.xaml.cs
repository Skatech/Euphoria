using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using Skatech.Components.Presentation;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;

namespace Skatech.Euphoria;

partial class StoriesWindow : Window {
    readonly StoriesWindowController Controller;

    internal StoriesWindow(Window owner, MainWindowController mainController) {
        InitializeComponent();
        Owner = owner;
        DataContext = Controller = new StoriesWindowController(mainController,
            Path.Combine(App.LegacyDataDirectory, Story.DefaultFile));
        Components.Presentation.WindowBoundsKeeper.Register(this, "StoriesWindowBounds");
    }

    void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadStories();
        //Controller.LoadStoriesLegacy(Path.Combine(Path.GetDirectoryName(storiesFile)!, "Stories"));
    }

    void OnSaveChanges(object sender, RoutedEventArgs e) {
        Controller.SaveStories();
    }

    void OnNewStory(object sender, RoutedEventArgs e) {
        Controller.NewStory();
    }

    void OnCopyImages(object sender, RoutedEventArgs e) {
        if (WindowHelpers.FindTaggedObject(e.Source, out StoryController? sc))
            Controller.CopyImages(sc);
    }

    void OnOpenImages(object sender, RoutedEventArgs e) {
        if (WindowHelpers.FindTaggedObject(e.Source, out StoryController? sc)) {
            bool keepImages = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            Controller.OpenImages(sc, keepImages);
        }
    }

    void OnDropStory(object sender, RoutedEventArgs e) {
        if (WindowHelpers.FindTaggedObject(e.Source, out StoryController? sc))
            Controller.DropStory(sc);
    }

    void OnKeyUp(object sender, KeyEventArgs e) {
        if (e.Handled = e.Key == Key.Escape)
            Close();
    }
}

class StoriesWindowController : LockableControllerBase {
    readonly MainWindowController _mainController;
    readonly string _dataFile;
    bool _modified = false;

    public bool IsNotLocked => _mainController.LockMessage is null || LockMessage is null;

    public ObservableCollection<StoryController> Stories { get; } = new();

    public StoriesWindowController(MainWindowController mainController, string dataFile) {
        _mainController = mainController;
        _dataFile = dataFile;
    }

    public bool DropStory(StoryController sc) {
        return Stories.Remove(sc)
            ? _modified = true
            : false;
    }

    public void NewStory() {
        Stories.Insert(0, new StoryController(Story.Create("New story")) {
            IsModified = true, IsExpanded = true });
    }

    public void LoadStories() {
        async Task Load() {
            if (await LockedDriveCheck(_dataFile) && File.Exists(_dataFile)) {
                await Task.Delay(250);
                Stories.Clear();
                foreach (var st in Story.LoadStories(_dataFile).OrderByDescending(s => s.Date))
                    Stories.Add(new StoryController(st));
            }
        }
        if (IsNotLocked)
            LockUntilComplete(Load(), "Loading stories...");
    }

    public void SaveStories() {
        async Task Save() {
            if (await LockedDriveCheck(_dataFile)) {
                await Task.Delay(500);
                Story.SaveStories(_dataFile, Stories.Select(sc => sc.Story));
                foreach (var s in Stories)
                    s.IsModified = false;
                _modified = false;
            }
        }
        if (IsNotLocked && (_modified || Stories.Any(s => s.IsModified)))
            LockUntilComplete(Save(), "Saving stories...");
    }

    public bool LoadStoriesLegacy(string dir) {
        Stories.Clear();
        if (Directory.Exists(dir)){
            foreach (var st in Story.LoadStoriesLegacy(dir).OrderByDescending(s => s.Date))
                Stories.Add(new StoryController(st));
                _modified = true;
            return true;
        }
        return false;
    }

    public void OpenImages(StoryController sc, bool keepOpened) {
        if (IsNotLocked)
            LockUntilComplete(_mainController.OpenImageGroupAsync(
                sc.Story.GetImageNames(), keepOpened), "Opening images...");
    }

    public void CopyImages(StoryController sc) {
        if (IsNotLocked)
            sc.Images = String.Join('|',
                _mainController.ShownImageGroups.Select(i => i.Name).Where(s => s is not null));
    }
}

class StoryController : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public Story Story { get; }
    public bool IsModified { get; set; } = false;
    public bool IsExpanded { get; set; } = false;

    public string Name {
        get => Story.Name;
        set {
            if (value != Story.Name) {
                Story.Name = value;
                Date = DateTime.Now;
                OnPropertyChanged();
            }
        }
    }

    public string Images {
        get => Story.Images;
        set {
            if (value != Story.Images) {
                Story.Images = value;
                Date = DateTime.Now;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayImages));
            }
        }
    }

    public string Text {
        get => Story.Text;
        set {
            if (value != Story.Text) {
                Story.Text = value;
                Date = DateTime.Now;
                OnPropertyChanged();
                UpdateCaptionBrush();
            }
        }
    }

    public DateTime Date {
        get => Story.Date;
        set {
            if (value != Story.Date) {
                Story.Date = value;
                IsModified = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayDate));
            }
        }
    }

    public string DisplayImages => Story.Images.Replace("|", ", ");
    public string DisplayDate => Story.Date.ToString("yyyy-MM-dd");
    public SolidColorBrush CaptionBrush { get; set; } = new SolidColorBrush(Colors.Black);


    public StoryController(Story story) {
        Story = story; UpdateCaptionBrush();
    }

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void UpdateCaptionBrush() {
        double progress = Math.Sqrt(Math.Min(Story.Text.Length, 1000)) / Math.Sqrt(1000);
        int angle = (int)Math.Round((245 + 3600 + progress * 160) % 360); // 245 - start, 160 - width
        var color = Story.Text.Length < 1 ? Colors.Black : Skatech.Media.ColorHelper.FromHSL(angle, 100, 50);
        if (color != CaptionBrush.Color) {
            CaptionBrush.Color = color;
            OnPropertyChanged(nameof(CaptionBrush));
        }
    }
}

public class Story {
    public const string DefaultFile = "Stories.sta";
    public required string Name { get; set; }
    public required string Images { get; set; }
    public required string Text { get; set; }
    public required DateTime Date { get; set; }

    public string[] GetImageNames() => Images.Split('|');

    public static Story Create(string name) {
        return new Story { Name = name, Images = String.Empty, Text = String.Empty, Date = DateTime.Now };
    }

    public static void SaveStories(string file, IEnumerable<Story> stories) {
        CompressLines(file, stories.SelectMany(s => new[] {
            s.Name, s.Images, s.Date.ToString("s"),
            s.Text, "------------------<end-of-story>--" }));
    }

    public static IEnumerable<Story> LoadStories(string file) {
        var lines = new List<string>();
        var story = (Story)null!;
        int steps = 0;
        foreach (var line in DecompressLines(file)) {
            switch (steps++) {
                case 0:
                    story = Story.Create(line);
                    break;
                case 1:
                    story.Images = line;
                    break;
                case 2:
                    story.Date = DateTime.Parse(line);
                    break;                
                default:
                    if (line.Equals("------------------<end-of-story>--")) {
                        story.Text = String.Join(Environment.NewLine, lines);
                        lines.Clear();
                        steps = 0;
                        yield return story;
                    }
                    else lines.Add(line);
                    break;
            }
        }
    }

    public static IEnumerable<Story> LoadStoriesLegacy(string directory) {
        foreach (var file in Directory.EnumerateFiles(directory, "*.str")) {
            var lines = File.ReadAllLines(file);
            var story = Story.Create(Path.GetFileNameWithoutExtension(file));
            story.Date = File.GetLastWriteTime(file);
            story.Images = String.Join('|', lines[0].Split('|').Select(s => Path.GetFileNameWithoutExtension(s)));
            story.Text = String.Join(Environment.NewLine, lines.Skip(2));
            yield return story;
        }
    }

    static IEnumerable<string> DecompressLines(string archiveFile) {
        using var stream = new DeflateStream(
            File.OpenRead(archiveFile), CompressionMode.Decompress, false);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        while(reader.ReadLine() is string line)
            yield return line;
    }

    static void CompressLines(string archiveFile, IEnumerable<string> lines) {
        using var stream = new DeflateStream(
            File.Create(archiveFile), CompressionLevel.Optimal, false);
        using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
        foreach (var line in lines)
            writer.WriteLine(line);
    }
}