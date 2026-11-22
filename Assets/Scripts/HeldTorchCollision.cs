using System.Collections.Generic;
using UnityEngine;

// Carries the unchanged lantern model. All correction is on a temporary parent;
// imported meshes, materials and the authored local pose are never modified.
[DefaultExecutionOrder(200)]
public sealed class HeldTorchCollision : MonoBehaviour
{
    private struct Part
    {
        public Vector3 center;
        public Vector3 halfSize;
        public MeshCollider shape;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Mesh ownedMesh;
    }
    private readonly List<Part> parts = new List<Part>();
    private readonly Collider[] overlaps = new Collider[128];
    private readonly RaycastHit[] hits = new RaycastHit[128];
    private Transform player;
    private Transform pivot;
    private Vector3 restPosition;
    private Vector3 offset;
    private Vector3 offsetVelocity;
    private float padding;
    private float returnTime;
    private float tuck;
    private float tuckVelocity;
    private Quaternion restRotation;
    private Camera view;
    private float originalNearClip;

    public void Initialize(Transform holder, Transform collisionPivot, float margin, float smoothTime)
    {
        player = holder;
        pivot = collisionPivot;
        padding = Mathf.Max(.005f, margin);
        returnTime = Mathf.Max(.04f, smoothTime);
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        view = GetComponentInParent<Camera>();
        if (view != null)
        {
            originalNearClip = view.nearClipPlane;
            view.nearClipPlane = Mathf.Min(originalNearClip, .015f);
        }
        // Bake skinning once: renderer.localBounds can contain stale import data.
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var skin = renderer as SkinnedMeshRenderer;
            if (skin != null)
            {
                var baked = new Mesh();
                skin.BakeMesh(baked);
                var vertices = baked.vertices;
                Bounds bounds = new Bounds();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                    if (i == 0) bounds = new Bounds(v, Vector3.zero); else bounds.Encapsulate(v);
                }
                if (vertices.Length > 0) AddPart(bounds, baked, skin.transform, true);
                else Destroy(baked);
            }
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds source = filter.sharedMesh.bounds;
                Bounds bounds = new Bounds();
                for (int c = 0; c < 8; c++)
                {
                    Vector3 v = source.center + Vector3.Scale(source.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    v = transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                    if (c == 0) bounds = new Bounds(v, Vector3.zero); else bounds.Encapsulate(v);
                }
                AddPart(bounds, filter.sharedMesh, filter.transform, false);
            }
        }
    }

    private void AddPart(Bounds bounds, Mesh mesh, Transform source, bool owned)
    {
        Vector3 s = transform.lossyScale;
        s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        var go = new GameObject("Held lantern convex query");
        go.transform.SetPositionAndRotation(source.position, source.rotation);
        go.transform.localScale = source.lossyScale;
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;
        collider.isTrigger = true;
        parts.Add(new Part { center = Vector3.Scale(bounds.center, s),
            halfSize = Vector3.Max(Vector3.Scale(bounds.extents, s), Vector3.one * .003f),
            shape = collider, localPosition = Quaternion.Inverse(transform.rotation) * (source.position - transform.position),
            localRotation = Quaternion.Inverse(transform.rotation) * source.rotation,
            ownedMesh = owned ? mesh : null });
    }

    private bool Ignore(Collider other)
    {
        return other == null || other.isTrigger || other.transform.IsChildOf(transform) ||
            (player != null && other.transform.IsChildOf(player));
    }

    private void LateUpdate()
    {
        if (pivot == null || parts.Count == 0) return;
        pivot.localRotation = Quaternion.identity;
        pivot.localPosition = Vector3.zero;
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        Vector3 authoredPosition = transform.position;
        Quaternion authoredRotation = transform.rotation;
        Vector3 desiredPosition = authoredPosition;
        Quaternion desiredRotation = authoredRotation;
        float wantedTuck = 0f;
        if (!Clear(authoredPosition, authoredRotation) && view != null)
        {
            wantedTuck = 1f;
            for (int step = 1; step <= 20; step++)
            {
                float t = step / 20f;
                TuckedPose(authoredPosition, authoredRotation, t, out Vector3 p, out Quaternion r);
                if (Clear(p, r)) { wantedTuck = t; break; }
            }
        }
        tuck = Mathf.SmoothDamp(tuck, wantedTuck, ref tuckVelocity,
            wantedTuck > tuck ? .065f : returnTime);
        if (wantedTuck == 0f && tuck < .01f && offset.sqrMagnitude < .0001f)
        {
            tuck = 0f;
            tuckVelocity = 0f;
            offset = Vector3.zero;
            offsetVelocity = Vector3.zero;
        }
        if (tuck < .0001f) tuck = 0f;
        TuckedPose(authoredPosition, authoredRotation, tuck, out desiredPosition, out desiredRotation);
        pivot.rotation = desiredRotation * Quaternion.Inverse(restRotation);
        pivot.position += desiredPosition - transform.position;
        // Ease back after contact, retaining the last correction rather than
        // teleporting to the desired pose on every frame.
        offset = Vector3.SmoothDamp(offset, Vector3.zero, ref offsetVelocity, returnTime);
        pivot.localPosition += offset;
        Vector3 candidate = transform.position;
        Quaternion rotation = transform.rotation;

        for (int iteration = 0; iteration < 20; iteration++)
        {
            float deepest = 0f;
            Vector3 correction = Vector3.zero;
            if (view != null)
            {
                float nearest = NearestCameraDepth(candidate, rotation);
                float minimum = view.nearClipPlane + .035f;
                if (nearest < minimum)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                    float alignment = Vector3.Dot(forward, view.transform.forward);
                    if (alignment > .15f) candidate += forward * ((minimum - nearest) / alignment);
                }
            }
            foreach (Part part in parts)
            {
                Vector3 center = candidate + rotation * part.center;
                Vector3 shapePosition = candidate + rotation * part.localPosition;
                Quaternion shapeRotation = rotation * part.localRotation;
                part.shape.transform.SetPositionAndRotation(shapePosition, shapeRotation);
                int count = Physics.OverlapBoxNonAlloc(center, part.halfSize + Vector3.one * padding, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = overlaps[i];
                    if (Ignore(other)) continue;
                    if (Physics.ComputePenetration(part.shape, shapePosition, shapeRotation, other,
                        other.transform.position, other.transform.rotation, out Vector3 direction, out float depth) && depth > deepest)
                    {
                        deepest = depth;
                        correction = direction * (depth + padding);
                    }
                }
            }
            if (deepest < .00001f) break;
            candidate += correction;
        }
        pivot.position += candidate - transform.position;
        // Store only the residual contact offset, not the authored tuck itself.
        offset = pivot.parent.InverseTransformVector(candidate - desiredPosition);
    }

    private void TuckedPose(Vector3 authoredPosition, Quaternion authoredRotation, float blend,
        out Vector3 position, out Quaternion rotation)
    {
        if (view == null || blend <= 0f) { position = authoredPosition; rotation = authoredRotation; return; }
        Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = player.forward;
        Quaternion upright = Quaternion.LookRotation(forward, Vector3.up);
        Quaternion carry = Quaternion.Slerp(view.transform.rotation, upright, blend);
        Vector3 relative = view.transform.InverseTransformPoint(authoredPosition);
        Vector3 tucked = new Vector3(relative.x * .15f, relative.y + .07f, relative.z * .85f);
        position = view.transform.position + carry * Vector3.Lerp(relative, tucked, blend);
        rotation = carry * Quaternion.Inverse(view.transform.rotation) * authoredRotation;
    }

    private bool Clear(Vector3 position, Quaternion rotation)
    {
        if (view != null && NearestCameraDepth(position, rotation) < view.nearClipPlane + .035f) return false;
        foreach (Part part in parts)
        {
            Vector3 center = position + rotation * part.center;
            // A clear endpoint on the far side of a thin wall is not a valid
            // carry pose. Keep every part on the player's side of that surface.
            if (view != null)
            {
                Vector3 line = center - view.transform.position;
                int crossed = Physics.RaycastNonAlloc(view.transform.position, line.normalized,
                    hits, line.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < crossed; i++) if (!Ignore(hits[i].collider)) return false;
            }
            Vector3 shapePosition = position + rotation * part.localPosition;
            Quaternion shapeRotation = rotation * part.localRotation;
            part.shape.transform.SetPositionAndRotation(shapePosition, shapeRotation);
            int count = Physics.OverlapBoxNonAlloc(center, part.halfSize + Vector3.one * padding, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (Ignore(other)) continue;
                if (Physics.ComputePenetration(part.shape, shapePosition, shapeRotation, other, other.transform.position,
                    other.transform.rotation, out _, out float depth) && depth > .0001f) return false;
            }
        }
        return true;
    }

    private float NearestCameraDepth(Vector3 position, Quaternion rotation)
    {
        Vector3 forward = view.transform.forward;
        float nearest = float.PositiveInfinity;
        foreach (Part part in parts)
        {
            float support = Mathf.Abs(Vector3.Dot(rotation * Vector3.right, forward)) * part.halfSize.x +
                Mathf.Abs(Vector3.Dot(rotation * Vector3.up, forward)) * part.halfSize.y +
                Mathf.Abs(Vector3.Dot(rotation * Vector3.forward, forward)) * part.halfSize.z;
            nearest = Mathf.Min(nearest, Vector3.Dot(position + rotation * part.center - view.transform.position, forward) - support);
        }
        return nearest;
    }

    public void Release()
    {
        enabled = false;
        if (view != null && originalNearClip > 0f)
        {
            view.nearClipPlane = originalNearClip;
            originalNearClip = 0f;
        }
        foreach (Part part in parts)
        {
            if (part.shape != null) Destroy(part.shape.gameObject);
            if (part.ownedMesh != null) Destroy(part.ownedMesh);
        }
        parts.Clear();
    }
    private void OnDestroy() { Release(); }
}
