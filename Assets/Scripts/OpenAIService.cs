using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Un mesaj dintr-o conversatie vocala: "user" (utilizatorul) sau "assistant" (AI).
/// Folosit de VoiceAIController ca sa tina istoricul conversatiei per planeta.
/// </summary>
public class ConversationMessage
{
    public readonly string Role;    // "user" sau "assistant"
    public readonly string Content;

    public ConversationMessage(string role, string content)
    {
        Role    = role;
        Content = content;
    }
}

/// <summary>
/// Serviciu HTTP pentru OpenAI Chat Completions + Whisper, rutate printr-un proxy Cloudflare.
/// Foloseste UnityWebRequest + JsonUtility + coroutine.
///
/// Proxy-ul (Proxy/worker.js) tine cheia OpenAI in Cloudflare Secrets, asa ca
/// cheia nu ajunge niciodata in build-ul iOS. Unity trimite doar un Bearer
/// token propriu (ProxyToken din OpenAISecrets).
///
/// Endpoint-uri (relative la ProxyBaseUrl din OpenAISecrets):
///   /v1/chat/completions      - GPT-4o-mini
///   /v1/audio/transcriptions  - Whisper STT
///   /v1/audio/speech          - TTS (text-to-speech)
///</summary>
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

    // Paths relative, concatenate la secrets.ProxyBaseUrl la runtime
    private const string ChatPath    = "/v1/chat/completions";
    private const string WhisperPath = "/v1/audio/transcriptions";
    private const string SpeechPath  = "/v1/audio/speech";

    // TTS returneaza PCM raw: 24 kHz, 16-bit signed little-endian, mono
    private const int TtsSampleRate = 24000;

    [Header("Whisper (Speech-to-text)")]
    [SerializeField] private string whisperModel = "whisper-1";
    [Tooltip("Codul limbii pentru transcriere (ex. 'ro', 'en'). Gol = auto-detect.")]
    [SerializeField] private string whisperLanguage = "ro";

    [Header("TTS (Text-to-speech)")]
    [Tooltip("tts-1 = rapid, $15/1M chars. tts-1-hd = calitate mai bună, $30/1M chars.")]
    [SerializeField] private string ttsModel = "tts-1";
    [Tooltip("Vocea TTS. Opțiuni: alloy, ash, coral, echo, fable, nova, onyx, sage, shimmer.")]
    [SerializeField] private string ttsVoice = "nova";
    [Tooltip("Viteza vorbirii. 1.0 = normal, 0.8 = mai lent, 1.2 = mai rapid.")]
    [SerializeField, Range(0.25f, 4f)] private float ttsSpeed = 1.0f;

    /// <summary>
    /// Trimite o intrebare la OpenAI. Raspunsul vine prin callback-uri.
    /// </summary>
    /// <param name="question">Intrebarea utilizatorului (text liber).</param>
    /// <param name="context">Context optional (ex. Description al SelectablePart).</param>
    /// <param name="onSuccess">Callback cu textul raspunsului.</param>
    /// <param name="onError">Callback cu mesajul de eroare.</param>
    public void Ask(string question, string context,
                    Action<string> onSuccess, Action<string> onError)
    {
        if (secrets == null || !secrets.IsConfigured)
        {
            onError?.Invoke("Proxy neconfigurat. Completează ProxyBaseUrl și ProxyToken în OpenAISecrets.");
            return;
        }
        StartCoroutine(AskCoroutine(question, context, onSuccess, onError));
    }

    /// <summary>
    /// Trimite o intrebare la OpenAI impreuna cu istoricul complet al conversatiei.
    /// Folosit de VoiceAIController pentru conversatii multi-turn per planeta.
    /// </summary>
    /// <param name="question">Intrebarea curenta a utilizatorului.</param>
    /// <param name="contextualSystemPrompt">System prompt ce include descrierea planetei selectate.</param>
    /// <param name="history">Schimburile anterioare (user + assistant), in ordine cronologica.</param>
    /// <param name="onSuccess">Callback cu textul raspunsului AI.</param>
    /// <param name="onError">Callback cu mesajul de eroare.</param>
    public void AskWithHistory(
        string question,
        string contextualSystemPrompt,
        List<ConversationMessage> history,
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (secrets == null || !secrets.IsConfigured)
        {
            onError?.Invoke("Proxy neconfigurat. Completează ProxyBaseUrl și ProxyToken în OpenAISecrets.");
            return;
        }
        StartCoroutine(AskWithHistoryCoroutine(question, contextualSystemPrompt, history, onSuccess, onError));
    }

    private IEnumerator AskWithHistoryCoroutine(
        string question,
        string contextualSystemPrompt,
        List<ConversationMessage> history,
        Action<string> onSuccess,
        Action<string> onError)
    {
        // System + istoricul existent + noua intrebare
        var messages = new List<OpenAIMessage>
        {
            new OpenAIMessage { role = "system", content = contextualSystemPrompt }
        };

        foreach (ConversationMessage turn in history)
            messages.Add(new OpenAIMessage { role = turn.Role, content = turn.Content });

        messages.Add(new OpenAIMessage { role = "user", content = question });

        var requestBody = new OpenAIRequest
        {
            model       = this.model,
            max_tokens  = this.maxTokens,
            temperature = this.temperature,
            messages    = messages.ToArray()
        };

        string jsonBody  = JsonUtility.ToJson(requestBody);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        string chatUrl   = secrets.ProxyBaseUrl + ChatPath;

        using (UnityWebRequest req = new UnityWebRequest(chatUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {secrets.ProxyToken}");
            req.timeout = 30;

            Debug.Log($"[OpenAIService] AskWithHistory → {chatUrl} | " +
                      $"turns={history.Count / 2} | model={model} | tokens={maxTokens}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = $"HTTP {req.responseCode}: {req.error}. Body: {req.downloadHandler?.text}";
                Debug.LogError($"[OpenAIService] {err}");
                onError?.Invoke($"HTTP {req.responseCode}: {req.error}");
                yield break;
            }

            string responseJson = req.downloadHandler.text;
            Debug.Log($"[OpenAIService] AskWithHistory răspuns: {responseJson}");

            OpenAIResponse parsed;
            try { parsed = JsonUtility.FromJson<OpenAIResponse>(responseJson); }
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

    /// <summary>
    /// Converteste textul in voce folosind OpenAI TTS si returneaza un AudioClip gata de redat.
    ///
    /// Foloseste response_format=pcm (24 kHz, 16-bit, mono) pentru conversie directa fara librarii.
    /// Textul apare in UI imediat; audio-ul porneste cand e gata (~0.5-1s latenta).
    /// </summary>
    /// <param name="text">Textul de citit (raspunsul AI).</param>
    /// <param name="onSuccess">Callback cu AudioClip-ul gata de redat (caller il distruge dupa use).</param>
    /// <param name="onError">Callback cu mesajul de eroare (non-fatal, textul e deja afisat).</param>
    public void Speak(string text, Action<AudioClip> onSuccess, Action<string> onError)
    {
        if (secrets == null || !secrets.IsConfigured)
        {
            onError?.Invoke("Proxy neconfigurat.");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            onError?.Invoke("Text gol — nimic de citit.");
            return;
        }
        StartCoroutine(SpeakCoroutine(text, onSuccess, onError));
    }

    private IEnumerator SpeakCoroutine(string text, Action<AudioClip> onSuccess, Action<string> onError)
    {
        // ── Construim corpul JSON ─────────────────────────────────────────────
        var ttsRequest = new TtsRequest
        {
            model           = ttsModel,
            voice           = ttsVoice,
            input           = text,
            response_format = "pcm",   // raw PCM, nu necesita parser MP3/WAV extern
            speed           = ttsSpeed
        };

        string jsonBody  = JsonUtility.ToJson(ttsRequest);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        string speechUrl = secrets.ProxyBaseUrl + SpeechPath;

        Debug.Log($"[OpenAIService] TTS POST → {speechUrl} | chars={text.Length} | voice={ttsVoice}");

        using (UnityWebRequest req = new UnityWebRequest(speechUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {secrets.ProxyToken}");
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = $"TTS HTTP {req.responseCode}: {req.error}";
                Debug.LogError($"[OpenAIService] {err}");
                onError?.Invoke(err);
                yield break;
            }

            byte[] pcmBytes = req.downloadHandler.data;
            if (pcmBytes == null || pcmBytes.Length < 2)
            {
                onError?.Invoke("TTS: răspuns audio gol.");
                yield break;
            }

            // ── PCM raw → AudioClip ───────────────────────────────────────────
            // Format garantat de OpenAI: 24000 Hz, 16-bit signed little-endian, mono
            int sampleCount = pcmBytes.Length / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }

            AudioClip clip = AudioClip.Create("tts_response", sampleCount, 1, TtsSampleRate, false);
            clip.SetData(samples, 0);

            Debug.Log($"[OpenAIService] TTS ✓ | {pcmBytes.Length / 1024} KB PCM → " +
                      $"{sampleCount} samples ({(float)sampleCount / TtsSampleRate:F1}s audio)");

            onSuccess?.Invoke(clip);
        }
    }

    /// <summary>
    /// Trimite bytes WAV la Whisper si intoarce textul transcris prin callback.
    /// </summary>
    /// <param name="wavData">Bytes WAV (de la <see cref="WavUtility.AudioClipToWav"/>).</param>
    /// <param name="onSuccess">Callback cu textul transcris.</param>
    /// <param name="onError">Callback cu mesajul de eroare.</param>
    public void Transcribe(byte[] wavData, Action<string> onSuccess, Action<string> onError)
    {
        if (secrets == null || !secrets.IsConfigured)
        {
            onError?.Invoke("Proxy neconfigurat. Completează ProxyBaseUrl și ProxyToken în OpenAISecrets.");
            return;
        }
        if (wavData == null || wavData.Length == 0)
        {
            onError?.Invoke("Date audio lipsă — nimic de transcris.");
            return;
        }
        StartCoroutine(TranscribeCoroutine(wavData, onSuccess, onError));
    }

    private IEnumerator TranscribeCoroutine(byte[] wavData,
                                            Action<string> onSuccess, Action<string> onError)
    {
        // ── Construim corpul multipart/form-data manual ──────────────────────────
        string boundary = "----HoloSpaceWhisper" + DateTime.Now.Ticks.ToString("x");
        byte[] body     = BuildWhisperMultipart(boundary, wavData);

        string whisperUrl = secrets.ProxyBaseUrl + WhisperPath;

        using (UnityWebRequest req = new UnityWebRequest(whisperUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", $"multipart/form-data; boundary={boundary}");
            req.SetRequestHeader("Authorization", $"Bearer {secrets.ProxyToken}");
            req.timeout = 30;

            Debug.Log($"[OpenAIService] Whisper POST → {whisperUrl} | {wavData.Length / 1024} KB audio");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = $"HTTP {req.responseCode}: {req.error}. Body: {req.downloadHandler?.text}";
                Debug.LogError($"[OpenAIService][Whisper] {err}");
                onError?.Invoke($"HTTP {req.responseCode}: {req.error}");
                yield break;
            }

            string json = req.downloadHandler.text;
            Debug.Log($"[OpenAIService][Whisper] Răspuns: {json}");

            WhisperResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<WhisperResponse>(json);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"JSON parse Whisper failed: {ex.Message}");
                yield break;
            }

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.text))
            {
                onError?.Invoke("Transcriere goală de la Whisper.");
                yield break;
            }

            onSuccess?.Invoke(parsed.text.Trim());
        }
    }

    /// <summary>Construieste corpul multipart/form-data pentru Whisper.</summary>
    private byte[] BuildWhisperMultipart(string boundary, byte[] wavData)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            // Field: model
            WriteFormField(ms, boundary, "model", whisperModel);

            // Field: language (optional)
            if (!string.IsNullOrEmpty(whisperLanguage))
                WriteFormField(ms, boundary, "language", whisperLanguage);

            // Field: file (WAV bytes)
            WriteFormFile(ms, boundary, "file", "audio.wav", "audio/wav", wavData);

            // Closing boundary
            byte[] closing = Encoding.ASCII.GetBytes($"--{boundary}--\r\n");
            ms.Write(closing, 0, closing.Length);

            return ms.ToArray();
        }
    }

    private static void WriteFormField(MemoryStream ms, string boundary, string name, string value)
    {
        string header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        byte[] valueBytes  = Encoding.UTF8.GetBytes(value + "\r\n");
        ms.Write(headerBytes, 0, headerBytes.Length);
        ms.Write(valueBytes,  0, valueBytes.Length);
    }

    private static void WriteFormFile(MemoryStream ms, string boundary,
                                      string fieldName, string filename,
                                      string contentType, byte[] data)
    {
        string header = $"--{boundary}\r\n" +
                        $"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{filename}\"\r\n" +
                        $"Content-Type: {contentType}\r\n\r\n";
        byte[] headerBytes  = Encoding.UTF8.GetBytes(header);
        byte[] footerBytes  = Encoding.UTF8.GetBytes("\r\n");
        ms.Write(headerBytes, 0, headerBytes.Length);
        ms.Write(data,        0, data.Length);
        ms.Write(footerBytes, 0, footerBytes.Length);
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

        string chatUrl = secrets.ProxyBaseUrl + ChatPath;

        using (UnityWebRequest req = new UnityWebRequest(chatUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {secrets.ProxyToken}");
            req.timeout = 30; // secunde

            Debug.Log($"[OpenAIService] POST {chatUrl} | model={model} | tokens={maxTokens}");

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

    // ─── DTO-uri pentru JsonUtility ───────────────────────────────────────────

    // Whisper response: {"text": "..."}
    [Serializable]
    private class WhisperResponse
    {
        public string text;
    }

    // TTS request: {"model":"tts-1","voice":"nova","input":"...","response_format":"pcm","speed":1.0}
    [Serializable]
    private class TtsRequest
    {
        public string model;
        public string voice;
        public string input;
        public string response_format;
        public float  speed;
    }

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