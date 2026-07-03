using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controleaza fluxul vocal complet, cu suport pentru conversatii multi-turn:
///   GazeButton -> StartStopRecording() -> Microphone -> WavUtility -> Whisper -> GPT -> MRPanelUI
///
/// Istoricul conversatiei se tine per planeta selectata si se reseteaza automat
/// cand utilizatorul priveste alta planeta. Contextul planetei (descrierea) e
/// injectat o singura data in system prompt, nu repetat in fiecare mesaj.
/// </summary>
[DisallowMultipleComponent]
public class VoiceAIController : MonoBehaviour
{
    // ─── State machine ──────────────────────────────────────────────────────────
    private enum State { Idle, Recording, Transcribing, Asking }

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private MRPanelUI       panel;
    [SerializeField] private OpenAIService   aiService;
    [Tooltip("AudioSource pentru răspunsul vocal AI (TTS). " +
             "Dacă e null, se creează automat.")]
    [SerializeField] private AudioSource     ttsAudioSource;
    [Tooltip("AudioSource pentru playback-ul mic-ului în timpul înregistrării. " +
             "Rutează vocea utilizatorului prin pipeline-ul Unity audio → " +
             "HoloKitVideoRecorder.OnAudioFilterRead o captează în video. " +
             "Dacă e null, se creează automat.")]
    [SerializeField] private AudioSource     micPlaybackSource;

    [Header("Buton vocal — label 3D (opțional)")]
    [Tooltip("TMP_Text pe butonul de înregistrare. Dacă e setat, textul se actualizează cu starea.")]
    [SerializeField] private TMP_Text voiceButtonLabel;

    [Header("Înregistrare")]
    [Tooltip("Durata maximă a înregistrării în secunde. Se oprește automat după expirare.")]
    [SerializeField, Range(3f, 20f)] private float maxRecordingSeconds = 8f;
    [Tooltip("Rata de eșantionare (Hz). 16000 e suficient pentru Whisper și economisește memorie.")]
    [SerializeField] private int sampleRate = 16000;

    [Header("Conversație")]
    [Tooltip("Numărul maxim de schimburi (user+assistant) păstrate în istoric. " +
             "Mesajele mai vechi sunt eliminate pentru a controla costul și latența.")]
    [SerializeField, Range(2, 20)] private int maxHistoryExchanges = 8;

    [Tooltip("System prompt de bază trimis la fiecare conversație. " +
             "Descrierea planetei selectate se adaugă automat la acesta.")]
    [TextArea(2, 5)]
    [SerializeField] private string baseSystemPrompt =
        "Ești un tutor prietenos care explică obiecte 3D vizualizate în AR. " +
        "Răspunzi concis, maxim 2-3 propoziții, fără jargon excesiv. " +
        "Răspunde în aceeași limbă ca întrebarea.";

    [Header("TTS")]
    [Tooltip("Dacă e bifat, AI-ul citește răspunsul cu voce. Debifează pentru text-only.")]
    [SerializeField] private bool ttsEnabled = true;

    [Header("Mesaje UI")]
    [SerializeField] private string labelIdle      = "Ask AI \U0001f3a4";   // 🎤
    [SerializeField] private string labelRecording = "⏹ Stop";
    [SerializeField] private string msgNoSelection = "Selectează mai întâi un obiect.";
    [SerializeField] private string msgNoMic       = "Microfonul nu e disponibil.";
    [SerializeField] private string msgListening   = "Ascult...";
    [SerializeField] private string msgProcessing  = "Procesez vocea...";
    [SerializeField] private string msgAsking      = "Întreb AI...";

    // ─── State interna ──────────────────────────────────────────────────────────

    private State       currentState = State.Idle;
    private AudioClip   recordingClip;
    private Coroutine   autoStopCoroutine;

    // Capturam selectia in momentul pornirii inregistrarii
    // (utilizatorul poate schimba selectia in timp ce vorbeste)
    private string capturedPartName;
    private string capturedLastQuestion; // pentru a adauga in istoric dupa raspuns

    // ─── Conversatie multi-turn ──────────────────────────────────────────────────

    /// <summary>Istoricul schimburilor vocale din conversatia curenta.</summary>
    private readonly List<ConversationMessage> conversationHistory = new List<ConversationMessage>();

    /// <summary>
    /// System prompt contextual, reconstruit la fiecare schimbare de planeta.
    /// Include baseSystemPrompt + descrierea planetei selectate.
    /// </summary>
    private string contextualSystemPrompt;

