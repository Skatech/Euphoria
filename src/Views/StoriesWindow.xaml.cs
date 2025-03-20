using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using Skatech.Components.Presentation;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using Skatech.IO;

namespace Skatech.Euphoria;

#nullable enable

public partial class StoriesWindow : Window {
    Action<Story> _openStory;
    StoriesWindowController Controller => (StoriesWindowController)DataContext;
    
    public IEnumerable<string> LoadedImages { get; private set; }


    public StoriesWindow(Window owner, string storiesFile, IEnumerable<string> loadedImages, Action<Story> openStory) {
        InitializeComponent();
        // WindowStateKeeper.Add(this, "StoriesWindowBounds");
        Components.Presentation.WindowBoundsKeeper.Register(this, "StoriesWindowBounds");
        Owner = owner; _openStory = openStory;
        LoadedImages = loadedImages;
        Controller.DataFile = storiesFile;
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

    private void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Controller.LoadStories();
        //Controller.LoadStoriesLegacy(Path.Combine(Path.GetDirectoryName(storiesFile)!, "Stories"));
    }

    private void OnSaveChanges(object sender, RoutedEventArgs e) {
        Controller.SaveStories();
    }

    private void OnNewStory(object sender, RoutedEventArgs e) {
        Controller.NewStory();
    }

    private void OnCopyImages(object sender, RoutedEventArgs e) {
        if (FindTaggedObject(e.Source, out StoryController? sc))
            sc.Images = String.Join('|', LoadedImages);
    }

    private void OnOpenImages(object sender, RoutedEventArgs e) {
        if (FindTaggedObject(e.Source, out StoryController? sc))
            _openStory(sc.Story);
    }

    private void OnDropStory(object sender, RoutedEventArgs e) {
        if (FindTaggedObject(e.Source, out StoryController? sc))
            Controller.DropStory(sc);
    }

    private void OnKeyUp(object sender, KeyEventArgs e) {
        switch (e.Key) {
            case Key.Escape:
                e.Handled = true;
                Close();
                break;
        }
    }
}

class StoriesWindowController : LockableControllerBase {
    bool _modified = false;

    public string DataFile { get; set; } = Story.DefaultFile;
    public ObservableCollection<StoryController> Stories { get; } = new();

    public bool DropStory(StoryController sc) {
        if (Stories.Remove(sc)) {
            return _modified = true;
        }
        return false;
    }

    public void NewStory() {
        Stories.Insert(0, new StoryController(Story.Create("New story", "", "", DateTime.Now)) {
            IsModified = true, IsExpanded = true });
    }

    public void LoadStories() {
        async Task Load() {
            if (await DriveChecker(DataFile) && File.Exists(DataFile)) {
                await Task.Delay(250);
                Stories.Clear();
                foreach (var st in Story.LoadStories(DataFile).OrderByDescending(s => s.Date))
                    Stories.Add(new StoryController(st));
            }
        }
        if (LockMessage is null)
            LockUntilComplete(Load(), "Loading stories...");
    }

    public void SaveStories() {
        async Task Save() {
            if (await DriveChecker(DataFile)) {
                await Task.Delay(500);
                Story.SaveStories(DataFile, Stories.Select(sc => sc.Story));
                foreach (var s in Stories)
                    s.IsModified = false;
                _modified = false;
            }
        }
        if (LockMessage is null && (_modified || Stories.Any(s => s.IsModified)))
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

    static readonly Func<string, ValueTask<bool>> DriveChecker =
        FilePath.CreateDriveAvailableChecker(TimeSpan.FromMinutes(1));
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
        var color = Story.Text.Length < 1 ? Colors.Black : ColorFromHSL(angle, 100, 50);
        if (color != CaptionBrush.Color) {
            CaptionBrush.Color = color;
            OnPropertyChanged(nameof(CaptionBrush));
        }
    }

    public static Color ColorFromHSL(int h, byte s, byte l) {
        double r = 1, g = 1, b = 1;

        double modH = h / 360.0;
        double modS = s / 100.0;
        double modL = l / 100.0;

        double q = (modL < 0.5) ? modL * (1 + modS) : modL + modS - modL * modS;
        double p = 2 * modL - q;

        if (modL == 0) {
            r = 0;
            g = 0;
            b = 0;
        } else if (modS != 0) {
            r = GetHue(p, q, modH + 1.0 / 3);
            g = GetHue(p, q, modH);
            b = GetHue(p, q, modH - 1.0 / 3);
        }
        else {
            r = modL;
            g = modL;
            b = modL;
        }

        return Color.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));

        static double GetHue(double p, double q, double t) {
            double value = p;

            if (t < 0) t++;
            if (t > 1) t--;

            if (t < 1.0 / 6) {
                value = p + (q - p) * 6 * t;
            }
            else if (t < 1.0 / 2) {
                value = q;
            }
            else if (t < 2.0 / 3) {
                value = p + (q - p) * (2.0 / 3 - t) * 6;
            }

            return value;
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

    public static Story Create(string name, string images, string text, DateTime date) {
        return new Story { Name = name, Images = images, Text = text, Date = date };
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
                    story = Story.Create(line, string.Empty, string.Empty, DateTime.Now);
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
            yield return Story.Create(Path.GetFileNameWithoutExtension(file),
                String.Join('|', lines[0].Split('|').Select(s => Path.GetFileNameWithoutExtension(s))),
                String.Join(Environment.NewLine, lines.Skip(2)),
                File.GetLastWriteTime(file));
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