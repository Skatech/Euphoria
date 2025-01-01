using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using Skatech.IO;

namespace Skatech.Euphoria;

#nullable enable

static class ImageArchive {
    public const string ArchiveFileExtension = ".ima", ImageFileExtension = ".jpg";
    private const string FormatMarker = "ima2";

    public static void UnpackImages(string archiveFile, string outputDir, string? fileSelector = default) {
        foreach (var rec in EnumerateArchive(archiveFile))
            if (FilePath.IsMatch(rec.Name, fileSelector)) {
                var ofp = Path.Combine(outputDir, rec.Name + ImageFileExtension);
                using var ofs = File.OpenWrite(ofp);
                rec.Copy(ofs);
            }
    }

    // public static BitmapFrame? TryLoadImage(string archiveFile, string fileNameNoExt) {
    //     return File.Exists(archiveFile)
    //         ? EnumerateArchive(archiveFile).FirstOrDefault(
    //             r => r.Name.Equals(fileNameNoExt, StringComparison.OrdinalIgnoreCase)).Load?.Invoke()
    //         : null;
    // }

    public static BitmapFrame? TryLoadImage(string archiveFile, string fileNameNoExt) {
        if (File.Exists(archiveFile))
            foreach (var rec in EnumerateArchive(archiveFile))
                if (FilePath.Equals(rec.Name, fileNameNoExt))
                    return rec.Load();
        return null;
    }

    // static Random random = new Random(Environment.TickCount);
    // public static BitmapFrame? TryLoadImage(string archiveFile, string fileNameNoExt) {
    //     if (File.Exists(archiveFile)) {
    //         var images = EnumerateArchive(archiveFile).Select(r => r.Load()).ToArray();
    //         return images[random.Next(images.Length)];
    //     }
    //     return null;
    // }

    public static IEnumerable<string> TryEnumerateImageNames(string archiveFile) {
        return File.Exists(archiveFile)
            ? EnumerateArchive(archiveFile).Select(r => r.Name)
            : Enumerable.Empty<string>();
    }

