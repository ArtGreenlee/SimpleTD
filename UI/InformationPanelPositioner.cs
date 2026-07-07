using UnityEngine;

/// <summary>
/// Unified positioning utility for information display panels on the same canvas and camera.
/// Handles positioning for both ScreenSpace and WorldSpace canvases.
/// </summary>
public static class InformationPanelPositioner
{
    /// <summary>
    /// Positions a panel based on a world position. Automatically handles both ScreenSpace and WorldSpace canvases.
    /// </summary>
    /// <param name="panelRect">The RectTransform of the panel to position</param>
    /// <param name="canvasRect">The RectTransform of the canvas</param>
    /// <param name="canvas">The Canvas component</param>
    /// <param name="worldPos">The world position to position the panel near</param>
    /// <param name="worldCamera">The camera used for world-to-screen conversion (uses Camera.main if null)</param>
    /// <param name="screenOffset">Optional pixel offset from the world position</param>
    public static void PositionPanel(RectTransform panelRect, RectTransform canvasRect, Canvas canvas, 
        Vector3 worldPos, Camera worldCamera = null, Vector2 screenOffset = default)
    {
        if (panelRect == null || canvasRect == null || canvas == null) return;
        return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return;

        // Handle WorldSpace canvas differently from ScreenSpace
        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            PositionPanelWorldSpace(panelRect, worldPos, screenOffset);
        }
        else
        {
            PositionPanelScreenSpace(panelRect, canvasRect, canvas, worldPos, cam, screenOffset);
        }
    }

    /// <summary>
    /// Positions a panel in WorldSpace canvas directly in world coordinates.
    /// </summary>
    private static void PositionPanelWorldSpace(RectTransform panelRect, Vector3 worldPos, Vector2 offset)
    {
        // For world space canvas, simply place the panel near the world position
        Vector3 panelPos = worldPos + new Vector3(offset.x * 0.01f, offset.y * 0.01f, 0);
        panelRect.position = panelPos;
    }

    /// <summary>
    /// Positions a panel in ScreenSpace canvas using screen-to-world conversion.
    /// </summary>
    private static void PositionPanelScreenSpace(RectTransform panelRect, RectTransform canvasRect, Canvas canvas, 
        Vector3 worldPos, Camera cam, Vector2 screenOffset)
    {
        // Convert world position to screen point
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);

        // Calculate offset based on screen quadrant to keep panel visible
        Vector2 offset = screenOffset;
        if (screenOffset == default)
        {
            offset = CalculateSmartOffset(panelRect, screenPoint);
        }

        // Convert screen point to canvas world/local position
        Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;
        Vector2 targetScreen = (Vector2)screenPoint + offset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, targetScreen, uiCamera, out var worldPoint))
        {
            panelRect.position = worldPoint;
        }
    }

    /// <summary>
    /// Calculates a smart offset to keep the panel within screen bounds.
    /// Flips the panel to the opposite side if it would go off-screen.
    /// </summary>
    private static Vector2 CalculateSmartOffset(RectTransform panelRect, Vector3 screenPoint)
    {
        Vector2 panelHalfPixels = new Vector2(
            panelRect.rect.width * panelRect.lossyScale.x * 0.5f,
            panelRect.rect.height * panelRect.lossyScale.y * 0.5f);

        // Flip X if past center
        if (screenPoint.x > Screen.width * 0.5f)
        {
            panelHalfPixels.x *= -1f;
        }

        // Flip Y if past center
        if (screenPoint.y > Screen.height * 0.5f)
        {
            panelHalfPixels.y *= -1f;
        }

        return panelHalfPixels;
    }
}
