using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

[DefaultExecutionOrder(100)]
public sealed class FaceMonsterEncounter : MonoBehaviour
{
    [Header("Detection and crawl")]
    [SerializeField] private Animator animator;
    [SerializeField, Min(.1f)] private float detectionDistance = 5f;
    [SerializeField, Min(.01f)] private float crawlSpeed = .45f;
    [SerializeField, Min(.1f)] private float catchDistance = .65f;
    [SerializeField, Range(.1f, 2f)] private float crawlAnimationSpeed = .65f;
    [Header("Torch charge — tap E")]
    [SerializeField, Min(1)] private int pressesToDefeat = 12;
    [SerializeField, Min(0f)] private float boostedIntensity = 6f;
    [SerializeField, Min(.1f)] private float boostedRange = 8f;
    [SerializeField, Min(.01f)] private float lightResponse = .14f;
    [SerializeField, Min(.1f)] private float lightReturnDuration = 2f;
    [Header("Ghostly black vanish")]
    [SerializeField, Min(.2f)] private float vanishDuration = 1.6f;
    [SerializeField, Min(0f)] private float ghostLift = .18f;
    [SerializeField] private Material vanishSmokeMaterial;
    [Header("3D sound — attach clips here")]
    [SerializeField] private AudioClip monsterLoop;
    [SerializeField] private AudioClip monsterDeath;
    [SerializeField, Range(0, 1)] private float soundVolume = .8f;
    [SerializeField, Min(1f)] private float soundDistance = 12f;
    [Header("Scene UI")]
    [SerializeField] private GameObject chargePanel;
    [SerializeField] private Slider chargeBar;
    [SerializeField] private Text chargeHint;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private Button tryAgain;

    private FirstPersonController player;
    private PickupController pickup;
    private ElfMazeNavigation navigation;
    private AudioSource monsterAudio;
    private Light chargedLight;
    private float normalIntensity, normalRange;
    private float intensityVelocity, rangeVelocity;
    private int presses;
    private bool active, defeated, caught, retrying;
    private Renderer[] visuals;
    private Collider[] colliders;
    private static FaceMonsterEncounter currentEncounter;
    private ParticleSystem vanishSmoke;
    private bool visualsVanished;
    private MaterialPropertyBlock vanishProperties;
    private static readonly int Vanish = Shader.PropertyToID("_Vanish");
    private static readonly int GhostLift = Shader.PropertyToID("_GhostLift");
    private static readonly int GhostBounds = Shader.PropertyToID("_GhostBounds");

    public bool IsDefeated => defeated;
    public float ChargeProgress => Mathf.Clamp01((float)presses / Mathf.Max(1, pressesToDefeat));