    public static IEnumerable<(string Name, int Size, Func<BitmapFrame> Load, Action<Stream> Copy)>
            EnumerateArchive(string filePath) {
        using var ifs = File.OpenRead(filePath);
        using (var ibr = new BinaryReader(ifs, Encoding.UTF8, true)) {
            if (ibr.ReadChars(4).AsSpan().SequenceEqual(FormatMarker)) {
                ifs.Seek(ibr.ReadInt32(), SeekOrigin.Begin); // seek header
            }
            else throw new Exception("Invalid file type");
        }

        var files = new Dictionary<string, (int Seek, int Size)>(StringComparer.OrdinalIgnoreCase);
        using (var ids = new DeflateStream(ifs, CompressionMode.Decompress, true)) // read file table
        using (var ibr = new BinaryReader(ids, Encoding.UTF8, true)) {
            for (var fnm = ibr.ReadString(); fnm != String.Empty; fnm = ibr.ReadString()) {
                files[fnm] = (ibr.ReadInt32(), ibr.ReadInt32());
            }
        }

        using var mms = new MemoryStream();
        BitmapFrame LoadData() {
            if (ifs.CanRead) {
                using var ids = new DeflateStream(ifs, CompressionMode.Decompress, true);
                mms.SetLength(0);
                ids.CopyTo(mms);
                mms.Seek(0, SeekOrigin.Begin);
                var image = BitmapFrame.Create(mms,
                    BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                image.Freeze();
                return image;
            }
            throw new Exception("Read attempt from disposed image archive stream");
        }

        void CopyData(Stream dst) {
            if (ifs.CanRead) {
                using var ids = new DeflateStream(ifs, CompressionMode.Decompress, true);
                mms.SetLength(0);
                ids.CopyTo(dst);
            }
            else throw new Exception("Read attempt from disposed image archive stream");
        }

        foreach(var rec in files) {
            ifs.Seek(rec.Value.Seek, SeekOrigin.Begin);
            yield return (rec.Key, rec.Value.Size, LoadData, CopyData);
        }
    }
 
    // public static BitmapFrame? TryLoadImage(string imagesRoot, string filePath) {
    //     var fullPath = Path.Combine(imagesRoot, filePath);
    //     var fileName = Path.GetFileNameWithoutExtension(fullPath);
    //     var actrBase = String.Concat(fileName.TakeWhile(Char.IsLetterOrDigit));
    //     var actrName = String.Concat(fileName.TakeWhile(Char.IsLetter));
    //     var dataFile = Path.Join(imagesRoot, actrName, actrBase + FileExtension);
    //     if (File.Exists(dataFile)) {
    //         foreach (var bmp in EnumerateBitmaps(
    //                 dataFile, (n, s) => fileName.Equals(n, StringComparison.OrdinalIgnoreCase)))
    //             return bmp;
    //     }

    //     if (File.Exists(fullPath)) {
    //         var bmp = BitmapFrame.Create(new Uri(fullPath),
    //             BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
    //         bmp.Freeze();
    //         return bmp;
    //     }

    //     return null;
    // }

    // public static BitmapFrame? TryLoadImage_(string archiveFile, string fileNameNoExt) {
    //     if (File.Exists(archiveFile))
    //         foreach (var bmp in EnumerateBitmaps(archiveFile,
    //                 (n, s) => fileNameNoExt.Equals(n, StringComparison.OrdinalIgnoreCase)))
    //             return bmp;
    //     return null;
    // }

    // public static List<string> GetImageNames(string archiveFile) {
    //     var names = new List<string>();
    //     if (File.Exists(archiveFile))
    //         foreach (var bmp in EnumerateBitmaps(archiveFile,
    //             (n, s) => { names.Add(n); return false; })) {}
    //     return names;
    // }
    
    // public static IEnumerable<BitmapFrame>
    //             EnumerateBitmaps(string archivePath, Func<string, int, bool> filter) {
    //     using var mms = new MemoryStream();
    //     foreach (var rec in EnumerateRecords(archivePath, filter)) {
    //         mms.SetLength(0);
    //         rec.Stream.CopyTo(mms);
    //         mms.Seek(0, SeekOrigin.Begin);

    //         var bmp = BitmapFrame.Create(mms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
    //         bmp.Freeze();
    //         yield return bmp;
    //     }
    // }

    static IEnumerable<(DeflateStream Stream, string Name, int Size)>
                EnumerateRecords(string filePath, Func<string, int, bool>? filter) {
        using var ifs = File.OpenRead(filePath);
        using (var ibr = new BinaryReader(ifs, Encoding.UTF8, true)) {
            if (ibr.ReadChars(4).AsSpan().SequenceEqual(FormatMarker)) {
                ifs.Seek(ibr.ReadInt32(), SeekOrigin.Begin); // seek header
            }
            else throw new Exception("Invalid file type");
        }

        var files = new Dictionary<string, (int Position, int Length)>(StringComparer.OrdinalIgnoreCase);
        using (var ids = new DeflateStream(ifs, CompressionMode.Decompress, true)) // read file table
        using (var ibr = new BinaryReader(ids, Encoding.UTF8, true)) {
            for (var fnm = ibr.ReadString(); fnm != String.Empty; fnm = ibr.ReadString()) {
                files[fnm] = (ibr.ReadInt32(), ibr.ReadInt32());
            }
        }

        foreach(var rec in files.Where(fd => filter is null || filter(fd.Key, fd.Value.Length))) {
            ifs.Seek(rec.Value.Position, SeekOrigin.Begin);
            using (var ids = new DeflateStream(ifs, CompressionMode.Decompress, true)) { // read file body
                yield return (ids, rec.Key, rec.Value.Length);
            }
        }
    }

    public static void UnwrapImageArchive(string filePath, string? fileSelector = null) {
        if (!File.Exists(filePath))
            throw new Exception("Source file not exists");
        string outputDir = Path.Join(
            Path.GetDirectoryName(filePath.AsSpan()),
            Path.GetFileNameWithoutExtension(filePath.AsSpan()));
        if (Path.Exists(outputDir))
            throw new Exception("Output directory already exists");
        foreach (var rec in EnumerateRecords(filePath, (n, s) => FilePath.IsMatch(n, fileSelector))) {
            Directory.CreateDirectory(outputDir);
            var ofp = Path.Combine(outputDir, rec.Name + ".jpg");
            using var ofs = File.OpenWrite(ofp);
            rec.Stream.CopyTo(ofs);
        }
    }

    public static string CreateImageArchiveInfo(string selector) {
        string path = Path.GetFullPath(selector);
        var files = Directory.EnumerateFiles(
                Path.GetDirectoryName(path) ?? throw new Exception("Invalid selector directory"),
                Path.GetFileName(path) ?? throw new Exception("Invalid selector pattern"))
            .OrderByDescending(n => n.ToLower()).ToArray();

        var arcfn = Path.ChangeExtension(files.FirstOrDefault()
            ?? throw new Exception("No files selected"), ArchiveFileExtension);
        if (arcfn.Length > 50)
            arcfn = "..." + arcfn.Substring(arcfn.Length - 50);

        var fndta = String.Join("\r\n", files.Select(n => "    " + Path.GetFileName(n)));

        return $"Files to pack:\r\n{fndta}\r\n\r\nFiles total: {files.Length}\r\n\r\nOutput file:\r\n    {arcfn}\r\n\r\nPress OK to start building package...";
    }

    public static void CreateImageArchive(string selector) {
        string path = Path.GetFullPath(selector);
        var files = Directory.EnumerateFiles(
                Path.GetDirectoryName(path) ?? throw new Exception("Invalid selector directory"),
                Path.GetFileName(path) ?? throw new Exception("Invalid selector pattern"))
            .OrderByDescending(n => n.ToLower())
            .ToDictionary<string, string, (int Position, int Length)>
                (s => s, s => (0, 0), StringComparer.OrdinalIgnoreCase);

        var arcfn = Path.ChangeExtension(files.Keys.FirstOrDefault()
            ?? throw new Exception("No files selected"), "ima");

        using var ofs = File.Create(arcfn);
        ofs.Seek(8, SeekOrigin.Begin);

        foreach (var fd in files) { // write files bodies
            using var ods = new DeflateStream(ofs, CompressionLevel.Optimal, true);
            using var ifs = File.OpenRead(fd.Key);
            files[fd.Key] = (Convert.ToInt32(ofs.Position), Convert.ToInt32(ifs.Length));
            ifs.CopyTo(ods);
        }

        var ftpos = Convert.ToInt32(ofs.Position); // file table position

        using (var ods = new DeflateStream(ofs, CompressionLevel.Optimal, true)) // write file table
        using (var obw = new BinaryWriter(ods, Encoding.UTF8, true)) {
            foreach (var fd in files) {
                obw.Write(Path.GetFileNameWithoutExtension(fd.Key));
                obw.Write(fd.Value.Position);
                obw.Write(fd.Value.Length);
            }
            obw.Write(String.Empty); // table end marker
        }

        ofs.Seek(0, SeekOrigin.Begin); // write header
        using (var obw = new BinaryWriter(ofs, Encoding.UTF8, false)) {
            obw.Write("ima2".AsSpan());
            obw.Write(ftpos);
        }
    }
}

#nullable restore