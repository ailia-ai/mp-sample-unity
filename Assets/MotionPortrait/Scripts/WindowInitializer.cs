using UnityEngine;

// Opens the standalone application as a normal floating window (with title bar)
// instead of borderless fullscreen, like the state after "Exit Full Screen" on
// macOS, so the window can be moved, resized and closed easily.
// The window size from the previous run is remembered and restored on the next launch.
public static class WindowInitializer
{
    private const string PrefKeyWidth = "WindowInitializer.Width";
    private const string PrefKeyHeight = "WindowInitializer.Height";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeWindow()
    {
#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
        Resolution display = Screen.currentResolution;

        // Restore the previous run's size, or fall back to 80% of the display.
        int width = PlayerPrefs.GetInt(PrefKeyWidth, Mathf.RoundToInt(display.width * 0.8f));
        int height = PlayerPrefs.GetInt(PrefKeyHeight, Mathf.RoundToInt(display.height * 0.8f));

        // Clamp to the current display so a stale value can't push the window off-screen.
        width = Mathf.Clamp(width, 640, display.width);
        height = Mathf.Clamp(height, 480, display.height);

        Screen.SetResolution(width, height, FullScreenMode.Windowed);

        // Save the window size when the application quits.
        Application.quitting += SaveWindowSize;
#endif
    }

#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
    private static void SaveWindowSize()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            PlayerPrefs.SetInt(PrefKeyWidth, Screen.width);
            PlayerPrefs.SetInt(PrefKeyHeight, Screen.height);
            PlayerPrefs.Save();
        }
    }
#endif
}
