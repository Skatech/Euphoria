using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using Skatech.IO;
using System.Data;

namespace Skatech.Euphoria;

public class ImageGroupData {
    public readonly string Base;
    public int Width;
    public int ShiftX;
    public int ShiftY;
    public double Rotation;
    public double ScaleX;
    public double ScaleY;
    public ImageGroupData(string baseName) => Base = baseName;
}

interface IImageDataService {
    string Root { get; }
    IEnumerable<ImageGroupData> Load();
    IEnumerable<ImageGroupData>LoadLegacy();
    void Save(IEnumerable<ImageGroupData> data);
    Dictionary<string, string> GetGroupImages(string baseName);
    BitmapFrame? TryLoadImage(string path, string fileNameNoExt);

    Task<IEnumerable<ImageGroupData>> LoadAsync();
    Task<bool> SaveAsync(IEnumerable<ImageGroupData> data);
    Task<Dictionary<string, string>> GetGroupImagesAsync(string baseName);
    Task<BitmapFrame?> TryLoadImageAsync(string path, string fileNameNoExt);
}

class ImageDataService : IImageDataService {
    readonly static Regex _parser = new(
        @"\A\""([\w\s-+$@%\(\)\\/.:']+)\""\s(-?\d+)\s(-?\d+)\s(-?\d+)\s(-?\d+\.?\d*)\s(-?\d+\.?\d+)\s(-?\d+\.?\d+)\z",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.CultureInvariant);
    readonly string _root, _file;
    public string Root => _root;

    public ImageDataService(string root) {
        _file = Path.Combine(_root = root, "Images.dbz");
    }

    public IEnumerable<ImageGroupData> Load() {
        if (File.Exists(_file)) {
            foreach (var line in DecompressLines(_file)) {
                var match = _parser.Match(line);
                if (match.Success) {
                    yield return new(match.Result("$1")) {
                        Width = Int32.Parse(match.Result("$2")),
                        ShiftX = Int32.Parse(match.Result("$3")),
                        ShiftY = Int32.Parse(match.Result("$4")),
                        Rotation = Double.Parse(match.Result("$5")),
                        ScaleX = Double.Parse(match.Result("$6")),
                        ScaleY = Double.Parse(match.Result("$7"))
                    };
                }
                else throw new FormatException("Image group record invalid format"); 
            }
        }
    }

    public async Task<IEnumerable<ImageGroupData>> LoadAsync() {
        await Task.Delay(100).ConfigureAwait(false);
        return await DriveChecker.Default.Check(_root) ? Load() : Enumerable.Empty<ImageGroupData>();
    }

    public void Save(IEnumerable<ImageGroupData> data) {
        CompressLines(_file, data.Select(i => 
            $"\"{i.Base}\" {i.Width} {i.ShiftX} {i.ShiftY} {i.Rotation} {i.ScaleX} {i.ScaleY}"));
    }

    public async Task<bool> SaveAsync(IEnumerable<ImageGroupData> data) {
        if (await DriveChecker.Default.Check(_root) is bool found && found) {
            await Task.Delay(1000).ConfigureAwait(false); Save(data); }
        return found;
    }

    public IEnumerable<ImageGroupData>LoadLegacy() {
        string file = Path.Combine(_root, "@exh", "Images.dbx");
        var parser = new Regex(
            @"\A\""([\w\s-+$@%\(\)\\/.:']+)\""\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(\d*\.?\d*)\z",
            RegexOptions.Compiled);
        using var gstream = new DeflateStream(
            File.OpenRead(file), CompressionMode.Decompress, false);
        using var breader = new StreamReader(gstream, System.Text.Encoding.UTF8);

        string[] fixes = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(
            "4D724D314572637C4D724D31657C456732277C456732787C45673262277C45673279")).Split('|');
        string[] doubles = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(
            "4761347C476134627C4761367C5065723163")).Split('|');
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (breader.ReadLine() is string line) {
            var match = parser.Match(line);
            if (match.Success) {
                string path = match.Result("$1");
                if (!Path.GetFileNameWithoutExtension(path.AsSpan()).Contains(' ')) {
                    string fname = Path.GetFileNameWithoutExtension(path);
                    for(int i = 0; i < fixes.Length; i += 2)
                        if (fname == fixes[i])
                            fname = fixes[i + 1];

                    var abase = String.Concat(fname.TakeWhile(Char.IsLetterOrDigit));
                    var token = new ImageLocator(fname);
                    if (!token.IsValid) {
                        Debug.WriteLine($"Invalid image name format: '{fname}'");
                        continue;
                    }

                    string values = line.Substring(line.IndexOf('"', 1));
                    if (dict.TryGetValue(abase, out string? prevvals)) {
                        string ismatch = values.Equals(prevvals) ? "matched" : "UNMATCHED";
                        Debug.WriteLine($"DOUBLED RECORDS: {abase}, {ismatch}");
                        Debug.WriteLine($"    {prevvals}");
                        Debug.WriteLine($"    {values}");
                    }
                    else {
                        dict.Add(abase, values);
                        if (doubles.Contains(abase))
                            continue;
                    }

                    yield return new(abase) {
                        Width = (int)Math.Round(Double.Parse(match.Result("$6"))),
                        ShiftX = (int)Math.Round(Double.Parse(match.Result("$2"))),
                        ShiftY = (int)Math.Round(Double.Parse(match.Result("$3"))),
                        Rotation = 0,
                        ScaleX = Double.Parse(match.Result("$4")),
                        ScaleY = Double.Parse(match.Result("$5"))
                    };
                }
            }
            else throw new FormatException($"Legacy image group record invalid format '{line}'");
        }
    }

    public Dictionary<string, string> GetGroupImages(string baseName) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var locate = new ImageLocator(baseName);
        var archfp = locate.CreateArchiveFilePath(_root);
        if (File.Exists(archfp)) {
            foreach (var arec in ImageArchive.EnumerateArchive(archfp))
                result.Add(arec.Name, archfp);
        }
        var grpdir = locate.CreateGroupDirectoryPath(_root);
        if (Directory.Exists(grpdir)) {
            foreach (var file in Directory.EnumerateFiles(grpdir, locate.CreateGroupSearchPattern()))
                result[Path.GetFileNameWithoutExtension(file)] = file;
        }
        return result;
    }

    public async Task<Dictionary<string, string>> GetGroupImagesAsync(string baseName) {
        await Task.Delay(25).ConfigureAwait(false);        
        return GetGroupImages(baseName);

        // #if (DEBUG)
        // await Task.Delay(25).ConfigureAwait(false);
        // #endif
        // return await DriveChecker.Default.Check(_root) ? GetGroupImages(baseName)
        //     : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public BitmapFrame? TryLoadImage(string path, string fileNameNoExt) {
        if (FilePath.IsExtensionEqual(path, ImageLocator.ImageFileExtension))
            return TryLoadImageFromFile(path);
        if (FilePath.IsExtensionEqual(path, ImageLocator.ArchiveFileExtension))
            return ImageArchive.TryLoadImage(path, fileNameNoExt);
        throw new Exception($"Unsupported image file type: '{Path.GetExtension(path)}'");
    }

    public async Task<BitmapFrame?> TryLoadImageAsync(string path, string fileNameNoExt) {
        await Task.Delay(25).ConfigureAwait(false);
        return TryLoadImage(path, fileNameNoExt);

        // #if (DEBUG)
        // await Task.Delay(25).ConfigureAwait(false);
        // #endif
        // return await DriveChecker.Default.Check(_root) ? TryLoadImage(path, fileNameNoExt) : null;
    }

    BitmapFrame? TryLoadImageFromFile(string filePath) {
        if (File.Exists(filePath)) {
            var image = BitmapFrame.Create(new Uri(filePath),
                BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
            image.Freeze();
            return image;
        }
        return null;
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

struct ImageLocator {
    public const string ImageFileExtension = ImageArchive.ImageFileExtension;
    public const string ArchiveFileExtension = ImageArchive.ArchiveFileExtension;    
    static Regex Parser = new(@"\A((\p{L}+)(\p{N}+)(\p{L}?))(?:\s\p{L}+)*\z",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.CultureInvariant);
    readonly Match _match;

    public bool IsValid => _match.Success;
    public string Name => Validate().Value;
    public ReadOnlySpan<char> Base => Validate().Groups[1].ValueSpan;
    public ReadOnlySpan<char> Actr => Validate().Groups[2].ValueSpan;
    public ReadOnlySpan<char> Body => Validate().Groups[3].ValueSpan;
    public ReadOnlySpan<char> Face => Validate().Groups[4].ValueSpan;

    public ImageLocator(string fileNameNoExt) {
        _match = Parser.Match(fileNameNoExt);
    }

    public void Throw_Invalid() {
        var match = Validate;
    }

    Match Validate() {
        return IsValid ? _match : throw new Exception("Invalid image name format");
    }

    public string CreateGroupSearchPattern() {
        return @$"{Base}*{ImageFileExtension}";
    }
    
    public string CreateGroupDirectoryPath(string? root = default) {
        return Path.Combine(root ?? "", @$"{Actr}\{Body}{Face}");
    }

    public string CreateImageFilePath(string? root = default) {
        return Path.Combine(root ?? "", @$"{Actr}\{Body}{Face}\{Name}{ImageFileExtension}");
    }

    public string CreateArchiveFilePath(string? root = default) {
        return Path.Combine(root ?? "", @$"{Actr}\{Base}{ArchiveFileExtension}");
    }

    public static bool HasAttribute(string name, string attr) {
        for (int i = 0; (i = name.IndexOf(attr, i)) >= 0; ++i) {
            if (i > 0 && Char.IsWhiteSpace(name[i - 1]) && (
                    name.Length == i + attr.Length || Char.IsWhiteSpace(name[i + attr.Length])))
                return true;
        }
        return false;
    }

    public static ReadOnlySpan<char> GetBaseName(string name) {
        int i = name.IndexOf(' ');
        return i < 0 ? name : name.AsSpan(0, i);
    }
}
