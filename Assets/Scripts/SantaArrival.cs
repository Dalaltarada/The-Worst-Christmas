using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class SantaArrival : MonoBehaviour
{
    [SerializeField] private FirstPersonController player;
    [SerializeField] private Transform hidingSpot;
    [SerializeField] private Transform santa;
    [SerializeField] private Animator santaAnimator;
    [SerializeField] private Transform fireplaceStandingSpot;
    [SerializeField] private Transform santaFace;
    [SerializeField] private Transform[] arrivalWaypoints;
    [SerializeField] private Transform[] chaseWaypoints;
    [SerializeField] private GameObject playerBlocker;

    [Header("Maze Transition")]
    [SerializeField] private GameObject mainArea;
    [Tooltip("Assign the Pillow Fort Maze object from this scene. Its Spawn child is used automatically.")]
    [SerializeField] private GameObject pillowFortMaze;
    [Tooltip("Optional. Assign a different spawn point inside the selected Pillow Fort Maze.")]
    [SerializeField] private Transform mazeSpawn;
    [Tooltip("The Global Fog component on the player camera, enabled when entering the maze.")]
    [SerializeField] private Behaviour mazeGlobalFog;
    [SerializeField, Min(0f)] private float mazeFadeDuration = 0.35f;

    [SerializeField] private GameObject[] lightsToTurnOff;
    [SerializeField, Range(0f, 1f)] private float nightBrightness = 0.28f;
    [SerializeField, Range(0f, 1f)] private float remainingLightBrightness = 0.45f;
    [SerializeField] private Text taskLabel;

    [Header("Fungus")]
    [SerializeField] private Flowchart dialogue;
    [SerializeField] private Block fireplaceLine;
    [SerializeField] private Block hidingHint;
    [SerializeField] private Block greeting;
    [SerializeField] private Flowchart whisperDialogue;
    [SerializeField] private Block whisper;

    [Header("Sound effects")]
    [SerializeField] private AudioSource fireplaceAudio;
    [SerializeField] private AudioSource doorAudio;
    [SerializeField] private AudioSource santaAudio;
    [SerializeField] private AudioClip fireplaceNoise;
    [SerializeField] private AudioClip doorOpen;
    [SerializeField] private AudioClip doorClose;
    [SerializeField] private AudioClip heavyFootsteps;
    [SerializeField] private AudioClip heavyBreathing;
    [SerializeField] private AudioClip santaReaction;
    [SerializeField] private AudioSource whisperAudio;
    [SerializeField] private AudioClip whisperNoise;

    [Header("Timing and distances")]
    [SerializeField, Min(0f)] private float hintAfterWalkingSeconds = 10f;
    [SerializeField, Min(0.1f)] private float hidingRadius = 0.85f;
    [SerializeField, Min(0.1f)] private float greetingRadius = 2f;
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3.1f;
    [SerializeField, Min(0.1f)] private float chasePlayerGap = 1.75f;
    [SerializeField, Min(0.1f)] private float fortSafetyRadius = 2.25f;
    [SerializeField, Min(0.1f)] private float fortStopDistance = 1.05f;
    [SerializeField, Min(0.1f)] private float livingRoomRadius = 3.8f;
    [SerializeField, Min(0.1f)] private float arrivalWalkSpeed = 1.2f;
    [SerializeField, Min(0f)] private float arrivalWaypointWait = 5f;
    [SerializeField, Range(0f, 35f)] private float arrivalLookAngle = 16f;

    [Header("Face close-up")]
    [SerializeField, Min(0f)] private float faceCutDuration = 2f;
    [Tooltip("Distance from Santa's face. Increase this to zoom out.")]
    [SerializeField, Min(0.1f)] private float faceCutDistance = 0.7f;
    [Tooltip("Moves the entire close-up up or down in world units.")]
    [SerializeField] private float faceCutHeightOffset = -0.04f;

    private bool started;
    private bool nightStarted;
    private Camera cutCamera;
    private Vector3 previousCameraPosition;
    private Quaternion previousCameraRotation;
    private float previousNearClip;
    private bool previousMovementLock;
    private AudioSource breathingAudio;
    private Transform santaHead;
    private float arrivalLookYaw;
    private Quaternion animatedHeadRotation;
    private Quaternion adjustedHeadRotation;
    private bool headOffsetApplied;
    private Image mazeFade;
    private bool mazeMovementWasLocked;

    private void Awake()
    {
        santa.gameObject.SetActive(false);
        SetFortBlocked(false);
        if (arrivalWaypoints == null || arrivalWaypoints.Length == 0) return;

        santaAudio.spatialBlend = 1f;
        santaAudio.dopplerLevel = 0f;
        if (heavyBreathing == null) return;

        breathingAudio = santaAudio.gameObject.AddComponent<AudioSource>();
        breathingAudio.playOnAwake = false;
        breathingAudio.clip = heavyBreathing;
        breathingAudio.loop = true;
        breathingAudio.spatialBlend = 1f;
        breathingAudio.dopplerLevel = 0f;
        breathingAudio.minDistance = 1.2f;
        breathingAudio.maxDistance = santaAudio.maxDistance;
        breathingAudio.volume = 0.65f;
        breathingAudio.outputAudioMixerGroup = santaAudio.outputAudioMixerGroup;
    }

    public void BeginEncounter()
    {
        if (started) return;
        started = true;
        StartCoroutine(Encounter());
    }

    public void BeginNight()
    {
        if (nightStarted) return;
        nightStarted = true;
        foreach (var lightObject in lightsToTurnOff)
            if (lightObject != null) lightObject.SetActive(false);

        RenderSettings.ambientIntensity *= nightBrightness;
        RenderSettings.ambientLight *= nightBrightness;
        RenderSettings.ambientSkyColor *= nightBrightness;
        RenderSettings.ambientEquatorColor *= nightBrightness;
        RenderSettings.ambientGroundColor *= nightBrightness;
        RenderSettings.reflectionIntensity *= nightBrightness;
        foreach (var light in FindObjectsOfType<Light>())
        {
            if (light.gameObject.scene != gameObject.scene) continue;
            if (fireplaceAudio != null && FlatDistance(light.transform.position, fireplaceAudio.transform.position) < 0.45f &&
                light.type == LightType.Point) continue;
            light.intensity *= remainingLightBrightness;
        }
    }

    private IEnumerator Encounter()
    {
        BeginNight();
        var explorationStart = player.transform.position;
        yield return new WaitUntil(() =>
            Near(fireplaceStandingSpot.position, livingRoomRadius) &&
            FlatDistance(player.transform.position, explorationStart) >= 0.6f);

        yield return PlaySound(fireplaceAudio, fireplaceNoise, 0.45f);
        yield return Say(fireplaceLine);

        bool usesWaypoints = arrivalWaypoints != null && arrivalWaypoints.Length > 0;
        if (usesWaypoints && !IsHiding()) ShowTask("Task: Hide in the pillow fort");

        float walking = 0f;
        var lastPosition = player.transform.position;
        while (!IsHiding() && walking < hintAfterWalkingSeconds)
        {
            yield return null;
            var position = player.transform.position;
            if (usesWaypoints || Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).sqrMagnitude > 0.000001f)
                walking += Time.deltaTime;
            lastPosition = position;
        }
        bool hintFinished = true;
        if (usesWaypoints)
        {
            if (!IsHiding() && hidingHint != null && dialogue != null)
            {
                hintFinished = false;
                if (!dialogue.ExecuteBlock(hidingHint, onComplete: () => hintFinished = true))
                    hintFinished = true;
            }
        }
        else
        {
            if (!IsHiding()) yield return Say(hidingHint);
            ShowTask("Task: Hide in the pillow fort");
        }
        yield return new WaitUntil(IsHiding);
        HideTask();
        SetFortBlocked(true);
        if (!hintFinished) yield return new WaitUntil(() => hintFinished);

        yield return PlaySound(doorAudio, doorOpen, 0.55f);
        yield return PlaySound(doorAudio, doorClose, 0.55f);
        if (usesWaypoints)
        {
            yield return WalkArrivalRoute();
        }
        else
        {
            santa.gameObject.SetActive(true);
            yield return MoveSanta(fireplaceStandingSpot.position, arrivalWalkSpeed, 0.08f, 0.48f);
            santa.rotation = fireplaceStandingSpot.rotation;
        }
        SetFortBlocked(false);
        ShowTask("Task: Go greet Santa");
        yield return new WaitUntil(() => Near(santa.position, greetingRadius));
        HideTask();
        yield return Say(greeting);

        if (santaReaction != null) santaAudio.PlayOneShot(santaReaction);
        yield return TurnSanta();
        yield return ShowFaceCut();
        ShowTask("Task: Run back to the pillow fort!");
        var spawn = FindMazeSpawn();
        if (pillowFortMaze != null && mainArea != null && spawn != null)
        {
            yield return FollowPlayerToFort();
            yield return new WaitUntil(IsHiding);
            HideTask();
            yield return EnterMaze(spawn);
        }
        else
        {
            yield return new WaitUntil(IsHiding);
            HideTask();
            SetFortBlocked(true);
            if (chaseWaypoints != null && chaseWaypoints.Length > 0)
                yield return RunChaseRoute();
            else
                yield return MoveSanta(hidingSpot.position, chaseSpeed, fortStopDistance, 1f);
        }

        if (whisperNoise != null && whisperAudio != null && Camera.main != null)
        {
            var listener = Camera.main.transform;
            whisperAudio.transform.position = listener.position - listener.forward * 0.9f;
            whisperAudio.PlayOneShot(whisperNoise);
        }
        yield return Say(whisper, whisperDialogue);
        SetFortBlocked(false);
    }

    private IEnumerator FollowPlayerToFort()
    {
        int waypointIndex = 0;
        bool running = false;
        Physics.SyncTransforms();
        while (!Near(hidingSpot.position, fortSafetyRadius))
        {
            var toPlayer = Vector3.ProjectOnPlane(player.transform.position - santa.position, Vector3.up);
            float permittedStep = Mathf.Max(0f, toPlayer.magnitude - chasePlayerGap);
            while (waypointIndex < chaseWaypoints.Length &&
                   FlatDistance(santa.position, chaseWaypoints[waypointIndex].position) < 0.06f)
                waypointIndex++;

            var offset = toPlayer.normalized * permittedStep;
            if (!ChasePathIsClear(offset) && waypointIndex < chaseWaypoints.Length)
                offset = Vector3.ProjectOnPlane(chaseWaypoints[waypointIndex].position - santa.position, Vector3.up);

            var step = Vector3.ClampMagnitude(offset, Mathf.Min(chaseSpeed * Time.deltaTime, permittedStep));
            bool moving = step.sqrMagnitude > 0.000001f && ChasePathIsClear(step);
            if (moving != running)
            {
                running = moving;
                santaAnimator.SetBool("Running", running);
                if (running) StartFootsteps();
                else StopFootsteps();
            }
            var facing = moving ? step : toPlayer;
            if (facing.sqrMagnitude > 0.000001f)
                santa.rotation = Quaternion.RotateTowards(santa.rotation, Quaternion.LookRotation(facing), 540f * Time.deltaTime);
            if (moving) santa.position += step;
            yield return null;
        }
        StopFootsteps();
        santaAnimator.SetBool("Running", false);
    }

    private bool ChasePathIsClear(Vector3 offset)
    {
        if (offset.sqrMagnitude < 0.000001f) return true;
        var lower = santa.position + Vector3.up * 0.45f;
        var upper = santa.position + Vector3.up * 1.31f;
        foreach (var hit in Physics.CapsuleCastAll(lower, upper, 0.29f, offset.normalized,
                     offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            var obstacle = hit.collider.transform;
            if (obstacle.IsChildOf(santa) || obstacle.IsChildOf(player.transform)) continue;
            if (hit.collider.bounds.max.y > santa.position.y + 0.2f) return false;
        }
        return true;
    }

    private Transform FindMazeSpawn()
    {
        if (pillowFortMaze == null) return null;

        var root = pillowFortMaze.transform;
        if (mazeSpawn != null && mazeSpawn.IsChildOf(root)) return mazeSpawn;
        foreach (var child in pillowFortMaze.GetComponentsInChildren<Transform>(true))
            if (child != root && child.name == "Spawn") return child;

        Debug.LogWarning("Assign a maze spawn inside Pillow Fort Maze or add a child named Spawn.", this);
        return null;
    }

    private IEnumerator EnterMaze(Transform spawn)
    {
        mazeMovementWasLocked = player.disableMovement;
        player.disableMovement = true;
        var overlay = new GameObject("Maze Transition Fade", typeof(RectTransform), typeof(Canvas));
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        mazeFade = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = mazeFade.rectTransform;
        rect.SetParent(overlay.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        mazeFade.raycastTarget = false;
        yield return FadeMaze(0f, 1f);

        SetFortBlocked(false);
        if (breathingAudio != null) breathingAudio.Stop();
        santa.gameObject.SetActive(false);
        mainArea.SetActive(false);
        pillowFortMaze.SetActive(true);
        ApplyMazeLighting();
        if (mazeGlobalFog != null) mazeGlobalFog.enabled = true;

        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = spawn.position;
        player.CrouchImmediately();
        controller.enabled = true;
        Physics.SyncTransforms();
        yield return FadeMaze(1f, 0f);
        ClearMazeFade();
    }

    private static void ApplyMazeLighting()
    {
        // Match the standalone maze scene's environment.
        var background = new Color(0.035f, 0.025f, 0.022f);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.28f, 0.235f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.12f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = background;
        RenderSettings.fogDensity = 0.055f;

        var camera = Camera.main;
        if (camera == null) return;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
    }

    private IEnumerator FadeMaze(float from, float to)
    {
        for (float elapsed = 0f; elapsed < mazeFadeDuration; elapsed += Time.deltaTime)
        {
            mazeFade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(from, to, elapsed / mazeFadeDuration));
            yield return null;
        }
        mazeFade.color = new Color(0f, 0f, 0f, to);
    }

    private void ClearMazeFade()
    {
        if (mazeFade == null) return;
        Destroy(mazeFade.transform.parent.gameObject);
        mazeFade = null;
        if (player != null) player.disableMovement = mazeMovementWasLocked;
    }

    private IEnumerator WalkArrivalRoute()
    {
        var destination = santa.position;
        var finalRotation = santa.rotation;
        var first = arrivalWaypoints[0];
        santa.position = new Vector3(first.position.x, destination.y, first.position.z);
        var next = arrivalWaypoints.Length > 1 ? arrivalWaypoints[1].position : destination;
        var direction = Vector3.ProjectOnPlane(next - santa.position, Vector3.up);
        if (direction.sqrMagnitude > 0.001f) santa.rotation = Quaternion.LookRotation(direction);
        santa.gameObject.SetActive(true);
        foreach (var bone in santa.GetComponentsInChildren<Transform>())
        {
            if (bone.name != "mixamorig:Head") continue;
            santaHead = bone;
            break;
        }
        if (breathingAudio != null) breathingAudio.Play();

        for (int index = 1; index < arrivalWaypoints.Length; index++)
        {
            yield return LookAroundAtWaypoint(index - 1);
            yield return WalkTo(arrivalWaypoints[index].position);
        }
        yield return LookAroundAtWaypoint(arrivalWaypoints.Length - 1);
        yield return WalkTo(destination);
        santa.SetPositionAndRotation(destination, finalRotation);
    }

    private IEnumerator LookAroundAtWaypoint(int waypointIndex)
    {
        float direction = waypointIndex % 2 == 0 ? 1f : -1f;
        for (float elapsed = 0f; elapsed < arrivalWaypointWait; elapsed += Time.deltaTime)
        {
            float phase = Mathf.PI * elapsed / arrivalWaypointWait;
            arrivalLookYaw = direction * arrivalLookAngle * Mathf.Sin(phase) * Mathf.Sin(2f * phase);
            yield return null;
        }
        arrivalLookYaw = 0f;
    }

    private void LateUpdate()
    {
        RestoreHeadRotation();
        if (santaHead == null || Mathf.Approximately(arrivalLookYaw, 0f)) return;

        animatedHeadRotation = santaHead.localRotation;
        Vector3 yawAxis = santaHead.parent.InverseTransformDirection(Vector3.up);
        adjustedHeadRotation = Quaternion.AngleAxis(arrivalLookYaw, yawAxis) * animatedHeadRotation;
        santaHead.localRotation = adjustedHeadRotation;
        headOffsetApplied = true;
    }

    private void RestoreHeadRotation()
    {
        if (headOffsetApplied && santaHead != null &&
            Quaternion.Angle(santaHead.localRotation, adjustedHeadRotation) < 0.01f)
            santaHead.localRotation = animatedHeadRotation;
        headOffsetApplied = false;
    }

    private IEnumerator WalkTo(Vector3 destination)
    {
        santaAnimator.CrossFadeInFixedTime("Base Layer.Walk", 0.12f);
        StartFootsteps();
        yield return MoveTo(destination, arrivalWalkSpeed, 0f);
        StopFootsteps();
        santaAnimator.CrossFadeInFixedTime("Base Layer.Idle", 0.12f);
    }

    private IEnumerator RunChaseRoute()
    {
        santaAnimator.SetBool("Running", true);
        StartFootsteps();
        foreach (var waypoint in chaseWaypoints)
            yield return MoveTo(waypoint.position, chaseSpeed, 0f);
        StopFootsteps();
        santaAnimator.SetBool("Running", false);
    }

    private void SetFortBlocked(bool blocked)
    {
        if (playerBlocker != null) playerBlocker.SetActive(blocked);
    }

    private IEnumerator MoveSanta(Vector3 target, float speed, float stoppingDistance, float animationSpeed)
    {
        santaAnimator.speed = animationSpeed;
        santaAnimator.SetBool("Running", true);
        StartFootsteps();
        yield return MoveTo(target, speed, stoppingDistance);
        StopFootsteps();
        santaAnimator.SetBool("Running", false);
        santaAnimator.speed = 1f;
    }

    private IEnumerator MoveTo(Vector3 target, float speed, float stoppingDistance)
    {
        while (FlatDistance(santa.position, target) > stoppingDistance + 0.01f)
        {
            Vector3 offset = Vector3.ProjectOnPlane(target - santa.position, Vector3.up);
            santa.rotation = Quaternion.RotateTowards(santa.rotation, Quaternion.LookRotation(offset), 540f * Time.deltaTime);
            santa.position += offset.normalized * Mathf.Min(speed * Time.deltaTime, offset.magnitude - stoppingDistance);
            yield return null;
        }
    }

    private void StartFootsteps()
    {
        if (heavyFootsteps == null) return;
        santaAudio.clip = heavyFootsteps;
        santaAudio.loop = true;
        santaAudio.Play();
    }

    private void StopFootsteps()
    {
        if (heavyFootsteps == null) return;
        santaAudio.Stop();
        santaAudio.loop = false;
    }

    private IEnumerator ShowFaceCut()
    {
        if (faceCutDuration <= 0f || Camera.main == null || santaFace == null) yield break;
        cutCamera = Camera.main;
        previousCameraPosition = cutCamera.transform.localPosition;
        previousCameraRotation = cutCamera.transform.localRotation;
        previousNearClip = cutCamera.nearClipPlane;
        previousMovementLock = player.disableMovement;
        player.disableMovement = true;
        cutCamera.nearClipPlane = 0.03f;
        for (float elapsed = 0; elapsed < faceCutDuration; elapsed += Time.deltaTime)
        {
            var target = santaFace.position + Vector3.up * faceCutHeightOffset;
            var position = target + santa.forward * faceCutDistance;
            cutCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            yield return null;
        }
        RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (cutCamera == null) return;
        cutCamera.transform.localPosition = previousCameraPosition;
        cutCamera.transform.localRotation = previousCameraRotation;
        cutCamera.nearClipPlane = previousNearClip;
        cutCamera = null;
        if (player != null) player.disableMovement = previousMovementLock;
    }

    private void OnDisable()
    {
        ClearMazeFade();
        arrivalLookYaw = 0f;
        RestoreHeadRotation();
        RestoreCamera();
        SetFortBlocked(false);
        if (breathingAudio != null) breathingAudio.Stop();
    }

    private IEnumerator TurnSanta()
    {
        var from = santa.rotation;
        var direction = Vector3.ProjectOnPlane(player.transform.position - santa.position, Vector3.up);
        if (direction.sqrMagnitude < 0.001f) yield break;
        var to = Quaternion.LookRotation(direction);
        const float duration = 0.19f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            santa.rotation = Quaternion.Slerp(from, to, elapsed / duration);
            yield return null;
        }
        santa.rotation = to;
    }

    private bool IsHiding() => Near(hidingSpot.position, hidingRadius);
    private bool Near(Vector3 target, float radius) =>
        Mathf.Abs(player.transform.position.y - target.y) < 1.25f &&
        FlatDistance(player.transform.position, target) <= radius;
    private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

    private IEnumerator Say(Block block, Flowchart flowchart = null)
    {
        bool done = false;
        if (block != null && (flowchart != null ? flowchart : dialogue).ExecuteBlock(block, onComplete: () => done = true))
            yield return new WaitUntil(() => done);
    }

    private static IEnumerator PlaySound(AudioSource source, AudioClip clip, float fallbackDelay)
    {
        if (clip != null) source.PlayOneShot(clip);
        yield return new WaitForSeconds(clip != null ? clip.length : fallbackDelay);
    }

    private void ShowTask(string message)
    {
        taskLabel.text = message;
        taskLabel.gameObject.SetActive(true);
    }

    private void HideTask() => taskLabel.gameObject.SetActive(false);
}
