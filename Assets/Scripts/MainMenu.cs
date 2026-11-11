using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject home;
    [SerializeField] private GameObject settings;
    [SerializeField] private GameObject credits;
    [SerializeField] private Button start;
    [SerializeField] private Button resume;
    [SerializeField] private Button openSettings;
    [SerializeField] private Button openCredits;
    [SerializeField] private Button quit;
    [SerializeField] private Button settingsBack;
    [SerializeField] private Button creditsBack;
    [SerializeField] private SettingsPanel settingsControls;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        resume.interactable = GameSave.HasSave;
        start.onClick.AddListener(GameSave.StartNewGame);
        resume.onClick.AddListener(() => { if (!GameSave.ContinueGame()) resume.interactable = false; });
        openSettings.onClick.AddListener(() => Show(settings, settingsControls.FirstControl));
        openCredits.onClick.AddListener(() => Show(credits, creditsBack));
        settingsBack.onClick.AddListener(() => Show(home, openSettings));
        creditsBack.onClick.AddListener(() => Show(home, openCredits));
        quit.onClick.AddListener(Quit);
    }

    private void Start() => Show(home, start);

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (settings.activeSelf) Show(home, openSettings);
        else if (credits.activeSelf) Show(home, openCredits);
    }

    private void Show(GameObject panel, Selectable focus)
    {
        home.SetActive(panel == home);
        settings.SetActive(panel == settings);
        credits.SetActive(panel == credits);
        EventSystem.current.SetSelectedGameObject(focus.gameObject);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
