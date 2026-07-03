using TMPro;
using UnityEngine;

/// <summary>
/// Buton gaze-selectabil pentru un model din biblioteca AR.
/// Creat dinamic de ModelLibraryUI, nu are nevoie de configurare in Inspector.
/// Vizual: text TMP alb, la hover devine galben, iar dupa 1.5s de dwell
/// selecteaza modelul. Implementeaza IGazeTarget pentru gaze interaction.
/// </summary>
[DisallowMultipleComponent]
public class ModelEntryButton : MonoBehaviour, IGazeTarget
{
    private ModelIndexEntry  entry;
    private ModelLibraryUI   libraryUI;
    private TMP_Text         label;
    private float            gazeTimer;
    private bool             isHovered;

    private static readonly Color ColorNormal   = Color.white;
    private static readonly Color ColorHovered  = new Color(1f, 0.85f, 0.2f);
    private static readonly Color ColorComplete = new Color(0.4f, 1f, 0.6f);

    // ── Setup ──────────────────────────────────────────────────────────────────

    public void Initialize(ModelIndexEntry modelEntry, ModelLibraryUI ui)
    {
        entry     = modelEntry;
        libraryUI = ui;

        // Collider slab pentru raycast gaze, un box flat in fata label-ului
        var box = gameObject.AddComponent<BoxCollider>();
        box.size   = new Vector3(0.7f, 0.09f, 0.01f);
        box.center = Vector3.zero;

        int gazeLayer = LayerMask.NameToLayer("GazeInteractable");
        if (gazeLayer >= 0) gameObject.layer = gazeLayer;

        // Label TMP
        label           = gameObject.AddComponent<TextMeshPro>();
        label.text      = modelEntry.title ?? modelEntry.id;
        label.fontSize  = 0.055f;
        label.color     = ColorNormal;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(0.7f, 0.09f);
    }

    // ── IGazeTarget ────────────────────────────────────────────────────────────

    public void OnGazeEnter()
    {
        isHovered = true;
        if (label != null) label.color = ColorHovered;
    }

    public void OnGazeStay(float progress)
    {
        // Interpolare vizuala: label devine mai luminos pe masura dwell-ului
        if (label != null)
            label.color = Color.Lerp(ColorHovered, ColorComplete, progress);
    }

    public void OnGazeExit()
    {
        isHovered  = false;
        gazeTimer  = 0f;
        if (label != null) label.color = ColorNormal;
    }

    public void OnGazeComplete()
    {
        if (label != null) label.color = ColorComplete;
        libraryUI?.SelectModel(entry);
    }
}
