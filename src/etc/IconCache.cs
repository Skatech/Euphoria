using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.IO;
using System.Text;
using Skatech.IO;
using Skatech.Imaging;
using System.Reflection.Metadata;

namespace Skatech.Euphoria;

static class IconCache {
    public const string DIRECTORY_NAME = ".temp";

    public static void CreateDirectory() {
        Directory.CreateDirectory(Path.GetFullPath(DIRECTORY_NAME, App.AppdataDirectory));
    }

    public static string GetFilePath(string name) {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(name)), 10, 10);
        return Path.GetFullPath($"{DIRECTORY_NAME}\\{hash}.min", App.AppdataDirectory);
    }

    public static void SaveIcon(string name, BitmapFrame image, byte[] key, byte[]? iv = null) {
        using var mstream = new MemoryStream();
        ImageHelpers.SaveAsJpeg(mstream, image);
        mstream.Seek(0, SeekOrigin.Begin);
        using var estream = AesHelpers.CreateEncryptStream(GetFilePath(name), false, key, iv);
        mstream.CopyTo(estream);
    }

    public static BitmapFrame? LoadIcon(string name, byte[] key, byte[]? iv = null) {
        try {
            string path = GetFilePath(name);
            if (File.Exists(path)) {
                using var dstream = AesHelpers.CreateDecryptStream(path, false, key, iv);
                using var mstream = new MemoryStream();
                dstream.CopyTo(mstream);
                mstream.Seek(0, SeekOrigin.Begin);
                var image = BitmapFrame.Create(mstream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                image.Freeze();
                return image;
            }
        }
        catch {
            DropIcon(name);
        }
        return null;
    }

    public static void DropIcon(string name) {
        string path = GetFilePath(name);
            File.Delete(path);
    }
}