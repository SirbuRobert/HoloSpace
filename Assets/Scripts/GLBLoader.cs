using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Descarca un GLB de pe server, il incarca cu glTFast si il paseaza la
/// ARPlacementManager pentru plasare pe o suprafata AR (acelasi flow ca solar system).
///
/// Pe scurt: descarca fisierul in temporaryCachePath, il incarca async cu glTFast,
/// distruge solar system-ul curent daca exista, normalizeaza scala si il trimite la
/// ARPlacementManager.ResetForObject(), de unde utilizatorul alege unde sa fie plasat.
/// </summary>
[DisallowMultipleComponent]
public class GLBLoader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera              arCamera;
    [SerializeField] private ARPlacementManager  arPlacementManager;

    [Header("Scală")]
    [Tooltip("Dimensiunea maximă a bounding box-ului după normalizare (metri).")]
    [SerializeField, Range(0.05f, 1f)] private float targetSize = 0.25f;

    [Header("Fallback (dacă ARPlacementManager lipsește)")]
    [SerializeField, Range(0.3f, 3f)] private float spawnDistance = 1.5f;

    [Header("Materiale")]
    [Tooltip("Material URP/Lit folosit ca template pentru fix-ul de shader. " +
             "Creează un Material nou în proiect (Create → Material) și asignează-l aici.")]
    [SerializeField] private Material urpLitTemplate;

    [Tooltip("Dimensiunea maximă a texturilor (px). Texturi mai mari sunt reduse automat " +
             "pentru a evita crash-ul OOM pe GPU iOS. 1024 = echilibru calitate/memorie.")]
    [SerializeField] private int maxTextureSize = 1024;

    [Header("Gaze layer")]
    [SerializeField] private string gazeLayerName = "GazeInteractable";

    // ─── Events ───────────────────────────────────────────────────────────────

    public event Action<GameObject, ModelMetadata> OnModelLoaded;
    public event Action<string>                    OnModelLoadFailed;

    // ─── State ────────────────────────────────────────────────────────────────

    public bool IsLoading { get; private set; }

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;
        if (arPlacementManager == null)
            arPlacementManager = FindFirstObjectByType<ARPlacementManager>();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void LoadModel(string proxyBaseUrl, string modelId, ModelMetadata metadata)
    {
        if (IsLoading) { Debug.LogWarning("[GLBLoader] Deja în curs."); return; }
        _ = LoadAsync(proxyBaseUrl, modelId, metadata);
    }

    // ─── Core async pipeline ──────────────────────────────────────────────────

    private async Task LoadAsync(string proxyBaseUrl, string modelId, ModelMetadata metadata)
    {
        IsLoading = true;

        // ── 1. Descarcam GLB ──────────────────────────────────────────────────
        string fileUrl   = $"{proxyBaseUrl}/models/{modelId}/file";
        string localPath = Path.Combine(Application.temporaryCachePath, $"{modelId}.glb");

        Debug.Log($"[GLBLoader] Descarcă: {fileUrl}");

        bool downloaded = await DownloadFileAsync(fileUrl, localPath);
        if (!downloaded)
        {
            Fail("Download eșuat — verifică conexiunea și URL-ul proxy.");
            return;
        }

        Debug.Log($"[GLBLoader] Salvat: {localPath}");

        // ── 2. Incarcam cu glTFast ────────────────────────────────────────────
        var gltf    = new GltfImport();
        var fileUri = new Uri(localPath);
        bool loaded = await gltf.Load(fileUri);

        if (!loaded)
        {
            Fail("glTFast: fișier GLB invalid sau format nesuportat.");
            return;
        }

        // ── 3. Instantiem ───────────────────────────────────────────────────
        var root = new GameObject(metadata?.title ?? "Model");
        bool instantiated = await gltf.InstantiateMainSceneAsync(root.transform);

        if (!instantiated)
        {
            Destroy(root);
            Fail("glTFast: instanțiere eșuată.");
            return;
        }

        // ── 4. Normalizam scala ────────────────────────────────────────────
        NormalizeScale(root);

        // ── 5. Asteptam 2 frame-uri (glTFast seteaza materialele lazy) ───────
        await WaitFramesAsync(2);

        // ── 6. Fix materiale (shaderele glTFast sunt stripped pe iOS) ────────
        // Pasam gltf ca sa putem accesa texturile direct via GetTexture()
        FixMaterialsForURP(root, gltf);

        // ── 6. Legam gaze interaction ──────────────────────────────────────
        int gazeLayer = LayerMask.NameToLayer(gazeLayerName);
        WireGazeInteraction(root, metadata, gazeLayer >= 0 ? gazeLayer : 0);

        // ── 6. Pasam la ARPlacementManager (sau fallback) ──────────────────
        if (arPlacementManager != null)
        {
            // Distruge solar system + intra in placement mode pentru noul model
            arPlacementManager.ResetForObject(root);
            Debug.Log($"[GLBLoader] ✓ '{metadata?.title}' pregătit pentru plasare AR.");
        }
        else
        {
            // Fallback: plasare directa in fata camerei
            PlaceInFrontOfCamera(root);
            Debug.Log($"[GLBLoader] ✓ '{metadata?.title}' plasat (fallback, fără ARPlacementManager).");
        }

        OnModelLoaded?.Invoke(root, metadata);
        IsLoading = false;
    }

    // ─── Frame waiter (bridge coroutine -> async Task) ─────────────────────────

    private Task WaitFramesAsync(int frames)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(WaitFramesCoroutine(frames, tcs));
        return tcs.Task;
    }

    private static IEnumerator WaitFramesCoroutine(int frames, TaskCompletionSource<bool> tcs)
    {
        for (int i = 0; i < frames; i++)
            yield return null;
        tcs.SetResult(true);
    }

    // ─── Download helper ──────────────────────────────────────────────────────

    private async Task<bool> DownloadFileAsync(string url, string destPath)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Pornim coroutina de download pe MonoBehaviour
        StartCoroutine(DownloadCoroutine(url, destPath, tcs));

        return await tcs.Task;
    }

    private System.Collections.IEnumerator DownloadCoroutine(
        string url, string destPath, TaskCompletionSource<bool> tcs)
    {
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerFile(destPath) { removeFileOnAbort = true };
        req.timeout = 60;
        yield return req.SendWebRequest();

        tcs.SetResult(req.result == UnityWebRequest.Result.Success);
    }

    // ─── Material fix ─────────────────────────────────────────────────────────

    /// <summary>
    /// glTFast foloseste propriile shadere (glTF/PbrMetallicRoughness etc.) care nu sunt
    /// incluse in build-ul iOS, de aici materialele roz. Fix: inlocuim shaderul cu
    /// URP/Lit si copiem textura principala + culoarea.
    /// </summary>
    private void FixMaterialsForURP(GameObject root, GltfImport gltf)
    {
        Shader urpShader = urpLitTemplate != null
            ? urpLitTemplate.shader
            : Shader.Find("Universal Render Pipeline/Lit");

        if (urpShader == null)
        {
            Debug.LogError("[GLBLoader] Shader URP/Lit nu a fost găsit.");
            return;
        }

        // ── Colectam texturile direct din GltfImport ──────────────────────────
        // Texturile sunt mereu disponibile chiar daca shaderele sunt stripped.
        // Proprietatea glTFast pentru albedo: "baseColorTexture" (ShaderGraph prop name)
        var gltfTextures = new System.Collections.Generic.List<Texture2D>();
        for (int i = 0; i < gltf.TextureCount; i++)
        {
            var t = gltf.GetTexture(i);
            if (t != null) gltfTextures.Add(t);
        }

        // Colectam texturile din materialele glTFast (daca nu sunt null/stripped)
        var matTextures = new System.Collections.Generic.List<Texture2D>();
        for (int i = 0; i < gltf.MaterialCount; i++)
        {
            var m = gltf.GetMaterial(i);
            Texture2D tex = null;
            if (m != null)
            {
                // Proprietatea glTFast ShaderGraph pentru albedo
                foreach (var p in new[] { "baseColorTexture", "_BaseColorTexture", "_MainTex", "_BaseMap" })
                    if (m.HasProperty(p)) { tex = m.GetTexture(p) as Texture2D; if (tex != null) break; }
            }
            matTextures.Add(tex); // poate fi null pentru materiale fara textura
        }

        Debug.Log($"[GLBLoader] FixMaterialsForURP | shader={urpShader.name} | " +
                  $"glTFast textures={gltfTextures.Count} | glTFast materials={matTextures.Count}");

        // ── Aplicam pe fiecare renderer ───────────────────────────────────────
        int matSlotIdx = 0;
        int fixed_ = 0;

        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = rend.sharedMaterials;

            // Count din submesh daca sharedMaterials e gol
            int matCount = mats.Length;
            if (matCount == 0 && rend is MeshRenderer mr2)
            {
                var mf = mr2.GetComponent<MeshFilter>();
                if (mf?.sharedMesh != null) matCount = mf.sharedMesh.subMeshCount;
            }
            if (matCount == 0) matCount = 1;

            var newMats = new Material[matCount];
            for (int i = 0; i < matCount; i++)
            {
                var dst = new Material(urpShader);

                // 1. Incercam textura din materialul glTFast corespunzator
                Texture2D tex = null;
                if (matSlotIdx < matTextures.Count)
                    tex = matTextures[matSlotIdx];

                // 2. Fallback: orice textura disponibila din gltf
                if (tex == null && gltfTextures.Count > 0)
                    tex = gltfTextures[matSlotIdx % gltfTextures.Count];

                if (tex != null)
                    dst.SetTexture("_BaseMap", tex);

                // 3. Copiem culoarea din materialul original daca exista
                var src = i < mats.Length ? mats[i] : null;
                if (src != null)
                {
                    dst.name = src.name;
                    foreach (var p in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                        if (src.HasProperty(p)) { dst.SetColor("_BaseColor", src.GetColor(p)); break; }
                }

                newMats[i] = dst;
                fixed_++;
                matSlotIdx++;
            }

            rend.sharedMaterials = newMats;
        }

        Debug.Log($"[GLBLoader] Materiale fix URP: {fixed_} actualizate, " +
                  $"texturi aplicate din {gltfTextures.Count} disponibile.");
    }

    // ─── Scale normalization ──────────────────────────────────────────────────

    private void NormalizeScale(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxExtent < 0.001f) return;

        float scale = targetSize / maxExtent;
        root.transform.localScale = Vector3.one * scale;
        Debug.Log($"[GLBLoader] Scale: {maxExtent:F3}m → {scale:F3}x");
    }

    // ─── Gaze wiring ──────────────────────────────────────────────────────────

    private void WireGazeInteraction(GameObject root, ModelMetadata metadata, int gazeLayer)
    {
        // Logam toate nodurile gasite ca sa le putem compara cu ce e scris in portal
        var allNodes = new System.Text.StringBuilder();
        CollectNodeNames(root.transform, allNodes);
        Debug.Log($"[GLBLoader] Noduri găsite în model:\n{allNodes}");

        if (metadata?.parts != null && metadata.parts.Length > 0)
        {
            Debug.Log($"[GLBLoader] Noduri căutate (din portal): " +
                      string.Join(", ", System.Array.ConvertAll(metadata.parts, p => p.name)));
        }

        int count = 0;
        if (metadata?.parts != null)
            WireRecursive(root.transform, metadata, gazeLayer, ref count);

        if (count == 0)
        {
            // Fallback: daca niciun nod nu coincide cu metadata, facem tot modelul selectabil
            Debug.LogWarning("[GLBLoader] Niciun nod nu a coincis cu metadata. " +
                             "Verifică în Console că numele din portal = numele nodurilor de mai sus. " +
                             "Fallback: modelul întreg e selectabil ca un singur obiect.");
            AddFallbackSelection(root, metadata, gazeLayer);
        }
        else
        {
            Debug.Log($"[GLBLoader] {count} noduri interactive wirate cu succes.");
        }
    }

    private void CollectNodeNames(Transform t, System.Text.StringBuilder sb, int depth = 0)
    {
        sb.AppendLine($"{"  ".PadLeft(depth * 2)}• {t.gameObject.name}");
        for (int i = 0; i < t.childCount; i++)
            CollectNodeNames(t.GetChild(i), sb, depth + 1);
    }

    private void WireRecursive(Transform t, ModelMetadata metadata, int gazeLayer, ref int count)
    {
        string desc = metadata.GetDescription(t.gameObject.name);
        if (!string.IsNullOrEmpty(desc))
        {
            t.gameObject.layer = gazeLayer;
            AddCollider(t.gameObject);

            var part = t.GetComponent<SelectablePart>() ?? t.gameObject.AddComponent<SelectablePart>();
            part.Initialize(t.gameObject.name, desc);
            count++;
        }

        for (int i = 0; i < t.childCount; i++)
            WireRecursive(t.GetChild(i), metadata, gazeLayer, ref count);
    }

    /// <summary>Fallback: face root-ul selectabil ca intreg cu un BoxCollider.</summary>
    private void AddFallbackSelection(GameObject root, ModelMetadata metadata, int gazeLayer)
    {
        root.layer = gazeLayer;

        // BoxCollider care acopera tot modelul, pe baza bounding box-ului
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center);
            box.size   = root.transform.InverseTransformVector(b.size);
        }
        else
        {
            root.AddComponent<BoxCollider>();
        }

        var part = root.GetComponent<SelectablePart>() ?? root.AddComponent<SelectablePart>();
        string title = metadata?.title ?? root.name;
        string desc  = $"Model AR: {title}. Poți pune întrebări despre el folosind butonul vocal.";
        part.Initialize(title, desc);
    }

    private static void AddCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() != null) return;

        var mf = go.GetComponent<MeshFilter>();
        if (mf?.sharedMesh != null)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            try { mc.convex = true; }
            catch { mc.convex = false; }
        }
        else
        {
            go.AddComponent<BoxCollider>();
        }
    }

    // ─── Fallback placement ───────────────────────────────────────────────────

    private void PlaceInFrontOfCamera(GameObject root)
    {
        if (arCamera == null) return;
        Transform cam     = arCamera.transform;
        Vector3   forward = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        root.transform.position = cam.position + forward * spawnDistance;
        root.transform.rotation = Quaternion.LookRotation(forward);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private void Fail(string message)
    {
        Debug.LogError($"[GLBLoader] {message}");
        OnModelLoadFailed?.Invoke(message);
        IsLoading = false;
    }
}
