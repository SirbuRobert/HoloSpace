using System.IO;
using UnityEngine;

/// <summary>
/// Convertor AudioClip -> WAV (PCM 16-bit, little-endian, mono sau stereo).
/// Format acceptat de OpenAI Whisper API si de majoritatea serviciilor STT.
///
/// Utilizare:
///     byte[] wavBytes = WavUtility.AudioClipToWav(myClip);
/// </summary>
public static class WavUtility
{
    // Headerul WAV standard are 44 de bytes (RIFF + fmt + data)
    private const int HeaderSize = 44;

    /// <summary>
    /// Converteste un <see cref="AudioClip"/> Unity in bytes WAV.
    /// Clipul poate fi mono sau stereo; float samples -> PCM 16-bit.
    /// </summary>
    public static byte[] AudioClipToWav(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[WavUtility] AudioClip null — imposibil de convertit.");
            return null;
        }

        // Extragem datele float brute
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        // Convertim float [-1, 1] -> PCM 16-bit signed little-endian
        byte[] pcm = FloatToPcm16(samples);

        int sampleRate  = clip.frequency;
        int channels    = clip.channels;
        int byteRate    = sampleRate * channels * 2; // 2 bytes per sample (16-bit)
        int blockAlign  = channels * 2;
        int dataSize    = pcm.Length;
        int fileSize    = dataSize + HeaderSize - 8; // fara primii 8 bytes (RIFF + size)

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            // ── RIFF chunk ──────────────────────────────────────────────────────
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(fileSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });

            // ── fmt  chunk ──────────────────────────────────────────────────────
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);              // marimea chunk-ului fmt (PCM standard = 16)
            bw.Write((short)1);        // audio format: PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)16);       // bits per sample

            // ── data chunk ──────────────────────────────────────────────────────
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            bw.Write(pcm);

            return ms.ToArray();
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static byte[] FloatToPcm16(float[] samples)
    {
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            // Clampam la [-1, 1] si scalam la Int16
            float clamped = Mathf.Clamp(samples[i], -1f, 1f);
            short value   = (short)(clamped < 0
                                    ? clamped * 32768f
                                    : clamped * 32767f);

            // Little-endian
            pcm[i * 2]     = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        return pcm;
    }
}
