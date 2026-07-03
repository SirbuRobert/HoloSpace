using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Post-build script care rulează automat după fiecare iOS build din Unity.
/// Adaugă Photos.framework și setează permisiunile necesare în Info.plist.
/// Nu necesită nicio configurare manuală în Xcode sau Unity Player Settings.
/// </summary>
public static class XcodeBuildPostProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        AddPhotosFramework(buildPath);
        UpdateInfoPlist(buildPath);
    }

    // ─── Photos.framework ────────────────────────────────────────────────────────

    private static void AddPhotosFramework(string buildPath)
    {
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        // Unity 2019.3+: target principal e UnityFramework, dar framework-urile
        // de sistem merg pe main target (Unity-iPhone)
        string mainTarget      = proj.GetUnityMainTargetGuid();
        string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

        proj.AddFrameworkToProject(mainTarget,      "Photos.framework", false);
        proj.AddFrameworkToProject(frameworkTarget, "Photos.framework", false);

        proj.WriteToFile(projPath);
        Debug.Log("[XcodeBuildPostProcessor] Photos.framework adăugat.");
    }

    // ─── Info.plist ──────────────────────────────────────────────────────────────

    private static void UpdateInfoPlist(string buildPath)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning("[XcodeBuildPostProcessor] Info.plist negăsit la: " + plistPath);
            return;
        }

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;

        // Permisiune completă Photo Library — necesară pentru UISaveVideoAtPathToSavedPhotosAlbum
        root.SetString(
            "NSPhotoLibraryUsageDescription",
            "HoloSpace salvează înregistrările AR în galeria ta."
        );
        // Permisiune add-only (fallback PHPhotoLibrary)
        root.SetString(
            "NSPhotoLibraryAddUsageDescription",
            "HoloSpace salvează înregistrările AR în galeria ta."
        );

        // Permisiune microfon — pentru VoiceAIController (Whisper STT)
        // Setează doar dacă nu există deja
        if (!root.values.ContainsKey("NSMicrophoneUsageDescription"))
        {
            root.SetString(
                "NSMicrophoneUsageDescription",
                "HoloSpace folosește microfonul pentru a-ți auzi întrebările despre obiecte AR."
            );
        }

        plist.WriteToFile(plistPath);
        Debug.Log("[XcodeBuildPostProcessor] Info.plist actualizat cu permisiuni Photos + Microfon.");
    }
}
