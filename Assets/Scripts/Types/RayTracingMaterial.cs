using UnityEngine;

[System.Serializable]
public struct RayTracingMaterial
{
    public Color colour;
    public Color emissionColour;
    public Color specularColour;
    public float emissionStrength;
    [Range(0, 1)] public float smoothness;
    [Range(0, 1)] public float specularity;

    public void SetDefaultValues()
    {
        colour = Color.white;
        emissionColour = Color.white;
        emissionStrength = 0;
        specularColour = Color.white;
        smoothness = 0;
        specularity = 0;
    }
}