    private void Awake()
    {
        if (chargePanel != null) chargePanel.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        if (tryAgain != null) tryAgain.onClick.AddListener(Retry);
        visuals = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        player = FindObjectOfType<FirstPersonController>();
        if (player == null) { enabled = false; return; }
        pickup = player.GetComponent<PickupController>();
        navigation = new ElfMazeNavigation(transform, player.transform);
        monsterAudio = gameObject.AddComponent<AudioSource>();
        monsterAudio.playOnAwake = false;
        monsterAudio.spatialBlend = 1;
        monsterAudio.dopplerLevel = 0;
        monsterAudio.minDistance = 1;
        monsterAudio.maxDistance = soundDistance;
        monsterAudio.rolloffMode = AudioRolloffMode.Linear;
        monsterAudio.volume = soundVolume;
        var playerAudio = player.GetComponent<AudioSource>();
        if (playerAudio != null) monsterAudio.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0;
        }
        SetChargeUI();
    }

    private void Update()
    {
        if (player == null || defeated || caught || Time.timeScale <= 0 || !player.enabled) return;
        float distance = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude;
        if (!active)
        {
            if (distance > detectionDistance || (currentEncounter != null && currentEncounter != this)) return;
            active = true;
            currentEncounter = this;
            if (chargePanel != null) chargePanel.SetActive(true);
            if (monsterLoop != null) { monsterAudio.clip = monsterLoop; monsterAudio.loop = true; monsterAudio.Play(); }
        }

        Light heldLight = HeldTorch();
        if (chargedLight != null && heldLight != chargedLight)
        {
            RestoreLightImmediately();
            presses = 0;
        }
        if (Input.GetKeyDown(KeyCode.E)) ChargeTorch();
        if (defeated) return;
        UpdateBoost();
        SetChargeUI();

        if (distance <= catchDistance && navigation.Clear(transform.position, player.transform.position, .04f))
        {
            CatchPlayer();
            return;
        }
        Vector3 direction = navigation.Direction(player.transform.position);
        direction.y = 0;
        bool moving = direction.sqrMagnitude > .0001f;
        if (moving)
        {
            float step = Mathf.Min(direction.magnitude, crawlSpeed * Time.deltaTime);
            direction.Normalize();
            moving = navigation.Clear(transform.position, transform.position + direction * step);
            if (moving)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1 - Mathf.Exp(-6 * Time.deltaTime));
                transform.position += direction * step;
            }
        }
        if (animator != null) animator.speed = moving ? crawlAnimationSpeed : 0;
    }

    private Light HeldTorch()
    {
        var item = pickup != null ? pickup.HeldItem : null;
        return item != null && item.IsHeld ? item.heldTorchLight : null;
    }

    // One key-down adds one step; holding E cannot fill the meter.
    public void ChargeTorch()
    {
        if (!active || defeated || caught || Time.timeScale <= 0) return;
        Light light = HeldTorch();
        if (light == null) return;
        if (chargedLight != light)
        {
            RestoreLightImmediately();
            chargedLight = light;
            normalIntensity = light.intensity;
            normalRange = light.range;
            presses = 0;
        }
        light.gameObject.SetActive(true);
        light.enabled = true;
        pickup.HeldItem.PlayTorchChargeSound();
        presses = Mathf.Min(presses + 1, Mathf.Max(1, pressesToDefeat));
        SetChargeUI();
        if (presses >= Mathf.Max(1, pressesToDefeat)) Defeat();
    }

    private void UpdateBoost()
    {
        if (chargedLight == null) return;
        chargedLight.intensity = Mathf.SmoothDamp(chargedLight.intensity, Mathf.Lerp(normalIntensity, Mathf.Max(normalIntensity, boostedIntensity), ChargeProgress), ref intensityVelocity, lightResponse);
        chargedLight.range = Mathf.SmoothDamp(chargedLight.range, Mathf.Lerp(normalRange, Mathf.Max(normalRange, boostedRange), ChargeProgress), ref rangeVelocity, lightResponse);
    }

    private void SetChargeUI()
    {
        if (chargeBar != null) chargeBar.SetValueWithoutNotify(ChargeProgress);
        if (chargeHint != null) chargeHint.text = HeldTorch() == null ? "Pick up your torch — tap E to increase light" : "Tap E to increase light";
    }

    private void Defeat()
    {
        if (defeated || caught) return;
        defeated = true;
        if (chargeBar != null) chargeBar.SetValueWithoutNotify(1);
        if (animator != null) animator.speed = 0;
        foreach (var collider in colliders) if (collider != null) collider.enabled = false;
        monsterAudio.Stop();
        monsterAudio.loop = false;
        if (monsterDeath != null) monsterAudio.PlayOneShot(monsterDeath, 1);
        StartCoroutine(VanishGhost());
        StartCoroutine(FinishDefeat());
    }

    private IEnumerator VanishGhost()
    {
        CreateVanishSmoke();
        vanishProperties = new MaterialPropertyBlock();
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool haveBounds = false;
        foreach (var renderer in visuals)
        {
            var skin = renderer as SkinnedMeshRenderer;
            if (skin != null)
            {
                foreach (var bone in skin.bones)
                {
                    if (bone == null) continue;
                    if (!haveBounds) { bounds = new Bounds(bone.position, Vector3.zero); haveBounds = true; }
                    else bounds.Encapsulate(bone.position);
                }
            }
            else if (renderer != null)
            {
                if (!haveBounds) { bounds = renderer.bounds; haveBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
        Vector4 ghostBounds = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, Mathf.Max(.1f, bounds.size.y + .14f));
        for (float elapsed = 0; elapsed < vanishDuration; elapsed += Time.deltaTime)
        {
            float amount = Mathf.SmoothStep(0, 1, elapsed / vanishDuration);
            foreach (var renderer in visuals)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(vanishProperties);
                vanishProperties.SetFloat(Vanish, amount);
                vanishProperties.SetFloat(GhostLift, ghostLift);
                vanishProperties.SetVector(GhostBounds, ghostBounds);
                renderer.SetPropertyBlock(vanishProperties);
            }
            yield return null;
        }
        foreach (var renderer in visuals) if (renderer != null) renderer.enabled = false;
        if (vanishSmoke != null) vanishSmoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        visualsVanished = true;
    }

    private void CreateVanishSmoke()
    {
        if (vanishSmokeMaterial == null) return;
        var go = new GameObject("Black ghost wisps");
        go.transform.SetParent(transform, false);
        vanishSmoke = go.AddComponent<ParticleSystem>();
        vanishSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = vanishSmoke.main;
        main.loop = false;
        main.duration = Mathf.Max(.1f, vanishDuration * .75f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.25f);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 220;
        var emission = vanishSmoke.emission;
        emission.rateOverTime = 85;
        var shape = vanishSmoke.shape;
        var skin = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skin;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        }
        else { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .25f; }
        var velocity = vanishSmoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.04f, .04f);
        velocity.y = new ParticleSystem.MinMaxCurve(.12f, .28f);
        velocity.z = new ParticleSystem.MinMaxCurve(-.04f, .04f);
        var noise = vanishSmoke.noise;
        noise.enabled = true;
        noise.strength = .07f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = .2f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var size = vanishSmoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .6f, 1, 1.8f));
        var colour = vanishSmoke.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.7f, .15f), new GradientAlphaKey(.4f, .65f), new GradientAlphaKey(0, 1) });
        colour.color = gradient;
        var smokeRenderer = go.GetComponent<ParticleSystemRenderer>();
        smokeRenderer.sharedMaterial = vanishSmokeMaterial;
        smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        smokeRenderer.sortMode = ParticleSystemSortMode.Distance;
        smokeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smokeRenderer.receiveShadows = false;
        vanishSmoke.Play();
    }

    private IEnumerator FinishDefeat()
    {
        // Briefly reach full power before easing back, including very fast taps.
        if (chargedLight != null)
        {
            chargedLight.intensity = Mathf.Max(normalIntensity, boostedIntensity);
            chargedLight.range = Mathf.Max(normalRange, boostedRange);
        }
        yield return new WaitForSeconds(.15f);
        if (chargePanel != null) chargePanel.SetActive(false);
        float intensity = chargedLight != null ? chargedLight.intensity : 0;
        float range = chargedLight != null ? chargedLight.range : 0;
        for (float elapsed = 0; elapsed < lightReturnDuration; elapsed += Time.deltaTime)
        {
            if (chargedLight != null)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / lightReturnDuration);
                chargedLight.intensity = Mathf.Lerp(intensity, normalIntensity, t);
                chargedLight.range = Mathf.Lerp(range, normalRange, t);
            }
            yield return null;
        }
        RestoreLightImmediately();
        if (currentEncounter == this) currentEncounter = null;
        while (!visualsVanished || (monsterAudio != null && monsterAudio.isPlaying) ||
            (vanishSmoke != null && vanishSmoke.IsAlive(true))) yield return null;
        gameObject.SetActive(false);
    }

    private void RestoreLightImmediately()
    {
        if (chargedLight != null) { chargedLight.intensity = normalIntensity; chargedLight.range = normalRange; }
        chargedLight = null;
        intensityVelocity = rangeVelocity = 0;
    }

    private void CatchPlayer()
    {
        if (caught || defeated) return;
        caught = true;
        RestoreLightImmediately();
        if (chargePanel != null) chargePanel.SetActive(false);
        if (monsterAudio != null) monsterAudio.Stop();
        if (vanishSmoke != null) vanishSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (animator != null) animator.speed = 0;
        player.enabled = false;
        var pause = FindObjectOfType<PauseMenu>();
        if (pause != null) pause.enabled = false;
        if (loseScreen != null) loseScreen.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Retry()
    {
        if (retrying) return;
        retrying = true;
        Time.timeScale = 1;
        if (!GameSave.ContinueGame()) SceneManager.LoadScene(gameObject.scene.path);
    }

    private void OnDisable()
    {
        RestoreLightImmediately();
        if (chargePanel != null) chargePanel.SetActive(false);
        if (monsterAudio != null) monsterAudio.Stop();
        if (vanishSmoke != null) vanishSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (currentEncounter == this) currentEncounter = null;
    }

    private void OnDestroy()
    {
        RestoreLightImmediately();
        if (tryAgain != null) tryAgain.onClick.RemoveListener(Retry);
        if (currentEncounter == this) currentEncounter = null;
    }
}
