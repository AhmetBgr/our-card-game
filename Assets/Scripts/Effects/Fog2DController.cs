using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// Drives the Custom/2D/Fog2D shader: fits a mesh to the camera view and pushes every tunable through a
// MaterialPropertyBlock, so values can be tweaked live in the inspector (edit or play mode) without
// touching the shared material. Noise is world-space, so the fog doesn't slide when the mesh follows.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Fog2DController : MonoBehaviour
{
    public enum FogQuality { High, Low, LowOnMobile }

    [Header("Setup")]
    [Tooltip("Camera to cover. Falls back to Camera.main.")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool followCamera = true;
    [Tooltip("Mesh size relative to the camera view; >1 avoids edge gaps. Off-screen area costs nothing.")]
    [SerializeField, Min(1f)] private float overscan = 1.1f;
    [SerializeField] private string sortingLayer = "Layer 2";
    [SerializeField] private int sortingOrder = 50;

    [Header("Quality")]
    [Tooltip("Low skips the swirl and the second layer: 2 texture reads per pixel instead of 4. " +
             "LowOnMobile picks Low on phones/tablets, including mobile browsers.")]
    [SerializeField] private FogQuality quality = FogQuality.LowOnMobile;
    [SerializeField] private Material highQualityMaterial;
    [SerializeField] private Material lowQualityMaterial;
    [Tooltip("Cut the fully clear core of the clear zone out of the mesh so those pixels are never drawn. " +
             "Only possible when Clear Strength is 1.")]
    [SerializeField] private bool cutClearZoneHole = true;

    [Header("Color")]
    [SerializeField] private Color fogColor = new Color(0.62f, 0.68f, 0.70f, 1f);
    [Tooltip("Tint of the densest parts of each puff.")]
    [SerializeField] private Color shadowColor = new Color(0.30f, 0.35f, 0.37f, 1f);

    [Header("Amount")]
    [SerializeField, Range(0f, 1f)] private float density = 0.45f;
    [Tooltip("How much of the screen the puffs cover.")]
    [SerializeField, Range(0f, 1f)] private float coverage = 0.45f;
    [Tooltip("Width of the puff edges.")]
    [SerializeField, Range(0.01f, 0.5f)] private float softness = 0.3f;

    [Header("Wind")]
    [Tooltip("Direction the fog travels, in degrees. 0 = right, 90 = up, 180 = left, 270 = down.")]
    [SerializeField, Range(0f, 360f)] private float windAngle = 0f;
    [Tooltip("Travel speed in world units per second.")]
    [SerializeField, Min(0f)] private float windSpeed = 0.6f;
    [Tooltip("Speed of the second layer relative to the first; != 1 gives depth.")]
    [SerializeField, Range(0f, 3f)] private float layer2SpeedMult = 1.5f;
    [Tooltip("Elongates puffs along the wind so they read as streaks.")]
    [SerializeField, Range(1f, 4f)] private float stretch = 2f;
    [Tooltip("How much the wind speeds up and slows down (0 = constant).")]
    [SerializeField, Range(0f, 1f)] private float gustStrength = 0.3f;
    [SerializeField, Min(0f)] private float gustsPerSecond = 0.15f;
    [Tooltip("How fast the puffs change shape while travelling.")]
    [SerializeField, Min(0f)] private float evolveSpeed = 0.03f;

    [Header("Shape")]
    [Tooltip("Size of a noise cell in world units; bigger = larger puffs.")]
    [SerializeField, Min(0.01f)] private float puffSize = 2.5f;
    [SerializeField, Range(0.25f, 4f)] private float layer2Scale = 1.8f;
    [SerializeField, Range(0f, 1f)] private float layer2Weight = 0.5f;
    [Tooltip("Curling of the wisps as they travel.")]
    [FormerlySerializedAs("swirl")]
    [SerializeField, Range(0f, 2f)] private float turbulence = 0.4f;

    [Header("Banks")]
    [Tooltip("Size of the large fog masses in world units.")]
    [SerializeField, Min(0.01f)] private float bankSize = 8f;
    [Tooltip("How clear the gaps between fog banks are (0 = uniform fog).")]
    [SerializeField, Range(0f, 1f)] private float bankGaps = 0.6f;

    [Header("Clear Zone")]
    [Tooltip("Fog thins out around this transform (e.g. the board). Uses Clear Center when empty.")]
    [SerializeField] private Transform clearTarget;
    [SerializeField] private Vector2 clearCenter = new Vector2(1f, -1f);
    [Tooltip("Ellipse radii in world units.")]
    [SerializeField] private Vector2 clearRadii = new Vector2(2.4f, 3.3f);
    [SerializeField, Range(0.01f, 1f)] private float clearFeather = 0.6f;
    [Tooltip("0 = no clear zone, 1 = fully clear inside (and cheaper: the core is cut out of the mesh).")]
    [SerializeField, Range(0f, 1f)] private float clearStrength = 1f;

    [Header("Screen Edge")]
    [SerializeField, Range(0f, 1f)] private float edgeVignette = 0.25f;

    [Header("Pixel Art")]
    [SerializeField] private bool pixelSnap;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 32f;
    [Tooltip("Quantise fog alpha into N bands (0 = smooth).")]
    [SerializeField, Range(0, 16)] private int alphaSteps;

    private const int HoleSegments = 32;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    private static readonly int Layer2SpeedId = Shader.PropertyToID("_Layer2Speed");
    private static readonly int StretchId = Shader.PropertyToID("_Stretch");
    private static readonly int GustStrengthId = Shader.PropertyToID("_GustStrength");
    private static readonly int GustFrequencyId = Shader.PropertyToID("_GustFrequency");
    private static readonly int EvolveId = Shader.PropertyToID("_Evolve");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int Layer2ScaleId = Shader.PropertyToID("_Layer2Scale");
    private static readonly int Layer2WeightId = Shader.PropertyToID("_Layer2Weight");
    private static readonly int WarpId = Shader.PropertyToID("_Warp");
    private static readonly int BankSizeId = Shader.PropertyToID("_BankSize");
    private static readonly int BankAmountId = Shader.PropertyToID("_BankAmount");
    private static readonly int ClearCenterId = Shader.PropertyToID("_ClearCenter");
    private static readonly int ClearSizeId = Shader.PropertyToID("_ClearSize");
    private static readonly int ClearFeatherId = Shader.PropertyToID("_ClearFeather");
    private static readonly int ClearStrengthId = Shader.PropertyToID("_ClearStrength");
    private static readonly int EdgeBoostId = Shader.PropertyToID("_EdgeBoost");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int StepsId = Shader.PropertyToID("_Steps");

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();
    private readonly List<float> angles = new List<float>();

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private MaterialPropertyBlock block;
    private Vector2 meshSize;
    private Vector2 meshHoleCenter;
    private Vector2 meshHoleRadii;
    private Coroutine densityFade;

    public float Density
    {
        get => density;
        set { density = Mathf.Clamp01(value); Apply(); }
    }

    public float WindAngle
    {
        get => windAngle;
        set { windAngle = Mathf.Repeat(value, 360f); Apply(); }
    }

    public float WindSpeed
    {
        get => windSpeed;
        set { windSpeed = Mathf.Max(0f, value); Apply(); }
    }

    public FogQuality Quality
    {
        get => quality;
        set { quality = value; ApplyQuality(); }
    }

    // Fades density over time (play mode), e.g. to roll fog in/out on a turn or event.
    public void FadeDensity(float target, float duration)
    {
        if (densityFade != null) StopCoroutine(densityFade);
        densityFade = StartCoroutine(FadeDensityRoutine(Mathf.Clamp01(target), duration));
    }

    private IEnumerator FadeDensityRoutine(float target, float duration)
    {
        float start = density;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Density = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        Density = target;
        densityFade = null;
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
            mesh = new Mesh { name = "Fog2D Mesh", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            meshSize = Vector2.zero;
        }
        meshFilter.sharedMesh = mesh;

        ApplyQuality();
        FitToCamera();
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
        FitToCamera();
        Apply();
    }

    private void LateUpdate()
    {
        FitToCamera();
        if (clearTarget != null) Apply();
    }

    private void ApplyQuality()
    {
        if (meshRenderer == null) return;
        bool low = quality == FogQuality.Low || (quality == FogQuality.LowOnMobile && Application.isMobilePlatform);
        Material material = low ? lowQualityMaterial : highQualityMaterial;
        if (material != null && meshRenderer.sharedMaterial != material) meshRenderer.sharedMaterial = material;
    }

    private void FitToCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        Vector2 size = meshSize == Vector2.zero ? new Vector2(12f, 12f) : meshSize;
        if (followCamera && cam != null && cam.orthographic)
        {
            float height = cam.orthographicSize * 2f * overscan;
            size = new Vector2(height * cam.aspect, height);

            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        // Hole = where the shader's clear mask is exactly 0: inside (1 - feather) of the ellipse, at full strength.
        // Shrunk by a pixel when pixel snap is on, since snapping can move a sample up to half a pixel outward.
        Vector2 holeCenter = Vector2.zero, holeRadii = Vector2.zero;
        bool unscaled = transform.rotation == Quaternion.identity && transform.lossyScale == Vector3.one;
        if (cutClearZoneHole && clearStrength >= 1f && unscaled)
        {
            Vector2 center = clearTarget != null ? (Vector2)clearTarget.position : clearCenter;
            float margin = pixelSnap ? 1f / pixelsPerUnit : 0.01f;
            holeCenter = center - (Vector2)transform.position;
            holeRadii = clearRadii * (1f - clearFeather) - new Vector2(margin, margin);
            if (holeRadii.x < 0.05f || holeRadii.y < 0.05f) holeRadii = Vector2.zero;
        }

        if (size != meshSize || holeCenter != meshHoleCenter || holeRadii != meshHoleRadii)
            BuildMesh(size, holeCenter, holeRadii);
    }

    private void BuildMesh(Vector2 size, Vector2 holeCenter, Vector2 holeRadii)
    {
        meshSize = size;
        meshHoleCenter = holeCenter;
        meshHoleRadii = holeRadii;

        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        vertices.Clear();
        triangles.Clear();

        bool hole = holeRadii.x > 0f && holeRadii.y > 0f
                    && Mathf.Abs(holeCenter.x) + holeRadii.x < hx
                    && Mathf.Abs(holeCenter.y) + holeRadii.y < hy;

        if (!hole)
        {
            vertices.Add(new Vector3(-hx, -hy));
            vertices.Add(new Vector3(hx, -hy));
            vertices.Add(new Vector3(-hx, hy));
            vertices.Add(new Vector3(hx, hy));
            triangles.AddRange(new[] { 0, 2, 1, 2, 3, 1 });
        }
        else
        {
            // A ring of quads between an inscribed polygon of the hole ellipse and the rectangle. Rays toward the
            // four corners are included so each quad's outer edge lies on a single side of the rectangle.
            angles.Clear();
            for (int i = 0; i < HoleSegments; i++) angles.Add(i * Mathf.PI * 2f / HoleSegments);
            AddAngle(Mathf.Atan2(hy - holeCenter.y, hx - holeCenter.x));
            AddAngle(Mathf.Atan2(hy - holeCenter.y, -hx - holeCenter.x));
            AddAngle(Mathf.Atan2(-hy - holeCenter.y, -hx - holeCenter.x));
            AddAngle(Mathf.Atan2(-hy - holeCenter.y, hx - holeCenter.x));
            angles.Sort();

            foreach (float angle in angles)
            {
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                float inner = 1f / Mathf.Sqrt(Sq(dir.x / holeRadii.x) + Sq(dir.y / holeRadii.y));
                float tx = dir.x > 1e-6f ? (hx - holeCenter.x) / dir.x : dir.x < -1e-6f ? (-hx - holeCenter.x) / dir.x : float.MaxValue;
                float ty = dir.y > 1e-6f ? (hy - holeCenter.y) / dir.y : dir.y < -1e-6f ? (-hy - holeCenter.y) / dir.y : float.MaxValue;
                float outer = Mathf.Min(tx, ty);

                vertices.Add(holeCenter + dir * inner);
                vertices.Add(holeCenter + dir * outer);
            }

            int count = angles.Count;
            for (int k = 0; k < count; k++)
            {
                int i0 = k * 2, o0 = i0 + 1;
                int i1 = (k + 1) % count * 2, o1 = i1 + 1;
                triangles.Add(i0); triangles.Add(o0); triangles.Add(o1);
                triangles.Add(i0); triangles.Add(o1); triangles.Add(i1);
            }
        }

        // UVs span the rectangle 0..1 (used by the screen-edge vignette), same as a plain quad.
        uvs.Clear();
        foreach (Vector3 v in vertices) uvs.Add(new Vector2((v.x + hx) / size.x, (v.y + hy) / size.y));

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void AddAngle(float angle)
    {
        angle = Mathf.Repeat(angle, Mathf.PI * 2f);
        foreach (float existing in angles)
            if (Mathf.Abs(existing - angle) < 1e-4f) return;
        angles.Add(angle);
    }

    private static float Sq(float v) => v * v;

    private void Apply()
    {
        if (meshRenderer == null) return;
        meshRenderer.sortingLayerName = sortingLayer;
        meshRenderer.sortingOrder = sortingOrder;

        block ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);

        Vector2 center = clearTarget != null ? (Vector2)clearTarget.position : clearCenter;
        float rad = windAngle * Mathf.Deg2Rad;

        block.SetColor(ColorId, fogColor);
        block.SetColor(ShadowColorId, shadowColor);
        block.SetFloat(DensityId, density);
        block.SetFloat(CoverageId, coverage);
        block.SetFloat(SoftnessId, softness);
        block.SetVector(WindDirId, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)));
        block.SetFloat(WindSpeedId, windSpeed);
        block.SetFloat(Layer2SpeedId, layer2SpeedMult);
        block.SetFloat(StretchId, stretch);
        block.SetFloat(GustStrengthId, gustStrength);
        block.SetFloat(GustFrequencyId, gustsPerSecond);
        block.SetFloat(EvolveId, evolveSpeed);
        block.SetFloat(NoiseScaleId, puffSize);
        block.SetFloat(Layer2ScaleId, layer2Scale);
        block.SetFloat(Layer2WeightId, layer2Weight);
        block.SetFloat(WarpId, turbulence);
        block.SetFloat(BankSizeId, bankSize);
        block.SetFloat(BankAmountId, bankGaps);
        block.SetVector(ClearCenterId, center);
        block.SetVector(ClearSizeId, clearRadii);
        block.SetFloat(ClearFeatherId, clearFeather);
        block.SetFloat(ClearStrengthId, clearStrength);
        block.SetFloat(EdgeBoostId, edgeVignette);
        block.SetFloat(PixelSizeId, pixelSnap ? 1f / pixelsPerUnit : 0f);
        block.SetFloat(StepsId, alphaSteps);

        meshRenderer.SetPropertyBlock(block);
    }
}
