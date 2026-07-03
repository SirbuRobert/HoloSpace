using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// UI world-space pentru biblioteca de modele 3D.
/// Afiseaza lista modelelor descarcate de pe server ca butoane gaze-selectabile.
///
/// Butonul "Library" cheama ToggleLibrary(), care aduce lista de pe server si o
/// afiseaza ca panou world-space. Dupa ~1.5s de dwell pe un model se apeleaza
/// SelectModel(), care il incarca prin GLBLoader.
/// </summary>
[DisallowMultipleComponent]
public class ModelLibraryUI : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private GLBLoader      glbLoader;
    [SerializeField] private Camera         arCamera;
    [SerializeField] private OpenAISecrets  secrets;

    [Header("Panou world-space")]
    [Tooltip("Distanța față de cameră la care apare panoul listei (metri).")]
    [SerializeField, Range(0.3f, 2f)] private float panelDistance = 1.0f;

    [Tooltip("Offset vertical față de centrul camerei (metri).")]
    [SerializeField] private float panelVerticalOffset = 0f;

    // ─── State interna ────────────────────────────────────────────────────────

    private bool isVisible = false;
    private bool isFetching = false;

    private GameObject         panelRoot;
    private TMP_Text           statusLabel;
    private List<ModelEntryButton> entryButtons = new List<ModelEntryButton>();

    private List<ModelIndexEntry>  modelIndex   = new List<ModelIndexEntry>();

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        if (arCamera == null) arCamera = Camera.main;
        BuildPanel();
        panelRoot.SetActive(false);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Arata sau ascunde biblioteca. Conectat la GazeButton.onGazeClick.</summary>
    public void ToggleLibrary()
    {
        if (isVisible) HideLibrary();
        else           ShowLibrary();
    }

    public void ShowLibrary()
    {
        isVisible = true;
        PositionPanel();
        panelRoot.SetActive(true);

        // Fetch de fiecare data, ca sa prinda si modelele uploadate dupa ce app-ul a pornit
        StartCoroutine(FetchModelList());
    }

    public void HideLibrary()
    {
        isVisible = false;
        panelRoot.SetActive(false);
    }

    // ─── Panel construction ───────────────────────────────────────────────────

    private void BuildPanel()
    {
        panelRoot = new GameObject("LibraryPanel");
        panelRoot.transform.SetParent(transform, false);

        // Status / loading label
        var statusGo  = new GameObject("StatusLabel");
        statusGo.transform.SetParent(panelRoot.transform, false);
        statusGo.transform.localPosition = Vector3.zero;

        statusLabel            = statusGo.AddComponent<TextMeshPro>();
        statusLabel.fontSize   = 0.06f;
        statusLabel.color      = Color.white;
        statusLabel.alignment  = TextAlignmentOptions.Center;
        statusLabel.text       = "Se încarcă...";
        statusLabel.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    }

    private void PositionPanel()
    {
        if (arCamera == null) return;

        Transform cam     = arCamera.transform;
        Vector3   forward = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        Vector3 pos = cam.position
                    + forward * panelDistance
                    + Vector3.up * panelVerticalOffset;

        panelRoot.transform.position = pos;
        panelRoot.transform.rotation = Quaternion.LookRotation(forward);
    }

    // ─── Fetch model list ─────────────────────────────────────────────────────

    private IEnumerator FetchModelList()
    {
        if (isFetching) yield break;
        isFetching = true;

        ClearEntries();
        statusLabel.text = "Se încarcă lista...";
        statusLabel.gameObject.SetActive(true);

        if (secrets == null || !secrets.IsConfigured)
        {
            statusLabel.text = "OpenAISecrets neconfigurat.";
            isFetching = false;
            yield break;
        }

        string url = $"{secrets.ProxyBaseUrl}/models";
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                statusLabel.text = $"Eroare: {req.responseCode}";
                Debug.LogError($"[ModelLibraryUI] Fetch eșuat: {req.error}");
                isFetching = false;
                yield break;
            }

            string json = req.downloadHandler.text;
            // JsonUtility nu parseaza un array root, asa ca il impachetam
            string wrapped = "{\"models\":" + json + "}";
            var wrapper    = JsonUtility.FromJson<ModelIndexWrapper>(wrapped);

            modelIndex.Clear();
            if (wrapper?.models != null)
                foreach (var m in wrapper.models)
                    modelIndex.Add(m);
        }

        statusLabel.gameObject.SetActive(false);
        isFetching = false;
        RenderEntries();
    }

    // ─── Render entries ───────────────────────────────────────────────────────

    private void RenderEntries()
    {
        ClearEntries();

        if (modelIndex.Count == 0)
        {
            statusLabel.text = "Niciun model disponibil.\nUploadează din portal.";
            statusLabel.gameObject.SetActive(true);
            return;
        }

        statusLabel.gameObject.SetActive(false);

        float spacing  = 0.12f;
        float startY   = (modelIndex.Count - 1) * spacing * 0.5f;

        for (int i = 0; i < modelIndex.Count; i++)
        {
            var entry = modelIndex[i];
            var btn   = CreateEntryButton(entry, new Vector3(0f, startY - i * spacing, 0f));
            entryButtons.Add(btn);
        }
    }

    private ModelEntryButton CreateEntryButton(ModelIndexEntry entry, Vector3 localPos)
    {
        var go  = new GameObject($"Entry_{entry.id}");
        go.transform.SetParent(panelRoot.transform, false);
        go.transform.localPosition = localPos;

        var btn = go.AddComponent<ModelEntryButton>();
        btn.Initialize(entry, this);
        return btn;
    }

    private void ClearEntries()
    {
        foreach (var b in entryButtons)
            if (b != null) Destroy(b.gameObject);
        entryButtons.Clear();
    }

    // ─── Model selection ──────────────────────────────────────────────────────

    /// <summary>Apelat de ModelEntryButton cand utilizatorul selecteaza un model.</summary>
    public void SelectModel(ModelIndexEntry entry)
    {
        if (glbLoader == null || glbLoader.IsLoading) return;
        HideLibrary();
        StartCoroutine(FetchAndLoad(entry));
    }

    private IEnumerator FetchAndLoad(ModelIndexEntry entry)
    {
        if (secrets == null || !secrets.IsConfigured) yield break;

        // Descarca metadata completa (cu parts)
        string metaUrl = $"{secrets.ProxyBaseUrl}/models/{entry.id}";
        ModelMetadata metadata = null;

        using (var req = UnityWebRequest.Get(metaUrl))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                metadata = JsonUtility.FromJson<ModelMetadata>(req.downloadHandler.text);
        }

        if (metadata == null)
        {
            Debug.LogError($"[ModelLibraryUI] Metadata fetch eșuat pentru {entry.id}");
            yield break;
        }

        glbLoader.LoadModel(secrets.ProxyBaseUrl, entry.id, metadata);
    }
}
