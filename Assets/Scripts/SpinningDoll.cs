using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SpinningDoll : MonoBehaviour
{
    [Header("Continuous spin")]
    [Tooltip("Degrees per second. Negative values reverse direction; zero pauses rotation.")]
    [SerializeField] private float spinSpeed = 30f;
    [Tooltip("World-space spin axis. Rotation stays around this object's own pivot.")]
    [SerializeField] private Vector3 spinAxis = Vector3.up;
    [Header("Nearby music")]
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0, 1)] private float volume = .7f;
    [SerializeField, Min(.01f)] private float fullVolumeDistance = .4f;
    [SerializeField, Min(.02f)] private float audibleDistance = 3f;
    private AudioSource source;

    private void Awake() { source = GetComponent<AudioSource>(); Configure(); }
    private void OnEnable()
    {
        if (source == null) source = GetComponent<AudioSource>();
        Configure();
        if (music != null) source.Play();
    }
    private void Update()
    {
        if (spinAxis.sqrMagnitude > .0001f)
            transform.Rotate(spinAxis.normalized, spinSpeed * Time.deltaTime, Space.World);
    }
    private void OnDisable() { if (source != null) source.Stop(); }
    private void OnValidate()
    {
        audibleDistance = Mathf.Max(fullVolumeDistance + .01f, audibleDistance);
        source = GetComponent<AudioSource>();
        if (source != null) Configure();
    }
    private void Configure()
    {
        source.playOnAwake = false;
        source.loop = true;
        source.clip = music;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = Mathf.Max(fullVolumeDistance + .01f, audibleDistance);
    }
}
