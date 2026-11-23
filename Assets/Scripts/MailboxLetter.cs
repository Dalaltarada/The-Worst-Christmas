using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class MailboxLetter : MonoBehaviour, IInteractable
{
    public const string CheckpointStage = "mailbox-letter-read";
    [Header("Scene objects")]
    [SerializeField] private Transform paper;
    [SerializeField] private GameObject mailboxMust;
    [SerializeField] private Transform floorDestination;
    [Header("Paper animation")]
    [SerializeField, Min(.1f)] private float releaseDuration = 1.5f;
    [Tooltip("Straight slide from inside the slot, before the paper starts falling.")]
    [SerializeField, Min(.1f)] private float slideDuration = .85f;
    [SerializeField] private Vector3 slideRotation = new Vector3(0, 0, 90);
    [Tooltip("Slot alignment relative to the paper's authored centre, in world metres.")]
    [SerializeField] private Vector3 slotOffset = new Vector3(0, .10f, 0);
    [Tooltip("Distance inside the mailbox at the start of the slide, in metres.")]
    [SerializeField, Min(0f)] private float slideInset = .11f;
    [SerializeField] private Vector3 exitOffset = new Vector3(-.14f, 0f, 0f);
    [SerializeField] private Vector3 floorRotation = new Vector3(0, 0, 90);
    [SerializeField, Min(.1f)] private float pickupDuration = .65f;
    [SerializeField, Range(0f, .1f)] private float edgeDroop = .035f;
    [Header("Paper release sound")]
    [SerializeField] private AudioClip paperDroopingSound;
    [SerializeField, Range(0f, 1f)] private float paperSoundVolume = .8f;
    private MeshFilter animatedFilter;
    private Mesh originalPaperMesh, flexiblePaperMesh;
    private Vector3[] flatVertices, bentVertices;
    [Header("Reading view")]
    [SerializeField, TextArea] private string message = "They are everywhere… they hate lights.";
    [SerializeField] private Font font;
    [SerializeField] private Image interactionPrompt;
    private Material readingMaterial;
    private Material[] paperMaterials;
    [SerializeField] private Vector3 readPosition = new Vector3(0, -.025f, .43f);
    [SerializeField] private Vector3 readRotation = new Vector3(0, 90, -4);
    [SerializeField, Min(.05f)] private float readSize = .3f;

    private bool released, collected, reading;
    private MailboxPaperInteractable paperInteraction;
    private Collider[] paperColliders;
    private FirstPersonController player;
    private bool playerWasEnabled;
    private GameObject readUI;
    private Renderer[] heldRenderers;
    private bool[] heldVisibility;

    private void Awake()
    {
        if (mailboxMust != null) mailboxMust.SetActive(true);
        if (paper == null) return;
        paperInteraction = paper.GetComponent<MailboxPaperInteractable>();
        paperColliders = paper.GetComponentsInChildren<Collider>(true);
        SetPaperCollision(false);
        if (paperInteraction != null) paperInteraction.enabled = false;
        var body = paper.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.useGravity = false; }
        paper.gameObject.SetActive(false);
    }

    public void Interact()
    {
        if (released || paper == null || floorDestination == null || Time.timeScale <= 0f) return;
        released = true;
        StartCoroutine(ReleasePaper());
    }

    private IEnumerator ReleasePaper()
    {
        Vector3 opening = paper.position + transform.TransformDirection(slotOffset);
        Vector3 exit = transform.TransformDirection(exitOffset);
        Vector3 direction = exit.sqrMagnitude > .000001f ? exit.normalized : -transform.right;
        Vector3 inside = opening - direction * slideInset;
        Vector3 lip = opening + exit;
        Quaternion rotation = transform.rotation * Quaternion.Euler(slideRotation);
        // Keep the sheet flat in the slot. Rotation and flutter only start once
        // the trailing edge has cleared the mailbox, so it cannot tumble through it.
        paper.SetPositionAndRotation(inside, rotation);
        PrepareFlexiblePaper();
        paper.gameObject.SetActive(true);
        if (paperDroopingSound != null)
        {
            var audio = paper.GetComponent<AudioSource>();
            if (audio == null) audio = paper.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = .8f;
            audio.maxDistance = 7f;
            var listener = FindObjectOfType<FirstPersonController>();
            var playerAudio = listener != null ? listener.GetComponent<AudioSource>() : null;
            if (playerAudio != null) audio.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
            audio.PlayOneShot(paperDroopingSound, paperSoundVolume);
        }
        for (float elapsed = 0; elapsed < slideDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
            paper.position = Vector3.LerpUnclamped(inside, lip, ease);
            BendPaper(opening, direction, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        paper.SetPositionAndRotation(lip, rotation);
        Vector3 start = lip;
        Vector3 end = floorDestination.position;
        Vector3 control = start + direction * .06f;
        Quaternion finalRotation = Quaternion.Euler(floorRotation);
        for (float elapsed = 0; elapsed < releaseDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / releaseDuration);
            Vector3 a = Vector3.Lerp(start, control, t);
            Vector3 b = Vector3.Lerp(control, end, t);
            paper.position = Vector3.Lerp(a, b, t);
            paper.rotation = Quaternion.Slerp(rotation, finalRotation, t) *
                Quaternion.Euler(Mathf.Sin(t * Mathf.PI * 2) * 4 * Mathf.Sin(t * Mathf.PI), 0, 0);
            BendPaper(opening, direction, 1f - Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        paper.SetPositionAndRotation(end, finalRotation);
        RestorePaperMesh();
        SetPaperCollision(true);
        if (paperInteraction != null) paperInteraction.enabled = true;
        enabled = false; // The mailbox cannot be used again; paper owns the next interaction.
    }

    public void CollectPaper()
    {
        if (!released || collected || paperInteraction == null || !paperInteraction.enabled || Time.timeScale <= 0) return;
        var camera = Camera.main;
        player = FindObjectOfType<FirstPersonController>();
        if (camera == null || player == null) return;
        collected = true;
        if (mailboxMust != null) mailboxMust.SetActive(false);
        paperInteraction.enabled = false;
        SetPaperCollision(false);
        playerWasEnabled = player.enabled;
        player.enabled = false;
        if (interactionPrompt != null) interactionPrompt.enabled = false;
        var paperRenderer = paper.GetComponent<Renderer>();
        var shader = Shader.Find("Unlit/Texture");
        if (paperRenderer != null && shader != null)
        {
            paperMaterials = paperRenderer.sharedMaterials;
            readingMaterial = new Material(shader);
            readingMaterial.mainTexture = paperRenderer.sharedMaterial != null && paperRenderer.sharedMaterial.mainTexture != null
                ? paperRenderer.sharedMaterial.mainTexture : Texture2D.whiteTexture;
            var materials = new Material[paperMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = readingMaterial;
            paperRenderer.sharedMaterials = materials;
        }
        var pickup = player.GetComponent<PickupController>();
        if (pickup != null && pickup.HeldItem != null)
        {
            heldRenderers = pickup.HeldItem.GetComponentsInChildren<Renderer>(true);
            heldVisibility = new bool[heldRenderers.Length];
            for (int i = 0; i < heldRenderers.Length; i++) { heldVisibility[i] = heldRenderers[i].enabled; heldRenderers[i].enabled = false; }
        }
        reading = true;
        StartCoroutine(ReadPaper(camera.transform));
    }

    private IEnumerator ReadPaper(Transform camera)
    {
        paper.SetParent(camera, true);
        Vector3 start = paper.localPosition;
        Quaternion startRotation = paper.localRotation;
        Vector3 startScale = paper.localScale;
        var mesh = paper.GetComponent<MeshFilter>();
        Vector3 size = mesh != null && mesh.sharedMesh != null ? Vector3.Scale(mesh.sharedMesh.bounds.size, startScale) : startScale;
        float longest = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3 targetScale = startScale * (readSize / Mathf.Max(.001f, longest));
        for (float elapsed = 0; elapsed < pickupDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / pickupDuration);
            paper.localPosition = Vector3.Lerp(start, readPosition, t);
            paper.localRotation = Quaternion.Slerp(startRotation, Quaternion.Euler(readRotation), t);
            paper.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        paper.localPosition = readPosition;
        paper.localRotation = Quaternion.Euler(readRotation);
        paper.localScale = targetScale;
        BuildReadUI();
        yield return new WaitForSeconds(.35f);
        while (Time.timeScale <= 0 || !Input.GetMouseButtonDown(0)) yield return null;
        FinishReading();
        GameSave.SaveCheckpoint(CheckpointStage);
    }

    public void RestoreReadCheckpoint()
    {
        released = collected = true;
        if (mailboxMust != null) mailboxMust.SetActive(false);
        if (paper != null) paper.gameObject.SetActive(false);
        if (paperInteraction != null) paperInteraction.enabled = false;
        SetPaperCollision(false);
        enabled = false;
    }

    private void BuildReadUI()
    {
        readUI = new GameObject("Mailbox Letter UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = readUI.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        var scaler = readUI.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Message", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(readUI.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.15f, .05f); rect.anchorMax = new Vector2(.85f, .24f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = panel.GetComponent<Image>(); image.color = new Color(.025f, .02f, .015f, .88f); image.raycastTarget = false;
        AddText(panel.transform, "Letter", message, 36, new Vector2(24, 52), new Vector2(-24, -12));
        AddText(panel.transform, "Close hint", "LMB — put away", 22, new Vector2(24, 10), new Vector2(-24, -140));
    }

    private void AddText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max;
        var text = go.GetComponent<Text>(); text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = value; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1, .95f, .83f); text.raycastTarget = false;
    }

    private void SetPaperCollision(bool active)
    {
        if (paperColliders == null) return;
        foreach (var collider in paperColliders) if (collider != null) collider.enabled = active;
    }

    private void FinishReading()
    {
        if (!reading) return;
        reading = false;
        if (readUI != null) Destroy(readUI);
        if (paper != null)
        {
            if (paperMaterials != null) paper.GetComponent<Renderer>().sharedMaterials = paperMaterials;
            paper.gameObject.SetActive(false);
        }
        if (readingMaterial != null) Destroy(readingMaterial);
        if (player != null) { player.enabled = playerWasEnabled; player.SuppressJumpUntilReleased(); }
        if (heldRenderers != null) for (int i = 0; i < heldRenderers.Length; i++) if (heldRenderers[i] != null) heldRenderers[i].enabled = heldVisibility[i];
    }

    // Subdivide a private copy only: the imported mesh and materials stay intact.
    private void PrepareFlexiblePaper()
    {
        animatedFilter = paper.GetComponent<MeshFilter>();
        if (animatedFilter == null || animatedFilter.sharedMesh == null || !animatedFilter.sharedMesh.isReadable) return;
        originalPaperMesh = animatedFilter.sharedMesh;
        var sourceVertices = originalPaperMesh.vertices;
        var sourceUV = originalPaperMesh.uv;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var indices = originalPaperMesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
            Subdivide(vertices, uv, sourceVertices[indices[i]], sourceVertices[indices[i + 1]], sourceVertices[indices[i + 2]],
                sourceUV.Length == sourceVertices.Length ? sourceUV[indices[i]] : Vector2.zero,
                sourceUV.Length == sourceVertices.Length ? sourceUV[indices[i + 1]] : Vector2.zero,
                sourceUV.Length == sourceVertices.Length ? sourceUV[indices[i + 2]] : Vector2.zero, 3);
        flatVertices = vertices.ToArray();
        bentVertices = new Vector3[flatVertices.Length];
        indices = new int[flatVertices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        flexiblePaperMesh = new Mesh { name = "Paper release bend (runtime)" };
        flexiblePaperMesh.MarkDynamic();
        flexiblePaperMesh.vertices = flatVertices;
        flexiblePaperMesh.uv = uv.ToArray();
        flexiblePaperMesh.triangles = indices;
        flexiblePaperMesh.RecalculateNormals();
        animatedFilter.sharedMesh = flexiblePaperMesh;
    }

    private static void Subdivide(List<Vector3> vertices, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c,
        Vector2 ua, Vector2 ub, Vector2 uc, int depth)
    {
        if (depth == 0) { vertices.Add(a); vertices.Add(b); vertices.Add(c); uv.Add(ua); uv.Add(ub); uv.Add(uc); return; }
        Vector3 ab = (a + b) * .5f, bc = (b + c) * .5f, ca = (c + a) * .5f;
        Vector2 uab = (ua + ub) * .5f, ubc = (ub + uc) * .5f, uca = (uc + ua) * .5f;
        Subdivide(vertices, uv, a, ab, ca, ua, uab, uca, depth - 1);
        Subdivide(vertices, uv, ab, b, bc, uab, ub, ubc, depth - 1);
        Subdivide(vertices, uv, ca, bc, c, uca, ubc, uc, depth - 1);
        Subdivide(vertices, uv, ab, bc, ca, uab, ubc, uca, depth - 1);
    }

    private void BendPaper(Vector3 opening, Vector3 direction, float amount)
    {
        if (flexiblePaperMesh == null) return;
        for (int i = 0; i < flatVertices.Length; i++)
        {
            Vector3 world = paper.TransformPoint(flatVertices[i]);
            float exposed = Mathf.Clamp01(Vector3.Dot(world - opening, direction) / .24f);
            // The portion still supported by the slot stays flat.
            world -= Vector3.up * (edgeDroop * exposed * exposed * amount);
            bentVertices[i] = paper.InverseTransformPoint(world);
        }
        flexiblePaperMesh.vertices = bentVertices;
        flexiblePaperMesh.RecalculateNormals();
        flexiblePaperMesh.RecalculateBounds();
    }

    private void RestorePaperMesh()
    {
        if (animatedFilter != null && originalPaperMesh != null) animatedFilter.sharedMesh = originalPaperMesh;
        if (flexiblePaperMesh != null) Destroy(flexiblePaperMesh);
        flexiblePaperMesh = null;
    }

    private void OnDestroy() { RestorePaperMesh(); FinishReading(); }
}
