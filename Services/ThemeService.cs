using Wpf.Ui.Appearance;

namespace AzerothCoreManager.Services;

public class ThemeService
{
    public void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case "Light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case "Dark":
            default:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
        }
    }

    public void SetBackground(string imageName)
    {
        // Handled by MainWindow code-behind
    }
}
