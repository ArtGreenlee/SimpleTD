using UnityEngine;

public class RayTriggerTesting : MonoBehaviour
{
    void OnDrawGizmos()
    {
        RectTransform rt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Gizmos.color = Color.red;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
    }
}
