using UnityEngine;

public class PanelFollower : MonoBehaviour
{
    private Canvas worldSpaceCanvas;
    private RectTransform panelRect;
    private RectTransform canvasRect;

    private Transform followTransform;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private bool mirrorMode = false;

    public void SetFollowTransform(Transform target)
    {
        followTransform = target;
    }

    public void SnapToFollowTarget()
    {
        UpdatePanelPosition();
    }

    void Awake()
    {
        worldSpaceCanvas = GetComponentInParent<Canvas>();
        panelRect = GetComponent<RectTransform>();
        if (worldSpaceCanvas != null)
        {
            canvasRect = worldSpaceCanvas.GetComponent<RectTransform>();
        }
    }

    void Update()
    {
        UpdatePanelPosition();
    }

    private void UpdatePanelPosition()
    {
        if (followTransform == null || panelRect == null)
        {
            return;
        }

        Camera cam = null;
        if (worldSpaceCanvas != null && worldSpaceCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = worldSpaceCanvas.worldCamera;
        }
        if (cam == null) cam = Camera.main;

        Vector3 adjustedOffset = GetClampedOffset(cam);
        Vector3 worldTarget = followTransform.position + adjustedOffset;


        if (worldSpaceCanvas != null && worldSpaceCanvas.renderMode == RenderMode.WorldSpace)
        {
            panelRect.position = worldTarget;
            return;
        }

        if (mirrorMode)
        {
            Vector2 followScreen = RectTransformUtility.WorldToScreenPoint(cam, followTransform.position);
            float halfScreenWidth = Screen.width * 0.5f;
            float leftCenterX = Screen.width * 0.25f;
            float rightCenterX = Screen.width * 0.75f;
            float centerY = Screen.height * 0.5f;

            // Mirror target position around the vertical midpoint to choose a side anchor.
            float anchorX = followScreen.x < halfScreenWidth ? rightCenterX : leftCenterX;
            Vector2 mirrorScreenPoint = new Vector2(anchorX, centerY);

            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mirrorScreenPoint, cam, out Vector2 mirrorLocalPoint))
            {
                panelRect.anchoredPosition = mirrorLocalPoint;
            }

            return;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldTarget);
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint))
        {
            panelRect.anchoredPosition = localPoint;
        }
    }

    private Vector3 GetClampedOffset(Camera cam)
    {
        Vector3 adjusted = offset;

        // On world-space canvases, flip horizontally based on where the panel sits in world space
        // relative to the canvas center (right side -> place panel to the left of target, and vice versa).
        if (worldSpaceCanvas != null && worldSpaceCanvas.renderMode == RenderMode.WorldSpace)
        {
            adjusted.x = followTransform.position.x > 0f ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x);
            return adjusted;
        }

        float halfW = panelRect.rect.width * 0.5f;
        float halfH = panelRect.rect.height * 0.5f;
        if (worldSpaceCanvas != null)
        {
            Rect pixelRect = RectTransformUtility.PixelAdjustRect(panelRect, worldSpaceCanvas);
            halfW = pixelRect.width * 0.5f;
            halfH = pixelRect.height * 0.5f;
        }

        Vector2 followScreen = RectTransformUtility.WorldToScreenPoint(cam, followTransform.position);

        float projectedX = followScreen.x + adjusted.x;
        if (projectedX + halfW > Screen.width)
        {
            adjusted.x = -Mathf.Abs(offset.x);
        }
        else if (projectedX - halfW < 0f)
        {
            adjusted.x = Mathf.Abs(offset.x);
        }

        float projectedY = followScreen.y + adjusted.y;
        if (projectedY + halfH > Screen.height)
        {
            adjusted.y = -Mathf.Abs(offset.y);
        }
        else if (projectedY - halfH < 0f)
        {
            adjusted.y = Mathf.Abs(offset.y);
        }

        return adjusted;
    }
}
