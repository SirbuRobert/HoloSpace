using UnityEngine;

/// <summary>
/// ScriptableObject ce conține cheia API OpenAI.
/// Asset-ul .asset corespunzător NU se commit-ă în Git (vezi .gitignore).
/// Creează-l din meniul: Project window > right-click > Create > HoloSpace > OpenAI Secrets.
/// </summary>
[CreateAssetMenu(fileName = "OpenAISecrets", menuName = "HoloSpace/OpenAI Secrets")]
public class OpenAISecrets : ScriptableObject
{
    [Tooltip("Cheia API OpenAI. Incepe cu \"sk-...\". NO COMMIT!")]
    [SerializeField] private string apiKey;

    public string ApiKey => apiKey;
}