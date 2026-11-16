using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Interaction UI")]
        [SerializeField] private Image squareUI;

        [Header("UI Canvas Root")]
        [SerializeField] private GameObject uiRoot;

        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Zoom Settings")]
        [SerializeField] private bool enableZoom = true;
        [SerializeField, Range(10f, 90f)] private float zoomFOV = 35f;
        [SerializeField] private float zoomSpeed = 10f;

        private float originalFOV;

        [Header("Crouch")]
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.05f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.1f;
        [SerializeField, Min(0f)] private float crouchRunSpeed = 2.5f;
        [SerializeField, Min(0f)] private float crouchTransitionTime = 0.18f;
        private float standingHeight;
        private Vector3 standingCenter;
        private float crouchCameraDrop;
        private bool crouchRequested;
        public bool IsCrouching { get; private set; }
        public bool RestoredFromCheckpoint { get; set; }

        public void BeginCrouchedView(Vector3 target, Vector2 pitchAndYaw)
        {
            crouchRequested = true;
            float height = Mathf.Clamp(crouchHeight, Mathf.Min(standingHeight, 2f * m_CharacterController.radius), standingHeight);
            crouchCameraDrop = standingHeight - height;
            m_CharacterController.height = height;
            m_CharacterController.center = standingCenter - Vector3.up * (crouchCameraDrop * 0.5f);
            IsCrouching = crouchCameraDrop > 0f;
            m_Camera.transform.localPosition = m_OriginalCameraPosition - Vector3.up * crouchCameraDrop;
            var direction = target - m_Camera.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                direction = Quaternion.AngleAxis(pitchAndYaw.y, Vector3.up) *
                    (Quaternion.LookRotation(direction) * Quaternion.Euler(pitchAndYaw.x, 0f, 0f) * Vector3.forward);
            var horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (horizontal.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(horizontal);
            if (direction.sqrMagnitude > 0.0001f)
                m_Camera.transform.rotation = Quaternion.LookRotation(direction);
            m_MouseLook.Init(transform, m_Camera.transform);
        }

        [SerializeField] private bool m_IsWalking;
        [SerializeField] private float m_WalkSpeed = 2f;
        [SerializeField] private float m_RunSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float m_RunstepLenghten = 0.7f;
        [SerializeField] private float m_JumpSpeed = 3f;
        [SerializeField] private float m_StickToGroundForce = 10f;
        [SerializeField] private float m_GravityMultiplier = 1f;
        [SerializeField] private MouseLook m_MouseLook;
        [SerializeField] private bool m_UseFovKick;
        [SerializeField] private FOVKick m_FovKick = new FOVKick();
        [SerializeField] private bool m_UseHeadBob;
        [SerializeField] private CurveControlledBob m_HeadBob = new CurveControlledBob();
        [SerializeField] private LerpControlledBob m_JumpBob = new LerpControlledBob();
        [SerializeField] private float m_StepInterval = 5f;
        [SerializeField] private AudioClip[] m_FootstepSounds;
        [SerializeField] private AudioClip m_JumpSound;
        [SerializeField] private AudioClip m_LandSound;

        private Camera m_Camera;
        private bool m_Jump;
        private bool suppressJumpUntilReleased = false;
        private bool isSeated = false;
        public bool IsSeated => isSeated;
        private Vector2 m_Input;
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;
        private float m_StepCycle;
        private float m_NextStep;
        private bool m_Jumping;
        private AudioSource m_AudioSource;

        private IInteractable currentInteractable;
        private PlayableDirector timelineDirector;
        private PickupController pickupController;

        // Movement lock for Timeline
        public bool disableMovement = false;

        [Header("Scripted Movement")]
        public bool scriptedMovement = false; // Keeps mouse look while suppressing player movement.

        private bool IsValidFloat(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private bool IsValidVector3(Vector3 value)
        {
            return IsValidFloat(value.x) &&
                   IsValidFloat(value.y) &&
                   IsValidFloat(value.z);
        }

        private void Start()
        {
            m_CharacterController = GetComponent<CharacterController>();
            standingHeight = m_CharacterController.height;
            standingCenter = m_CharacterController.center;
            m_Camera = Camera.main;

            if (m_Camera == null)
            {
                Debug.LogError(
                    "FirstPersonController: Main Camera not found!"
                );
                enabled = false;
                return;
            }

            originalFOV = m_Camera.fieldOfView;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;

            if (!IsValidVector3(m_OriginalCameraPosition))
            {
                m_OriginalCameraPosition = Vector3.zero;
                m_Camera.transform.localPosition =
                    m_OriginalCameraPosition;
            }

            m_FovKick.Setup(m_Camera);
            m_HeadBob.Setup(m_Camera, m_StepInterval);

            m_StepCycle = 0f;
            m_NextStep = m_StepCycle / 2f;
            m_Jumping = false;

            m_AudioSource = GetComponent<AudioSource>();
            pickupController = GetComponent<PickupController>();
            if (pickupController == null)
                Debug.LogWarning("FirstPersonController: Add PickupController to the player to enable carrying items.");
            m_MouseLook.Init(transform, m_Camera.transform);
            SetSensitivity(GameSettings.MouseSensitivity);

            // Hide interaction square initially
            if (squareUI != null)
                squareUI.enabled = false;

            // Hide and lock cursor during gameplay
            LockCursor();

            timelineDirector = FindObjectOfType<PlayableDirector>();

            if (timelineDirector != null)
            {
                timelineDirector.played += OnTimelineStart;
                timelineDirector.stopped += OnTimelineStop;
            }
        }

        private void OnDestroy()
        {
            if (timelineDirector != null)
            {
                timelineDirector.played -= OnTimelineStart;
                timelineDirector.stopped -= OnTimelineStop;
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnTimelineStart(PlayableDirector obj)
        {
            disableMovement = true;
            m_CharacterController.enabled = false;

            currentInteractable = null;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (uiRoot != null)
                uiRoot.SetActive(false);

            if (squareUI != null)
                squareUI.enabled = false;

            if (m_Camera != null)
                m_Camera.fieldOfView = originalFOV;
        }

        private void OnTimelineStop(PlayableDirector obj)
        {
            disableMovement = false;
            m_CharacterController.enabled = true;

            LockCursor();

            if (uiRoot != null)
                uiRoot.SetActive(true);

            if (squareUI != null)
                squareUI.enabled = false;
        }

        private void Update()
        {
            if (disableMovement || Time.timeScale == 0f)
            {
                m_Jump = false;
                currentInteractable = null;

                if (squareUI != null)
                    squareUI.enabled = false;

                return;
            }

            RotateView();
            HandleZoom();
            HandleInteraction();

            if (isSeated || scriptedMovement)
            {
                m_Jump = false;
                m_MoveDir = Vector3.zero;
                return;
            }

            // A minigame can consume Space without causing a buffered jump.
            if (Input.GetKeyDown(crouchKey))
                crouchRequested = !crouchRequested;

            if (IsCrouching || crouchRequested)
            {
                m_Jump = false;
            }
            else if (suppressJumpUntilReleased)
            {
                m_Jump = false;
                if (!Input.GetKey(KeyCode.Space))
                    suppressJumpUntilReleased = false;
            }
            else if (!m_Jump)
            {
                m_Jump =
                    CrossPlatformInputManager.GetButtonDown("Jump");
            }

            if (!m_PreviouslyGrounded &&
                m_CharacterController.isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayLandingSound();

                m_MoveDir.y = 0f;
                m_Jumping = false;
            }

            if (!m_CharacterController.isGrounded &&
                !m_Jumping &&
                m_PreviouslyGrounded)
            {
                m_MoveDir.y = 0f;
            }

            m_PreviouslyGrounded =
                m_CharacterController.isGrounded;
        }

        [Header("Interaction Detection")]
        [SerializeField, Range(0.01f, 0.5f)] private float interactionCastRadius = 0.18f;
        [SerializeField] private bool debugInteractionHits = true;

        // Uses a small sphere cast so objects can be targeted even when their
        // colliders are small or partly below the center of the screen.
        private void HandleInteraction()
        {
            currentInteractable = null;

            bool holdingItem = pickupController != null &&
                               pickupController.IsHoldingItem();

            Ray ray = new Ray(m_Camera.transform.position,
                              m_Camera.transform.forward);

            // Physics can lag one frame behind reparenting / repositioning.
            Physics.SyncTransforms();

            RaycastHit[] hits = Physics.SphereCastAll(
                ray,
                interactionCastRadius,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore
            );

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Collider firstBlockingCollider = null;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                // Ignore the player's own collider(s).
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                MonoBehaviour[] behaviours =
                    hit.collider.GetComponentsInParent<MonoBehaviour>();

                bool foundInteractable = false;

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null || !behaviour.isActiveAndEnabled)
                        continue;

                    IInteractable interactable = behaviour as IInteractable;
                    if (interactable == null)
                        continue;

                    DoorInteractable door = behaviour as DoorInteractable;
                    if (door != null && (!door.allowPlayerInteraction || door.IsAnimating))
                        continue;

                    PickupItem pickup = behaviour as PickupItem;
                    if (pickup != null &&
                        (holdingItem || !pickup.canPickup || pickup.IsHeld))
                        continue;

                    currentInteractable = interactable;
                    foundInteractable = true;
                    break;
                }

                if (foundInteractable)
                    break;

                // Don't interact through solid walls or other obstacles.
                firstBlockingCollider = hit.collider;
                break;
            }

            if (squareUI != null)
                squareUI.enabled = currentInteractable != null;

            if (!Input.GetMouseButtonDown(0))
                return;

            if (currentInteractable != null)
            {
                MonoBehaviour target = currentInteractable as MonoBehaviour;

                Debug.Log(
                    "[FPC] Interacting with: " +
                    (target != null ? target.gameObject.name : "Unknown") +
                    " | Script: " +
                    currentInteractable.GetType().Name
                );

                currentInteractable.Interact();
            }
            else
            {
                if (debugInteractionHits)
                {
                    if (firstBlockingCollider != null)
                    {
                        Debug.Log("[FPC Interaction] Blocked by: " +
                            firstBlockingCollider.name +
                            " | Layer: " +
                            LayerMask.LayerToName(firstBlockingCollider.gameObject.layer) +
                            " | IsTrigger: " + firstBlockingCollider.isTrigger);
                    }
                    else
                    {
                        Debug.Log("[FPC Interaction] No collider detected in range. " +
                            "Check item collider, layer, position, and interactionDistance.");
                    }
                }

                if (holdingItem)
                    pickupController.Drop();
            }
        }

        // Hold RMB to zoom
        private void HandleZoom()
        {
            if (!enableZoom || m_Camera == null)
                return;

            bool isZooming = Input.GetMouseButton(1);

            float targetFOV =
                isZooming ? zoomFOV : originalFOV;

            m_Camera.fieldOfView = Mathf.Lerp(
                m_Camera.fieldOfView,
                targetFOV,
                Mathf.Clamp01(zoomSpeed * Time.deltaTime)
            );
        }

        private void UpdateCrouch()
        {
            float minimumHeight = Mathf.Min(standingHeight, 2f * m_CharacterController.radius);
            float loweredHeight = Mathf.Clamp(crouchHeight, minimumHeight, standingHeight);
            float currentHeight = m_CharacterController.height;
            bool wantsCrouch = crouchRequested && loweredHeight < standingHeight;
            float targetHeight = wantsCrouch ? loweredHeight : standingHeight;
            if (targetHeight > currentHeight && !HasStandingClearance(currentHeight))
                targetHeight = currentHeight;

            float height = crouchTransitionTime > 0f
                ? Mathf.MoveTowards(currentHeight, targetHeight, (standingHeight - loweredHeight) * Time.fixedDeltaTime / crouchTransitionTime)
                : targetHeight;
            if (loweredHeight == standingHeight) height = targetHeight;
            float drop = standingHeight - height;
            if (height != currentHeight)
            {
                m_CharacterController.height = height;
                m_CharacterController.center = standingCenter - Vector3.up * (drop * 0.5f);
                if (!m_UseHeadBob)
                    m_Camera.transform.localPosition += Vector3.up * (crouchCameraDrop - drop);
            }
            crouchCameraDrop = drop;
            IsCrouching = wantsCrouch || drop > 0.001f;
            if (IsCrouching) m_Jump = false;
        }

        private bool HasStandingClearance(float currentHeight)
        {
            var scale = transform.lossyScale;
            float radius = Mathf.Max(0.01f, m_CharacterController.radius - m_CharacterController.skinWidth);
            float worldRadius = radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var standingTop = standingCenter + Vector3.up * (standingHeight * 0.5f - radius);
            var currentTop = standingTop - Vector3.up * (standingHeight - currentHeight);
            foreach (var hit in Physics.OverlapCapsule(transform.TransformPoint(currentTop),
                         transform.TransformPoint(standingTop), worldRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) &&
                    !Physics.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer) &&
                    !Physics.GetIgnoreCollision(m_CharacterController, hit))
                    return false;
            }
            return true;
        }

        private void FixedUpdate()
        {
            if (disableMovement ||
                isSeated ||
                scriptedMovement ||
                !m_CharacterController.enabled)
                return;

            UpdateCrouch();
            float speed;
            GetInput(out speed);

            Vector3 desiredMove =
                transform.forward * m_Input.y +
                transform.right * m_Input.x;

            RaycastHit hitInfo;

            bool hasGroundHit = Physics.SphereCast(
                transform.position,
                m_CharacterController.radius,
                Vector3.down,
                out hitInfo,
                standingHeight / 2f
            );

            if (hasGroundHit)
            {
                desiredMove = Vector3.ProjectOnPlane(
                    desiredMove,
                    hitInfo.normal
                ).normalized;
            }
            else
            {
                desiredMove = desiredMove.normalized;
            }

            m_MoveDir.x = desiredMove.x * speed;
            m_MoveDir.z = desiredMove.z * speed;

            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_Jump)
                {
                    m_MoveDir.y = m_JumpSpeed;
                    PlayJumpSound();

                    m_Jump = false;
                    m_Jumping = true;
                }
            }
            else
            {
                m_MoveDir +=
                    Physics.gravity *
                    m_GravityMultiplier *
                    Time.fixedDeltaTime;
            }

            m_CollisionFlags =
                m_CharacterController.Move(
                    m_MoveDir * Time.fixedDeltaTime
                );

            ProgressStepCycle(speed);
            UpdateCameraPosition(speed);
        }

        private void PlayLandingSound()
        {
            if (m_LandSound != null)
            {
                m_AudioSource.clip = m_LandSound;
                m_AudioSource.Play();
            }

            m_NextStep = m_StepCycle + 0.5f;
        }

        private void PlayJumpSound()
        {
            if (m_JumpSound != null)
            {
                m_AudioSource.clip = m_JumpSound;
                m_AudioSource.Play();
            }
        }

        private void ProgressStepCycle(float speed)
        {
            if (m_CharacterController.velocity.sqrMagnitude > 0 &&
                (m_Input.x != 0 || m_Input.y != 0))
            {
                m_StepCycle +=
                    (m_CharacterController.velocity.magnitude +
                    (speed * (m_IsWalking
                        ? 1f
                        : m_RunstepLenghten))) *
                    Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep))
                return;

            m_NextStep = m_StepCycle + m_StepInterval;

            PlayFootStepAudio();
        }

        private void PlayFootStepAudio()
        {
            if (!m_CharacterController.isGrounded)
                return;

            if (m_FootstepSounds == null ||
                m_FootstepSounds.Length == 0)
                return;

            if (m_FootstepSounds.Length == 1)
            {
                if (m_FootstepSounds[0] != null)
                    m_AudioSource.PlayOneShot(m_FootstepSounds[0]);

                return;
            }

            int n = Random.Range(1, m_FootstepSounds.Length);

            AudioClip selectedClip = m_FootstepSounds[n];

            if (selectedClip == null)
                return;

            m_AudioSource.clip = selectedClip;
            m_AudioSource.PlayOneShot(selectedClip);

            m_FootstepSounds[n] = m_FootstepSounds[0];
            m_FootstepSounds[0] = selectedClip;
        }

        private void UpdateCameraPosition(float speed)
        {
            if (!m_UseHeadBob)
                return;

            Vector3 newCameraPosition =
                m_OriginalCameraPosition;

            if (m_CharacterController.velocity.magnitude > 0 &&
                m_CharacterController.isGrounded)
            {
                float bobSpeed =
                    m_CharacterController.velocity.magnitude +
                    (speed * (m_IsWalking
                        ? 1f
                        : m_RunstepLenghten));

                if (IsValidFloat(bobSpeed) && bobSpeed > 0f)
                {
                    Vector3 bobPosition =
                        m_HeadBob.DoHeadBob(bobSpeed);

                    if (IsValidVector3(bobPosition))
                    {
                        newCameraPosition = bobPosition;
                    }
                }
            }

            float jumpOffset = m_JumpBob.Offset();

            if (IsValidFloat(jumpOffset))
            {
                newCameraPosition.y -= jumpOffset;
            }

            if (!IsValidVector3(newCameraPosition))
            {
                newCameraPosition = m_OriginalCameraPosition;
            }

            m_Camera.transform.localPosition =
                newCameraPosition - Vector3.up * crouchCameraDrop;
        }

        // Instant WASD movement
        private void GetInput(out float speed)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            bool waswalking = m_IsWalking;

