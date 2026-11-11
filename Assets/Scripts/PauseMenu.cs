using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public sealed class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject home;
    [SerializeField] private SettingsPanel settings;
    [SerializeField] private GameObject confirm;
    [SerializeField] private Button resume;
    [SerializeField] private Button openSettings;
    [SerializeField] private Button quit;
    [SerializeField] private Button settingsBack;
    [SerializeField] private Button cancelQuit;
    [SerializeField] private Button confirmQuit;
    [SerializeField] private Text lastSave;
    [SerializeField] private GameObject saveNotice;
    [SerializeField] private RectTransform snowflake;
    [SerializeField] private bool testAutosave = true;

    private bool paused;
    private bool resumeRequested;
    private float previousTimeScale;
    private bool previousAudioPause;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisibility;
    private Coroutine noticeAnimation;
    private CanvasGroup noticeFade;

    private void Awake()
    {
        resume.onClick.AddListener(() => resumeRequested = true);
        openSettings.onClick.AddListener(() => Show(settings.gameObject, settings.FirstControl));
        quit.onClick.AddListener(ShowQuit);
        settingsBack.onClick.AddListener(() => Show(home, openSettings));
        cancelQuit.onClick.AddListener(() => Show(home, quit));
        confirmQuit.onClick.AddListener(MainMenu.Quit);
        overlay.SetActive(false);
        noticeFade = saveNotice.GetComponent<CanvasGroup>();
        if (noticeFade == null) noticeFade = saveNotice.AddComponent<CanvasGroup>();
        noticeFade.blocksRaycasts = false;
        saveNotice.SetActive(false);
    }

    private void OnEnable() => GameSave.CheckpointSaved += ShowSaveNotice;

    private void OnDisable()
    {
        GameSave.CheckpointSaved -= ShowSaveNotice;
        if (paused) Resume();
    }

    private IEnumerator Start()
    {
        if (!testAutosave) yield break;
        yield return new WaitForSeconds(5f);
        GameSave.SaveCheckpoint();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (!paused) Pause();
        else if (settings.gameObject.activeSelf) Show(home, openSettings);
        else if (confirm.activeSelf) Show(home, quit);
        else resumeRequested = true;
    }

    private void LateUpdate()
    {
        // Keep the resume click from also interacting with a gameplay object.
        if (resumeRequested) Resume();
    }

    private void Pause()
    {
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        paused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        overlay.SetActive(true);
        Show(home, resume);
    }

    private void Resume()
    {
        resumeRequested = false;
        paused = false;
        overlay.SetActive(false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisibility;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Show(GameObject panel, Selectable focus)
    {
        home.SetActive(panel == home);
        settings.gameObject.SetActive(panel == settings.gameObject);
        confirm.SetActive(panel == confirm);
        EventSystem.current.SetSelectedGameObject(focus.gameObject);
    }

    private void ShowQuit()
    {
        DateTime? saved = GameSave.LastSavedUtc;
        lastSave.text = saved.HasValue
            ? "Last save: " + saved.Value.ToLocalTime().ToString("g")
            : GameSave.HasSave ? "Last save: time unavailable" : "No save yet.";
        Show(confirm, cancelQuit);
    }

    private void ShowSaveNotice()
    {
        if (noticeAnimation != null) StopCoroutine(noticeAnimation);
        noticeAnimation = StartCoroutine(AnimateSaveNotice());
    }

    private IEnumerator AnimateSaveNotice()
    {
        noticeFade.alpha = 0f;
        saveNotice.SetActive(true);
        const float duration = 2.4f;
        const float secondsPerTurn = 0.9f;
        const float fadeIn = 0.25f;
        const float fadeOut = 0.4f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float opacity = Mathf.Clamp01(Mathf.Min(elapsed / fadeIn, (duration - elapsed) / fadeOut));
            noticeFade.alpha = Mathf.SmoothStep(0f, 1f, opacity);
            snowflake.localRotation = Quaternion.Euler(0f, 0f, -360f * elapsed / secondsPerTurn);
            yield return null;
        }
        noticeFade.alpha = 0f;
        saveNotice.SetActive(false);
        noticeAnimation = null;
    }
}
