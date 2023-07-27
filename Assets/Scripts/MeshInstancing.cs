using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.UI;

[ExecuteInEditMode]
public class MeshInstancing : MonoBehaviour
{
    [SerializeField] RayTracingShader rayTracingShader = null;
    [SerializeField] Mesh mesh = null;
    [SerializeField] Material material = null;
    [SerializeField] Vector2Int counts = new Vector2Int(50, 50); 
    [SerializeField] Texture envTexture = null;
    [SerializeField] Color color1 = Color.red;
    [SerializeField] Color color2 = Color.yellow;

    public Toggle enableInstancingToggle;
    public Toggle enableAnimationToggle;
    public Toggle enableRayBounceToggle;
    public Text fpsText;
    public Text titleText;

    private float lastRealtimeSinceStartup = 0;
    private float updateFPSTimer = 1.0f;

    uint cameraWidth = 0;
    uint cameraHeight = 0;

    RenderTexture rayTracingOutput = null;

    RayTracingAccelerationStructure rtas = null;

    RayTracingInstanceData instanceData = null;

    private void ReleaseResources()
    {
        if (rtas != null)
        {
            rtas.Release();
            rtas = null;
        }

        if (rayTracingOutput)
        {
            rayTracingOutput.Release();
            rayTracingOutput = null;
        }

        cameraWidth = 0;
        cameraHeight = 0;

        if (instanceData != null)
        {
            instanceData.Dispose();
            instanceData = null;
        }
    }

    private void CreateResources()
    {
        if (cameraWidth != Camera.main.pixelWidth || cameraHeight != Camera.main.pixelHeight)
        {
            if (rayTracingOutput)
                rayTracingOutput.Release();

            rayTracingOutput = new RenderTexture(Camera.main.pixelWidth, Camera.main.pixelHeight, 0, RenderTextureFormat.ARGBHalf);
            rayTracingOutput.enableRandomWrite = true;
            rayTracingOutput.Create();

            cameraWidth = (uint)Camera.main.pixelWidth;
            cameraHeight = (uint)Camera.main.pixelHeight;
        }

        if (instanceData == null || instanceData.columns != counts.x || instanceData.rows != counts.y || instanceData.color1 != color1 || instanceData.color2 != color2)
        {
            if (instanceData != null)
            {
                instanceData.Dispose();
            }

            instanceData = new RayTracingInstanceData(counts.x, counts.y, color1, color2);
        }
    }

    void OnDestroy()
    {
        ReleaseResources();
    }

    void OnDisable()
    {
        ReleaseResources();
    }

    private void OnEnable()
    {
        if (rtas != null)
            return;

        rtas = new RayTracingAccelerationStructure();
    }
    private void Update()
    {
        if (fpsText)
        {
            float deltaTime = Time.realtimeSinceStartup - lastRealtimeSinceStartup;
            updateFPSTimer += deltaTime;
            
            if (updateFPSTimer >= 0.2f)
            {
                float fps = 1.0f / Mathf.Max(deltaTime, 0.0001f);
                fpsText.text = "FPS: " + Mathf.Ceil(fps).ToString();
                updateFPSTimer = 0.0f;
            }

            lastRealtimeSinceStartup = Time.realtimeSinceStartup;
        }

        if (titleText)
        {
            titleText.text = "Adding " + counts.x * counts.y + " instances to RTAS";
        }
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!SystemInfo.supportsRayTracing || !rayTracingShader)
        {
            Debug.Log("The Ray Tracing API is not supported by this GPU or by the current graphics API.");
            Graphics.Blit(src, dest);
            return;
        }

        if (mesh == null)
        {
            Debug.Log("Please set a Mesh!");
            Graphics.Blit(src, dest);
            return;
        }

        if (material == null)
        {
            Debug.Log("Please set a Material!");
            Graphics.Blit(src, dest);
            return;
        }

        CreateResources();

        CommandBuffer cmdBuffer = new CommandBuffer();
        cmdBuffer.name = "Mesh Instancing Test";

        rtas.ClearInstances();

        try
        {
            bool instancingEnabled = (enableInstancingToggle == null) || enableInstancingToggle.isOn;

            if (instanceData != null)
            {
                Profiler.BeginSample("Animate Mesh RT Instances");

                bool enableAnimation = !enableAnimationToggle || enableAnimationToggle.isOn;

                if (enableAnimation)
                    instanceData.Update(Application.isPlaying ? Time.time * 3 : 0);

                Profiler.EndSample();

                Profiler.BeginSample("Add Mesh RT Instances to RTAS");

                RayTracingMeshInstanceConfig config = new RayTracingMeshInstanceConfig(mesh, 0, material);

                config.materialProperties = new MaterialPropertyBlock();
                config.materialProperties.SetBuffer("g_Colors", instanceData.colors);

                // Not providing light probe data at all.
                config.lightProbeUsage = LightProbeUsage.CustomProvided;

                if (instancingEnabled)
                {
                    rtas.AddInstances(config, instanceData.matrices);
                }
                else
                {
                    // Add the instances one by one.
                    for (int i = 0; i < instanceData.matrices.Length; i++)
                    {
                        // Use custom InstanceID() in HLSL to read the instance matrix from g_Colors;
                        // The last argument is the custom InstanceID().

                        rtas.AddInstance(config, instanceData.matrices[i], null, (uint)i);
                    }
                }

                Profiler.EndSample();
            }
        }
        catch (Exception e) 
        { 
            Debug.Log("An exception occurred: " + e.Message);
        }

        cmdBuffer.BuildRayTracingAccelerationStructure(rtas);

        cmdBuffer.SetRayTracingShaderPass(rayTracingShader, "Test");

        // Input
        cmdBuffer.SetRayTracingAccelerationStructure(rayTracingShader, Shader.PropertyToID("g_AccelStruct"), rtas);
        cmdBuffer.SetRayTracingMatrixParam(rayTracingShader, Shader.PropertyToID("g_InvViewMatrix"), Camera.main.cameraToWorldMatrix);
        cmdBuffer.SetRayTracingFloatParam(rayTracingShader, Shader.PropertyToID("g_Zoom"), Mathf.Tan(Mathf.Deg2Rad * Camera.main.fieldOfView * 0.5f));
        cmdBuffer.SetGlobalTexture(Shader.PropertyToID("g_EnvTexture"), envTexture);

        bool enableRayBounce = !enableRayBounceToggle || enableRayBounceToggle.isOn;
        cmdBuffer.SetGlobalInteger(Shader.PropertyToID("g_EnableRayBounce"), enableRayBounce ? 1 : 0);

        // Output
        cmdBuffer.SetRayTracingTextureParam(rayTracingShader, Shader.PropertyToID("g_Output"), rayTracingOutput);

        cmdBuffer.DispatchRays(rayTracingShader, "MainRayGenShader", cameraWidth, cameraHeight, 1);

        Graphics.ExecuteCommandBuffer(cmdBuffer);

        cmdBuffer.Release();

        Graphics.Blit(rayTracingOutput, dest);
    }
}
