Shader "Custom/RayTracing"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

            static const float PI = 3.1415;

            float3 ViewParams;
            float4x4 CamLocalToWorldMatrix;

            int MaxBounceCount;
            int NumRaysPerPixel;
            int Frame;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            struct Ray
            {
                float3 origin;
                float3 dir;
            };

            struct RayTracingMaterial
            {
                float4 color;
                float4 emissionColor;
                float4 specularColor;
                float emissionStrength;
                float smoothness;
                float specularity;
            };
            
            struct Sphere
            {
                float3 position;
                float radius;
                RayTracingMaterial material;
            };

            struct Triangle
            {
                float3 posA, posB, posC;
                float3 normalA, normalB, normalC;
            };
            
            struct HitInfo
            {
                bool didHit;
                float dst;
                float3 hitPoint;
                float3 normal;
                RayTracingMaterial material;
            };

            struct MeshInfo
            {
                uint firstTriangleIndex;
                uint numTriangles;
                float3 boundsMin;
                float3 boundsMax;
                RayTracingMaterial material;
            };
            
            StructuredBuffer<Sphere> Spheres;
            int NumSpheres;
            
            StructuredBuffer<Triangle> Triangles;
            StructuredBuffer<MeshInfo> AllMeshInfo;
            int NumMeshes;

            // Calculate the intersection of a ray with a triangle using Möller–Trumbore algorithm 
            // https://stackoverflow.com/a/42752998
            HitInfo RayTriangle(Ray ray, Triangle tri)
            {
                float3 edgeAB = tri.posB - tri.posA;
                float3 edgeAC = tri.posC - tri.posA;
                float3 normalVector = cross(edgeAB, edgeAC);
                float3 ao = ray.origin - tri.posA;
                float3 dao = cross(ao, ray.dir);

                float determinant = -dot(ray.dir, normalVector);
                float invDet = 1 / determinant;
				
                float dst = dot(ao, normalVector) * invDet;
                float u = dot(edgeAC, dao) * invDet;
                float v = -dot(edgeAB, dao) * invDet;
                float w = 1 - u - v;
				
                HitInfo hitInfo;
                hitInfo.didHit = determinant >= 1E-6 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
                hitInfo.hitPoint = ray.origin + ray.dir * dst;
                hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
                hitInfo.dst = dst;
                return hitInfo;
            }

    
            // Calculate the intersection of a ray with a sphere
            HitInfo RaySphere(Ray ray, float3 sphereCentre, float sphereRadius)
            {
                HitInfo hitInfo = (HitInfo) 0;
                float3 offsetRayOrigin = ray.origin - sphereCentre;
    
                float a = dot(ray.dir, ray.dir);
                float b = 2 * dot(offsetRayOrigin, ray.dir);
                float c = dot(offsetRayOrigin, offsetRayOrigin) - sphereRadius * sphereRadius;

                float discriminant = b * b - 4 * a * c;

				// If solution
                if (discriminant >= 0)
                {
					// Distance to nearest intersection point (from quadratic formula)
                    float dst = (-b - sqrt(discriminant)) / (2 * a);

					// Ignore intersections that occur behind the ray
                    if (dst >= 0)
                    {
                        hitInfo.didHit = true;
                        hitInfo.dst = dst;
                        hitInfo.hitPoint = ray.origin + ray.dir * dst;
                        hitInfo.normal = normalize(hitInfo.hitPoint - sphereCentre);
                    }
                }
                return hitInfo;
            }

            bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax)
            {
                float3 invDir = 1 / ray.dir;
                float3 tMin = (boxMin - ray.origin) * invDir;
                float3 tMax = (boxMax - ray.origin) * invDir;
                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);
                float tNear = max(max(t1.x, t1.y), t1.z);
                float tFar = min(min(t2.x, t2.y), t2.z);
                return tNear <= tFar;
            };

            HitInfo CalculateRayCollision(Ray ray)
            {
                HitInfo closestHit = (HitInfo) 0;
                closestHit.dst = 1.#INF;
    
                // Raycast against all spheres and meshes (triangles) and keep info about the closest hit
                for (int i = 0; i < NumSpheres; i++)
                {
                    Sphere sphere = Spheres[i];
                    HitInfo hitInfo = RaySphere(ray, sphere.position, sphere.radius);

                    if (hitInfo.didHit && hitInfo.dst < closestHit.dst)
                    {
                        closestHit = hitInfo;
                        closestHit.material = sphere.material;
                    }
                }
    
                for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
                {
                    MeshInfo meshInfo = AllMeshInfo[meshIndex];
                    if (!RayBoundingBox(ray, meshInfo.boundsMin, meshInfo.boundsMax))
                    {
                        continue;
                    }

                    for (uint i = 0; i < meshInfo.numTriangles; i++)
                    {
                        int triIndex = meshInfo.firstTriangleIndex + i;
                        Triangle tri = Triangles[triIndex];
                        HitInfo hitInfo = RayTriangle(ray, tri);
	
                        if (hitInfo.didHit && hitInfo.dst < closestHit.dst)
                        {
                            closestHit = hitInfo;
                            closestHit.material = meshInfo.material;
                        }
                    }
                }
    
                return closestHit;
            }
            
            uint NextRandom(inout uint state)
            {
                state = state * 747796405 + 2891336453;
                uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
                result = (result >> 22) ^ result;
                return result;
            }

            float RandomValue(inout uint state)
            {
                return NextRandom(state) / 4294967295.0; // 2^32 - 1
            }

            // Random value in normal distribution
		    // https://stackoverflow.com/a/6178290
            float RandomValueNormalDistribution(inout uint state)
            {
                float theta = 2 * PI * RandomValue(state);
                float rho = sqrt(-2 * log(RandomValue(state)));
                return rho * cos(theta);
            }

			// Calculate a random direction
			// https://math.stackexchange.com/a/1585996
            float3 RandomDirection(inout uint state)
            {
                float x = RandomValueNormalDistribution(state);
                float y = RandomValueNormalDistribution(state);
                float z = RandomValueNormalDistribution(state);
                return normalize(float3(x, y, z));
            }

            float2 RandomPointInCircle(inout uint rngState)
            {
                float angle = RandomValue(rngState) * 2 * PI;
                float2 pointOnCircle = float2(cos(angle), sin(angle));
                return pointOnCircle * sqrt(RandomValue(rngState));
            }
            
            // Send rays
            float3 Trace(Ray ray, inout uint rngState)
            {
                float3 incomingLight = 0;
                float3 rayColor = 1;
                
                for (int i = 0; i <= MaxBounceCount; i++)
                {
                    HitInfo hitInfo = CalculateRayCollision(ray);
                    
                    if (hitInfo.didHit)
                    {
                        ray.origin = hitInfo.hitPoint;
                        RayTracingMaterial material = hitInfo.material;
            
                        // Diffuse & Specular Reflections
                        float3 diffuseDir = normalize(hitInfo.normal + RandomDirection(rngState));
                        float3 specularDir = reflect(ray.dir, hitInfo.normal);
                        bool isSpecular = material.specularity >= RandomValue(rngState);
                        ray.dir = lerp(diffuseDir, specularDir, material.smoothness * isSpecular);
                        
                        // Each bounce contributes slightly to the final color
                        float3 emittedLight = material.emissionColor * material.emissionStrength;
                        incomingLight += emittedLight * rayColor;
                        rayColor *= lerp(material.color, material.specularColor, isSpecular);
                    }
                    else
                    {
                        break;
                    }
                }
    
                return incomingLight;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Random Seed based on pixel coords
                uint2 numPixels = _ScreenParams.xy;
                uint2 pixelCoords = i.uv * numPixels;
                uint pixelIndex = pixelCoords.y * numPixels.x + pixelCoords.x;
                uint rngState = pixelIndex;
                
                float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));
                
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);
    
                float3 totalIncomingLight = 0;
                for (int rayIndex = 0; rayIndex < NumRaysPerPixel; rayIndex++)
                {
                    totalIncomingLight += Trace(ray, rngState);
                }
                float3 pixelCol = totalIncomingLight / NumRaysPerPixel;
    
                return float4(pixelCol, 1);
            }

			ENDCG
        }
    }
}
