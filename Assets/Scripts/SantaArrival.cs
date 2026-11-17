using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class SantaArrival : MonoBehaviour
{
    [SerializeField] private FirstPersonController player;
    [SerializeField] private GameObject pillowMaze;
    [SerializeField] private Transform hidingSpot;
    [SerializeField] private Transform santa;
    [SerializeField] private Animator santaAnimator;
    [SerializeField] private Transform fireplaceStandingSpot;
    [SerializeField] private Transform santaFace;
    [SerializeField] private Transform[] arrivalWaypoints;
    [SerializeField] private Transform[] chaseWaypoints;
    [SerializeField] private GameObject playerBlocker;
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
        pillowMaze.SetActive(false);
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
        yield return new WaitUntil(IsHiding);
        HideTask();
        SetFortBlocked(true);
        if (chaseWaypoints != null && chaseWaypoints.Length > 0)
            yield return RunChaseRoute();
        else
            yield return MoveSanta(hidingSpot.position, chaseSpeed, fortStopDistance, 1f);

        if (whisperNoise != null && whisperAudio != null && Camera.main != null)
        {
            var listener = Camera.main.transform;
            whisperAudio.transform.position = listener.position - listener.forward * 0.9f;
            whisperAudio.PlayOneShot(whisperNoise);
        }
        yield return Say(whisper, whisperDialogue);
        SetFortBlocked(false);
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
