using System;
using UnityEngine;

/// <summary>
/// Marcheaza un obiect copil al modelului plasat (ex. o planeta) ca gaze-selectabil.
/// Se leaga de GazeInteractor prin IGazeTarget.
///
/// Feedback-ul vizual e un scale tween subtil pe localScale-ul acestui transform,
/// care se compune multiplicativ cu scalarea de la nivelul parintelui din
/// ModelController (asa ScaleUp / ScaleDown pe tot modelul raman fara conflict).
///
/// Selectia propriu-zisa e delegata catre SelectionManager printr-un eveniment
/// static, deci scriptul functioneaza si de unul singur.
/// </summary>
[DisallowMultipleComponent]
public class SelectablePart : MonoBehaviour, IGazeTarget
{
    // --- Eveniment static -----------------------------------------------------
    // SelectionManager asculta asta si decide ce inseamna "selectat"
    // (single vs multi). SelectablePart ramane cu o singura responsabilitate:
    // detecteaza gaze, animeaza, raporteaza.
    public static event Action<SelectablePart> GazeCompleted;

    // --- Campuri Inspector ----------------------------------------------------
    [Header("Identity")]
    [Tooltip("Pretty name shown on the MR panel (e.g. \"Mars\"). " +
             "Independent of GameObject.name so meshes can be called anything.")]
    [SerializeField] private string displayName = "Unknown";

    [Tooltip("Short factual description. Used later as fallback context for AskAI.")]
    [TextArea(2, 5)]
    [SerializeField] private string description = "";

    [Header("Hover / Select scale feedback")]
    [Tooltip("Scale multiplier applied while the user is gazing but not yet selected.")]
    [SerializeField, Range(1f, 1.5f)] private float hoverScaleMultiplier = 1.05f;

    [Tooltip("Scale multiplier applied once this part is the currently selected one.")]
    [SerializeField, Range(1f, 1.5f)] private float selectedScaleMultiplier = 1.10f;

    [Tooltip("How quickly the scale tween reaches its target. Higher = snappier.")]
    [SerializeField, Range(1f, 30f)] private float scaleLerpSpeed = 10f;

    // --- API public -----------------------------------------------------------
    public string DisplayName => displayName;
    public string Description => description;
    public bool IsSelected { get; private set; }

    /// <summary>
    /// Seteaza numele si descrierea la runtime (folosit de SolarSystemBuilder).
    /// </summary>
    public void Initialize(string name, string desc)
    {
        displayName = name;
        description = desc;
    }

    /// <summary>
    /// Apelat de SelectionManager ca sa marcheze aceasta parte ca fiind cea
    /// selectata (sau ca sa ii curete starea cand alta parte castiga selectia).
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshTargetScale(isHovered: false);
    }

    // --- Stare interna --------------------------------------------------------
    private Vector3 baseLocalScale;
    private Vector3 targetLocalScale;

    // --- Unity lifecycle ------------------------------------------------------
    private void Awake()
    {
        baseLocalScale = transform.localScale;
        targetLocalScale = baseLocalScale;
    }

    private void OnDisable()
    {
        // Ne asiguram ca nu lasam GazeInteractor sa creada ca inca suntem in hover
        // daca partea e dezactivata in mijlocul gaze-ului (ex. user reseteaza modelul).
        targetLocalScale = IsSelected
            ? baseLocalScale * selectedScaleMultiplier
            : baseLocalScale;
        transform.localScale = targetLocalScale;
    }

    private void Update()
    {
        // Iesire rapida ca sa nu consumam CPU pe parti care nu se animeaza.
        if ((transform.localScale - targetLocalScale).sqrMagnitude < 0.000001f)
            return;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetLocalScale,
            Time.deltaTime * scaleLerpSpeed
        );
    }

    // --- IGazeTarget ----------------------------------------------------------
    public void OnGazeEnter()
    {
        RefreshTargetScale(isHovered: true);
    }

    public void OnGazeStay(float progress)
    {
        // Rezervat pentru mai tarziu, ex. radial fill pe reticulul panoului MR.
        // Lasat gol intentionat: GazeInteractor se ocupa deja de timerul de dwell.
    }

    public void OnGazeExit()
    {
        RefreshTargetScale(isHovered: false);
    }

    public void OnGazeComplete()
    {
        // Nu decidem selectia aici, lasam SelectionManager sa arbitreze.
        // El e singura sursa de adevar pentru "ce parte e selectata".
        GazeCompleted?.Invoke(this);
    }

    // --- Helpers --------------------------------------------------------------
    private void RefreshTargetScale(bool isHovered)
    {
        // Selectia bate hover-ul (o parte selectata ramane marita si dupa ce gaze-ul pleaca).
        float multiplier = IsSelected
            ? selectedScaleMultiplier
            : (isHovered ? hoverScaleMultiplier : 1f);

        targetLocalScale = baseLocalScale * multiplier;
    }
}
