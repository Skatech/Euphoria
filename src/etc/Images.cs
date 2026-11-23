using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Skatech.Imaging;

namespace Skatech.Euphoria;

static class Images {
    public static readonly BitmapFrame LoadIcon = ImageHelpers.CreateFromGeometry(
            32, 32, null, new Pen(SystemColors.GrayTextBrush, 1),
            "M0,0 H32V32H0V0 M9,8H23 M9,23H23 M11,8L21,23 M11,23L21,8");
    public static readonly BitmapFrame DeadIcon = ImageHelpers.CreateFromGeometry(
        32, 32, null, new Pen(Brushes.Red, 1), "M0,0 H32V32H0V0 M5,5L26,26 M5,26L26,4");
}