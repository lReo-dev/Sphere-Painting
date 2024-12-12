using Unity.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using Random = Unity.Mathematics.Random;
using Unity.Mathematics;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
public class RayMarchingMaster : MonoBehaviour
{
    private Camera m_Camera;
    private RenderTexture m_Target;
    private GraphicsBuffer m_SpheresBuffer;
    
    [Header("Rendering")]
    [SerializeField] private ComputeShader RayMarchingShader;
    [SerializeField] private Light m_DirectionalLight;

    [Header("Sphere")]
    [SerializeField] private Vector2 m_SphereRadiusRange = new Vector2(1.0f, 10.0f);
    [Range(1, 1000)][SerializeField] private int m_SpheresCount = 30;

    [Header("Generation")]
    [SerializeField] private uint m_Seed = 1;
    [SerializeField] private Bounds m_SphereSpawnBounds;

    public struct Sphere
    {
        public Vector3 position;
        public float radius;
    };

    void OnEnable()
    {
        GenerateSpheres();
    }

    void OnValidate()
    {
        GenerateSpheres();
    }

    void OnDisable()
    {
        m_SpheresBuffer?.Dispose();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        m_Camera = Camera.current;
        SetShaderParameters();
        Render(destination);
        if(m_Camera.transform.hasChanged)
        {
            m_Camera.transform.hasChanged = false;
        }
    }
    
    private void SetShaderParameters()
    {
        RayMarchingShader.SetMatrix("_CameraToWorld", m_Camera.cameraToWorldMatrix);
        RayMarchingShader.SetMatrix("_CameraInverseProjection", m_Camera.projectionMatrix.inverse);
        Vector2 clipRange = new Vector2(m_Camera.nearClipPlane, m_Camera.farClipPlane);
        RayMarchingShader.SetVector("_ClipRange", clipRange);
        Vector3 l = m_DirectionalLight.transform.forward;
        RayMarchingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, m_DirectionalLight.intensity));
        RayMarchingShader.SetBuffer(0, "_Spheres", m_SpheresBuffer);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        RayMarchingShader.SetTexture(0, "Result", m_Target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // Dispatch メソッドで ComputeShader を実行します。
        RayMarchingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit(m_Target, destination);
    }

    private void InitRenderTexture()
    {
        if (m_Target == null || m_Target.width != Screen.width || m_Target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (m_Target != null)
                m_Target.Release();

            // Get a render target for Ray Tracing
            m_Target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            m_Target.enableRandomWrite = true;
            m_Target.Create();
        }
    }

    public void GenerateSpheres()
    {
        Random random = new Random(m_Seed);
        var sphereArray = new NativeArray<Sphere>(m_SpheresCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for(int i = 0; i < sphereArray.Length; ++i)
        {
            float sphereRadius = random.NextFloat(m_SphereRadiusRange.x, m_SphereRadiusRange.y);
            float3 spherePos = (float3)m_SphereSpawnBounds.center + random.NextFloat3(-m_SphereSpawnBounds.extents + Vector3.one * sphereRadius, m_SphereSpawnBounds.extents - Vector3.one * sphereRadius);
            sphereArray[i] = new Sphere
            {
                position = spherePos,
                radius = sphereRadius
            };
        }
        GraphicsBuffer sphereBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sphereArray.Length, Marshal.SizeOf<Sphere>());
        sphereBuffer.SetData(sphereArray);
        sphereArray.Dispose();
        m_SpheresBuffer = sphereBuffer;
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.02f);
        Gizmos.DrawCube(m_SphereSpawnBounds.center, m_SphereSpawnBounds.extents*2.0f);
        Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.25f);
        Gizmos.DrawWireCube(m_SphereSpawnBounds.center, m_SphereSpawnBounds.extents*2.0f);
    }
}