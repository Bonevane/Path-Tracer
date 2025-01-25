using UnityEngine;

public struct MeshInfo
{
    public int triangleStartIndex;
    public int triangleCount;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public RayTracingMaterial material;

    public MeshInfo(int triangleStartIndex, int triangleCount, RayTracingMaterial material, Bounds bounds)
    {
        this.triangleStartIndex = triangleStartIndex;
        this.triangleCount = triangleCount;
        this.boundsMin = bounds.min;
        this.boundsMax = bounds.max;
        this.material = material;
    }

}