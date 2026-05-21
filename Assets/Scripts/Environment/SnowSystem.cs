using UnityEngine;

/// <summary>
/// Optimized snow particle system that follows the player to create an infinite snow effect
/// without performance issues. Uses Unity's built-in Particle System with optimized settings.
/// </summary>
public class SnowSystem : MonoBehaviour
{
    [Header("Snow Settings")]
    [SerializeField] private int maxParticles = 1000;
    [SerializeField] private float emissionRate = 100f;
    [SerializeField] private float snowfallSpeed = 2f;
    [SerializeField] private float snowfallArea = 50f; // Size of the area where snow falls
    [SerializeField] private float snowHeight = 20f; // Height above player where snow spawns
    
    [Header("Appearance")]
    [SerializeField] private Material snowMaterial; // Optional: Assign custom material, or leave empty for default
    
    [Header("Wind Settings")]
    [SerializeField] private bool enableWind = true;
    [SerializeField] private float windStrength = 0.5f;
    [SerializeField] private float windVariation = 0.3f;
    
    [Header("Performance")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float updateInterval = 0.1f; // How often to update position
    
    private ParticleSystem snowParticleSystem;
    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.ShapeModule shapeModule;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private float lastUpdateTime;

    void Start()
    {
        // Try to find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogWarning("SnowSystem: Player not found! Snow will not follow player.");
                followPlayer = false;
            }
        }

        // Create or get particle system
        snowParticleSystem = GetComponent<ParticleSystem>();
        if (snowParticleSystem == null)
        {
            snowParticleSystem = gameObject.AddComponent<ParticleSystem>();
        }

        SetupParticleSystem();
    }

    void SetupParticleSystem()
    {
        // Main Module - Core settings
        mainModule = snowParticleSystem.main;
        mainModule.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(snowfallSpeed * 0.8f, snowfallSpeed * 1.2f);
        mainModule.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        mainModule.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        mainModule.startColor = new Color(1f, 1f, 1f, 0.8f);
        mainModule.gravityModifier = 0.1f; // Slight gravity for realistic fall
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World; // Important for following player
        mainModule.maxParticles = maxParticles;
        mainModule.playOnAwake = true;
        mainModule.loop = true;
        mainModule.prewarm = true; // Start with particles already in the air!

        // Emission Module - Control spawn rate
        emissionModule = snowParticleSystem.emission;
        emissionModule.rateOverTime = emissionRate;

        // Shape Module - Define spawn area (box above player)
        shapeModule = snowParticleSystem.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        shapeModule.scale = new Vector3(snowfallArea, 0.1f, snowfallArea);

        // Velocity Over Lifetime - Add wind effect
        if (enableWind)
        {
            velocityModule = snowParticleSystem.velocityOverLifetime;
            velocityModule.enabled = true;
            velocityModule.space = ParticleSystemSimulationSpace.World;
            
            // All axes must use the same curve mode - using MinMaxCurve with random between two constants
            velocityModule.x = new ParticleSystem.MinMaxCurve(-windStrength, windStrength);
            velocityModule.y = new ParticleSystem.MinMaxCurve(0f, 0f); // No vertical wind
            velocityModule.z = new ParticleSystem.MinMaxCurve(-windStrength, windStrength);
        }

        // Renderer Module - Optimize rendering
        var renderer = snowParticleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0.0001f;
        renderer.maxParticleSize = 0.5f;
        
        // Use custom material if assigned, otherwise create default
        if (snowMaterial != null)
        {
            renderer.material = snowMaterial;
            Debug.Log("Using custom snow material: " + snowMaterial.name);
        }
        else
        {
            renderer.material = CreateSnowMaterial();
            Debug.Log("Using auto-generated default snow material");
        }
        
        // Enable GPU instancing for better performance
        renderer.enableGPUInstancing = true;

        // Collision Module - Disabled for performance
        var collision = snowParticleSystem.collision;
        collision.enabled = false; // Enable only if you need snow to bounce off surfaces

        Debug.Log("Snow particle system initialized with " + maxParticles + " max particles");
    }

    Material CreateSnowMaterial()
    {
        // Create a simple unlit material for snowflakes
        Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.8f));
        mat.SetFloat("_Mode", 2); // Fade mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        
        return mat;
    }

    void Update()
    {
        // Follow player with throttled updates for performance
        if (followPlayer && playerTransform != null)
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateSnowPosition();
                lastUpdateTime = Time.time;
            }
        }
    }

    void UpdateSnowPosition()
    {
        // Position snow system above the player
        Vector3 targetPosition = playerTransform.position;
        targetPosition.y += snowHeight;
        transform.position = targetPosition;
    }

    // Public methods to control snow at runtime
    public void SetSnowIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        emissionModule.rateOverTime = emissionRate * intensity;
    }

    public void SetWindStrength(float strength)
    {
        windStrength = strength;
        if (enableWind)
        {
            velocityModule.xMultiplier = windStrength * windVariation;
            velocityModule.zMultiplier = windStrength * windVariation;
        }
    }

    public void EnableSnow(bool enable)
    {
        if (enable)
        {
            snowParticleSystem.Play();
        }
        else
        {
            snowParticleSystem.Stop();
        }
    }

    public void SetMaxParticles(int count)
    {
        maxParticles = Mathf.Clamp(count, 100, 5000);
        mainModule.maxParticles = maxParticles;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize snow area
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Vector3 center = transform.position;
        Gizmos.DrawWireCube(center, new Vector3(snowfallArea, 0.5f, snowfallArea));
    }
}
