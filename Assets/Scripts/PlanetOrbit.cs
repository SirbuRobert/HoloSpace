using UnityEngine;

/// <summary>
/// Animeaza orbita unei planete in jurul centrului sistemului solar.
///
/// Structura in scena:
///   SolarSystemRoot
///     └── OrbitPivot_Mercury      (aici sta scriptul)
///           └── Mercury (sfera)   (la distanta orbitRadius pe X)
///
/// Rotind pivotul in jurul axei Y, planeta descrie un cerc complet.
/// Rotatia proprie (planeta in jurul propriei axe) se face tot aici.
/// </summary>
public class PlanetOrbit : MonoBehaviour
{
    [Header("Orbită")]
    [Tooltip("Viteza unghiulară a orbitei în grade/secundă (față de simulationSpeed global).")]
    [SerializeField] public float orbitDegreesPerSecond = 10f;

    [Header("Rotație proprie")]
    [Tooltip("Viteza de auto-rotație a planetei (grade/secundă). 0 = fără rotație.")]
    [SerializeField] public float selfRotationDegreesPerSecond = 30f;

    [Tooltip("Transform-ul mesei (sfera propriu-zisă) pentru self-rotation. " +
             "Dacă e null, scriptul încearcă primul child.")]
    [SerializeField] private Transform planetMesh;

    [Header("Start")]
    [Tooltip("Offset unghiular inițial (grade) — distribuie planetele pe orbită la start.")]
    [SerializeField] public float initialAngle = 0f;

    // Referinta la SolarSystemBuilder pentru speedMultiplier global
    private SolarSystemBuilder builder;

    private void Awake()
    {
        // Plasam pivotul la unghiul initial
        transform.localRotation = Quaternion.Euler(0f, initialAngle, 0f);

        // Detectam automat mesh-ul daca nu e setat
        if (planetMesh == null && transform.childCount > 0)
            planetMesh = transform.GetChild(0);
    }

    private void Start()
    {
        // Cautam builder-ul in ierarhia parinte
        builder = GetComponentInParent<SolarSystemBuilder>();
    }

    private void Update()
    {
        float speed = builder != null ? builder.SimulationSpeed : 1f;

        // Orbita in jurul axei Y a pivotului (centrat pe Soare)
        transform.Rotate(Vector3.up, orbitDegreesPerSecond * speed * Time.deltaTime, Space.Self);

        // Rotatia proprie a planetei
        if (planetMesh != null)
            planetMesh.Rotate(Vector3.up, selfRotationDegreesPerSecond * speed * Time.deltaTime, Space.Self);
    }
}
