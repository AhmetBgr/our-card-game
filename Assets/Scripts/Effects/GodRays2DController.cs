using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Drives the Custom/2D/GodRays2D shader. Builds one quad per light shaft with the ray's parameters baked into its
// vertex data; the shader constructs the geometry and animates sway/flicker, so nothing is rebuilt per frame. The
// mesh is only regenerated when a layout setting changes. Look values go through a MaterialPropertyBlock, so the
// shared material stays untouched and everything can be tuned live in the inspector (edit or play mode).
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GodRays2DController : MonoBehaviour
{
    public enum RayQuality { High, Low, LowOnMobile }

    [Header("Setup")]
    [Tooltip("Camera to follow. Falls back to Camera.main.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Keep the rays centred on the camera. Layout offsets are relative to this object.")]
    [SerializeField] private bool followCamera = true;
    [SerializeField] private string sortingLayer = "Layer 2";
    [SerializeField] private int sortingOrder = 60;

    [Header("Quality")]
    [Tooltip("Low skips the dust streaks: no texture reads per pixel instead of one. " +
             "LowOnMobile picks Low on phones/tablets, including mobile browsers.")]
    [SerializeField] private RayQuality quality = RayQuality.LowOnMobile;
    [SerializeField] private Material highQualityMaterial;
    [SerializeField] private Material lowQualityMaterial;

    [Header("Light")]
    [SerializeField] private Color lightColor = new Color(1f, 0.9f, 0.65f, 1f);
    [SerializeField, Range(0f, 2f)] private float intensity = 0.22f;

    [Header("Layout")]
    [Tooltip("Direction the light travels, in degrees. 0 = right, 90 = up, 270 = down; 300 = from the top-left.")]
    [SerializeField, Range(0f, 360f)] private float lightAngle = 300f;
    [Tooltip("Changes the random widths, gaps and timing of the shafts.")]
    [SerializeField] private int seed = 7;
    [SerializeField, Range(1, 24)] private int rayCount = 7;
    [Tooltip("Width of the band the shafts are spread across, in world units.")]
    [SerializeField, Min(0f)] private float spread = 12f;
    [Tooltip("Irregular spacing between shafts (0 = evenly spaced).")]
    [SerializeField, Range(0f, 1f)] private float spacingJitter = 0.7f;
    [Tooltip("Shafts fan out by this many degrees across the band, as if from a nearby source.")]
    [SerializeField, Range(0f, 60f)] private float fanAngle = 10f;
    [SerializeField, Range(0f, 15f)] private float angleJitter = 2f;
    [Tooltip("How far behind the centre (against the light) the shafts start, in world units.")]
    [SerializeField] private float sourceDistance = 8f;
    [Tooltip("Shifts the whole ray pattern, in world units.")]
    [SerializeField] private Vector2 sourceOffset = Vector2.zero;

    [Header("Shafts")]
    [SerializeField, Min(0.1f)] private float length = 16f;
    [SerializeField, Range(0f, 1f)] private float lengthVariation = 0.35f;
    [Tooltip("Random width range of a shaft at its source, in world units.")]
    [SerializeField] private Vector2 widthRange = new Vector2(0.8f, 2.2f);
    [Tooltip("Width at the tail relative to the source (>1 = widens with distance).")]
    [SerializeField, Range(0.2f, 4f)] private float widthGrowth = 1.6f;
    [Tooltip("Random per-shaft brightness difference.")]
    [SerializeField, Range(0f, 1f)] private float brightnessVariation = 0.6f;
    [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 1f;
    [Tooltip("Fade-in length at the source, as a fraction of the shaft.")]
    [SerializeField, Range(0.01f, 1f)] private float sourceFade = 0.15f;
    [Tooltip("Fade-out length at the tail, as a fraction of the shaft.")]
    [SerializeField, Range(0.01f, 1f)] private float tailFade = 0.6f;

    [Header("Motion")]
    [Tooltip("How far shafts swing around their source, in degrees.")]
    [SerializeField, Range(0f, 10f)] private float swayDegrees = 1.5f;
    [SerializeField, Min(0f)] private float swaySpeed = 0.3f;
    [Tooltip("How much each shaft dims and brightens over time (1 = fades out completely).")]
    [SerializeField, Range(0f, 1f)] private float flicker = 0.5f;
    [SerializeField, Min(0f)] private float flickerSpeed = 0.4f;

    [Header("Dust (High quality only)")]
    [Tooltip("Streaky breakup inside the shafts.")]
    [SerializeField, Range(0f, 1f)] private float dustAmount = 0.5f;
    [SerializeField, Min(0.01f)] private float dustSize = 0.6f;
    [SerializeField, Range(1f, 8f)] private float dustStretch = 4f;
    [Tooltip("Drift of the streaks across the light, in world units per second.")]
    [SerializeField] private float dustDrift = 0.15f;

    [Header("Clear Zone")]
    [Tooltip("Shafts fade around this transform (e.g. the board). Uses Clear Center when empty.")]
    [SerializeField] private Transform clearTarget;
    [SerializeField] private Vector2 clearCenter = new Vector2(1f, -1f);
    [Tooltip("Ellipse radii in world units.")]
    [SerializeField] private Vector2 clearRadii = new Vector2(4f, 6f);
    [SerializeField, Range(0.01f, 1f)] private float clearFeather = 0.5f;
    [Tooltip("0 = no clear zone, 1 = no light inside.")]
    [SerializeField, Range(0f, 1f)] private float clearStrength = 0.5f;

    [Header("Pixel Art")]
    [SerializeField] private bool pixelSnap;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 32f;
    [Tooltip("Quantise brightness into N bands (0 = smooth).")]
    [SerializeField, Range(0, 16)] private int brightnessSteps;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int SourceFadeId = Shader.PropertyToID("_SourceFade");
    private static readonly int TailFadeId = Shader.PropertyToID("_TailFade");
    private static readonly int SwayAmountId = Shader.PropertyToID("_SwayAmount");
    private static readonly int SwaySpeedId = Shader.PropertyToID("_SwaySpeed");
    private static readonly int FlickerId = Shader.PropertyToID("_Flicker");
    private static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
    private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
    private static readonly int DustAmountId = Shader.PropertyToID("_DustAmount");
    private static readonly int DustScaleId = Shader.PropertyToID("_DustScale");
    private static readonly int DustStretchId = Shader.PropertyToID("_DustStretch");
    private static readonly int DustSpeedId = Shader.PropertyToID("_DustSpeed");
    private static readonly int ClearCenterId = Shader.PropertyToID("_ClearCenter");
    private static readonly int ClearSizeId = Shader.PropertyToID("_ClearSize");
    private static readonly int ClearFeatherId = Shader.PropertyToID("_ClearFeather");
    private static readonly int ClearStrengthId = Shader.PropertyToID("_ClearStrength");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int StepsId = Shader.PropertyToID("_Steps");

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> corners = new List<Vector2>();
    private readonly List<Vector4> rays = new List<Vector4>();
    private readonly List<Vector4> anims = new List<Vector4>();
    private readonly List<int> triangles = new List<int>();

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private MaterialPropertyBlock block;
    private Coroutine intensityFade;

    public float Intensity
    {
        get => intensity;
        set { intensity = Mathf.Clamp(value, 0f, 2f); Apply(); }
    }

    public float LightAngle
    {
        get => lightAngle;
        set { lightAngle = Mathf.Repeat(value, 360f); BuildMesh(); Apply(); }
    }

    public RayQuality Quality
    {
        get => quality;
        set { quality = value; ApplyQuality(); }
    }

    // Fades intensity over time (play mode), e.g. to bring the light in on a turn or event.
    public void FadeIntensity(float target, float duration)
    {
        if (intensityFade != null) StopCoroutine(intensityFade);
        intensityFade = StartCoroutine(FadeIntensityRoutine(Mathf.Clamp(target, 0f, 2f), duration));
    }

    private IEnumerator FadeIntensityRoutine(float target, float duration)
    {
        float start = intensity;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Intensity = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        Intensity = target;
        intensityFade = null;
    }

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (mesh == null)
        {
            mesh = new Mesh { name = "GodRays2D Mesh", hideFlags = HideFlags.DontSave };
        }
        meshFilter.sharedMesh = mesh;

        ApplyQuality();
        FollowCamera();
        BuildMesh();
        Apply();
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled || meshRenderer == null) return;
        ApplyQuality();
        BuildMesh();
        Apply();
    }

    private void LateUpdate()
    {
        FollowCamera();
        if (clearTarget != null) Apply();
    }

    private void ApplyQuality()
    {
        if (meshRenderer == null) return;
        bool low = quality == RayQuality.Low || (quality == RayQuality.LowOnMobile && Application.isMobilePlatform);
        Material material = low ? lowQualityMaterial : highQualityMaterial;
        if (material != null && meshRenderer.sharedMaterial != material) meshRenderer.sharedMaterial = material;
    }

    private void FollowCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (!followCamera || cam == null) return;

        Vector3 camPos = cam.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void BuildMesh()
    {
        if (mesh == null) return;

        vertices.Clear();
        corners.Clear();
        rays.Clear();
        anims.Clear();
        triangles.Clear();

        var random = new System.Random(seed);
        float Next() => (float)random.NextDouble();

        float baseRad = lightAngle * Mathf.Deg2Rad;
        var baseDir = new Vector2(Mathf.Cos(baseRad), Mathf.Sin(baseRad));
        var basePerp = new Vector2(-baseDir.y, baseDir.x);
        float swayRad = swayDegrees * Mathf.Deg2Rad;
        float minWidth = Mathf.Max(0.01f, Mathf.Min(widthRange.x, widthRange.y));
        float maxWidth = Mathf.Max(minWidth, Mathf.Max(widthRange.x, widthRange.y));

        Vector2 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 boundsMax = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < rayCount; i++)
        {
            float slot = rayCount == 1 ? 0.5f : i / (rayCount - 1f);
            float across = (slot - 0.5f + (Next() - 0.5f) * spacingJitter / rayCount) * spread;
            float angle = baseRad + ((slot - 0.5f) * fanAngle + (Next() - 0.5f) * angleJitter) * Mathf.Deg2Rad;

            Vector2 origin = sourceOffset - baseDir * sourceDistance + basePerp * across;
            float rayLength = length * Mathf.Lerp(1f - lengthVariation, 1f, Next());
            float sourceWidth = Mathf.Lerp(minWidth, maxWidth, Next());
            float tailWidth = sourceWidth * widthGrowth;

            float phase = Next() * 100f;
            float brightness = Mathf.Lerp(1f - brightnessVariation, 1f, Next());
            float flickerMult = Mathf.Lerp(0.6f, 1.4f, Next());
            float swayMult = Mathf.Lerp(0.7f, 1.3f, Next());

            int start = vertices.Count;
            for (int end = 0; end < 2; end++)
            for (int side = 0; side < 2; side++)
            {
                vertices.Add(origin);
                corners.Add(new Vector2(side * 2 - 1, end));
                rays.Add(new Vector4(angle, rayLength, sourceWidth, tailWidth));
                anims.Add(new Vector4(phase, brightness, flickerMult, swayMult));
            }
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);

            // Every vertex sits at the ray origin (the shader moves it), so bounds must be computed by hand:
            // the tail at both sway extremes, padded by the widest part of the shaft.
            float pad = Mathf.Max(sourceWidth, tailWidth) * 0.5f;
            Encapsulate(ref boundsMin, ref boundsMax, origin, pad);
            Encapsulate(ref boundsMin, ref boundsMax, origin + Direction(angle - swayRad) * rayLength, pad);
            Encapsulate(ref boundsMin, ref boundsMax, origin + Direction(angle + swayRad) * rayLength, pad);
            Encapsulate(ref boundsMin, ref boundsMax, origin + Direction(angle) * rayLength, pad);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, rays);
        mesh.SetUVs(2, anims);
        mesh.SetTriangles(triangles, 0, false);
        mesh.bounds = rayCount > 0
            ? new Bounds((boundsMin + boundsMax) * 0.5f, new Vector3(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 0.1f))
            : new Bounds();
    }

    private static Vector2 Direction(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

    private static void Encapsulate(ref Vector2 min, ref Vector2 max, Vector2 point, float pad)
    {
        min = Vector2.Min(min, point - new Vector2(pad, pad));
        max = Vector2.Max(max, point + new Vector2(pad, pad));
    }

    private void Apply()
    {
        if (meshRenderer == null) return;
        meshRenderer.sortingLayerName = sortingLayer;
        meshRenderer.sortingOrder = sortingOrder;

        block ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);

        Vector2 center = clearTarget != null ? (Vector2)clearTarget.position : clearCenter;
        float rad = lightAngle * Mathf.Deg2Rad;

        block.SetColor(ColorId, lightColor);
        block.SetFloat(IntensityId, intensity);
        block.SetFloat(SoftnessId, edgeSoftness);
        block.SetFloat(SourceFadeId, sourceFade);
        block.SetFloat(TailFadeId, tailFade);
        block.SetFloat(SwayAmountId, swayDegrees * Mathf.Deg2Rad);
        block.SetFloat(SwaySpeedId, swaySpeed);
        block.SetFloat(FlickerId, flicker);
        block.SetFloat(FlickerSpeedId, flickerSpeed);
        block.SetVector(LightDirId, Direction(rad));
        block.SetFloat(DustAmountId, dustAmount);
        block.SetFloat(DustScaleId, dustSize);
        block.SetFloat(DustStretchId, dustStretch);
        block.SetFloat(DustSpeedId, dustDrift);
        block.SetVector(ClearCenterId, center);
        block.SetVector(ClearSizeId, clearRadii);
        block.SetFloat(ClearFeatherId, clearFeather);
        block.SetFloat(ClearStrengthId, clearStrength);
        block.SetFloat(PixelSizeId, pixelSnap ? 1f / pixelsPerUnit : 0f);
        block.SetFloat(StepsId, brightnessSteps);

        meshRenderer.SetPropertyBlock(block);
    }
}
