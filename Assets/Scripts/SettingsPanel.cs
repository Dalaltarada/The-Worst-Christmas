using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider volume;
    [SerializeField] private Slider sensitivity;
    [SerializeField] private Text volumeValue;
    [SerializeField] private Text sensitivityValue;
    [SerializeField] private Text resolutionValue;
    [SerializeField] private Button previousResolution;
    [SerializeField] private Button nextResolution;
    [SerializeField] private Button fullscreen;
    [SerializeField] private Text fullscreenValue;

    public Selectable FirstControl => volume;

    private Vector2Int[] sizes;
    private int selectedSize;
    private bool isFullscreen;

    private void Awake()
    {
        volume.onValueChanged.AddListener(value => { GameSettings.SetMasterVolume(value); UpdateValueLabels(); });
        sensitivity.onValueChanged.AddListener(value => { GameSettings.SetMouseSensitivity(value); UpdateValueLabels(); });
        previousResolution.onClick.AddListener(() => ChangeSize(-1));
        nextResolution.onClick.AddListener(() => ChangeSize(1));
        fullscreen.onClick.AddListener(() => { isFullscreen = !isFullscreen; ApplyDisplay(); });
    }

    private void OnEnable()
    {
        volume.SetValueWithoutNotify(GameSettings.MasterVolume);
        sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        UpdateValueLabels();
        sizes = GameSettings.GetDisplaySizes();
        selectedSize = System.Array.IndexOf(sizes, GameSettings.DisplaySize);
        if (selectedSize < 0) selectedSize = sizes.Length - 1;
        isFullscreen = GameSettings.Fullscreen;
        UpdateDisplayLabels();
    }

    private void ChangeSize(int direction)
    {
        selectedSize = (selectedSize + direction + sizes.Length) % sizes.Length;
        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        GameSettings.SetDisplay(sizes[selectedSize], isFullscreen);
        UpdateDisplayLabels();
    }

    private void UpdateValueLabels()
    {
        volumeValue.text = Mathf.RoundToInt(volume.value * 100f) + "%";
        sensitivityValue.text = sensitivity.value.ToString("0.0");
    }

    private void UpdateDisplayLabels()
    {
        var size = sizes[selectedSize];
        resolutionValue.text = size.x + " × " + size.y;
        fullscreenValue.text = isFullscreen ? "On" : "Off";
    }
}