#if !MOBILE_INPUT
            m_IsWalking = !Input.GetKey(KeyCode.LeftShift);
#endif

            float walkSpeed = IsCrouching ? crouchSpeed : m_WalkSpeed;
            float runSpeed = IsCrouching ? crouchRunSpeed : m_RunSpeed;
            speed = m_IsWalking ? walkSpeed : runSpeed;

            m_Input = new Vector2(horizontal, vertical);

            if (m_Input.sqrMagnitude > 1f)
                m_Input.Normalize();

            if (m_IsWalking != waswalking &&
                m_UseFovKick &&
                m_CharacterController.velocity.sqrMagnitude > 0)
            {
                StopAllCoroutines();

                StartCoroutine(
                    !m_IsWalking
                        ? m_FovKick.FOVKickUp()
                        : m_FovKick.FOVKickDown()
                );
            }
        }

        private void RotateView()
        {
            m_MouseLook.LookRotation(
                transform,
                m_Camera.transform
            );
        }

        private void OnControllerColliderHit(
            ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            if (m_CollisionFlags == CollisionFlags.Below)
                return;

            if (body == null || body.isKinematic)
                return;

            body.AddForceAtPosition(
                m_CharacterController.velocity * 0.1f,
                hit.point,
                ForceMode.Impulse
            );
        }

        // Keeps mouse look, zoom and LMB interactions active while seated.
        public void SitAt(Transform seatPoint)
        {
            if (seatPoint == null || m_CharacterController == null) return;
            m_CharacterController.enabled = false;
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            m_MoveDir = Vector3.zero;
            m_Input = Vector2.zero;
            m_Jump = false;
            isSeated = true;
            m_CharacterController.enabled = true;
        }

        public void StandUpAt(Transform standingPoint)
        {
            if (m_CharacterController == null) return;
            m_CharacterController.enabled = false;
            if (standingPoint != null)
            {
                transform.position = standingPoint.position;
                transform.rotation = standingPoint.rotation;
            }
            m_MoveDir = Vector3.zero;
            m_Jump = false;
            isSeated = false;
            m_CharacterController.enabled = true;
        }

        // Called when entering/leaving the candy timing minigame.
        // Clear buffered jump and wait until Space has been released.
        public void SuppressJumpUntilReleased()
        {
            m_Jump = false;
            suppressJumpUntilReleased = true;
        }

        // Sensitivity slider support
        public void SetSensitivity(float value)
        {
            m_MouseLook.XSensitivity = value;
            m_MouseLook.YSensitivity = value;
        }
    }
}
