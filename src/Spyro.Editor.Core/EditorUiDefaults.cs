namespace Spyro.Editor.Core;

public static class EditorUiDefaults
{
    public const bool UseGameViewMapOrientation = true;

    public static double MapXAxisScreenSign(bool useGameViewOrientation)
    {
        return 1.0;
    }

    public static double MapYAxisScreenSign(bool useGameViewOrientation)
    {
        return useGameViewOrientation ? -1.0 : 1.0;
    }
}
