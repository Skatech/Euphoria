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
using System.Windows.Media;
using Microsoft.VisualBasic;

namespace Skatech.Imaging;

class ImageHelpers {
    // public static BitmapFrame? TryLoadImage(string path) {
    //     if (File.Exists(path)) {
    //         var image = BitmapFrame.Create(new Uri(path),
    //             BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
    //         image.Freeze();
    //         return image;
    //     }
    //     return null;
    // }

    // public static async Task<BitmapFrame> LoadImageAsync(string path) {
    //     await Task.Delay(1).ConfigureAwait(false);
    //     return BitmapFrame.Create(new Uri(Path.GetFullPath(path)),
    //             BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
    // }

    public static BitmapFrame CreateMiniature(BitmapFrame image, int sizePx = 100) {
        var size = (int)Math.Round(Math.Min(image.PixelWidth * 0.8f, image.PixelHeight * 0.6f));
        var crop = new CroppedBitmap(image,
            new Int32Rect((image.PixelWidth - size) / 2, (image.PixelHeight - size) / 6, size, size));
        var mini = new TransformedBitmap(crop,
            new ScaleTransform(sizePx / (float)crop.PixelWidth, sizePx / (float)crop.PixelHeight));
        var frame =  BitmapFrame.Create(mini);
        frame.Freeze();
        return frame;
    }

    public static BitmapFrame CreateFromGeometry(int Width, int height,
             Brush? brush, Pen? pen, string geometry, double dpiX = 96d, double dpiY = 96d) {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) {
            context.DrawGeometry(brush, pen, Geometry.Parse(geometry));
        }
        var bitmap = new RenderTargetBitmap(Width, height, dpiX, dpiY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var frame = BitmapFrame.Create(bitmap);
        frame.Freeze();
        return frame;
    }

    public static void SaveAsJpeg(Stream stream, BitmapFrame image) {
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(image);
        encoder.Save(stream);
    }

    public static void SaveAsJpeg(string path, BitmapFrame image) {
        using var fstream = new FileStream(path, FileMode.Create);
        SaveAsJpeg(fstream, image);
    }
}