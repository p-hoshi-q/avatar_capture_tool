#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Recorder;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public enum CustomFitMode
{
    FitVertical,
    FitHorizontal,
    FitBoth
}

public class AvatarCaptureToolWindow : EditorWindow
{
    private List<GameObject> targetAvatars = new List<GameObject>();
    private Texture2D backgroundImage;
    private List<Texture2D> stickerImages = new List<Texture2D>();
    private List<RawImage> stickerRawImages = new List<RawImage>();
    private List<Vector2> stickerPositions = new List<Vector2>();
    private List<float> stickerScales = new List<float>();
    private Canvas backgroundCanvas;
    private RawImage canvasImage;
    private Vector2 scrollPosition;
    private Camera previewCamera;
    private List<Light> lights = new List<Light>();
    private AnimationClip selectedAnimation;
    private AnimationClip expressionAnimation;
    private bool isVideoRecording;
    private bool followCamera;
    private Vector3 followOffset = new Vector3(0, 1.5f, -2f);
    private Vector3 initialCameraPos;
    private string saveFolderPath = "Assets/CaptureOutput";
    private FilterMode filterMode = FilterMode.Bilinear;
    private RecorderController recorderController;
    private CustomFitMode fitMode = CustomFitMode.FitBoth;
    private LanguageManager.Language currentLanguage = LanguageManager.Language.Japanese;

    [MenuItem("Tools/Avatar Capture Tool")]
    public static void ShowWindow()
    {
        GetWindow<AvatarCaptureToolWindow>("Avatar Capture Tool");
    }

    private void OnEnable()
    {
        SetupPreviewCamera();
        SetupBackgroundCanvas();
        SetupRecorder();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 言語選択
        currentLanguage = (LanguageManager.Language)EditorGUILayout.EnumPopup("Language", currentLanguage);

        // アバター選択
        EditorGUILayout.LabelField(LanguageManager.GetText("TargetAvatars", currentLanguage), EditorStyles.boldLabel);
        for (int i = 0; i < targetAvatars.Count; i++)
        {
            targetAvatars[i] = (GameObject)EditorGUILayout.ObjectField(
                $"{LanguageManager.GetText("Avatar", currentLanguage)} {i + 1}",
                targetAvatars[i],
                typeof(GameObject),
                true
            );
        }
        if (GUILayout.Button(LanguageManager.GetText("AddAvatar", currentLanguage))) targetAvatars.Add(null);
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastAvatar", currentLanguage)) && targetAvatars.Count > 0)
            targetAvatars.RemoveAt(targetAvatars.Count - 1);

        // 背景設定
        EditorGUILayout.LabelField(LanguageManager.GetText("BackgroundSettings", currentLanguage), EditorStyles.boldLabel);
        backgroundImage = (Texture2D)EditorGUILayout.ObjectField(
            LanguageManager.GetText("BackgroundImage", currentLanguage),
            backgroundImage,
            typeof(Texture2D),
            false
        );
        if (backgroundImage != null && canvasImage != null)
        {
            canvasImage.texture = backgroundImage;
            fitMode = (CustomFitMode)EditorGUILayout.EnumPopup(LanguageManager.GetText("FitMode", currentLanguage), fitMode);
            UpdateCanvasFitMode();
        }

        // ステッカー設定
        EditorGUILayout.LabelField(LanguageManager.GetText("StickerImage", currentLanguage), EditorStyles.boldLabel);
        for (int i = 0; i < stickerImages.Count; i++)
        {
            stickerImages[i] = (Texture2D)EditorGUILayout.ObjectField(
                $"{LanguageManager.GetText("StickerImage", currentLanguage)} {i + 1}",
                stickerImages[i],
                typeof(Texture2D),
                false
            );
            if (stickerImages[i] != null && i < stickerRawImages.Count)
            {
                stickerRawImages[i].texture = stickerImages[i];
                stickerPositions[i] = EditorGUILayout.Vector2Field(
                    string.Format(LanguageManager.GetText("StickerPosition", currentLanguage), i + 1),
                    stickerPositions[i]
                );
                stickerScales[i] = EditorGUILayout.Slider(
                    string.Format(LanguageManager.GetText("StickerScale", currentLanguage), i + 1),
                    stickerScales[i],
                    0.1f,
                    2f
                );
                UpdateStickerTransform(i);
            }
        }
        if (GUILayout.Button(LanguageManager.GetText("AddSticker", currentLanguage))) AddSticker();
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastSticker", currentLanguage)) && stickerImages.Count > 0)
            RemoveSticker();

        // ライト設定
        EditorGUILayout.LabelField(LanguageManager.GetText("LightSettings", currentLanguage), EditorStyles.boldLabel);
        for (int i = 0; i < lights.Count; i++)
        {
            lights[i].color = EditorGUILayout.ColorField(
                string.Format(LanguageManager.GetText("LightColor", currentLanguage), i + 1),
                lights[i].color
            );
            lights[i].intensity = EditorGUILayout.Slider(
                string.Format(LanguageManager.GetText("LightIntensity", currentLanguage), i + 1),
                lights[i].intensity,
                0f,
                8f
            );
        }
        if (GUILayout.Button(LanguageManager.GetText("AddLight", currentLanguage))) AddLight();
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastLight", currentLanguage)) && lights.Count > 0) RemoveLight();

        // カメラ設定
        EditorGUILayout.LabelField(LanguageManager.GetText("CameraSettings", currentLanguage), EditorStyles.boldLabel);
        if (previewCamera != null)
        {
            previewCamera.transform.position = EditorGUILayout.Vector3Field(
                LanguageManager.GetText("CameraPosition", currentLanguage),
                previewCamera.transform.position
            );
            previewCamera.transform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field(
                LanguageManager.GetText("CameraRotation", currentLanguage),
                previewCamera.transform.rotation.eulerAngles
            ));
        }