    // ─── Unity lifecycle ────────────────────────────────────────────────────────

    private void Start()
    {
        SetLabel(labelIdle);

        // AudioSource TTS, fallback cu auto-create
        if (ttsAudioSource == null)
        {
            ttsAudioSource = gameObject.AddComponent<AudioSource>();
            ttsAudioSource.playOnAwake  = false;
            ttsAudioSource.spatialBlend = 0f;
            Debug.Log("[VoiceAI] AudioSource creat automat pentru TTS.");
        }

        // AudioSource mic playback: ruteaza vocea in pipeline-ul Unity audio -> video
        if (micPlaybackSource == null)
        {
            micPlaybackSource = gameObject.AddComponent<AudioSource>();
            micPlaybackSource.playOnAwake  = false;
            micPlaybackSource.spatialBlend = 0f;
            micPlaybackSource.loop        = true;
            // Volum redus: vocea ajunge in video dar userul nu o aude ca ecou.
            // La 1.0 s-ar auzi confirmarea auditiva ca mic-ul e activ.
            micPlaybackSource.volume      = 0.15f;
            Debug.Log("[VoiceAI] AudioSource creat automat pentru mic playback.");
        }

        // Initializam cu selectia curenta daca exista
        if (SelectionManager.Instance?.CurrentSelection != null)
            RebuildConversationContext(SelectionManager.Instance.CurrentSelection);
    }

    private void OnEnable()
    {
        SelectionManager.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        SelectionManager.SelectionChanged -= OnSelectionChanged;
    }

    private void OnDestroy()
    {
        // Eliberam microfonul si mic playback-ul daca raman deschise
        if (currentState == State.Recording)
        {
            if (micPlaybackSource != null && micPlaybackSource.isPlaying)
                micPlaybackSource.Stop();
            Microphone.End(null);
            Debug.Log("[VoiceAI] Microfon eliberat în OnDestroy.");
        }
    }

    // ─── Gestionare schimbare selectie ──────────────────────────────────────────

    private void OnSelectionChanged(SelectablePart part)
    {
        if (part == null) return; // deselectie, pastram istoricul pentru re-selectie

        // Planeta noua, resetam conversatia complet
        conversationHistory.Clear();
        RebuildConversationContext(part);

        Debug.Log($"[VoiceAI] Conversație nouă pentru: {part.DisplayName} " +
                  $"(istoric șters, context actualizat).");
    }

    /// <summary>
    /// Reconstruieste system prompt-ul contextual cu descrierea planetei.
    /// Apelat la Start si la fiecare schimbare de selectie.
    /// </summary>
    private void RebuildConversationContext(SelectablePart part)
    {
        if (string.IsNullOrEmpty(part.Description))
        {
            contextualSystemPrompt = baseSystemPrompt;
        }
        else
        {
            contextualSystemPrompt =
                $"{baseSystemPrompt}\n\n" +
                $"Acum vorbim despre: {part.DisplayName}.\n" +
                $"{part.Description}";
        }
    }

    // ─── Mic playback helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Asteptam pana Microphone.Start() produce date reale, apoi pornim playback-ul.
    /// Fara acest wait, AudioSource.Play() pe un clip gol nu face nimic.
    /// </summary>
    private IEnumerator StartMicPlaybackWhenReady()
    {
        // Asteptam pana mic-ul a produs cel putin un sample
        float timeout = 2f;
        float elapsed = 0f;
        while (Microphone.GetPosition(null) <= 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentState != State.Recording) yield break; // s-a oprit intre timp

        if (micPlaybackSource != null && recordingClip != null)
        {
            micPlaybackSource.clip = recordingClip;
            micPlaybackSource.Play();
            Debug.Log("[VoiceAI] Mic playback pornit → vocea intră în pipeline-ul video.");
        }
    }

    // ─── Public API (wired la GazeButton.onGazeClick) ──────────────────────────

    /// <summary>
    /// Prima apasare porneste inregistrarea.
    /// A doua apasare (in timp ce inregistreaza) opreste si proceseaza.
    /// In starile Transcribing/Asking apasarea e ignorata.
    /// </summary>
    public void StartStopRecording()
    {
        switch (currentState)
        {
            case State.Idle:
                StartRecording();
                break;

            case State.Recording:
                StopAndProcess();
                break;

            case State.Transcribing:
            case State.Asking:
                Debug.Log("[VoiceAI] Apăsare ignorată — procesare în curs.");
                break;
        }
    }

