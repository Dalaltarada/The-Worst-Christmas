using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class JackInTheBoxInteraction : MonoBehaviour, IInteractable
{
    private enum Phase { Waiting, PickingUp, Winding, Anticipation, Opening, Opened, Returning, Used }
    [Header("Model animation")]
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private Transform crank;
    [SerializeField] private Transform crankGrip;
    [SerializeField] private Transform lid;
    [SerializeField] private Transform spring;
    [SerializeField] private Vector3 crankAxis = Vector3.right;
    [SerializeField, Min(0)] private float closedPoseTime = 1f / 30f;
    [SerializeField] private float closedLidAngle = 102f;
    [SerializeField, Min(0)] private float openingStartTime = 1f / 30f;
    [SerializeField, Min(0)] private float openingEndTime = 2.1f;
    [Header("Music and winding")]
    [SerializeField] private AudioClip music;
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private bool useMusicClipLength = true;
    [SerializeField, Min(.1f)] private float windingSeconds = 15;
    [SerializeField, Min(1f)] private float minimumDegreesPerSecond = 45;
    [SerializeField, Min(4f)] private float crankGrabPadding = 22f;
    [SerializeField, Range(0, 1)] private float musicVolume = .7f;
    [SerializeField, Range(0, 1)] private float jumpscareVolume = 1;
    [Header("Held view — relative to player camera")]
    [SerializeField] private Vector3 crankViewPosition = new Vector3(0, -.16f, .55f);
    [SerializeField] private Vector3 crankViewRotation = new Vector3(0, 90, 0);
    [SerializeField] private Vector3 frontViewPosition = new Vector3(0, -.25f, .72f);
    [SerializeField] private Vector3 frontViewRotation = new Vector3(0, 180, 0);
    [SerializeField, Min(.1f)] private float heldScaleMultiplier = 1;
    [SerializeField, Min(.1f)] private float pickupDuration = .55f;
    [SerializeField, Min(0)] private float anticipationDelay = 3;
    [SerializeField, Min(.1f)] private float turnDuration = .75f;
    [SerializeField, Min(.1f)] private float putDownDuration = .5f;
    [SerializeField, Range(0f, 2f)] private float inspectionLightIntensity = .8f;
    [Header("Scene UI")]
    [SerializeField] private GameObject windingUI;
    [SerializeField] private GameObject windingControls;
    [SerializeField] private Text statusText;
    [SerializeField] private Slider windingProgress;
    [SerializeField] private Button putDownButton;
    [SerializeField] private Image interactionPrompt;
    [Header("After opening and putting down")]
    [SerializeField] private Flowchart hintFlowchart;
    [SerializeField] private Block hintBlock;
    [SerializeField] private Text taskLabel;

    private Phase phase;
    private FirstPersonController player;
    private PauseMenu pauseMenu;
    private bool playerWasEnabled, pauseWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private Transform originalParent;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation;
    private Collider[] colliders;
    private bool[] colliderEnabled;
    private Rigidbody body;
    private bool bodyKinematic, bodyGravity;
    private Renderer[] heldRenderers;
    private bool[] heldRendererEnabled;
    private Light heldLight;
    private bool heldLightEnabled, heldLightActive;
    private AudioSource musicSource, jumpscareSource;
    private Light inspectionLight;
    private float woundSeconds, requiredSeconds, crankAngle, animationTime;
    private bool dragging, musicStarted, musicPaused, completed;
    private float lastPointerAngle;
    private float dragRotationSign = -1;
    private Camera viewCamera;
    private int windingFrame = -1;

    public bool IsWinding => phase == Phase.Winding;
    public bool HasOpened => completed;
    public float WindingProgress => requiredSeconds > 0 ? Mathf.Clamp01(woundSeconds / requiredSeconds) : 0;

    private void Awake()
    {
        foreach (var node in GetComponentsInChildren<Transform>(true))
        {
            if (lid == null && node.name == "lid") lid = node;
            if (spring == null && node.name == "spring_01") spring = node;
        }
        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        colliders = GetComponentsInChildren<Collider>(true);
        body = GetComponent<Rigidbody>();
        if (windingUI != null) windingUI.SetActive(false);
        if (putDownButton != null) putDownButton.onClick.AddListener(PutDown);
        animationTime = closedPoseTime;
        SamplePose();
    }

    public void Interact()
    {
        if (phase != Phase.Waiting || Time.timeScale <= 0) return;
        player = FindObjectOfType<FirstPersonController>();
        var camera = Camera.main;
        if (player == null || camera == null) return;
        viewCamera = camera;
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        Vector3 worldScale = transform.lossyScale;
        playerWasEnabled = player.enabled;
        player.enabled = false;
        pauseMenu = FindObjectOfType<PauseMenu>();
        if (pauseMenu != null) { pauseWasEnabled = pauseMenu.enabled; pauseMenu.enabled = false; }
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (interactionPrompt != null) interactionPrompt.enabled = false;
        HideHeldItem();
        var lamp = new GameObject("Music box inspection light");
        lamp.transform.SetParent(camera.transform, false);
        lamp.transform.localPosition = new Vector3(0, .08f, .12f);
        inspectionLight = lamp.AddComponent<Light>();
        inspectionLight.type = LightType.Point;
        inspectionLight.range = 1.3f;
        inspectionLight.intensity = inspectionLightIntensity;
        inspectionLight.color = new Color(1, .86f, .68f);
        inspectionLight.shadows = LightShadows.None;
        colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) { colliderEnabled[i] = colliders[i].enabled; colliders[i].enabled = false; }
        if (body != null) { bodyKinematic = body.isKinematic; bodyGravity = body.useGravity; body.isKinematic = true; body.useGravity = false; }
        PrepareAudio();
        requiredSeconds = useMusicClipLength && music != null ? Mathf.Max(.1f, music.length) : Mathf.Max(.1f, windingSeconds);
        woundSeconds = crankAngle = 0;
        dragging = musicStarted = musicPaused = false;
        animationTime = closedPoseTime;
        phase = Phase.PickingUp;
        transform.SetParent(camera.transform, true);
        StartCoroutine(PickUp(worldScale * heldScaleMultiplier));
    }

    private IEnumerator PickUp(Vector3 targetScale)
    {
        Vector3 start = transform.localPosition, scale = transform.localScale;
        Quaternion rotation = transform.localRotation;
        for (float elapsed = 0; elapsed < pickupDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / pickupDuration);
            transform.localPosition = Vector3.Lerp(start, crankViewPosition, t);
            transform.localRotation = Quaternion.Slerp(rotation, Quaternion.Euler(crankViewRotation), t);
            transform.localScale = Vector3.Lerp(scale, targetScale, t);
            yield return null;
        }
        transform.localPosition = crankViewPosition;
        transform.localRotation = Quaternion.Euler(crankViewRotation);
        transform.localScale = targetScale;
        phase = Phase.Winding;
        if (windingUI != null) windingUI.SetActive(true);
        if (windingControls != null) windingControls.SetActive(true);
        if (windingProgress != null) { windingProgress.gameObject.SetActive(true); windingProgress.SetValueWithoutNotify(0); }
        if (putDownButton != null) putDownButton.interactable = true;
        UpdateStatus();
    }

    private void Update()
    {
        if (Time.timeScale <= 0) { PauseMusic(); return; }
        if (phase == Phase.Winding)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { PutDown(); return; }
            ReadCrankInput();
            if (windingFrame != Time.frameCount) PauseMusic();
        }
        else if (phase == Phase.Opened && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))) PutDown();
    }

    private void ReadCrankInput()
    {
        if (Input.GetMouseButtonDown(0)) BeginCrankDrag(Input.mousePosition);
        if (!Input.GetMouseButton(0)) { EndCrankDrag(); return; }
        DragCrank(Input.mousePosition, Time.deltaTime);
    }

    public bool BeginCrankDrag(Vector2 pointer)
    {
        dragging = false;
        if (phase != Phase.Winding || Time.timeScale <= 0 || crank == null || crankGrip == null || viewCamera == null) return false;
        Vector3 centre3 = viewCamera.WorldToScreenPoint(crank.position);
        Vector3 grip3 = viewCamera.WorldToScreenPoint(crankGrip.position);
        if (centre3.z <= 0 || grip3.z <= 0) return false;
        Vector2 centre = centre3, grip = grip3;
        Vector2 arm = grip - centre;
        if (arm.sqrMagnitude < 16) return false;
        float along = Mathf.Clamp01(Vector2.Dot(pointer - centre, arm) / arm.sqrMagnitude);
        float padding = crankGrabPadding * Screen.height / 1080f;
        if (along < .15f || Vector2.Distance(pointer, centre + arm * along) > Mathf.Max(8, padding)) return false;
        Vector3 axis = crank.TransformDirection(crankAxis.sqrMagnitude > .001f ? crankAxis.normalized : Vector3.right);
        Vector3 rotated = viewCamera.WorldToScreenPoint(crank.position + Quaternion.AngleAxis(1, axis) * (crankGrip.position - crank.position));
        float sign = Mathf.DeltaAngle(PointerAngle(grip, centre), PointerAngle(rotated, centre));
        dragRotationSign = sign >= 0 ? 1 : -1;
        lastPointerAngle = PointerAngle(pointer, centre);
        dragging = true;
        return true;
    }

    public void DragCrank(Vector2 pointer, float seconds)
    {
        if (!dragging || phase != Phase.Winding || viewCamera == null || Time.timeScale <= 0) return;
        Vector3 centre3 = viewCamera.WorldToScreenPoint(crank.position);
        if (centre3.z <= 0) { EndCrankDrag(); return; }
        Vector2 centre = centre3;
        if (Vector2.Distance(pointer, centre) < Mathf.Max(8, Screen.height * .01f)) { lastPointerAngle = float.NaN; return; }
        float angle = PointerAngle(pointer, centre);
        if (float.IsNaN(lastPointerAngle)) { lastPointerAngle = angle; return; }
        float delta = Mathf.DeltaAngle(lastPointerAngle, angle);
        lastPointerAngle = angle;
        TurnWheel(-delta * dragRotationSign, seconds);
    }

    public void EndCrankDrag() { dragging = false; PauseMusic(); }
    private static float PointerAngle(Vector2 pointer, Vector2 centre)
    {
        Vector2 offset = pointer - centre;
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    public void TurnWheel(float degrees, float seconds)
    {
        if (phase != Phase.Winding || Time.timeScale <= 0 || Mathf.Abs(degrees) < .05f) return;
        float frameSeconds = Mathf.Min(Mathf.Max(0, seconds), Time.deltaTime);
        if (frameSeconds <= 0) return;
        float effort = Mathf.Clamp01(Mathf.Abs(degrees) / (minimumDegreesPerSecond * frameSeconds));
        woundSeconds = Mathf.Min(requiredSeconds, woundSeconds + frameSeconds * effort);
        crankAngle -= degrees;
        windingFrame = Time.frameCount;
        ResumeMusic();
        if (windingProgress != null) windingProgress.SetValueWithoutNotify(WindingProgress);
        UpdateStatus();
        if (woundSeconds >= requiredSeconds) { phase = Phase.Anticipation; dragging = false; StartCoroutine(OpenSequence()); }
    }

    private void LateUpdate() { SamplePose(); }
    private void SamplePose()
    {
        if (animationClip == null) return;
        animationClip.SampleAnimation(gameObject, Mathf.Clamp(animationTime, 0, animationClip.length));
        float shut = completed || phase == Phase.Opened || phase == Phase.Used ? 0 :
            phase == Phase.Opening ? 1 - Mathf.Clamp01((animationTime - openingStartTime) / .12f) : 1;
        if (lid != null) lid.localRotation = Quaternion.Slerp(lid.localRotation, Quaternion.Euler(closedLidAngle, 0, 0), shut);
        if (spring != null) spring.localScale *= Mathf.Lerp(1, .86f, shut);
        if (crank != null && phase != Phase.Opening && phase != Phase.Opened && phase != Phase.Used && !completed)
            crank.localRotation *= Quaternion.AngleAxis(crankAngle, crankAxis.sqrMagnitude > .001f ? crankAxis.normalized : Vector3.right);
    }

    private IEnumerator OpenSequence()
    {
        if (musicSource != null) musicSource.Stop();
        if (windingUI != null) windingUI.SetActive(false);
        Cursor.visible = false;
        if (putDownButton != null) putDownButton.interactable = false;
        Vector3 start = transform.localPosition;
        Quaternion rotation = transform.localRotation;
        for (float elapsed = 0; elapsed < anticipationDelay; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, Mathf.Min(turnDuration, anticipationDelay)));
            transform.localPosition = Vector3.Lerp(start, frontViewPosition, t);
            transform.localRotation = Quaternion.Slerp(rotation, Quaternion.Euler(frontViewRotation), t);
            yield return null;
        }
        transform.localPosition = frontViewPosition;
        transform.localRotation = Quaternion.Euler(frontViewRotation);
        phase = Phase.Opening;
        animationTime = openingStartTime;
        if (jumpscareSound != null) jumpscareSource.PlayOneShot(jumpscareSound, jumpscareVolume);
        float end = animationClip != null ? Mathf.Clamp(openingEndTime, openingStartTime, animationClip.length) : openingEndTime;
        while (animationTime < end) { animationTime = Mathf.Min(end, animationTime + Time.deltaTime); yield return null; }
        completed = true;
        phase = Phase.Opened;
        Cursor.visible = true;
        if (windingUI != null) windingUI.SetActive(true);
        if (windingControls != null) windingControls.SetActive(false);
        if (windingProgress != null) windingProgress.gameObject.SetActive(false);
        if (statusText != null) statusText.text = "LMB to put down";
        if (putDownButton != null) putDownButton.interactable = true;
    }

    public void PutDown()
    {
        if (phase != Phase.Winding && phase != Phase.Opened) return;
        phase = Phase.Returning;
        dragging = false;
        if (musicSource != null) musicSource.Stop();
        if (windingUI != null) windingUI.SetActive(false);
        StartCoroutine(ReturnToScene());
    }

    private IEnumerator ReturnToScene()
    {
        Vector3 start = transform.position;
        Quaternion rotation = transform.rotation;
        transform.SetParent(originalParent, true);
        Vector3 startScale = transform.localScale;
        for (float elapsed = 0; elapsed < putDownDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / putDownDuration);
            transform.SetPositionAndRotation(Vector3.Lerp(start, originalPosition, t), Quaternion.Slerp(rotation, originalRotation, t));
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            yield return null;
        }
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        transform.localScale = originalScale;
        RestorePlayer();
        if (completed)
        {
            phase = Phase.Used;
            if (hintFlowchart == null || hintBlock == null ||
                !hintFlowchart.ExecuteBlock(hintBlock, onComplete: ShowCrystalBallTask))
                Debug.LogWarning("Assign the jack in the box hint Flowchart and Block to play the hint dialogue.", this);
            enabled = false;
        }
        else { phase = Phase.Waiting; animationTime = closedPoseTime; crankAngle = 0; }
    }

    private void ShowCrystalBallTask()
    {
        if (taskLabel == null) return;
        taskLabel.text = "Task: Find the crystal ball.";
        taskLabel.gameObject.SetActive(true);
    }

    private void PrepareAudio()
    {
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (jumpscareSource == null) jumpscareSource = gameObject.AddComponent<AudioSource>();
        var playerAudio = player.GetComponent<AudioSource>();
        foreach (var source in new[] { musicSource, jumpscareSource })
        {
            source.playOnAwake = false;
            if (playerAudio != null) source.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        }
        musicSource.clip = music; musicSource.loop = true; musicSource.volume = musicVolume;
        musicSource.spatialBlend = 1; musicSource.minDistance = 1; musicSource.maxDistance = 6;
        jumpscareSource.loop = false; jumpscareSource.spatialBlend = 0;
    }
    private void ResumeMusic()
    {
        if (musicSource == null || music == null) return;
        if (!musicStarted) { musicSource.Play(); musicStarted = true; }
        else if (musicPaused) musicSource.UnPause();
        musicPaused = false;
    }
    private void PauseMusic()
    {
        if (musicSource == null || !musicStarted || musicPaused) return;
        musicSource.Pause(); musicPaused = true;
    }
    private void UpdateStatus()
    {
        int remaining = Mathf.CeilToInt(Mathf.Max(0, requiredSeconds - woundSeconds));
        if (statusText != null) statusText.text = remaining + (remaining == 1 ? " second" : " seconds") + " of winding remaining";
    }
    private void HideHeldItem()
    {
        var pickup = player.GetComponent<PickupController>();
        var held = pickup != null ? pickup.HeldItem : null;
        if (held == null) return;
        heldRenderers = held.GetComponentsInChildren<Renderer>(true);
        heldRendererEnabled = new bool[heldRenderers.Length];
        for (int i = 0; i < heldRenderers.Length; i++) { heldRendererEnabled[i] = heldRenderers[i].enabled; heldRenderers[i].enabled = false; }
        heldLight = held.heldTorchLight;
        if (heldLight != null) { heldLightActive = heldLight.gameObject.activeSelf; heldLightEnabled = heldLight.enabled; heldLight.enabled = false; }
    }
    private void RestorePlayer()
    {
        if (inspectionLight != null) { Destroy(inspectionLight.gameObject); inspectionLight = null; }
        if (colliderEnabled != null) for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderEnabled[i];
        if (body != null) { body.isKinematic = bodyKinematic; body.useGravity = bodyGravity; }
        if (heldRenderers != null) for (int i = 0; i < heldRenderers.Length; i++) if (heldRenderers[i] != null) heldRenderers[i].enabled = heldRendererEnabled[i];
        if (heldLight != null) { heldLight.gameObject.SetActive(heldLightActive); heldLight.enabled = heldLightEnabled; }
        if (player != null) { player.enabled = playerWasEnabled; player.SuppressJumpUntilReleased(); }
        if (pauseMenu != null) pauseMenu.enabled = pauseWasEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }
    private void OnDisable()
    {
        if (phase == Phase.Waiting || phase == Phase.Used) return;
        StopAllCoroutines();
        if (windingUI != null) windingUI.SetActive(false);
        if (musicSource != null) musicSource.Stop();
        transform.SetParent(originalParent, true);
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        transform.localScale = originalScale;
        RestorePlayer();
        phase = completed ? Phase.Used : Phase.Waiting;
    }
    private void OnDestroy() { if (putDownButton != null) putDownButton.onClick.RemoveListener(PutDown); }
}
