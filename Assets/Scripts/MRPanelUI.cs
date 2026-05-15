using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MRPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelContent;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private Camera arCamera;

    [Header("Positioning")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private bool billboardToCamera = true;

    [Header("Debug")]
    [Tooltip("Dacă e bifat, panoul rămâne vizibil tot timpul, în fața camerei, indiferent de selecție.")]
    [SerializeField] private bool alwaysVisibleForDebug = false;
    [Tooltip("Cât de departe în fața camerei să apară când e în mod debug (în metri).")]
    [SerializeField] private float debugDistanceFromCamera = 1.0f;

    private SelectablePart followTarget;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;

        Debug.Log(
            $"[MRPanelUI] Awake | arCamera={(arCamera != null ? arCamera.name : "NULL!")}, " +
            $"panelContent={(panelContent != null ? panelContent.name : "NULL!")}, " +
            $"titleLabel={(titleLabel != null ? titleLabel.GetType().Name : "NULL!")}, " +
            $"selfPos={transform.position}, selfScale={transform.lossyScale}", this);

        if (panelContent != null)
            panelContent.SetActive(alwaysVisibleForDebug);

        if (alwaysVisibleForDebug && titleLabel != null)
            titleLabel.text = "DEBUG VISIBLE";
    }

    private void OnEnable()
    {
        SelectionManager.SelectionChanged += HandleSelectionChanged;
        if (SelectionManager.Instance != null)
            HandleSelectionChanged(SelectionManager.Instance.CurrentSelection);
    }

    private void OnDisable()
    {
        SelectionManager.SelectionChanged -= HandleSelectionChanged;
    }

    private void HandleSelectionChanged(SelectablePart part)
    {
        if (alwaysVisibleForDebug) return; // ignoră selecția, panoul rămâne vizibil

        followTarget = part;

        if (part != null)
        {
            if (titleLabel != null) titleLabel.text = part.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = part.Description;
            if (panelContent != null) panelContent.SetActive(true);
            UpdateTransform();
        }
        else
        {
            if (panelContent != null) panelContent.SetActive(false);
        }
    }

    /// <summary>
    /// Suprascrie textul descriere afișat pe panou. Folosit de AskAIController
    /// pentru a arăta răspunsul AI. La următoarea schimbare de selecție,
    /// textul revine automat la Description-ul părții (vezi HandleSelectionChanged).
    /// </summary>
    public void ShowDescription(string text)
    {
        if (descriptionLabel != null)
            descriptionLabel.text = text;
    }

    private void LateUpdate()
    {
        if (alwaysVisibleForDebug)
        {
            // ține panoul ancorat la 1m în fața camerei, billboard
            if (arCamera == null) return;
            transform.position = arCamera.transform.position + arCamera.transform.forward * debugDistanceFromCamera;
            transform.rotation = Quaternion.LookRotation(transform.position - arCamera.transform.position, Vector3.up);
            return;
        }

        if (followTarget == null) return;
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (followTarget == null) return;
        transform.position = followTarget.transform.position + worldOffset;

        if (billboardToCamera && arCamera != null)
        {
            Vector3 awayFromCamera = transform.position - arCamera.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(awayFromCamera, Vector3.up);
        }
    }
}