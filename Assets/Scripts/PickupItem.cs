
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    public bool canPickup = true;

    [Header("Sound Effects")]
    public AudioClip pickupSound;
    public AudioClip dropSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Header("Hold Settings")]

    [Tooltip("Individual position offset while held.")]
    public Vector3 holdPositionOffset = Vector3.zero;

    [Tooltip("Individual rotation offset while held.")]
    public Vector3 holdRotationOffset = Vector3.zero;

    [Min(0.01f)]
    public float holdScaleMultiplier = 1f;

    [Header("Exact held pose (optional)")]
    public bool useExactHeldPose;
    public Vector3 heldLocalPosition;
    public Vector3 heldLocalScale = Vector3.one;
    [Tooltip("Optional torch light toggled with F while this item is held.")]
    public Light heldTorchLight;
    [Header("Torch toggle sounds")]
    [SerializeField] private AudioClip torchOnSound;
    [SerializeField] private AudioClip torchOffSound;
    [SerializeField, Range(0f, 1f)] private float torchToggleVolume = 1f;
    private AudioSource torchToggleAudio;
    [Header("Torch collision")]
    [SerializeField, Min(0.01f)] private float heldCollisionPadding = 0.025f;
    private Transform holdingPlayer;
    private readonly Collider[] obstructionBuffer = new Collider[64];
    private Transform heldCollisionPivot;
    [SerializeField, Min(.01f)] private float heldReturnTime = .15f;
    private HeldTorchCollision heldCollision;

    private void OnDestroy()
    {
        if (heldCollisionPivot != null) Destroy(heldCollisionPivot.gameObject);
    }

    private bool IsOwnCollider(Collider collider)
    {
        return collider.transform.IsChildOf(transform) ||
            (holdingPlayer != null && collider.transform.IsChildOf(holdingPlayer));
    }

    private void Update()
    {
        if (isHeld && heldTorchLight != null && Time.timeScale > 0f && Input.GetKeyDown(KeyCode.F))
        {
            bool turnOn = !heldTorchLight.gameObject.activeSelf || !heldTorchLight.enabled;
            heldTorchLight.gameObject.SetActive(true);
            heldTorchLight.enabled = turnOn;
            AudioClip clip = turnOn ? torchOnSound : torchOffSound;
            if (clip != null)
            {
                if (torchToggleAudio == null)
                {
                    torchToggleAudio = gameObject.AddComponent<AudioSource>();
                    torchToggleAudio.playOnAwake = false;
                    torchToggleAudio.spatialBlend = 0f;
                    var playerAudio = holdingPlayer != null ? holdingPlayer.GetComponent<AudioSource>() : null;
                    if (playerAudio != null) torchToggleAudio.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
                }
                torchToggleAudio.Stop();
                torchToggleAudio.PlayOneShot(clip, torchToggleVolume);
            }
        }
    }

    [Header("Drop Settings")]

    [Tooltip(
        "Individual drop offset relative to the player. " +
        "X = right/left, Y = up/down, Z = forward/back."
    )]
    public Vector3 dropPositionOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Rigidbody rb;
    private Collider[] itemColliders;
    private bool[] originalColliderStates;

    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Quaternion originalWorldRotation;

    private bool originalUseGravity;
    private bool originalIsKinematic;
    private RigidbodyConstraints originalConstraints;

    private bool isHeld;

    public bool IsHeld => isHeld;
    private string checkpointId;
    // Capture before pickup reparents the item into the player's hand.
    public string CheckpointId
    {
        get
        {
            if (!string.IsNullOrEmpty(checkpointId)) return checkpointId;
            string path = "";
            for (Transform node = transform; node != null; node = node.parent)
                path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            checkpointId = path;
            return checkpointId;
        }
    }

    private void Awake()
    {
        _ = CheckpointId;
        rb = GetComponent<Rigidbody>();

        if (heldTorchLight != null)
        {
            // Fit the solid collision box to the mesh; the imported collider can
            // otherwise leave parts of the visible lantern outside its bounds.
            var solidBox = GetComponent<BoxCollider>();
            var mesh = GetComponent<MeshFilter>();
            if (solidBox != null && mesh != null && mesh.sharedMesh != null)
            {
                solidBox.center = mesh.sharedMesh.bounds.center;
                solidBox.size = mesh.sharedMesh.bounds.size;
            }
        }

        itemColliders =
            GetComponentsInChildren<Collider>(true);

        originalColliderStates =
            new bool[itemColliders.Length];

        for (int i = 0; i < itemColliders.Length; i++)
        {
            originalColliderStates[i] =
                itemColliders[i].enabled;
        }

        originalParent = transform.parent;
        originalLocalScale = transform.localScale;
        originalWorldRotation = transform.rotation;

        originalUseGravity = rb.useGravity;
        originalIsKinematic = rb.isKinematic;
        originalConstraints = rb.constraints;
    }

    public void Interact()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "[PickupItem] Interact: " + gameObject.name +
                " | canPickup=" + canPickup +
                " | isHeld=" + isHeld
            );
        }

        if (!canPickup || isHeld)
            return;

        PickupController controller =
            FindObjectOfType<PickupController>();

        if (controller == null)
        {
            Debug.LogWarning(
                "[PickupItem] PickupController not found!"
            );
            return;
        }

        controller.Pickup(this);

        if (showDebugLogs)
        {
            if (controller.HeldItem == this && isHeld)
            {
                Debug.Log(
                    "[PickupItem] Pickup successful: " +
                    gameObject.name
                );
            }
            else
            {
                Debug.LogWarning(
                    "[PickupItem] Pickup request was not completed."
                );
            }
        }
    }

    public void OnPickup(Transform holdPoint)
    {
        if (isHeld || holdPoint == null)
            return;

        isHeld = true;
        var holder = holdPoint.GetComponentInParent<PickupController>();
        holdingPlayer = holder != null ? holder.transform : holdPoint.root;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (heldTorchLight != null) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = true;
        rb.useGravity = false;

        foreach (Collider col in itemColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        // Preserve current world size.
        if (heldTorchLight != null)
        {
            heldCollisionPivot = new GameObject("Torch Hold Collision Offset").transform;
            heldCollisionPivot.SetParent(holdPoint, false);
            transform.SetParent(heldCollisionPivot, true);
        }
        else transform.SetParent(holdPoint, true);

        // The PickupController applies the
        // individual hold position offset.
        transform.localPosition = Vector3.zero;

        transform.localRotation =
            Quaternion.Euler(holdRotationOffset);

        transform.localScale *= holdScaleMultiplier;
        if (useExactHeldPose)
        {
            transform.localPosition = heldLocalPosition;
            transform.localScale = heldLocalScale;
        }

        if (heldTorchLight != null)
        {
            heldCollision = gameObject.AddComponent<HeldTorchCollision>();
            heldCollision.Initialize(holdingPlayer, heldCollisionPivot, heldCollisionPadding, heldReturnTime);
        }

        Physics.SyncTransforms();

        if (showDebugLogs)
        {
            Debug.Log(
                "[PickupItem] Picked up: " +
                gameObject.name +
                " | World Scale: " +
                transform.lossyScale +
                " | Hold Position Offset: " +
                holdPositionOffset
            );
        }

        PlaySound(pickupSound);
    }

    public void OnDrop(Vector3 dropPosition)
    {
        if (!isHeld)
            return;

        // Detach from the camera.
        transform.SetParent(originalParent, true);
        if (heldCollision != null) { heldCollision.Release(); Destroy(heldCollision); heldCollision = null; }
        if (heldCollisionPivot != null)
        {
            Destroy(heldCollisionPivot.gameObject);
            heldCollisionPivot = null;
        }

        // Restore original transform.
        transform.localScale = originalLocalScale;
        transform.position = dropPosition;
        transform.rotation = originalWorldRotation;

        // Restore original physics settings.
        rb.isKinematic = originalIsKinematic;
        rb.useGravity = originalUseGravity;
        rb.constraints = originalConstraints;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Restore original collider states.
        for (int i = 0; i < itemColliders.Length; i++)
        {
            if (itemColliders[i] != null)
            {
                itemColliders[i].enabled =
                    originalColliderStates[i];
            }
        }

        // Ensure the required root collider is enabled.
        Collider mainCollider = GetComponent<Collider>();

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }

        if (heldTorchLight != null)
        {
            // Dynamic bodies need solid convex colliders and continuous collision
            // detection, including when dropped very close to the floor.
            foreach (var collider in itemColliders)
            {
                if (collider == null) continue;
                var mesh = collider as MeshCollider;
                if (mesh != null)
                {
                    // The imported root mesh collider has no mesh. The fitted
                    // solid box supplies drop contacts without an empty collider.
                    if (mesh.sharedMesh == null) { mesh.enabled = false; continue; }
                    mesh.convex = true;
                }
                collider.isTrigger = false;
                collider.enabled = true;
            }
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Physics.SyncTransforms();
            var view = Camera.main;
            if (view != null)
            {
                Vector3 origin = view.transform.position;
                Vector3 flat = Vector3.ProjectOnPlane(transform.position - origin, Vector3.up);
                float distance = flat.magnitude;
                if (distance > .001f)
                {
                    float allowed = distance;
                    foreach (var hit in Physics.RaycastAll(origin, flat / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                        if (!IsOwnCollider(hit.collider)) allowed = Mathf.Min(allowed, Mathf.Max(.05f, hit.distance - .35f));
                    Vector3 safe = origin + flat.normalized * allowed;
                    transform.position = new Vector3(safe.x, transform.position.y, safe.z);
                }
                // Drop on top of the floor even when the crouched camera's
                // drop-height offset would otherwise place the body below it.
                Vector3 rayOrigin = new Vector3(transform.position.x, origin.y + .03f, transform.position.z);
                float floor = float.NegativeInfinity;
                foreach (var hit in Physics.RaycastAll(rayOrigin, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                    if (!IsOwnCollider(hit.collider) && hit.normal.y > .4f) floor = Mathf.Max(floor, hit.point.y);
                Physics.SyncTransforms();
                if (!float.IsNegativeInfinity(floor))
                {
                    float bottom = float.PositiveInfinity;
                    foreach (var col in itemColliders) if (col != null && col.enabled) bottom = Mathf.Min(bottom, col.bounds.min.y);
                    if (bottom < floor + .025f) transform.position += Vector3.up * (floor + .025f - bottom);
                }
                Physics.SyncTransforms();
            }
            // Resolve an initial overlap before gravity starts. This also handles
            // drops made while crouched or facing a pillow wall.
            for (int iteration = 0; iteration < 8; iteration++)
            {
                bool moved = false;
                foreach (var own in itemColliders)
                {
                    if (own == null || !own.enabled) continue;
                    int count = Physics.OverlapBoxNonAlloc(own.bounds.center, own.bounds.extents + Vector3.one * .02f,
                        obstructionBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        var other = obstructionBuffer[i];
                        if (IsOwnCollider(other)) continue;
                        if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 direction, out float depth))
                        {
                            transform.position += direction * (depth + .01f);
                            Physics.SyncTransforms();
                            moved = true;
                        }
                    }
                }
                if (!moved) break;
            }
            rb.WakeUp();
        }

        isHeld = false;

        // Synchronize physics after dropping.
        Physics.SyncTransforms();

        if (showDebugLogs)
        {
            Debug.Log(
                "[PickupItem] Dropped: " +
                gameObject.name +
                " | Position: " +
                transform.position +
                " | Main Collider Enabled: " +
                (mainCollider != null &&
                 mainCollider.enabled) +
                " | Layer: " +
                LayerMask.LayerToName(gameObject.layer)
            );
        }

        PlaySound(dropSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(
                clip,
                transform.position,
                soundVolume
            );
        }
    }
}
