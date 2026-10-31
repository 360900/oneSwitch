using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace OneSwitch.Theme;

// oneDesign "One" color tokens, resolved the same way as oneDesign/tools/onetokens.py
// (tokens/languages.json). Registers one brush set per theme variant so the UI follows
// the OS light/dark preference.
public static class OneTheme
{
    // One's "blue" accent preset.
    private const double AccentHue = 255;
    private const double AccentChroma = 0.16;
    private const double NeutralChroma = 0.006;

    private sealed record Neutrals(
        double Canvas, double Surface, double SurfaceAlt, double Float, double Headerbar,
        double Text, double Text2, double Text3, double Stroke, double StrokeStrong);

    private static readonly Neutrals LightNeutrals = new(.975, 1.0, .965, .99, .975, .2, .45, .62, .1, .2);
    private static readonly Neutrals DarkNeutrals = new(.215, .25, .285, .28, .215, .96, .76, .6, .1, .2);

    public static void Register(IResourceDictionary resources)
    {
        resources.ThemeDictionaries[ThemeVariant.Light] = Build(dark: false);
        resources.ThemeDictionaries[ThemeVariant.Dark] = Build(dark: true);
    }

    private static ResourceDictionary Build(bool dark)
    {
        var n = dark ? DarkNeutrals : LightNeutrals;
        var ink = dark ? Colors.White : Colors.Black;
        var h = AccentHue;
        var c = AccentChroma;
        var aL = dark ? .74 : .56;
        var (cL, ocL, cf) = dark ? (.36, .9, .55) : (.92, .36, .45);
        var statusL = dark ? .74 : .56;

        var d = new ResourceDictionary();
        void Add(string name, Color color)
        {
            d[$"One.{name}Color"] = color;
            d[$"One.{name}"] = new SolidColorBrush(color);
        }

        Add("Canvas", Oklch(n.Canvas, NeutralChroma, h));
        Add("Surface", Oklch(n.Surface, NeutralChroma, h));
        Add("SurfaceAlt", Oklch(n.SurfaceAlt, NeutralChroma, h));
        Add("Float", Oklch(n.Float, NeutralChroma, h));
        Add("Headerbar", Oklch(n.Headerbar, NeutralChroma, h));
        Add("Text", Oklch(n.Text, NeutralChroma * 2, h));
        Add("Text2", Oklch(n.Text2, NeutralChroma * 2, h));
        Add("Text3", Oklch(n.Text3, NeutralChroma * 2, h));
        Add("Stroke", WithAlpha(ink, n.Stroke));
        Add("StrokeStrong", WithAlpha(ink, n.StrokeStrong));
        Add("Hover", WithAlpha(ink, dark ? .07 : .05));
        Add("Active", WithAlpha(ink, dark ? .12 : .09));
        Add("Fill", WithAlpha(ink, dark ? .09 : .06));
        Add("FillStrong", WithAlpha(ink, dark ? .16 : .12));
        Add("Accent", Oklch(aL, c, h));
        Add("AccentHover", Oklch(aL + (dark ? .04 : -.04), c, h));
        Add("AccentPressed", Oklch(aL + (dark ? .08 : -.08), c, h));
        Add("OnAccent", aL > .68 ? Oklch(.2, Math.Min(c, .06), h) : Colors.White);
        Add("AccentText", Oklch(dark ? Math.Max(aL, .76) : Math.Min(aL, .52), c, h));
        Add("Container", Oklch(cL, c * cf, h));
        Add("OnContainer", Oklch(ocL, c * .8, h));
        Add("AccentSoft", Oklch(aL, c, h, dark ? .22 : .14));
        Add("Success", Oklch(statusL, .15, 150));
        Add("SuccessSoft", Oklch(statusL, .15, 150, dark ? .2 : .13));
        Add("Warning", Oklch(statusL, .16, 75));
        Add("WarningSoft", Oklch(statusL, .16, 75, dark ? .2 : .13));
        Add("Danger", Oklch(statusL, .2, 25));
        Add("DangerSoft", Oklch(statusL, .2, 25, dark ? .2 : .13));
        Add("Scrim", WithAlpha(Colors.Black, dark ? .45 : .28));

        // "soft" elevation from oneDesign's engine.js, shadows are 1.8x stronger in dark mode.
        var k = dark ? 1.8 : 1.0;
        string A(double alpha) => $"#{(byte)Math.Round(Math.Min(alpha * k, .9) * 255):X2}000000";
        d["One.Shadow1"] = BoxShadows.Parse($"0 1 2 0 {A(.05)}");
        d["One.Shadow2"] = BoxShadows.Parse($"0 1 2 0 {A(.06)}, 0 10 28 -6 {A(.18)}");
        d["One.Shadow3"] = BoxShadows.Parse($"0 2 6 0 {A(.07)}, 0 28 70 -18 {A(.36)}");
        return d;
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B);

    // OKLCH -> sRGB (Ottosson), out-of-gamut channels clipped like the prototypes.
    public static Color Oklch(double l, double c, double h, double alpha = 1)
    {
        l = Math.Clamp(l, 0, 1);
        var rad = h * Math.PI / 180;
        var a = c * Math.Cos(rad);
        var b = c * Math.Sin(rad);
        var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = l - 0.0894841775 * a - 1.2914855480 * b;
        double L3 = l_ * l_ * l_, M3 = m_ * m_ * m_, S3 = s_ * s_ * s_;
        var r = 4.0767416621 * L3 - 3.3077115913 * M3 + 0.2309699292 * S3;
        var g = -1.2684380046 * L3 + 2.6097574011 * M3 - 0.3413193965 * S3;
        var bl = -0.0041960863 * L3 - 0.7034186147 * M3 + 1.7076147010 * S3;
        return Color.FromArgb(ToByte(alpha), ToByte(Gamma(r)), ToByte(Gamma(g)), ToByte(Gamma(bl)));
    }

    private static double Gamma(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;
    }

    private static byte ToByte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
}
