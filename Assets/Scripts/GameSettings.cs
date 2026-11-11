using System.Linq;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public static class GameSettings
{
    private const string Prefix = "settings.";
    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", 1f));
    public static float MouseSensitivity => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sensitivity", 2f), 0.1f, 10f);
    public static bool Fullscreen => PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
    public static Vector2Int DisplaySize => new Vector2Int(
        PlayerPrefs.GetInt(Prefix + "width", Screen.width),
        PlayerPrefs.GetInt(Prefix + "height", Screen.height));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettings()
    {
        AudioListener.volume = MasterVolume;
        if (PlayerPrefs.HasKey(Prefix + "width"))
            ApplyDisplay(DisplaySize, Fullscreen);
    }

    public static void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "volume", AudioListener.volume);
        PlayerPrefs.Save();
    }

    public static void SetMouseSensitivity(float value)
    {
        float sensitivity = Mathf.Clamp(value, 0.1f, 10f);
        PlayerPrefs.SetFloat(Prefix + "sensitivity", sensitivity);
        PlayerPrefs.Save();
        foreach (var controller in Object.FindObjectsOfType<FirstPersonController>())
            controller.SetSensitivity(sensitivity);
    }

    public static Vector2Int[] GetDisplaySizes()
    {
        // Keep the current size available even when Unity reports no display modes in the editor.
        var desktop = Screen.currentResolution;
        var windowSizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };
        return Screen.resolutions.Select(r => new Vector2Int(r.width, r.height))
            .Concat(windowSizes.Where(size => size.x <= desktop.width && size.y <= desktop.height))
            .Append(DisplaySize)
            .Distinct()
            .OrderBy(size => size.x).ThenBy(size => size.y).ToArray();
    }

    public static void SetDisplay(Vector2Int size, bool fullscreen)
    {
        if (size.x <= 0 || size.y <= 0) return;
        PlayerPrefs.SetInt(Prefix + "width", size.x);
        PlayerPrefs.SetInt(Prefix + "height", size.y);
        PlayerPrefs.SetInt(Prefix + "fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        ApplyDisplay(size, fullscreen);
    }

    private static void ApplyDisplay(Vector2Int size, bool fullscreen)
    {
        // Exclusive fullscreen uses the selected display mode on Windows.
        Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
    }
}
