using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class CrystalBallInteraction : MonoBehaviour, IInteractable
{
    [Header("Dialogue and task")]
    [SerializeField] private Flowchart flowchart;
    [SerializeField] private Block dialogueBlock;
    [SerializeField] private Text taskLabel;
    [SerializeField] private Image interactionPrompt;
    [Header("Sounds — attach clips here")]
    [SerializeField] private AudioClip lookBehindSound;
    [SerializeField] private AudioClip glassBreakingSound;
    [SerializeField, Range(0, 1)] private float soundVolume = 1;
    [Header("First-person animation")]
    [SerializeField] private Vector3 heldPosition = new Vector3(.22f, -.18f, .5f);
    [SerializeField, Min(.01f)] private float heldDiameter = .22f;
    [SerializeField, Min(.01f)] private float pickupDuration = .6f;
    [SerializeField, Min(.01f)] private float headTurnDuration = .7f;
    [SerializeField, Min(.01f)] private float windupDuration = .5f;
    [SerializeField, Min(.01f)] private float strikeDuration = .16f;
    private FirstPersonController player;
    private PauseMenu pause;
    private Transform view, originalParent;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation, normalView;
    private bool running, finished, playerEnabled, pauseEnabled;
    private bool promptEnabled, taskActive;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Rigidbody body;
    private bool gravity, kinematic;
    private PickupItem heldItem;
    private bool heldEnabled;
    private Renderer[] heldRenderers;
    private bool[] rendererStates;
    private AudioSource audioSource;
    private GameObject blackout;

    public void Interact()
    {
        if (running || finished || Time.timeScale <= 0 || SayDialog.IsConsumingPlayerInput) return;
        player = FindObjectOfType<FirstPersonController>();
        if (player == null || Camera.main == null || flowchart == null || dialogueBlock == null || dialogueBlock.IsExecuting()) return;
        running = true;
        view = Camera.main.transform;
        normalView = view.localRotation;
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        playerEnabled = player.enabled;
        player.enabled = false;
        pause = FindObjectOfType<PauseMenu>();
        if (pause != null) { pauseEnabled = pause.enabled; pause.enabled = false; }
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (interactionPrompt != null) { promptEnabled = interactionPrompt.enabled; interactionPrompt.enabled = false; }
        if (taskLabel != null) { taskActive = taskLabel.gameObject.activeSelf; taskLabel.gameObject.SetActive(false); }
        var pickup = player.GetComponent<PickupController>();
        heldItem = pickup != null ? pickup.HeldItem : null;
        if (heldItem != null)
        {
            heldEnabled = heldItem.enabled;
            heldItem.enabled = false;
            heldRenderers = heldItem.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[heldRenderers.Length];
            for (int i = 0; i < heldRenderers.Length; i++) { rendererStates[i] = heldRenderers[i].enabled; heldRenderers[i].enabled = false; }
        }
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) { colliderStates[i] = colliders[i].enabled; colliders[i].enabled = false; }
        body = GetComponent<Rigidbody>();
        if (body != null) { gravity = body.useGravity; kinematic = body.isKinematic; body.useGravity = false; body.isKinematic = true; }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0;
        var playerAudio = player.GetComponent<AudioSource>();
        if (playerAudio != null) audioSource.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        StartCoroutine(PickUp());
    }

    private IEnumerator PickUp()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        transform.SetParent(view, true);
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * (heldDiameter / Mathf.Max(.001f, diameter));
        Vector3 start = transform.localPosition;
        Quaternion rotation = transform.localRotation;
        for (float t = 0; t < pickupDuration; t += Time.deltaTime)
        {
            float blend = Mathf.SmoothStep(0, 1, t / pickupDuration);
            transform.localPosition = Vector3.Lerp(start, heldPosition, blend);
            transform.localScale = Vector3.Lerp(startScale, targetScale, blend);
            transform.localRotation = Quaternion.Slerp(rotation, Quaternion.identity, blend);
            yield return null;
        }
        transform.localPosition = heldPosition;
        transform.localScale = targetScale;
        transform.localRotation = Quaternion.identity;
        if (!flowchart.ExecuteBlock(dialogueBlock, onComplete: DialogueFinished)) Cancel();
    }

    // Fungus waits for this coroutine before moving to the next line.
    public IEnumerator TurnHead(bool behind)
    {
        if (!running || view == null) yield break;
        Quaternion start = view.localRotation;
        Quaternion target = behind ? Quaternion.Euler(0, 180, 0) * normalView : normalView;
        if (behind && lookBehindSound != null) audioSource.PlayOneShot(lookBehindSound, soundVolume);
        for (float t = 0; t < headTurnDuration; t += Time.deltaTime)
        {
            view.localRotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0, 1, t / headTurnDuration));
            yield return null;
        }
        view.localRotation = target;
    }

    private void DialogueFinished() { if (running && isActiveAndEnabled) StartCoroutine(Strike()); }
    private IEnumerator MoveBall(Vector3 target, float duration)
    {
        Vector3 start = transform.localPosition;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, t / duration));
            yield return null;
        }
        transform.localPosition = target;
    }
    private IEnumerator Strike()
    {
        yield return TurnHead(false);
        yield return MoveBall(new Vector3(.26f, .05f, .65f), windupDuration);
        yield return MoveBall(new Vector3(0, .05f, .06f), strikeDuration);
        if (glassBreakingSound != null) audioSource.PlayOneShot(glassBreakingSound, soundVolume);
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        blackout = new GameObject("Crystal ball blackout", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(GraphicRaycaster));
        var canvas = blackout.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(blackout.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;
        finished = true;
        // Hold the blackout and player lock for the next story transition.
    }

    private void Cancel()
    {
        if (!running) return;
        StopAllCoroutines();
        if (dialogueBlock != null && dialogueBlock.IsExecuting()) dialogueBlock.Stop();
        if (view != null) view.localRotation = normalView;
        transform.SetParent(originalParent, true);
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        transform.localScale = originalScale;
        if (colliders != null) for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        if (body != null) { body.useGravity = gravity; body.isKinematic = kinematic; }
        if (heldRenderers != null) for (int i = 0; i < heldRenderers.Length; i++) if (heldRenderers[i] != null) heldRenderers[i].enabled = rendererStates[i];
        if (heldItem != null) heldItem.enabled = heldEnabled;
        if (player != null) { player.enabled = playerEnabled; player.SuppressJumpUntilReleased(); }
        if (pause != null) pause.enabled = pauseEnabled;
        if (interactionPrompt != null) interactionPrompt.enabled = promptEnabled;
        if (taskLabel != null && !finished) taskLabel.gameObject.SetActive(taskActive);
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        if (blackout != null) Destroy(blackout);
        if (audioSource != null) Destroy(audioSource);
        running = false;
    }
    private void OnDisable() { Cancel(); }
}

