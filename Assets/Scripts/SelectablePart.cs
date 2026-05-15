using System;
using UnityEngine;

/// <summary>
/// Marks a child object of the placed model (e.g. a planet) as gaze-selectable.
/// Plugs into the existing GazeInteractor via IGazeTarget.
///
/// Visual feedback uses a subtle scale tween on this transform's localScale,
/// which composes multiplicatively with ModelController's parent-level scaling
/// (so ScaleUp / ScaleDown on the whole model keep working without conflict).
///
/// Selection itself is delegated to SelectionManager via a static event,
/// so this script compiles and runs on its own — SelectionManager subscribes
/// in step 2 of the milestone.
/// </summary>
[DisallowMultipleComponent]
public class SelectablePart : MonoBehaviour, IGazeTarget
{
    // --- Static event ---------------------------------------------------------
    // SelectionManager subscribes to this and decides what "selected" means
    // (single vs multi). SelectablePart itself stays single-responsibility:
    // detect gaze, animate, report.
    public static event Action<SelectablePart> GazeCompleted;

    // --- Inspector fields -----------------------------------------------------
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

    // --- Public API -----------------------------------------------------------
    public string DisplayName => displayName;
    public string Description => description;
    public bool IsSelected { get; private set; }

    /// <summary>
    /// Called by SelectionManager to mark this part as the selected one
    /// (or to clear its selection state when another part wins).
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshTargetScale(isHovered: false);
    }

    // --- Internal state -------------------------------------------------------
    private Vector3 baseLocalScale;
    private Vector3 targetLocalScale;
    private bool isHovered;

    // --- Unity lifecycle ------------------------------------------------------
    private void Awake()
    {
        baseLocalScale = transform.localScale;
        targetLocalScale = baseLocalScale;
    }

    private void OnDisable()
    {
        // Make sure we don't leave the GazeInteractor thinking we're still hovered
        // if this part gets disabled mid-gaze (e.g. user resets the model).
        isHovered = false;
        targetLocalScale = IsSelected
            ? baseLocalScale * selectedScaleMultiplier
            : baseLocalScale;
        transform.localScale = targetLocalScale;
    }

    private void Update()
    {
        // Cheap early-out so we don't burn CPU on parts that aren't animating.
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
        isHovered = true;
        RefreshTargetScale(isHovered: true);
    }

    public void OnGazeStay(float progress)
    {
        // Reserved for future use — e.g. radial fill on the MR panel reticle.
        // Intentionally empty: GazeInteractor already handles the dwell timer.
    }

    public void OnGazeExit()
    {
        isHovered = false;
        RefreshTargetScale(isHovered: false);
    }

    public void OnGazeComplete()
    {
        // Don't decide selection here — let SelectionManager arbitrate.
        // It's the single source of truth for "which part is selected".
        GazeCompleted?.Invoke(this);
    }

    // --- Helpers --------------------------------------------------------------
    private void RefreshTargetScale(bool isHovered)
    {
        // Selection beats hover (a selected part stays grown even after gaze leaves).
        float multiplier = IsSelected
            ? selectedScaleMultiplier
            : (isHovered ? hoverScaleMultiplier : 1f);

        targetLocalScale = baseLocalScale * multiplier;
    }
}
