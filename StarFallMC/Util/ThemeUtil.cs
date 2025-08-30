using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class ThemeUtil {
    public enum ThemeType {
        Puce,
        Ebony
    }
    
    private static ThemeColor puce = new ("#D7C6C5", "#C4ABAA", "#B2908F", "#957A78", "#513938", "#264446", "#113032", "#001D1F");
    private static ThemeColor ebony = new ThemeColor("#E0E3DE", "#C8CDC5", "#B1B8AD","#6C7665 ","#555D50", "#385A80", "#204569", "#003153");
    public static SolidColorBrush PrimaryBrush;
    public static SolidColorBrush PrimaryBrush_1;
    public static SolidColorBrush PrimaryBrush_2;
    public static SolidColorBrush PrimaryBrush_3;
    public static SolidColorBrush PrimaryBrush_4;
    public static SolidColorBrush SecondaryBrush;
    public static SolidColorBrush SecondaryBrush_1;
    public static SolidColorBrush SecondaryBrush_2;

    public static Action updateColor;

    public static void init() {
        ThemeColor color = puce;
        PrimaryBrush = ToSolidColorBrush(color.Primary);
        PrimaryBrush_1 = ToSolidColorBrush(color.Primary_1);
        PrimaryBrush_2 = ToSolidColorBrush(color.Primary_2);
        PrimaryBrush_3 = ToSolidColorBrush(color.Primary_3);
        PrimaryBrush_4 = ToSolidColorBrush(color.Primary_4);
        SecondaryBrush = ToSolidColorBrush(color.Secondary);
        SecondaryBrush_1 = ToSolidColorBrush(color.Secondary_1);
        SecondaryBrush_2 = ToSolidColorBrush(color.Secondary_2);
        
        Application.Current.Resources["PrimaryBrush"] = PrimaryBrush;
        Application.Current.Resources["PrimaryBrush_1"] = PrimaryBrush_1;
        Application.Current.Resources["PrimaryBrush_2"] = PrimaryBrush_2;
        Application.Current.Resources["PrimaryBrush_3"] = PrimaryBrush_3;
        Application.Current.Resources["PrimaryBrush_4"] = PrimaryBrush_4;
        Application.Current.Resources["SecondaryBrush"] = SecondaryBrush;
        Application.Current.Resources["SecondaryBrush_1"] = SecondaryBrush_1;
        Application.Current.Resources["SecondaryBrush_2"] = SecondaryBrush_2;
    }
    
    public static SolidColorBrush ToSolidColorBrush(string colorHex) {
        return new SolidColorBrush(ToColor(colorHex));
    }

    public static Color ToColor(string colorHex) {
        return (Color)ColorConverter.ConvertFromString(colorHex);
    }

    public static void ChangeColor() {
        ThemeColor color = ebony;
        ColorChange(ref PrimaryBrush, nameof(PrimaryBrush), color.Primary);
        ColorChange(ref PrimaryBrush_1, nameof(PrimaryBrush_1), color.Primary_1);
        ColorChange(ref PrimaryBrush_2, nameof(PrimaryBrush_2), color.Primary_2);
        ColorChange(ref PrimaryBrush_3, nameof(PrimaryBrush_3), color.Primary_3);
        ColorChange(ref PrimaryBrush_4, nameof(PrimaryBrush_4), color.Primary_4);
        ColorChange(ref SecondaryBrush, nameof(SecondaryBrush), color.Secondary);
        ColorChange(ref SecondaryBrush_1, nameof(SecondaryBrush_1), color.Secondary_1);
        ColorChange(ref SecondaryBrush_2, nameof(SecondaryBrush_2), color.Secondary_2);
        updateColor();
    }

    private static void ColorChange(ref SolidColorBrush Brush,string BrushKey,string toColorHex) {
        Brush = (Application.Current.Resources[BrushKey] as SolidColorBrush).Clone();
        var animation = new ColorAnimation {
            From = Brush.Color,
            To = ToColor(toColorHex),
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        Brush.Color = ToColor(toColorHex);
        Brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        Application.Current.Resources[BrushKey] = Brush;
    }
}