        // アニメーション設定
        EditorGUILayout.LabelField(LanguageManager.GetText("AnimationSettings", currentLanguage), EditorStyles.boldLabel);
        selectedAnimation = (AnimationClip)EditorGUILayout.ObjectField(
            LanguageManager.GetText("PoseAnimation", currentLanguage),
            selectedAnimation,
            typeof(AnimationClip),
            false
        );
        expressionAnimation = (AnimationClip)EditorGUILayout.ObjectField(
            LanguageManager.GetText("ExpressionAnimation", currentLanguage),
            expressionAnimation,
            typeof(AnimationClip),
            false
        );
        if (GUILayout.Button(LanguageManager.GetText("PlayAnimation", currentLanguage))) PlayAnimation();

        // フィルター設定
        EditorGUILayout.LabelField(LanguageManager.GetText("FilterSettings", currentLanguage), EditorStyles.boldLabel);
        filterMode = (FilterMode)EditorGUILayout.EnumPopup(
            LanguageManager.GetText("TextureFilter", currentLanguage),
            filterMode
        );

        // 動画設定
        EditorGUILayout.LabelField(LanguageManager.GetText("VideoSettings", currentLanguage), EditorStyles.boldLabel);
        followCamera = EditorGUILayout.Toggle(LanguageManager.GetText("FollowCamera", currentLanguage), followCamera);
        followOffset = EditorGUILayout.Vector3Field(LanguageManager.GetText("FollowOffset", currentLanguage), followOffset);
        if (GUILayout.Button(isVideoRecording ? LanguageManager.GetText("StopRecording", currentLanguage) : LanguageManager.GetText("StartRecording", currentLanguage)))
        {
            if (isVideoRecording) StopRecording();
            else StartRecording();
        }

