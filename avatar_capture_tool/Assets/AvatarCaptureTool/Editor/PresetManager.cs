#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

[System.Serializable]
public class CapturePreset
{
    public Vector3 cameraPosition;
    public Quaternion cameraRotation;
    public Color[] lightColors;
    public float[] lightIntensities;
    public string animationClipName;
}

public static class PresetManager
{
    private static readonly string PresetPath = "Assets/AvatarCaptureTool/Presets";

    public static void SavePreset(CapturePreset preset, string name)
    {
        Directory.CreateDirectory(PresetPath);
        string path = Path.Combine(PresetPath, $"{name}.asset");
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CapturePresetSO>(), path);
        AssetDatabase.SaveAssets();
    }

    public static CapturePreset LoadPreset(string name)
    {
        string path = Path.Combine(PresetPath, $"{name}.asset");
        return AssetDatabase.LoadAssetAtPath<CapturePresetSO>(path)?.preset;
    }
}

public class CapturePresetSO : ScriptableObject
{
    public CapturePreset preset;
}
#endif