    // ─── Inregistrare ───────────────────────────────────────────────────────────

    private void StartRecording()
    {
        // Verificare: trebuie sa existe o selectie
        SelectablePart current = SelectionManager.Instance?.CurrentSelection;
        if (current == null)
        {
            ShowInPanel(msgNoSelection);
            Debug.LogWarning("[VoiceAI] Nicio selecție activă — înregistrare anulată.");
            return;
        }

        // Verificare: microfon disponibil
        if (Microphone.devices.Length == 0)
        {
            ShowInPanel(msgNoMic);
            Debug.LogError("[VoiceAI] Niciun microfon detectat.");
            return;
        }

        // Capturam selectia acum; se poate schimba in timp ce vorbim
        capturedPartName = current.DisplayName;

        // Daca selectia s-a schimbat fata de contextul curent, reconstruim
        // (edge case: OnSelectionChanged poate sa nu fi fost apelat daca componentul
        //  era dezactivat cand s-a selectat planeta)
        if (string.IsNullOrEmpty(contextualSystemPrompt))
            RebuildConversationContext(current);

        // Pornim inregistrarea in loop mode cu un buffer suficient de mare.
        // Loop=true (in loc de false) permite playback simultan prin micPlaybackSource:
        // vocea trece prin Unity audio pipeline -> OnAudioFilterRead pe HoloKitVideoRecorder
        // o capteaza in video. Buffer = maxRecordingSeconds + 2s marja anti-wrap.
        int micBufferSeconds = Mathf.CeilToInt(maxRecordingSeconds) + 2;
        recordingClip = Microphone.Start(null, true, micBufferSeconds, sampleRate);

        // Asteptam pana mic-ul a pornit efectiv (Unity poate intarzia cateva frame-uri);
        // fara wait, clip-ul e null si AudioSource.Play() nu face nimic.
        StartCoroutine(StartMicPlaybackWhenReady());

        currentState = State.Recording;
        SetLabel(labelRecording);
        ShowInPanel(msgListening);

        autoStopCoroutine = StartCoroutine(AutoStopAfterTimeout());

        Debug.Log($"[VoiceAI] Înregistrare pornită pentru '{capturedPartName}'. " +
                  $"Max {maxRecordingSeconds}s | buffer {micBufferSeconds}s | loop=true.");
    }

    private IEnumerator AutoStopAfterTimeout()
    {
        yield return new WaitForSeconds(maxRecordingSeconds);

        if (currentState == State.Recording)
        {
            Debug.Log("[VoiceAI] Auto-stop — s-a atins durata maximă.");
            StopAndProcess();
        }
    }

    private void StopAndProcess()
    {
        // Oprim timer-ul de auto-stop daca mai ruleaza
        if (autoStopCoroutine != null)
        {
            StopCoroutine(autoStopCoroutine);
            autoStopCoroutine = null;
        }

        // Oprim playback-ul mic-ului, vocea nu mai trebuie rutata in video
        if (micPlaybackSource != null && micPlaybackSource.isPlaying)
        {
            micPlaybackSource.Stop();
            micPlaybackSource.clip = null;
        }

        // Cate sample-uri s-au inregistrat efectiv?
        int samplesRecorded = Microphone.GetPosition(null);
        Microphone.End(null);
        SetLabel(labelIdle);

        if (samplesRecorded <= 100) // prea scurt = probabil tacere/eroare
        {
            currentState = State.Idle;
            ShowInPanel("Nu am captat nimic. Apasă și vorbește din nou.");
            Debug.LogWarning($"[VoiceAI] Prea puține sample-uri ({samplesRecorded}) — ignorat.");
            return;
        }

        // Taiem AudioClip-ul la lungimea reala (restul e 0)
        float[] rawData = new float[samplesRecorded * recordingClip.channels];
        recordingClip.GetData(rawData, 0);

        AudioClip trimmed = AudioClip.Create("voice_trim", samplesRecorded,
                                              recordingClip.channels, sampleRate, false);
        trimmed.SetData(rawData, 0);
        Destroy(recordingClip); // eliberam clip-ul original
        recordingClip = null;

        float durationSec = (float)samplesRecorded / sampleRate;
        Debug.Log($"[VoiceAI] Înregistrare oprită: {durationSec:F1}s, {samplesRecorded} samples.");

        currentState = State.Transcribing;
        ShowInPanel(msgProcessing);

        // Convertim la WAV si trimitem la Whisper
        byte[] wavBytes = WavUtility.AudioClipToWav(trimmed);
        Destroy(trimmed);

        if (wavBytes == null)
        {
            currentState = State.Idle;
            ShowInPanel("Eroare la conversia audio.");
            return;
        }

        if (aiService == null)
        {
            currentState = State.Idle;
            ShowInPanel("[Placeholder] OpenAIService nu e configurat.");
            Debug.LogWarning("[VoiceAI] aiService e null — fallback placeholder.");
            return;
        }

        aiService.Transcribe(wavBytes,
            onSuccess: transcribed =>
            {
                Debug.Log($"[VoiceAI] Whisper: \"{transcribed}\"");
                capturedLastQuestion = transcribed; // salvam pentru a adauga in istoric
                ShowInPanel($"Tu: {transcribed}");
                StartCoroutine(AskAfterDelay(transcribed));
            },
            onError: err =>
            {
                Debug.LogError($"[VoiceAI] Eroare Whisper: {err}");
                ShowInPanel($"Eroare transcriere: {err}");
                currentState = State.Idle;
            }
        );
    }

