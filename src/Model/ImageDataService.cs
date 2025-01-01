using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows.Diagnostics;
using System.Windows.Media.Imaging;
using Skatech.IO;

namespace Skatech.Euphoria;

public interface IImageGroup {
    string Root { get; init; }
    int Width { get; set; }
    int ShiftX { get; set; }
    int ShiftY { get; set; }
    double Rotation { get; set; }
    double ScaleX { get; set; }
    double ScaleY { get; set; }
}

class ImageDataService {
    readonly static Regex _parser = new(
        @"\A\""([\w\s-+$@%\(\)\\/.:']+)\""\s(-?\d+)\s(-?\d+)\s(-?\d+)\s(-?\d+\.?\d*)\s(-?\d+\.?\d+)\s(-?\d+\.?\d+)\z",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.CultureInvariant);
    readonly string _root, _file;
    
    public ImageDataService(string root) {
        _file = Path.Combine(_root = root, "Images.dbz");
    }

    public bool Load(Func<string, IImageGroup> createItem) {
        if (File.Exists(_file)) {
            foreach (var line in DecompressLines(_file)) {
                var match = _parser.Match(line);
                if (match.Success) {
                    var item = createItem(match.Result("$1"));
                    item.Width = Int32.Parse(match.Result("$2"));
                    item.ShiftX = Int32.Parse(match.Result("$3"));
                    item.ShiftY = Int32.Parse(match.Result("$4"));
                    item.Rotation = Double.Parse(match.Result("$5"));
                    item.ScaleX = Double.Parse(match.Result("$6"));
                    item.ScaleY = Double.Parse(match.Result("$7"));
                }
                else throw new FormatException("Image group record invalid format"); 
            }
            return true;
        }
        return false;
    }

    public void Save(IEnumerable<IImageGroup> items) {
        CompressLines(_file, items.Select(i => 
            $"\"{i.Root}\" {i.Width} {i.ShiftX} {i.ShiftY} {i.Rotation} {i.ScaleX} {i.ScaleY}"));
    }

    public void LoadLegacy(Func<string, IImageGroup> createItem) {
        string file = Path.Combine(_root, "Images.dbx");
        var parser = new Regex(
            @"\A\""([\w\s-+$@%\(\)\\/.:']+)\""\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(-?\d*\.?\d*)\s(\d*\.?\d*)\z",
            RegexOptions.Compiled);
        using var gstream = new DeflateStream(
            File.OpenRead(file), CompressionMode.Decompress, false);
        using var breader = new StreamReader(gstream, System.Text.Encoding.UTF8);

        while (breader.ReadLine() is string line) {
            var match = parser.Match(line);
            if (match.Success) {
                string path = match.Result("$1");
                if (!Path.GetFileNameWithoutExtension(path.AsSpan()).Contains(' ')) {
                    string fname = Path.GetFileNameWithoutExtension(path);
                    if (fname == "MrM1Erc") {
                        fname = "MrM1";
                    }
                    else if (fname == "Eg2'") {
                        fname = "Eg2";
                    }
                    else if (fname == "Eg2b'") {
                        fname = "Eg2b";
                    }

                    var abase = String.Concat(fname.TakeWhile(Char.IsLetterOrDigit));
                    var token = new ImageLocator(fname);
                    if (!token.IsValid) {
                        Debug.WriteLine($"Invalid image name format: '{fname}'");
                        continue;
                    }
                    var item = createItem(abase);
                    item.Width = (int)Math.Round(Double.Parse(match.Result("$6")));
                    item.ShiftX = (int)Math.Round(Double.Parse(match.Result("$2")));
                    item.ShiftY = (int)Math.Round(Double.Parse(match.Result("$3")));
                    item.ScaleX = Double.Parse(match.Result("$4"));
                    item.ScaleY = Double.Parse(match.Result("$5"));
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


    public BitmapFrame? TryLoadImage(string path, string fileNameNoExt) {
        if (FilePath.IsExtensionEqual(path, ImageLocator.ImageFileExtension))
            return TryLoadImageFromFile(path);
        if (FilePath.IsExtensionEqual(path, ImageLocator.ArchiveFileExtension))
            return ImageArchive.TryLoadImage(path, fileNameNoExt);
        throw new Exception($"Unsupported image file type: '{Path.GetExtension(path)}'");
    }

    // public IEnumerable<string> LoadImageNames(string rootName) {
    //     var token = new ImageLocator(rootName);
    //     // var archvived = ImageArchive.TryEnumerateFileNames(token.CreateArchiveFilePath(_root));
    //     var archiveFile = token.CreateArchiveFilePath(_root);

    //     var archvived = File.Exists(archiveFile)
    //         ? ImageArchive.EnumerateArchive(archiveFile).Select(r => r.Name)
    //         : Enumerable.Empty<string>();

    //     var unarcData = token.CreateImagesSearchData(_root);
    //     if (Directory.Exists(unarcData.Directory)) {  //TEST THIS
    //         var nonarchived = Directory.EnumerateFiles(unarcData.Directory, unarcData.Pattern)
    //             .Select(s => Path.GetFileNameWithoutExtension(s)).ToArray(); /// REMOVE ARRAY

    //         return archvived.Concat(nonarchived)   // REPLACE WITH RAW CODE
    //             .Distinct(StringComparer.OrdinalIgnoreCase)
    //             .Order(StringComparer.OrdinalIgnoreCase);
    //     }

    //     // // REMOVE
    //     // else if (File.Exists(archiveFile)){
    //     //     Directory.CreateDirectory(unarcData.Directory);
    //     //     ImageArchive.UnpackImages(archiveFile, unarcData.Directory);
    //     //     Debug.WriteLine($"Image archive unpacked: {rootName}");
    //     // }

    //     return archvived;
    // }

    // public BitmapFrame? TryLoadImage(string fileNameNoExt) {
    //     var token = new ImageLocator(fileNameNoExt);
    //     return TryLoadImageFile(token.CreateImageFilePath(_root))
    //         ?? ImageArchive.TryLoadImage(token.CreateArchiveFilePath(_root), fileNameNoExt);
    // }

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
        return @$"{Base} *{ImageFileExtension}";
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
}
