using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class ElfEncounter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float detectionDistance = 5, runSpeed = 2.3f, catchDistance = .65f;
    [SerializeField] private float stillTime = 5, escapeTime = 2.5f, fadeTime = 1.5f;
    [SerializeField, Min(.1f)] private float retreatSpeed = 1.2f;
    [Header("Elf sound")]
    [SerializeField] private AudioClip elfSfx;
    [SerializeField, Range(0, 1)] private float sfxVolume = .8f;
    [SerializeField] private Font loseFont;
    private FirstPersonController player;
    private PickupController pickup;
    private Vector3 previous;
    private float safeTime;
    private bool active, retreating, caught, finished;
    private Coroutine escape;
    [Header("Lose screen")]
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private Button retryButton;
    private readonly List<Material> fadeMaterials = new List<Material>();
    private static readonly int Running = Animator.StringToHash("Running");
    private ElfMazeNavigation navigation;
    private AudioSource elfAudio;
    private bool retrying;
    private bool wasSafe;
    private CharacterController playerBody;
    private float movementDangerUntil;

    private void Start()
    {
        if (loseScreen != null) loseScreen.SetActive(false);
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        player = FindObjectOfType<FirstPersonController>();
        if (player == null) { enabled = false; return; }
        pickup = player.GetComponent<PickupController>();
        playerBody = player.GetComponent<CharacterController>();
        previous = player.transform.position;
        navigation = new ElfMazeNavigation(transform, player.transform);
        if (elfSfx != null)
        {
            elfAudio = gameObject.AddComponent<AudioSource>();
            elfAudio.playOnAwake = false; elfAudio.clip = elfSfx; elfAudio.loop = true;
            elfAudio.spatialBlend = 1; elfAudio.minDistance = 1; elfAudio.maxDistance = 12; elfAudio.volume = sfxVolume;
            var playerAudio = player.GetComponent<AudioSource>(); if (playerAudio != null) elfAudio.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        }
        if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        SetRun(false);
    }

    private bool Safe()
    {
        var item = pickup != null ? pickup.HeldItem : null;
        bool lit = item != null && item.heldTorchLight != null && item.heldTorchLight.gameObject.activeInHierarchy && item.heldTorchLight.enabled;
        bool moving = Vector3.ProjectOnPlane(player.transform.position - previous, Vector3.up).sqrMagnitude > .00000025f;
        moving |= Mathf.Abs(Input.GetAxisRaw("Horizontal")) > .01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > .01f;
        moving |= Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
        if (playerBody != null && playerBody.enabled)
            moving |= Vector3.ProjectOnPlane(playerBody.velocity, Vector3.up).sqrMagnitude > .0001f;
        // Keep movement dangerous across Update/physics timing and brief gaps
        // between footsteps. Switching the light off never cancels pursuit.
        if (moving) movementDangerUntil = Time.time + .15f;
        return !lit && !moving && Time.time >= movementDangerUntil;
    }

    private void Update()
    {
        if (finished || caught || player == null || !player.enabled || Time.timeScale <= 0) return;
        bool safe = Safe();
        if (safe != wasSafe) { navigation.ResetRoute(); wasSafe = safe; }
        previous = player.transform.position;
        // Compare on the floor plane: crouch/character pivot height must not prevent a catch.
        float distance = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up).magnitude;
        if (!active) { if (distance > detectionDistance) return; active = true; if (elfAudio != null) elfAudio.Play(); }
        if (retreating)
        {
            if (safe) return;
            if (escape != null) StopCoroutine(escape);
            retreating = false;
            SetOpacity(1);
            safeTime = 0;
        }
        if (safe)
        {
            Move(navigation.AwayDirection(player.transform.position), retreatSpeed);
            safeTime += Time.deltaTime;
            if (safeTime >= stillTime) { retreating = true; escape = StartCoroutine(Escape()); }
            return;
        }
        safeTime = 0;
        SetRun(true);
        if (distance <= catchDistance && ClearPathToPlayer()) { CatchPlayer(); return; }
        Move(navigation.Direction(player.transform.position));
    }

    private bool ClearPathToPlayer()
    {
        Vector3 start = transform.position + Vector3.up * .3f;
        Vector3 end = player.transform.position; end.y = start.y;
        return navigation.Clear(start, end, .04f);
    }

    private void SetRun(bool run) { if (animator != null) { animator.SetBool(Running, run); if (!run) animator.speed = 1; } }

    private void Move(Vector3 direction, float speed = -1f)
    {
        direction.y = 0;
        if (direction.sqrMagnitude < .0001f) { SetRun(false); return; }
        direction.Normalize();
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 9);
        float step = (speed > 0 ? speed : runSpeed) * Time.deltaTime;
        if (animator != null) animator.speed = speed > 0 ? Mathf.Clamp(speed / Mathf.Max(.1f, runSpeed), .2f, 2f) : 1f;
        if (navigation.Clear(transform.position, transform.position + direction * step)) { SetRun(true); transform.position += direction * step; }
        else SetRun(false);
    }

    private IEnumerator Escape()
    {
        SetRun(true);
        for (float t = 0; t < escapeTime; t += Time.deltaTime) { Move(navigation.AwayDirection(player.transform.position)); yield return null; }
        if (fadeMaterials.Count == 0)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>()) foreach (var material in renderer.materials)
            {
                if (!material.HasProperty("_Color")) continue;
                material.SetFloat("_Mode", 2); material.SetInt("_SrcBlend", 5); material.SetInt("_DstBlend", 10); material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000;
                fadeMaterials.Add(material);
            }
        }
        for (float t = 0; t < fadeTime; t += Time.deltaTime) { SetOpacity(1 - t / Mathf.Max(.01f, fadeTime)); Move(navigation.AwayDirection(player.transform.position)); yield return null; }
        finished = true;
        gameObject.SetActive(false);
    }

    private void SetOpacity(float alpha) { foreach (var material in fadeMaterials) { var c = material.color; c.a = alpha; material.color = c; } }

    private void CatchPlayer()
    {
        if (caught) return;
        caught = true;
        player.enabled = false;
        SetRun(false);
        var pause = FindObjectOfType<PauseMenu>(); if (pause != null) pause.enabled = false;
        if (loseScreen != null) loseScreen.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (elfAudio != null) elfAudio.Stop();
    }

    private void Retry()
    {
        if (retrying) return;
        retrying = true;
        Time.timeScale = 1;
        if (!GameSave.ContinueGame()) UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.path);
    }

    private void OnDestroy() { if (retryButton != null) retryButton.onClick.RemoveListener(Retry); foreach (var material in fadeMaterials) if (material != null) Destroy(material); }
}


