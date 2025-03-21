using System;
using System.Windows.Media;

namespace Skatech.Media;

static class ColorHelper {
    public static Color FromHTML(string color) {
        var c = System.Drawing.ColorTranslator.FromHtml(color);
        return Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    public static Color FromHSL(int h, byte s, byte l) {
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