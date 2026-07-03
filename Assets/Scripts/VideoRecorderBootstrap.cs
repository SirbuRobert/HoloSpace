#if UNITY_IOS
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using HoloKit.iOS;
using HoloKit.UI;
#endif
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wrapper peste HoloKitVideoRecorder care rezolva cateva probleme:
///
///   1. Conflict de nume fisier: GetTimestampedFilename() are precizie de 1 secunda,
///      deci StartRecording rapid dupa StopRecording produce AVFoundation -11823
///      "Cannot Save". Fix: curatam temp files inainte de fiecare Start.
///
///   2. UI nu se actualizeaza: SafeToggleRecording apeleaza recorder-ul direct,
///      ocolind HoloKitDefaultUICanvas care tine referinta la RecordButtonText.
///      Fix: actualizam textul butonului dupa fiecare toggle.
///
///   3. Camera session intrerupta (FigXPCUtilities -17281): apare cand app-ul
///      trece prin background/foreground in timp ce recordeaza.
///      Fix: detectam revenirea din background si facem cleanup automat.
/// </summary>
[DisallowMultipleComponent]
public class VideoRecorderBootstrap : MonoBehaviour
{
#if UNITY_IOS
    [Header("UI")]
    [Tooltip("Textul de pe butonul de record. Actualizat după fiecare toggle.")]
    [SerializeField] private Text recordButtonText;

    [Header("Mesaje buton")]
    [SerializeField] private string textStart = "Start Recording";
    [SerializeField] private string textStop  = "Stop Recording";

    private HoloKitVideoRecorder recorder;
    private bool wasInterruptedByBackground;

    // ─── Native plugin ───────────────────────────────────────────────────────────
#if !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void HoloSpace_SaveVideoToGallery(string filePath);
#endif

    // ─── Unity lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        recorder = FindFirstObjectByType<HoloKitVideoRecorder>();

        CleanTempVideoFiles();
        UpdateButtonText();
        Debug.Log("[VideoRecorderBootstrap] Inițializat.");
    }

    private void OnApplicationPause(bool paused)
    {
        // iOS trimite OnApplicationPause(true) cand app-ul merge in background
        if (paused && recorder != null && recorder.IsRecording)
        {
            // Camera session va fi intrerupta de iOS, oprim recording-ul curat
            Debug.Log("[VideoRecorderBootstrap] App în background — opresc recording preventiv.");
            DoStopRecording();
            wasInterruptedByBackground = true;
        }
        else if (!paused && wasInterruptedByBackground)
        {
            wasInterruptedByBackground = false;
            CleanTempVideoFiles();
            Debug.Log("[VideoRecorderBootstrap] App revenit din background — temp files curățate.");
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inlocuitor drop-in pentru HoloKitDefaultUICanvas.ToggleRecording(),
    /// cablat pe butonul de record in locul metodei originale.
    /// </summary>
    public void SafeToggleRecording()
    {
        if (recorder == null)
        {
            Debug.LogWarning("[VideoRecorderBootstrap] HoloKitVideoRecorder negăsit.");
            return;
        }

        if (recorder.IsRecording)
            DoStopRecording();
        else
            DoStartRecording();
    }

    // ─── Internals ───────────────────────────────────────────────────────────────

    private void DoStartRecording()
    {
        CleanTempVideoFiles(); // elimina conflictul de nume
        recorder.StartRecording();
        Debug.Log($"[VideoRecorderBootstrap] Recording pornit. IsRecording={recorder.IsRecording}");
        UpdateButtonText();
    }

    private void DoStopRecording()
    {
        recorder.EndRecording();
        Debug.Log($"[VideoRecorderBootstrap] Recording oprit. IsRecording={recorder.IsRecording}");
        UpdateButtonText();

        // Salvam cel mai recent fisier Record_*.mp4 in Photo Library
        StartCoroutine(SaveLatestRecordingAfterDelay(0.5f));
    }

    private System.Collections.IEnumerator SaveLatestRecordingAfterDelay(float delay)
    {
        // Delay initial minim
        yield return new UnityEngine.WaitForSeconds(delay);

        // Gasim fisierul inainte sa asteptam stabilizarea
        string tempDir = Application.temporaryCachePath;
        string latestFile = null;
        if (Directory.Exists(tempDir))
        {
            latestFile = Directory.GetFiles(tempDir, "Record_*.mp4")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }

        if (latestFile == null)
        {
            Debug.LogWarning("[VideoRecorderBootstrap] Niciun fișier video găsit.");
            yield break;
        }

        // Asteptam ca dimensiunea fisierului sa se stabilizeze;
        // finalizarea MP4 (moov atom) poate dura cateva secunde sub load
        long lastSize  = -1;
        int  stable    = 0;
        int  maxChecks = 20; // max 10 secunde (20 x 0.5s)

        while (stable < 3 && maxChecks-- > 0)
        {
            yield return new UnityEngine.WaitForSeconds(0.5f);
            try
            {
                long size = new FileInfo(latestFile).Length;
                if (size > 0 && size == lastSize)
                    stable++;
                else
                {
                    stable   = 0;
                    lastSize = size;
                }
            }
            catch { break; }
        }

        Debug.Log($"[VideoRecorderBootstrap] Fișier stabil ({lastSize} bytes) — salvez în galerie.");
        SaveLatestRecordingToGallery();
    }

    private void SaveLatestRecordingToGallery()
    {
        string tempDir = Application.temporaryCachePath;
        if (!Directory.Exists(tempDir)) return;

        // Gasim cel mai recent fisier Record_*.mp4
        string latestFile = Directory.GetFiles(tempDir, "Record_*.mp4")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (latestFile == null)
        {
            Debug.LogWarning("[VideoRecorderBootstrap] Niciun fișier video găsit pentru salvare.");
            return;
        }

        Debug.Log($"[VideoRecorderBootstrap] Salvez în galerie: {latestFile}");

#if UNITY_IOS && !UNITY_EDITOR
        HoloSpace_SaveVideoToGallery(latestFile);
#else
        Debug.Log("[VideoRecorderBootstrap] (Editor — salvarea în galerie e disponibilă doar pe device)");
#endif
    }

    private void UpdateButtonText()
    {
        if (recordButtonText == null) return;
        bool isRec = recorder != null && recorder.IsRecording;
        recordButtonText.text = isRec ? textStop : textStart;
    }

    private static void CleanTempVideoFiles()
    {
        string tempDir = Application.temporaryCachePath;
        if (!Directory.Exists(tempDir)) return;

        int count = 0;
        foreach (string file in Directory.GetFiles(tempDir, "Record_*.mp4"))
        {
            try { File.Delete(file); count++; }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VideoRecorderBootstrap] Nu am putut șterge {file}: {ex.Message}");
            }
        }

        if (count > 0)
            Debug.Log($"[VideoRecorderBootstrap] {count} fișier(e) temp șterse.");
    }
#endif
}
