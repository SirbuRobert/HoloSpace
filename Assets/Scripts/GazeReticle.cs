using UnityEngine;

/// <summary>
/// Reticulul vizual de gaze: arata unde priveste utilizatorul si progresul dwell-ului.
///
/// E format din doua parti:
///   - un ring de fundal (subtire, semi-transparent) vizibil cat timp tinta e in gaze
///   - un arc de progres (plin, alb) care se umple de la 0 la 360 de grade in 1.5s
///
/// Sta in world-space, la hit point-ul raycast-ului, orientat spre camera.
/// Raza se scaleaza cu distanta fata de camera, ca sa pastreze un unghi vizual constant.
/// Daca nu e setat manual, GazeInteractor il creeaza singur.
/// </summary>
[DisallowMultipleComponent]
public class GazeReticle : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────────

    [Header("Dimensiuni")]
    [Tooltip("Raza de bază a ring-ului la 1 metru distanță (metri).")]
    [SerializeField, Range(0.005f, 0.05f)] private float baseRadius = 0.018f;

    [Tooltip("Grosimea liniei arc de progres.")]
    [SerializeField, Range(0.001f, 0.01f)] private float fillWidth  = 0.004f;

    [Tooltip("Grosimea liniei ring de fundal (mai subțire decât arcul).")]
    [SerializeField, Range(0.0005f, 0.006f)] private float ringWidth  = 0.0015f;

    [Tooltip("Numărul de segmente al cercului. Mai mare = mai smooth, mai costisitor.")]
    [SerializeField, Range(16, 64)] private int segments = 48;

    [Header("Culori")]
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.30f);
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 1.00f);

    [Header("Scalare cu distanța")]
    [Tooltip("Raza se înmulțește cu distanța față de cameră → unghi vizual constant.")]
    [SerializeField] private bool scaleWithDistance = true;

    [Tooltip("Raza minimă indiferent de distanță (metri).")]
    [SerializeField, Range(0.005f, 0.05f)] private float minRadius = 0.010f;

    [Tooltip("Raza maximă indiferent de distanță (metri).")]
    [SerializeField, Range(0.05f, 0.40f)] private float maxRadius = 0.070f;

    // ─── Stare interna ────────────────────────────────────────────────────────────

    private LineRenderer ringLR;
    private LineRenderer fillLR;
    private Camera       arCamera;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        arCamera = Camera.main;
        ringLR   = CreateLineRenderer("Ring", ringColor, ringWidth);
        fillLR   = CreateLineRenderer("Fill", fillColor, fillWidth);
        gameObject.SetActive(false);
    }

    // ─── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Actualizeaza pozitia si progresul reticulului.
    /// Apelat de GazeInteractor in fiecare frame cat timp o tinta e in raza de gaze.
    /// </summary>
    /// <param name="worldPos">Punctul din lume unde raza de gaze a lovit colliderul.</param>
    /// <param name="cameraPos">Pozitia camerei (pentru billboard + calcul distanta).</param>
    /// <param name="progress">Progresul dwell-ului: 0 = start, 1 = complet.</param>
    public void ShowAt(Vector3 worldPos, Vector3 cameraPos, float progress)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Billboard: reticulul e orientat spre camera
        Vector3 toCamera = cameraPos - worldPos;
        transform.position = worldPos;
        if (toCamera.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized);

        // Raza scalata cu distanta, ca sa pastreze un unghi vizual constant
        float distance = toCamera.magnitude;
        float radius   = scaleWithDistance
            ? Mathf.Clamp(baseRadius * distance, minRadius, maxRadius)
            : baseRadius;

        // Grosimi scalate la fel pentru consistenta vizuala
        float rWidth = scaleWithDistance ? Mathf.Clamp(ringWidth * distance, 0.0005f, 0.006f) : ringWidth;
        float fWidth = scaleWithDistance ? Mathf.Clamp(fillWidth * distance, 0.001f,  0.012f) : fillWidth;

        DrawArc(ringLR, radius, segments, 1f,      rWidth);
        DrawArc(fillLR, radius, segments, progress, fWidth);
    }

    /// <summary>Ascunde reticulul. Apelat cand gaze-ul nu loveste nicio tinta.</summary>
    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Creeaza un LineRenderer copil pe un GameObject separat.</summary>
    private LineRenderer CreateLineRenderer(string childName, Color color, float width)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = false;  // coordonate locale fata de reticul
        lr.loop           = false;
        lr.startWidth     = width;
        lr.endWidth       = width;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Sprites/Default e inclus in orice proiect Unity si merge cu URP
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        lr.material = mat;

        return lr;
    }

    /// <summary>
    /// Deseneaza un arc de cerc pe planul XY local al reticulului.
    /// </summary>
    /// <param name="lr">LineRenderer-ul tinta.</param>
    /// <param name="radius">Raza in unitati locale (metri in scene-space).</param>
    /// <param name="totalSegments">Numarul total de segmente pentru un cerc complet.</param>
    /// <param name="progress">0..1, ce fractiune din cerc se deseneaza.</param>
    /// <param name="width">Grosimea liniei.</param>
    private static void DrawArc(LineRenderer lr, float radius, int totalSegments,
                                 float progress, float width)
    {
        lr.startWidth = width;
        lr.endWidth   = width;

        progress = Mathf.Clamp01(progress);
        bool full = progress >= 0.999f;

        int count = full
            ? totalSegments + 1                              // +1 pentru inchiderea cercului
            : Mathf.Max(2, Mathf.CeilToInt(totalSegments * progress) + 1);

        lr.positionCount = count;
        lr.loop          = full;

        for (int i = 0; i < count; i++)
        {
            float t     = i / (float)totalSegments;
            float angle = t * progress * 2f * Mathf.PI;
            // Porneste din sus (offset -PI/2), arata mai natural
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle - Mathf.PI * 0.5f) * radius,
                Mathf.Sin(angle - Mathf.PI * 0.5f) * radius,
                0f
            ));
        }
    }
}