        // 保存設定
        EditorGUILayout.LabelField(LanguageManager.GetText("SaveSettings", currentLanguage), EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(LanguageManager.GetText("SaveFolder", currentLanguage), GUILayout.Width(100));
        EditorGUILayout.TextField(saveFolderPath);
        if (GUILayout.Button("Browse"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Save Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    saveFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    saveFolderPath = selectedPath;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button(LanguageManager.GetText("CaptureImage", currentLanguage))) CaptureImage();
        if (GUILayout.Button(LanguageManager.GetText("CaptureImageTransparent", currentLanguage))) CaptureImage(true);

        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        if (previewCamera != null && followCamera && targetAvatars.Any(a => a != null))
        {
            var target = targetAvatars.First(a => a != null);
            previewCamera.transform.position = target.transform.position + followOffset;
        }
        Repaint();
    }

    private void SetupPreviewCamera()
    {
        var camObj = new GameObject("PreviewCamera");
        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.Skybox;
        previewCamera.transform.position = new Vector3(0, 1.5f, -2f);
        initialCameraPos = previewCamera.transform.position;
    }

    private void SetupBackgroundCanvas()
    {
        var canvasObj = new GameObject("BackgroundCanvas");
        backgroundCanvas = canvasObj.AddComponent<Canvas>();
        backgroundCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasImage = canvasObj.AddComponent<RawImage>();
        var rectTransform = canvasImage.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void SetupRecorder()
    {
        var settings = new RecorderControllerSettings();
        recorderController = new RecorderController(settings);
    }

    private void UpdateCanvasFitMode()
    {
        if (canvasImage == null || backgroundImage == null) return;

        var rectTransform = canvasImage.GetComponent<RectTransform>();
        var canvasScaler = backgroundCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = backgroundCanvas.gameObject.AddComponent<CanvasScaler>();

        switch (fitMode)
        {
            case CustomFitMode.FitVertical:
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(backgroundImage.width, backgroundImage.height);
                canvasScaler.matchWidthOrHeight = 1f; // 縦フィット
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                break;
            case CustomFitMode.FitHorizontal:
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(backgroundImage.width, backgroundImage.height);
                canvasScaler.matchWidthOrHeight = 0f; // 横フィット
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                break;
            case CustomFitMode.FitBoth:
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(backgroundImage.width, backgroundImage.height);
                // 全体フィットはRawImageのアンカーを調整して全画面に引き伸ばす
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one; // スケールリセット
                break;
        }
    }

    private void AddSticker()
    {
        stickerImages.Add(null);
        stickerPositions.Add(Vector2.zero);
        stickerScales.Add(1f);

        var stickerObj = new GameObject($"Sticker_{stickerImages.Count}");
        stickerObj.transform.SetParent(backgroundCanvas.transform, false);
        var rawImage = stickerObj.AddComponent<RawImage>();
        var rectTransform = rawImage.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100, 100);
        stickerRawImages.Add(rawImage);
    }

    private void RemoveSticker()
    {
        if (stickerImages.Count > 0)
        {
            stickerImages.RemoveAt(stickerImages.Count - 1);
            stickerPositions.RemoveAt(stickerPositions.Count - 1);
            stickerScales.RemoveAt(stickerScales.Count - 1);
            if (stickerRawImages.Count > 0)
            {
                DestroyImmediate(stickerRawImages[stickerRawImages.Count - 1].gameObject);
                stickerRawImages.RemoveAt(stickerRawImages.Count - 1);
            }
        }
    }

    private void UpdateStickerTransform(int index)
    {
        if (index >= stickerRawImages.Count || stickerRawImages[index] == null) return;

        var rectTransform = stickerRawImages[index].GetComponent<RectTransform>();
        rectTransform.anchoredPosition = stickerPositions[index];
        rectTransform.localScale = Vector3.one * stickerScales[index];
    }

    private void AddLight()
    {
        var lightObj = new GameObject($"Light_{lights.Count + 1}");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 1f;
        light.color = Color.white;
        light.transform.position = new Vector3(0, 2, -1);
        lights.Add(light);
    }

    private void RemoveLight()
    {
        if (lights.Count > 0)
        {
            DestroyImmediate(lights[lights.Count - 1].gameObject);
            lights.RemoveAt(lights.Count - 1);
        }
    }

    private void PlayAnimation()
    {
        foreach (var avatar in targetAvatars.Where(a => a != null))
        {
            var animator = avatar.GetComponent<Animator>();
            if (animator != null && selectedAnimation != null)
            {
                animator.Play(selectedAnimation.name);
                if (expressionAnimation != null)
                {
                    animator.Play(expressionAnimation.name, 1);
                }
            }
        }
    }

    private void CaptureImage(bool transparent = false)
    {
        if (string.IsNullOrEmpty(saveFolderPath))
        {
            Debug.LogError("Save folder path is not specified.");
            return;
        }

        string folder = Path.Combine(saveFolderPath, "Images");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Capture_{System.DateTime.Now:yyyyMMddHHmmss}.png");

        if (transparent)
        {
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = Color.clear;
        }

        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        previewCamera.targetTexture = rt;
        previewCamera.Render();
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        previewCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);
        DestroyImmediate(tex);

        if (transparent)
        {
            previewCamera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    private void StartRecording()
    {
        if (string.IsNullOrEmpty(saveFolderPath))
        {
            Debug.LogError("Save folder path is not specified.");
            return;
        }

        string folder = Path.Combine(saveFolderPath, "Videos");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Video_{System.DateTime.Now:yyyyMMddHHmmss}.mp4");

        var movieRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieRecorder.OutputFile = path;
        recorderController.PrepareRecording();
        recorderController.StartRecording();
        isVideoRecording = true;
    }

    private void StopRecording()
    {
        recorderController.StopRecording();
        isVideoRecording = false;
        AssetDatabase.Refresh();
    }

    private void Cleanup()
    {
        if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
        if (backgroundCanvas != null) DestroyImmediate(backgroundCanvas.gameObject);
        foreach (var light in lights) if (light != null) DestroyImmediate(light.gameObject);
        foreach (var sticker in stickerRawImages) if (sticker != null) DestroyImmediate(sticker.gameObject);
        lights.Clear();
        stickerRawImages.Clear();
    }
}
#endif