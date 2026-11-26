using UnityEngine;

// A suspended pose with a small, slow swing; the neck stays at the rope attachment.
[DefaultExecutionOrder(200)]
public sealed class HangingBodySway : MonoBehaviour
{
    [SerializeField] private Transform hips;
    [SerializeField] private Transform neck;
    [SerializeField, Range(0, 3)] private float swayDegrees = .8f;
    [SerializeField, Min(1)] private float cycleSeconds = 7f;
    [SerializeField] private float phaseOffset;
    private Vector3 restPosition, attachment;
    private Quaternion restRotation;
    private bool initialized;

    private void Awake()
    {
        if (hips == null || neck == null) { enabled = false; return; }
        restPosition = hips.localPosition;
        restRotation = hips.localRotation;
        attachment = transform.InverseTransformPoint(neck.position);
        initialized = true;
    }
    private void LateUpdate()
    {
        EvaluatePose(Time.time);
    }
    private void EvaluatePose(float time)
    {
        if (!initialized) return;
        hips.localPosition = restPosition;
        hips.localRotation = restRotation;
        float angle = Mathf.Sin(time * Mathf.PI * 2 / Mathf.Max(1, cycleSeconds) + phaseOffset) * swayDegrees;
        hips.rotation = Quaternion.AngleAxis(angle, transform.forward) * hips.rotation;
        hips.position += transform.TransformPoint(attachment) - neck.position;
    }
    private void OnDisable()
    {
        if (!initialized || hips == null) return;
        hips.localPosition = restPosition;
        hips.localRotation = restRotation;
    }
}
