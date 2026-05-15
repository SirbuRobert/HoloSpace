using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Serviciu HTTP pentru OpenAI Chat Completions API.
/// Folosește UnityWebRequest + JsonUtility + coroutine.
/// Convenție: scene-level singleton ușor, referențiat de AskAIController.
/// </summary>
public class OpenAIService : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private OpenAISecrets secrets;
    [SerializeField] private string model = "gpt-4o-mini";
    [SerializeField, Range(50, 500)] private int maxTokens = 150;
    [SerializeField, Range(0f, 1f)] private float temperature = 0.7f;

    [TextArea(2, 5)]
    [SerializeField] private string systemPrompt =
        "Ești un tutor prietenos care explică obiecte 3D vizualizate în AR. " +
        "Răspunzi concis, maxim 2-3 propoziții, fără jargon excesiv. " +
        "Răspunde în aceeași limbă ca întrebarea.";

    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    /// <summary>
    /// Trimite o întrebare la OpenAI. Răspunsul vine prin callback-uri.
    /// </summary>
    /// <param name="question">Întrebarea utilizatorului (text liber).</param>
    /// <param name="context">Context opțional (ex. Description al SelectablePart).</param>
    /// <param name="onSuccess">Callback cu textul răspunsului.</param>
    /// <param name="onError">Callback cu mesajul de eroare.</param>
    public void Ask(string question, string context,
                    Action<string> onSuccess, Action<string> onError)
    {
        if (secrets == null || string.IsNullOrEmpty(secrets.ApiKey))
        {
            onError?.Invoke("API key lipsă. Verifică OpenAISecrets în Inspector.");
            return;
        }
        StartCoroutine(AskCoroutine(question, context, onSuccess, onError));
    }

    private IEnumerator AskCoroutine(string question, string context,
                                     Action<string> onSuccess, Action<string> onError)
    {
        string userContent = string.IsNullOrWhiteSpace(context)
            ? question
            : $"{question}\n\nContext: {context}";

        var requestBody = new OpenAIRequest
        {
            model = this.model,
            max_tokens = this.maxTokens,
            temperature = this.temperature,
            messages = new[]
            {
                new OpenAIMessage { role = "system", content = systemPrompt },
                new OpenAIMessage { role = "user",   content = userContent }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest req = new UnityWebRequest(Endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {secrets.ApiKey}");
            req.timeout = 30; // secunde

            Debug.Log($"[OpenAIService] POST {Endpoint} | model={model} | tokens={maxTokens}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = $"HTTP {req.responseCode}: {req.error}. Body: {req.downloadHandler?.text}";
                Debug.LogError($"[OpenAIService] {err}");
                onError?.Invoke($"HTTP {req.responseCode}: {req.error}");
                yield break;
            }

            string responseJson = req.downloadHandler.text;
            Debug.Log($"[OpenAIService] Raw response: {responseJson}");

            OpenAIResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<OpenAIResponse>(responseJson);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"JSON parse failed: {ex.Message}");
                yield break;
            }

            if (parsed?.choices == null || parsed.choices.Length == 0)
            {
                onError?.Invoke("Răspuns gol de la AI.");
                yield break;
            }

            string content = parsed.choices[0].message?.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("Conținut răspuns gol.");
                yield break;
            }

            onSuccess?.Invoke(content.Trim());
        }
    }

    // ─── DTO-uri pentru JsonUtility ───

    [Serializable]
    private class OpenAIRequest
    {
        public string model;
        public OpenAIMessage[] messages;
        public int max_tokens;
        public float temperature;
    }

    [Serializable]
    private class OpenAIMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OpenAIResponse
    {
        public OpenAIChoice[] choices;
    }

    [Serializable]
    private class OpenAIChoice
    {
        public OpenAIMessage message;
    }
}