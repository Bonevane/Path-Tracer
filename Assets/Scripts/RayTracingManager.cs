using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    [Header("Ray Tracing Settings")]
    [SerializeField, Range(0, 32)] int maxBounceCount = 4;
    [SerializeField, Range(0, 64)] int numRaysPerPixel = 2;

    [Header("View Settings")]
    [SerializeField] bool useShaderInSceneView;
    [SerializeField] Shader rayTracingShader;
    Material rayTracingMaterial;

    ComputeBuffer triangleBuffer;
    ComputeBuffer meshInfoBuffer;

    List<Triangle> allTriangles;
    List<MeshInfo> allMeshInfo;

    ComputeBuffer sphereBuffer;

    void OnRenderImage(RenderTexture src, RenderTexture target)
    {
        if(Camera.current.name != "SceneCamera" || useShaderInSceneView)
        {
            ShaderHelper.InitMaterial(rayTracingShader, ref rayTracingMaterial);
            UpdateCameraParams(Camera.current);
            CreateSpheres();
            CreateMeshes();
            SetShaderParams();
            Graphics.Blit(null, target, rayTracingMaterial);
        }
        else
        {
            Graphics.Blit(src, target);
        }
    }

    void SetShaderParams()
    {
        rayTracingMaterial.SetInt("MaxBounceCount", maxBounceCount);
        rayTracingMaterial.SetInt("NumRaysPerPixel", numRaysPerPixel);
    }

    void UpdateCameraParams(Camera cam) {
        float planeHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;
        
        rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cam.nearClipPlane));
        rayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", cam.transform.localToWorldMatrix);
    }

    void CreateSpheres()
    {
        // Create sphere data from the sphere objects in the scene
        RayTracedSphere[] sphereObjects = FindObjectsByType<RayTracedSphere>(FindObjectsSortMode.None);
        Sphere[] spheres = new Sphere[sphereObjects.Length];

        for (int i = 0; i < sphereObjects.Length; i++)
        {
            spheres[i] = new Sphere()
            {
                position = sphereObjects[i].transform.position,
                radius = sphereObjects[i].transform.localScale.x * 0.5f,
                material = sphereObjects[i].material
            };
        }

        // Create buffer containing all sphere data, and send it to the shader
        ShaderHelper.CreateStructuredBuffer(ref sphereBuffer, spheres);
        rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
        rayTracingMaterial.SetInt("NumSpheres", sphereObjects.Length);
    }

    void CreateMeshes()
    {
        RayTracedMesh[] meshObjects = FindObjectsByType<RayTracedMesh>(FindObjectsSortMode.None);
        allTriangles ??= new List<Triangle>();
        allMeshInfo ??= new List<MeshInfo>();
        allTriangles.Clear();
        allMeshInfo.Clear();

        for (int i = 0; i < meshObjects.Length; i++)
        {
            MeshChunk[] chunks = meshObjects[i].GetSubMeshes();
            foreach (MeshChunk chunk in chunks)
            {
                RayTracingMaterial material = meshObjects[i].GetMaterial(chunk.subMeshIndex);
                allMeshInfo.Add(new MeshInfo(allTriangles.Count, chunk.triangles.Length, material, chunk.bounds));
                allTriangles.AddRange(chunk.triangles);

            }
        }

        ShaderHelper.CreateStructuredBuffer(ref triangleBuffer, allTriangles);
        ShaderHelper.CreateStructuredBuffer(ref meshInfoBuffer, allMeshInfo);
        rayTracingMaterial.SetBuffer("Triangles", triangleBuffer);
        rayTracingMaterial.SetBuffer("AllMeshInfo", meshInfoBuffer);
        rayTracingMaterial.SetInt("NumMeshes", allMeshInfo.Count);

        print(rayTracingMaterial.GetBuffer("Triangles").value);
    }
}
