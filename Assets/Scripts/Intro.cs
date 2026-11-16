using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

[DefaultExecutionOrder(100)]
public sealed class Intro : MonoBehaviour
{
    [SerializeField] private FirstPersonController player;
    [SerializeField] private ParentsArrival arrival;
    [SerializeField] private Transform paper;
    [SerializeField] private Transform drawingCrayon;
    [SerializeField] private GameObject controls;
    [SerializeField] private Text task;
    [SerializeField, Min(0f)] private float controlsDuration = 7f;
    [Header("Opening View")]
    [Tooltip("Offset from the paper in its local axes, measured in world units.")]
    [SerializeField] private Vector3 lookTargetOffset;
    [Tooltip("Pitch (X) and yaw (Y) offsets in degrees from the look target.")]
    [SerializeField] private Vector2 viewPitchAndYaw;
    [Header("Drawing Crayon")]
    [Tooltip("Crayon position relative to the paper, in camera-space units.")]
    [SerializeField] private Vector3 crayonPositionOffset = new Vector3(0.075f, -0.06f, -0.04f);
    [SerializeField] private Vector3 crayonRotation = new Vector3(18f, -12f, -24f);
    [Tooltip("Minimum gap between the crayon and the paper during the drawing animation.")]
    [SerializeField, Min(0f)] private float crayonPaperClearance = 0.001f;

    private bool ownsMovementLock;
    private bool previousMovementLock;

    private IEnumerator Start()
    {
        if (player.RestoredFromCheckpoint) yield break;
        arrival.Arrived += ShowArrival;
        arrival.DialogueFinished += HideTask;

        player.BeginCrouchedView(paper.position + paper.TransformDirection(lookTargetOffset), viewPitchAndYaw);
        previousMovementLock = player.disableMovement;
        player.disableMovement = true;
        ownsMovementLock = true;
        drawingCrayon.SetParent(Camera.main.transform, false);
        drawingCrayon.gameObject.SetActive(true);
        var crayonMesh = drawingCrayon.GetComponentInChildren<MeshFilter>();
        var paperMesh = paper.GetComponent<MeshFilter>();
        var paperBounds = paperMesh.sharedMesh.bounds;
        var paperSurface = paper.TransformPoint(new Vector3(paperBounds.center.x, paperBounds.max.y, paperBounds.center.z));
        float elapsed = 0;
        while (!arrival.HasArrived)
        {
            elapsed += Time.deltaTime;
            float stroke = Mathf.Sin(elapsed * 8f);
            var drawingPosition = drawingCrayon.parent.InverseTransformPoint(paper.position) + crayonPositionOffset;
            drawingCrayon.localPosition = drawingPosition + new Vector3(stroke * 0.045f,
                Mathf.Sin(elapsed * 12f) * 0.012f, 0f);
            drawingCrayon.localRotation = Quaternion.Euler(crayonRotation + new Vector3(stroke * 5f, 0f, stroke * 8f));
            KeepCrayonAbovePaper(crayonMesh, paperSurface);
            yield return null;
        }
    }

    private void KeepCrayonAbovePaper(MeshFilter crayonMesh, Vector3 paperSurface)
    {
        var normal = paper.up;
        var meshTransform = crayonMesh.transform;
        var bounds = crayonMesh.sharedMesh.bounds;
        var extent = bounds.extents;
        float projectedExtent =
            Mathf.Abs(Vector3.Dot(normal, meshTransform.TransformVector(Vector3.right * extent.x))) +
            Mathf.Abs(Vector3.Dot(normal, meshTransform.TransformVector(Vector3.up * extent.y))) +
            Mathf.Abs(Vector3.Dot(normal, meshTransform.TransformVector(Vector3.forward * extent.z)));
        float lowestPoint = Vector3.Dot(normal, meshTransform.TransformPoint(bounds.center) - paperSurface) - projectedExtent;
        drawingCrayon.position += normal * Mathf.Max(0f, crayonPaperClearance - lowestPoint);
    }

    private void ShowArrival()
    {
        DropCrayon();
        ReleaseMovementLock();
        controls.SetActive(true);
        task.gameObject.SetActive(true);
        StartCoroutine(HideControls());
    }

    private IEnumerator HideControls()
    {
        yield return new WaitForSeconds(controlsDuration);
        controls.SetActive(false);
    }

    private void HideTask() => task.gameObject.SetActive(false);

    private void LateUpdate()
    {
        if (arrival.HasArrived) arrival.FacePlayer(player.transform.position);
    }

    private void DropCrayon()
    {
        if (!ownsMovementLock || drawingCrayon == null) return;

        // Detach from the camera without changing the pose from the last drawing stroke.
        drawingCrayon.SetParent(transform, true);
        var mesh = drawingCrayon.GetComponentInChildren<MeshFilter>();
        if (mesh == null || mesh.sharedMesh == null) return;

        var collider = mesh.gameObject.AddComponent<BoxCollider>();
        collider.center = mesh.sharedMesh.bounds.center;
        collider.size = mesh.sharedMesh.bounds.size;
        Physics.IgnoreCollision(collider, player.GetComponent<CharacterController>());

        var body = mesh.gameObject.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void ReleaseMovementLock()
    {
        if (!ownsMovementLock) return;
        ownsMovementLock = false;
        player.disableMovement = previousMovementLock;
    }

    private void OnDestroy()
    {
        if (arrival != null)
        {
            arrival.Arrived -= ShowArrival;
            arrival.DialogueFinished -= HideTask;
        }
        ReleaseMovementLock();
    }
}
