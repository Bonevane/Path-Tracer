using UnityEngine;

class CameraTest : MonoBehaviour
{

    [SerializeField] Vector2 debugPointCount;
    [SerializeField] float debugRadius;
    [SerializeField] Color pointColor = Color.red;

    private void OnDrawGizmos()
    {
        print("Drawing Gizmos!");
        CameraRayTest();
    }

    public static void DrawArrow(Vector3 start, Vector3 direction, Color color, float arrowHeadLength = 100f, float arrowHeadAngle = 0.0f)
    {
        Debug.DrawRay(start, arrowHeadLength * direction, color);
    }


    void CameraRayTest()
    {
        Camera cam = Camera.main;
        Transform camT = cam.transform;

        float planeHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;

        Vector3 bottomLeftLocal = new Vector3(-planeWidth / 2, -planeHeight / 2, cam.nearClipPlane);

        for (int x = 0; x < debugPointCount.x; x++)
        {
            for(int y = 0; y < debugPointCount.y; y++)
            {
                float tx = x / (debugPointCount.x - 1f);
                float ty = y / (debugPointCount.y - 1f);

                Vector3 pointLocal = bottomLeftLocal + new Vector3(planeWidth * tx, planeHeight * ty);
                Vector3 point = camT.position + camT.right * pointLocal.x + camT.up * pointLocal.y + camT.forward * pointLocal.z;
                Vector3 dir = (point - camT.position).normalized;

                Gizmos.DrawSphere(point, debugRadius);
                DrawArrow(camT.position, dir, pointColor);
            }
        }

    }
}
