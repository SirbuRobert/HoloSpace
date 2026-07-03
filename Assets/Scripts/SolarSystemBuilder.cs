using System;
using UnityEngine;

/// <summary>
/// Construieste sistemul solar la runtime ca set de sfere colorate URP.
/// Componenta sta pe prefab-ul plasat de ARPlacementManager.
///
/// Structura in scena dupa Build():
///   [acest GameObject] - radacina sistemului solar
///     ├── Sun
///     ├── OrbitPivot_Mercury
///     │     └── Mercury
///     ├── OrbitPivot_Venus
///     │     └── Venus
///     └── ... (restul planetelor)
///
/// Fiecare planeta are: MeshRenderer, SphereCollider, SelectablePart, PlanetOrbit.
/// Soarele are emisie activa (straluceste), iar Saturn primeste un inel ca child cylinder.
/// </summary>
public class SolarSystemBuilder : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Simulare")]
    [Tooltip("Multiplicator global de viteză pentru orbite și auto-rotație. " +
             "1 = viteză default, 0 = pauză, 2 = dublu.")]
    [Range(0f, 5f)]
    [SerializeField] private float simulationSpeed = 1f;

    [Header("Scală vizuală")]
    [Tooltip("Factorul cu care se înmulțesc razele și orbitele (default 1 = valori în metri).")]
    [Range(0.3f, 3f)]
    [SerializeField] private float scaleFactor = 1f;

    // Layer-ul "GazeInteractable" e citit din TagManager la runtime (vezi Build()).

    // ─── Proprietate publica accesata de PlanetOrbit ─────────────────────────────

    /// <summary>Multiplicatorul de viteza citit de PlanetOrbit in Update.</summary>
    public float SimulationSpeed => simulationSpeed;

    // ─── Date planete ─────────────────────────────────────────────────────────────

    // Raze vizuale (metri), exagerate fata de real pentru lizibilitate in AR.
    // Soare = 0.12m referinta; planetele sunt scalate relativ, nu proportional real.
    private static readonly PlanetData[] Planets = new PlanetData[]
    {
        new PlanetData(
            name:           "Soare",
            texFunc:        PlanetTextureGenerator.ForSun,
            radius:         0.12f,
            orbitRadius:    0f,
            orbitSpeed:     0f,
            selfRotSpeed:   5f,
            initialAngle:   0f,
            color:          new Color(1.00f, 0.90f, 0.20f),
            emissionColor:  new Color(1.00f, 0.60f, 0.00f) * 1.5f,
            description:    "Steaua centrală a sistemului nostru solar. " +
                            "Are un diametru de 1.4 milioane km și conține 99.8% din masa întregului sistem solar. " +
                            "Temperatura la suprafață ajunge la 5500°C."
        ),
        new PlanetData(
            name:           "Mercur",
            texFunc:        PlanetTextureGenerator.ForMercury,
            radius:         0.016f,
            orbitRadius:    0.22f,
            orbitSpeed:     38f,
            selfRotSpeed:   2f,
            initialAngle:   0f,
            color:          new Color(0.65f, 0.60f, 0.58f),
            description:    "Cea mai mică planetă și cea mai apropiată de Soare. " +
                            "O zi pe Mercur durează 59 de zile pământești, " +
                            "iar temperatura variază dramatic între -180°C și +430°C."
        ),
        new PlanetData(
            name:           "Venus",
            texFunc:        PlanetTextureGenerator.ForVenus,
            radius:         0.030f,
            orbitRadius:    0.33f,
            orbitSpeed:     24f,
            selfRotSpeed:   -3f,
            initialAngle:   60f,
            color:          new Color(0.92f, 0.82f, 0.52f),
            description:    "Cea mai fierbinte planetă din sistemul solar, cu 465°C la suprafață, " +
                            "din cauza efectului de seră extrem. " +
                            "Rotația sa este retrogradă — Soarele răsare la vest."
        ),
        new PlanetData(
            name:           "Pământ",
            texFunc:        PlanetTextureGenerator.ForEarth,
            radius:         0.032f,
            orbitRadius:    0.45f,
            orbitSpeed:     18f,
            selfRotSpeed:   25f,
            initialAngle:   130f,
            color:          new Color(0.20f, 0.50f, 0.90f),
            description:    "Singura planetă știută că adăpostește viață. " +
                            "Are o atmosferă bogată în azot și oxigen " +
                            "și este acoperită 71% de apă lichidă."
        ),
        new PlanetData(
            name:           "Marte",
            texFunc:        PlanetTextureGenerator.ForMars,
            radius:         0.024f,
            orbitRadius:    0.58f,
            orbitSpeed:     13f,
            selfRotSpeed:   24f,
            initialAngle:   200f,
            color:          new Color(0.80f, 0.32f, 0.12f),
            description:    "Planeta roșie, numită astfel din cauza oxidului de fier de pe suprafață. " +
                            "Are cel mai înalt vulcan din sistemul solar — Olympus Mons, de 22 km înălțime."
        ),
        new PlanetData(
            name:           "Jupiter",
            texFunc:        PlanetTextureGenerator.ForJupiter,
            radius:         0.075f,
            orbitRadius:    0.83f,
            orbitSpeed:     7f,
            selfRotSpeed:   45f,
            initialAngle:   280f,
            color:          new Color(0.86f, 0.68f, 0.48f),
            description:    "Cea mai mare planetă din sistemul solar, de 1300 de ori mai mare decât Pământul. " +
                            "Marea Pată Roșie este o furtună care durează de peste 400 de ani."
        ),
        new PlanetData(
            name:           "Saturn",
            texFunc:        PlanetTextureGenerator.ForSaturn,
            radius:         0.062f,
            orbitRadius:    1.08f,
            orbitSpeed:     5f,
            selfRotSpeed:   40f,
            initialAngle:   40f,
            color:          new Color(0.90f, 0.82f, 0.60f),
            hasRings:       true,
            description:    "Cunoscut pentru inelele sale spectaculoase formate din gheață și rocă. " +
                            "Are 146 de luni confirmate, inclusiv Titan, care are o atmosferă densă."
        ),
        new PlanetData(
            name:           "Uranus",
            texFunc:        PlanetTextureGenerator.ForUranus,
            radius:         0.045f,
            orbitRadius:    1.30f,
            orbitSpeed:     3f,
            selfRotSpeed:   -15f,
            initialAngle:   160f,
            color:          new Color(0.42f, 0.82f, 0.90f),
            description:    "Gigantă de gheață cu axa de rotație înclinată la 98°, orbitând practic pe o parte. " +
                            "Temperatura minimă de -224°C o face cea mai rece planetă din sistemul solar."
        ),
        new PlanetData(
            name:           "Neptun",
            texFunc:        PlanetTextureGenerator.ForNeptune,
            radius:         0.043f,
            orbitRadius:    1.52f,
            orbitSpeed:     2f,
            selfRotSpeed:   28f,
            initialAngle:   310f,
            color:          new Color(0.10f, 0.28f, 0.90f),
            description:    "Cea mai îndepărtată planetă, cu vânturi de până la 2100 km/h — " +
                            "cele mai rapide din sistemul solar. " +
                            "O orbită completă în jurul Soarelui durează 165 de ani pământești."
        ),
    };

    // ─── Unity lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        Build();
    }

    // ─── Build ───────────────────────────────────────────────────────────────────

    private void Build()
    {
        // Cautam "GazeInteractable" direct din TagManager, fara dependenta de Inspector.
        int layer = LayerMask.NameToLayer("GazeInteractable");
        if (layer < 0)
        {
            Debug.LogError("[SolarSystemBuilder] Layer 'GazeInteractable' nu există în proiect! " +
                           "Mergi la Project Settings → Tags and Layers și verifică layer 6.");
            layer = gameObject.layer; // fallback
        }

        Debug.Log($"[SolarSystemBuilder] Build() pornit — layer={layer} " +
                  $"({LayerMask.LayerToName(layer)}), scaleFactor={scaleFactor}");

        foreach (PlanetData data in Planets)
        {
            if (data.orbitRadius < 0.001f)
                BuildSun(data, layer);
            else
                BuildPlanet(data, layer);
        }

        Debug.Log($"[SolarSystemBuilder] ✓ Construit {Planets.Length} obiecte pe layer '{LayerMask.LayerToName(layer)}'.");
    }

    // ─── Sun ─────────────────────────────────────────────────────────────────────

    private void BuildSun(PlanetData data, int layer)
    {
        GameObject sun = CreateSphere(data.name, transform,
                                      Vector3.zero, data.radius * scaleFactor,
                                      data.color, layer,
                                      texFunc: data.texFunc,
                                      emissionColor: data.emissionColor);

        // Soarele se roteste pe loc: PlanetOrbit cu orbita 0
        var selfSpin = sun.AddComponent<PlanetOrbit>();
        selfSpin.orbitDegreesPerSecond   = 0f;
        selfSpin.selfRotationDegreesPerSecond = data.selfRotSpeed;

        var part = sun.AddComponent<SelectablePart>();
        part.Initialize(data.name, data.description);

        Debug.Log($"[SolarSystemBuilder] Soare creat la origin.");
    }

    // ─── Planeta ─────────────────────────────────────────────────────────────────

    private void BuildPlanet(PlanetData data, int layer)
    {
        // Pivot de orbita (se roteste in jurul Soarelui)
        GameObject pivot = new GameObject($"OrbitPivot_{data.name}");
        pivot.transform.SetParent(transform, false);
        pivot.transform.localPosition = Vector3.zero;

        // Sfera planetei, deplasata pe X fata de pivot (= raza orbitei)
        float orbitR = data.orbitRadius * scaleFactor;
        GameObject planet = CreateSphere(data.name, pivot.transform,
                                         new Vector3(orbitR, 0f, 0f),
                                         data.radius * scaleFactor,
                                         data.color, layer,
                                         texFunc: data.texFunc);

        // Inel pentru Saturn
        if (data.hasRings)
            AddRings(planet.transform, data.radius * scaleFactor);

        // Script orbita pe pivot
        var orbit = pivot.AddComponent<PlanetOrbit>();
        orbit.orbitDegreesPerSecond          = data.orbitSpeed;
        orbit.selfRotationDegreesPerSecond   = data.selfRotSpeed;
        orbit.initialAngle                   = data.initialAngle;

        // SelectablePart pe sfera (nu pe pivot)
        var part = planet.AddComponent<SelectablePart>();
        part.Initialize(data.name, data.description);

        Debug.Log($"[SolarSystemBuilder] {data.name} — orbit {orbitR:F2}m, radius {data.radius * scaleFactor:F3}m");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static GameObject CreateSphere(string objName, Transform parent,
                                           Vector3 localPos, float radius,
                                           Color color, int layer,
                                           Func<Texture2D> texFunc = null,
                                           Color emissionColor = default)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = objName;
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * (radius * 2f);

        if (go.GetComponent<SphereCollider>() == null)
            go.AddComponent<SphereCollider>();

        // Material URP
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        if (texFunc != null)
        {
            Texture2D tex = texFunc();
            mat.mainTexture = tex;
            // Cu textura, tint-ul ramane alb ca sa nu deformeze culorile
            mat.color = Color.white;
        }
        else
        {
            mat.color = color;
        }

        bool hasEmission = emissionColor != default && emissionColor != Color.black;
        if (hasEmission)
        {
            mat.EnableKeyword("_EMISSION");
            // Emisie pe textura soarelui, nu culoare fixa
            mat.SetColor("_EmissionColor", texFunc != null
                ? new Color(1.0f, 0.5f, 0.0f) * 0.8f
                : emissionColor);
        }

        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    private static void AddRings(Transform planetTransform, float planetRadius)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Rings";
        ring.transform.SetParent(planetTransform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = new Vector3(2.5f, 0.03f, 2.5f);

        // Stergem orice collider de pe inel, nu vrem sa interfereze cu gaze
        // (CreatePrimitive poate adauga CapsuleCollider, acoperim ambele cazuri)
        foreach (Collider col in ring.GetComponents<Collider>())
            UnityEngine.Object.Destroy(col);

        Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMat.color = new Color(0.82f, 0.72f, 0.50f);
        ring.GetComponent<Renderer>().material = ringMat;
    }

    // ─── PlanetData ──────────────────────────────────────────────────────────────

    private class PlanetData
    {
        public readonly string          name;
        public readonly Func<Texture2D> texFunc;     // generator procedural de textura
        public readonly float           radius;
        public readonly float           orbitRadius;
        public readonly float           orbitSpeed;
        public readonly float           selfRotSpeed;
        public readonly float           initialAngle;
        public readonly Color           color;       // fallback daca textura lipseste
        public readonly Color           emissionColor;
        public readonly bool            hasRings;
        public readonly string          description;

        public PlanetData(string name, float radius, float orbitRadius,
                          float orbitSpeed, float selfRotSpeed, float initialAngle,
                          Color color, string description,
                          Func<Texture2D> texFunc = null,
                          Color emissionColor = default, bool hasRings = false)
        {
            this.name           = name;
            this.texFunc        = texFunc;
            this.radius         = radius;
            this.orbitRadius    = orbitRadius;
            this.orbitSpeed     = orbitSpeed;
            this.selfRotSpeed   = selfRotSpeed;
            this.initialAngle   = initialAngle;
            this.color          = color;
            this.emissionColor  = emissionColor;
            this.hasRings       = hasRings;
            this.description    = description;
        }
    }
}
