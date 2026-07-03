using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Buton world-space mereu vizibil care deschide/inchide ModelLibraryUI.
/// Urmareste camera cu un offset configurat ca sa fie mereu accesibil,
/// fara sa blocheze centrul vizualului AR.
/// </summary>
[DisallowMultipleComponent]
public class LibraryButton : MonoBehaviour, IGazeTarget
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("ModelLibraryUI din scenă. Dacă e null, se caută automat.")]
    [SerializeField] private ModelLibraryUI libraryUI;

    [Header("Poziție față de cameră")]
    [Tooltip("Distanța față de cameră (metri).")]
    [SerializeField, Range(0.3f, 2f)] private float distance = 0.8f;

    [Tooltip("Offset orizontal (+ = dreapta, - = stânga).")]
    [SerializeField, Range(-0.5f, 0.5f)] private float horizontalOffset = 0.25f;

    [Tooltip("Offset vertical (+ = sus, - = jos).")]
    [SerializeField, Range(-0.5f, 0.5f)] private float verticalOffset = -0.2f;

    [Header("Aspect")]
    [SerializeField] private string buttonText = "[ Library ]";
    [SerializeField] private Color normalColor  = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color hoverColor   = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color completeColor = new Color(0.4f, 1f, 0.6f, 1f);

    // ─── State interna ────────────────────────────────────────────────────────

    private Camera   arCamera;
    private TMP_Text label;
    private bool     isHovered;

    // Cand utilizatorul priveste butonul, acesta ingheata.
    // Cand nu e privit, urmareste camera lent (lerp) spre pozitia tinta.
    private bool    isFrozen = false;
    private Vector3 frozenPosition;
    private Quaternion frozenRotation;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        arCamera = Camera.main;

        if (libraryUI == null)
            libraryUI = FindFirstObjectByType<ModelLibraryUI>();

        BuildVisuals();
    }

    private void LateUpdate()
    {
        if (arCamera == null) return;

        // Cand butonul e privit sta fix, ca utilizatorul sa poata completa dwell-ul
        if (isFrozen)
        {
            transform.position = frozenPosition;
            transform.rotation = frozenRotation;
            return;
        }

        // Calculam pozitia tinta fata de camera
        Transform cam     = arCamera.transform;
        Vector3   forward = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        Vector3 right  = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 target = cam.position
                       + forward * distance
                       + right   * horizontalOffset
                       + Vector3.up * verticalOffset;

        Quaternion targetRot = Quaternion.LookRotation(target - cam.position);

        // Urmarire lenta (lerp): butonul aluneca usor, nu trepideaza
        float t = 1f - Mathf.Pow(0.001f, Time.deltaTime); // ~smooth follow ~3s
        transform.position = Vector3.Lerp(transform.position, target, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    // ─── Build visuals ────────────────────────────────────────────────────────

    private void BuildVisuals()
    {
        // Label TMP 3D
        label           = gameObject.AddComponent<TextMeshPro>();
        label.text      = buttonText;
        label.fontSize  = 0.07f;
        label.color     = normalColor;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(0.4f, 0.12f);

        // BoxCollider pentru raycast gaze
        var box = gameObject.AddComponent<BoxCollider>();
        box.size   = new Vector3(0.4f, 0.12f, 0.02f);
        box.center = Vector3.zero;

        // Layer GazeInteractable
        int gazeLayer = LayerMask.NameToLayer("GazeInteractable");
        if (gazeLayer >= 0)
            gameObject.layer = gazeLayer;
        else
            Debug.LogWarning("[LibraryButton] Layer 'GazeInteractable' inexistent.");
    }

    // ─── IGazeTarget ──────────────────────────────────────────────────────────

    public void OnGazeEnter()
    {
        // Inghetam pozitia: butonul nu se mai misca cat timp e privit
        isFrozen       = true;
        frozenPosition = transform.position;
        frozenRotation = transform.rotation;

        if (label != null) label.color = hoverColor;
    }

    public void OnGazeStay(float progress)
    {
        if (label != null)
            label.color = Color.Lerp(hoverColor, completeColor, progress);
    }

    public void OnGazeExit()
    {
        // Dezghetam: butonul reia urmarirea lenta
        isFrozen = false;
        if (label != null) label.color = normalColor;
    }

    public void OnGazeComplete()
    {
        if (label != null) label.color = completeColor;
        isFrozen = false;

        libraryUI?.ToggleLibrary();
        Invoke(nameof(ResetColor), 0.3f);
    }

    private void ResetColor()
    {
        if (label != null) label.color = normalColor;
    }
}
