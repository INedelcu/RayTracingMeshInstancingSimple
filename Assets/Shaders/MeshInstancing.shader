Shader "RayTracing/MeshInstancing"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "DisableBatching" = "True" }
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }

            ENDCG
        }
    }

    SubShader
    {
        Pass
        {
            Name "Test"

            HLSLPROGRAM

            #include "UnityRayTracingMeshUtils.cginc"
            #include "RayPayload.hlsl"
            #include "GlobalResources.hlsl"

            #pragma raytracing some_name

            // Use INSTANCING_ON shader keyword for supporting instanced and non-instanced geometries.
            // Unity will setup SH coeffiecients - unity_SHAArray, unity_SHBArray, etc when RayTracingAccelerationStructure.AddInstances in used.
            //#pragma multi_compile _ INSTANCING_ON

            // Unity built-in shader property and represents the index of the fist ray tracing Mesh instance in the TLAS.
            uint unity_BaseInstanceID;

            uint unity_InstanceCount;

            StructuredBuffer<float3> g_Colors;

            // Calculated in the closest hit shader and it's the difference between the global ray tracing instance in the TLAS and unity_BaseInstanceID.
            static uint unity_InstanceID;

            struct AttributeData
            {
                float2 barycentrics;
            };

            struct Vertex
            {
                float3 position;
                float3 normal;
            };

            Vertex FetchVertex(uint vertexIndex)
            {
                Vertex v;
                v.position = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
                v.normal = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
                return v;
            }

            Vertex InterpolateVertices(Vertex v0, Vertex v1, Vertex v2, float3 barycentrics)
            {
                Vertex v;
#define INTERPOLATE_ATTRIBUTE(attr) v.attr = v0.attr * barycentrics.x + v1.attr * barycentrics.y + v2.attr * barycentrics.z
                INTERPOLATE_ATTRIBUTE(position);
                INTERPOLATE_ATTRIBUTE(normal);
                return v;
            }

            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
            {
                unity_InstanceID = InstanceIndex() - unity_BaseInstanceID;

                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                Vertex v0, v1, v2;
                v0 = FetchVertex(triangleIndices.x);
                v1 = FetchVertex(triangleIndices.y);
                v2 = FetchVertex(triangleIndices.z);

                float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
                Vertex v = InterpolateVertices(v0, v1, v2, barycentricCoords);

                float3 worldNormal = normalize(mul(v.normal, (float3x3)WorldToObject()));

                float3 reflectionVec = reflect(WorldRayDirection(), worldNormal);

                float3 reflectionCol = float3(0, 0, 0);

                float t = saturate(0.1 + pow(saturate(dot(reflectionVec, WorldRayDirection())), 3));

                float3 lightDir = float3(0.0, -1.0, 0.0);
                float3 light = saturate(dot(worldNormal, normalize(-lightDir)));

                if (payload.bounceIndex < 2)
                {
                    float3 worldPosition = mul(ObjectToWorld(), float4(v.position, 1));

                    RayDesc ray;
                    ray.Origin = worldPosition + worldNormal * 0.005f;
                    ray.Direction = reflectionVec;
                    ray.TMin = 0;
                    ray.TMax = 1e20f;

                    RayPayload payloadRefl;
                    payloadRefl.color = float3(0, 0, 0);
                    payloadRefl.bounceIndex = payload.bounceIndex + 1;

                    uint missShaderIndex = 0;
                    TraceRay(g_AccelStruct, 0, 0xFF, 0, 1, missShaderIndex, ray, payloadRefl);

                    reflectionCol = payloadRefl.color;
                }
                else
                {
                    reflectionCol = g_EnvTexture.SampleLevel(sampler_g_EnvTexture, reflectionVec, 0).xyz;
                }
                
                payload.color = lerp(light, reflectionCol, t) * g_Colors[unity_InstanceID];
            }

            ENDHLSL
        }
    }
}
