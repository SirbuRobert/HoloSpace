using UnityEngine;

/// <summary>
/// ScriptableObject cu configuratia proxy-ului Cloudflare pentru OpenAI.
/// Asset-ul .asset corespunzator nu se comite in Git (vezi .gitignore).
///
/// proxyBaseUrl tine URL-ul worker-ului Cloudflare (fara slash la final), iar
/// proxyToken e bearer-ul trimis la proxy (acelasi cu HOLOSPACE_TOKEN din
/// Cloudflare Secrets). Cheia OpenAI (sk-...) sta in Cloudflare Secrets si nu
/// ajunge niciodata in Unity sau in build-ul iOS.
/// </summary>
[CreateAssetMenu(fileName = "OpenAISecrets", menuName = "HoloSpace/OpenAI Secrets")]
public class OpenAISecrets : ScriptableObject
{
    [Header("Proxy Cloudflare (recomandat)")]
    [Tooltip("URL-ul worker-ului Cloudflare, fără slash la final.\n" +
             "ex. https://holospace-openai-proxy.nume.workers.dev")]
    [SerializeField] private string proxyBaseUrl;

    [Tooltip("Bearer token trimis la proxy pentru autentificare.\n" +
             "Același cu HOLOSPACE_TOKEN setat în Cloudflare Secrets. NO COMMIT!")]
    [SerializeField] private string proxyToken;

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>URL-ul de baza al proxy-ului (fara slash la final).</summary>
    public string ProxyBaseUrl => proxyBaseUrl;

    /// <summary>Token de autentificare trimis la proxy in header-ul Authorization.</summary>
    public string ProxyToken => proxyToken;

    /// <summary>True daca proxy-ul e configurat corect (ambele campuri completate).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(proxyBaseUrl) &&
        !string.IsNullOrWhiteSpace(proxyToken);
}