    // ─── Interogare AI ──────────────────────────────────────────────────────────

    private IEnumerator AskAfterDelay(string transcribedQuestion)
    {
        // Pauza scurta: utilizatorul poate citi ce a zis inainte sa apara raspunsul
        yield return new WaitForSeconds(1.2f);

        currentState = State.Asking;
        ShowInPanel(msgAsking);

        int exchangesBefore = conversationHistory.Count / 2;
        Debug.Log($"[VoiceAI] AskWithHistory | planetă={capturedPartName} | " +
                  $"schimburi anterioare={exchangesBefore}");

        aiService.AskWithHistory(
            question:               transcribedQuestion,
            contextualSystemPrompt: contextualSystemPrompt,
            history:                conversationHistory,
            onSuccess: response =>
            {
                Debug.Log($"[VoiceAI] Răspuns AI ({capturedPartName}): {response}");

                // Adaugam schimbul in istoric
                AddToHistory(capturedLastQuestion, response);

                // 1. Textul apare imediat, fara sa asteptam TTS
                ShowInPanel(response);
                currentState = State.Idle;

                // 2. TTS in background, eroarea e non-fatala (textul e deja afisat)
                if (ttsEnabled && aiService != null)
                {
                    aiService.Speak(response,
                        onSuccess: clip =>
                        {
                            if (ttsAudioSource == null) { Destroy(clip); return; }
                            // Oprim orice redare anterioara (ex. raspuns precedent)
                            ttsAudioSource.Stop();
                            ttsAudioSource.clip = clip;
                            ttsAudioSource.Play();
                            Debug.Log($"[VoiceAI] TTS redare pornită ({clip.length:F1}s).");
                        },
                        onError: err =>
                        {
                            // Non-fatal: textul e deja vizibil
                            Debug.LogWarning($"[VoiceAI] TTS ignorat: {err}");
                        }
                    );
                }
            },
            onError: err =>
            {
                Debug.LogError($"[VoiceAI] Eroare AI: {err}");
                ShowInPanel($"Eroare AI: {err}");
                // Nu adaugam in istoric, utilizatorul poate reincerca
                currentState = State.Idle;
            }
        );
    }

    /// <summary>
    /// Adauga un schimb user+assistant in istoric.
    /// Daca depasim maxHistoryExchanges, eliminam cel mai vechi schimb (2 mesaje).
    /// </summary>
    private void AddToHistory(string userMessage, string assistantMessage)
    {
        conversationHistory.Add(new ConversationMessage("user",      userMessage));
        conversationHistory.Add(new ConversationMessage("assistant", assistantMessage));

        // Trimming: maxHistoryExchanges x 2 mesaje = limita listei
        int maxMessages = maxHistoryExchanges * 2;
        while (conversationHistory.Count > maxMessages)
        {
            conversationHistory.RemoveAt(0); // sterge cel mai vechi user
            conversationHistory.RemoveAt(0); // sterge cel mai vechi assistant
        }

        Debug.Log($"[VoiceAI] Istoric actualizat: {conversationHistory.Count / 2} schimburi " +
                  $"(max {maxHistoryExchanges}).");
    }

    // ─── Helpers UI ─────────────────────────────────────────────────────────────

    private void ShowInPanel(string text)
    {
        if (panel != null)
            panel.ShowDescription(text);
    }

    private void SetLabel(string text)
    {
        if (voiceButtonLabel != null)
            voiceButtonLabel.text = text;
    }
}
