using UnityEngine;

public class VolumetricSnowManager : MonoBehaviour
{
    public static VolumetricSnowManager Instance { get; private set; }

    [Header("Snow Map Settings")]
    [Tooltip("Resolution of the snow map. Higher means more detail but more VRAM usage.")]
    public int mapResolution = 1024;
    [Tooltip("The size of the area covered by the snow map in Unity units.")]
    public float mapWorldSize = 100f;
    [Tooltip("The compute shader used to process the snow trails.")]
    public ComputeShader snowCompute;
    [Tooltip("How fast the snow naturally refills over time (0.1 = 10% per second).")]
    public float fadeSpeed = 0.1f;

    private RenderTexture snowMap;
    private int fadeKernel;
    private int drawKernel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        snowMap = new RenderTexture(mapResolution, mapResolution, 0, RenderTextureFormat.RHalf);
        snowMap.enableRandomWrite = true;
        snowMap.Create();

        fadeKernel = snowCompute.FindKernel("FadeSnow");
        drawKernel = snowCompute.FindKernel("DrawSnow");

        Shader.SetGlobalTexture("_SnowMap", snowMap);
        Shader.SetGlobalFloat("_SnowMapSize", mapWorldSize);
    }

    private void Update()
    {
        if (snowCompute == null || snowMap == null) return;

        snowCompute.SetFloat("deltaTime", Time.deltaTime);
        snowCompute.SetFloat("fadeSpeed", fadeSpeed);
        snowCompute.SetTexture(fadeKernel, "SnowMap", snowMap);
        
        int threadGroupsX = Mathf.CeilToInt(mapResolution / 8f);
        int threadGroupsY = Mathf.CeilToInt(mapResolution / 8f);
        snowCompute.Dispatch(fadeKernel, threadGroupsX, threadGroupsY, 1);
    }

    public void AddImpulse(Vector3 worldPos, float radius, float amount)
    {
        if (snowCompute == null || snowMap == null) return;

        snowCompute.SetVector("drawWorldPos", new Vector2(worldPos.x, worldPos.z));
        snowCompute.SetFloat("drawRadius", radius);
        snowCompute.SetFloat("drawAmount", amount);
        snowCompute.SetFloat("mapWorldSize", mapWorldSize);
        snowCompute.SetTexture(drawKernel, "SnowMap", snowMap);

        int threadGroupsX = Mathf.CeilToInt(mapResolution / 8f);
        int threadGroupsY = Mathf.CeilToInt(mapResolution / 8f);
        snowCompute.Dispatch(drawKernel, threadGroupsX, threadGroupsY, 1);
    }

    private void OnDestroy()
    {
        if (snowMap != null) snowMap.Release();
    }
}
