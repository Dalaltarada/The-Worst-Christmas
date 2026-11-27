using UnityEngine;
using UnityEngine.UI;

// Presentation only: the original item still owns the light, sounds and pickup state.
// The textures are transparent renders of the scene's lantern in its held pose.
[DefaultExecutionOrder(300)]
public sealed class HeldTorchImage : MonoBehaviour
{
    private PickupItem item;
    private Renderer[] modelRenderers;
    private bool[] previousForceRenderingOff;
    private Canvas canvas;
    private RawImage image;
    private Camera view;
    private Material foregroundMaterial;
    private Texture2D litTexture, unlitTexture;

    public void Initialize(PickupItem owner)
    {
        item = owner;
        litTexture = Resources.Load<Texture2D>("HeldTorch/On");
        unlitTexture = Resources.Load<Texture2D>("HeldTorch/Off");
        if (litTexture == null || unlitTexture == null)
        {
            Debug.LogError("Held torch images are missing from Resources/HeldTorch.", this);
            return;
        }

        modelRenderers = item.GetComponentsInChildren<Renderer>(true);
        previousForceRenderingOff = new bool[modelRenderers.Length];
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            previousForceRenderingOff[i] = modelRenderers[i].forceRenderingOff;
            // Leave enabled untouched so existing story sequences can hide the held item.
            modelRenderers[i].forceRenderingOff = true;
        }

        var root = new GameObject("Held Torch Image", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = LayerMask.NameToLayer("UI");
        canvas = root.GetComponent<Canvas>();
        // Render into the gameplay camera before its image effects, so the torch
        // shares DarkBringer's palette, dithering and resolution with the maze.
        view = item.GetComponentInParent<Camera>();
        if (view == null) view = Camera.main;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = view;
        canvas.sortingOrder = -100; // Dialogue, menus and blackouts remain above the torch.
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1;

        var picture = new GameObject("Torch", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        picture.layer = root.layer;
        picture.transform.SetParent(root.transform, false);
        image = picture.GetComponent<RawImage>();
        foregroundMaterial = new Material(Resources.Load<Shader>("HeldTorch/Foreground"));
        image.material = foregroundMaterial;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(800, 900);
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        if (view != null) canvas.planeDistance = view.nearClipPlane + .01f;
        bool visible = false;
        if (item != null && item.IsHeld && item.isActiveAndEnabled)
        {
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                var renderer = modelRenderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && !previousForceRenderingOff[i])
                {
                    visible = true;
                    break;
                }
            }
        }
        canvas.enabled = visible;
        var light = item != null ? item.heldTorchLight : null;
        bool lit = light != null && light.isActiveAndEnabled && light.intensity > 0;
        image.texture = lit ? litTexture : unlitTexture;
        // Keep the studio render subdued against the maze's dark surroundings.
        float brightness = lit ? .65f : .5f;
        image.color = new Color(brightness, brightness, brightness, 1);
    }

    public void Release()
    {
        enabled = false;
        if (modelRenderers != null)
        {
            for (int i = 0; i < modelRenderers.Length; i++)
                if (modelRenderers[i] != null) modelRenderers[i].forceRenderingOff = previousForceRenderingOff[i];
            modelRenderers = null;
        }
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
            canvas = null;
        }
        if (foregroundMaterial != null)
        {
            Destroy(foregroundMaterial);
            foregroundMaterial = null;
        }
    }

    private void OnDisable() { if (canvas != null) canvas.enabled = false; }
    private void OnDestroy() { Release(); }
}
