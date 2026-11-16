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

    private void Awake() => santa.gameObject.SetActive(false);

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

        float walking = 0f;
        var lastPosition = player.transform.position;
        while (!IsHiding() && walking < hintAfterWalkingSeconds)
        {
            yield return null;
            var position = player.transform.position;
            if (Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).sqrMagnitude > 0.000001f)
                walking += Time.deltaTime;
            lastPosition = position;
        }
        if (!IsHiding()) yield return Say(hidingHint);
        ShowTask("Task: Hide in the pillow fort");
        yield return new WaitUntil(IsHiding);
        HideTask();

        yield return PlaySound(doorAudio, doorOpen, 0.55f);
        yield return PlaySound(doorAudio, doorClose, 0.55f);
        santa.gameObject.SetActive(true);
        yield return MoveSanta(fireplaceStandingSpot.position, arrivalWalkSpeed, 0.08f, 0.48f);
        santa.rotation = fireplaceStandingSpot.rotation;
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
        yield return MoveSanta(hidingSpot.position, chaseSpeed, fortStopDistance, 1f);

        if (whisperNoise != null && whisperAudio != null && Camera.main != null)
        {
            var listener = Camera.main.transform;
            whisperAudio.transform.position = listener.position - listener.forward * 0.9f;
            whisperAudio.PlayOneShot(whisperNoise);
        }
        yield return Say(whisper, whisperDialogue);
    }

    private IEnumerator MoveSanta(Vector3 target, float speed, float stoppingDistance, float animationSpeed)
    {
        santaAnimator.speed = animationSpeed;
        santaAnimator.SetBool("Running", true);
        if (heavyFootsteps != null)
        {
            santaAudio.clip = heavyFootsteps;
            santaAudio.loop = true;
            santaAudio.Play();
        }
        while (FlatDistance(santa.position, target) > stoppingDistance + 0.01f)
        {
            Vector3 offset = Vector3.ProjectOnPlane(target - santa.position, Vector3.up);
            santa.rotation = Quaternion.RotateTowards(santa.rotation, Quaternion.LookRotation(offset), 540f * Time.deltaTime);
            santa.position += offset.normalized * Mathf.Min(speed * Time.deltaTime, offset.magnitude - stoppingDistance);
            yield return null;
        }
        if (heavyFootsteps != null)
        {
            santaAudio.Stop();
            santaAudio.loop = false;
        }
        santaAnimator.SetBool("Running", false);
        santaAnimator.speed = 1f;
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

    private void OnDisable() => RestoreCamera();

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
