using System.Windows;
using System.Windows.Media;

namespace GoogleDesktopWidget;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Enforce single instance
        _singleInstanceMutex = new Mutex(true, "GoogleDesktopWidget_UniqueMutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            Current.Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    public static void ApplyTheme(string themeName)
    {
        var appRes = Current.Resources;

        switch (themeName)
        {
            case "EmeraldForest":
                SetColor(appRes, "ColorBackground", "#0F1612");
                SetColor(appRes, "ColorSurface", "#17211B");
                SetColor(appRes, "ColorSurfaceVariant", "#223129");
                SetColor(appRes, "ColorPrimary", "#A8DAB5");
                SetColor(appRes, "ColorOnPrimary", "#0A3818");
                SetColor(appRes, "ColorPrimaryContainer", "#1B3B26");
                SetColor(appRes, "ColorAccent", "#34A853");
                SetColor(appRes, "ColorTextPrimary", "#E2EBE4");
                SetColor(appRes, "ColorTextSecondary", "#95A699");
                SetColor(appRes, "ColorOutline", "#2E4237");
                break;

            case "SunsetCoral":
                SetColor(appRes, "ColorBackground", "#191110");
                SetColor(appRes, "ColorSurface", "#261917");
                SetColor(appRes, "ColorSurfaceVariant", "#382522");
                SetColor(appRes, "ColorPrimary", "#FFB4A2");
                SetColor(appRes, "ColorOnPrimary", "#561E14");
                SetColor(appRes, "ColorPrimaryContainer", "#4D2821");
                SetColor(appRes, "ColorAccent", "#EA4335");
                SetColor(appRes, "ColorTextPrimary", "#F2DFDC");
                SetColor(appRes, "ColorTextSecondary", "#AA9693");
                SetColor(appRes, "ColorOutline", "#4E3632");
                break;

            case "AmoledBlack":
                SetColor(appRes, "ColorBackground", "#000000");
                SetColor(appRes, "ColorSurface", "#0A0A0A");
                SetColor(appRes, "ColorSurfaceVariant", "#161616");
                SetColor(appRes, "ColorPrimary", "#90CAF9");
                SetColor(appRes, "ColorOnPrimary", "#0D47A1");
                SetColor(appRes, "ColorPrimaryContainer", "#152238");
                SetColor(appRes, "ColorAccent", "#2196F3");
                SetColor(appRes, "ColorTextPrimary", "#FFFFFF");
                SetColor(appRes, "ColorTextSecondary", "#8E8E8E");
                SetColor(appRes, "ColorOutline", "#242424");
                break;

            case "GoogleBlue":
            default:
                SetColor(appRes, "ColorBackground", "#121316");
                SetColor(appRes, "ColorSurface", "#1E1F22");
                SetColor(appRes, "ColorSurfaceVariant", "#2B2D31");
                SetColor(appRes, "ColorPrimary", "#8AB4F8");
                SetColor(appRes, "ColorOnPrimary", "#00315B");
                SetColor(appRes, "ColorPrimaryContainer", "#1A2B4C");
                SetColor(appRes, "ColorAccent", "#4285F4");
                SetColor(appRes, "ColorTextPrimary", "#E3E3E3");
                SetColor(appRes, "ColorTextSecondary", "#9AA0A6");
                SetColor(appRes, "ColorOutline", "#3C4043");
                break;
        }
    }

    private static void SetColor(ResourceDictionary dict, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        dict[key] = color;
    }
}
