#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public enum CustomFitMode
{
    FitVertical,
    FitHorizontal,
    FitBoth
}

public enum ResolutionOption
{
    FHD, // 1920x1080
    TwoK, // 2560x1440
    FourK, // 3840x2160
    Custom
}

public enum FrameRateOption
{
    FPS24 = 24,
    FPS30 = 30,
    FPS60 = 60,
}

[System.Serializable]
public class AvatarAnimationSettings
{
    public GameObject avatar;
    public AnimationClip poseAnimation; // ポーズまたは動きのあるアニメーション
    public AnimationClip expressionAnimation;
}

public class AvatarCaptureToolWindow : EditorWindow
{
    private List<AvatarAnimationSettings> avatarSettings = new List<AvatarAnimationSettings>();
    private Texture2D backgroundImage;
    private List<Texture2D> stickerImages = new List<Texture2D>();
    private List<RawImage> stickerRawImages = new List<RawImage>();
    private List<Vector2> stickerPositions = new List<Vector2>();
    private List<float> stickerScales = new List<float>();
    private Canvas backgroundCanvas;
    private RawImage backgroundImageComponent;
    private Vector2 scrollPosition;
    private Camera previewCamera;
    private List<Light> lights = new List<Light>();
    private List<LightType> lightTypes = new List<LightType>();
    private bool isVideoRecording;
    private bool followCamera;
    private Vector3 followOffset = new Vector3(0, 1.5f, -2f);
    private Vector3 initialCameraPos;
    private string saveFolderPath = "Assets/CaptureOutput";
    private FilterMode filterMode = FilterMode.Bilinear;
    private RecorderController recorderController;
    private CustomFitMode fitMode = CustomFitMode.FitBoth;
    private LanguageManager.Language currentLanguage = LanguageManager.Language.Japanese;
    private RenderTexture previewTexture;
    private Rect previewRect;
    private float maxStickerScale = 10f;
    private ResolutionOption resolutionOption = ResolutionOption.FHD;
    private FrameRateOption frameRateOption = FrameRateOption.FPS30;
    private int customWidth = 1920;
    private int customHeight = 1080;
    // 録画時のファイルパスを保持
    private string currentRecordingPath = null; // 物理パス（例：C:/Project/Assets/CaptureOutput/Videos/Video_....mp4）
    private string currentRelativePath = null; // 相対パス（例：Assets/CaptureOutput/Videos/Video_....mp4）

    [MenuItem("Tools/Avatar Capture Tool")]
    public static void ShowWindow()
    {
        GetWindow<AvatarCaptureToolWindow>("Avatar Capture Tool");
    }

    private void OnEnable()
    {
        ResetState();
        SetupPreviewCamera();
        SetupBackgroundCanvas();
        SetupRecorder();
        SetupPreviewTexture();
        DisableEnvironmentLighting(); // 環境光を無効化
    }

    private void OnDisable()
    {
        Cleanup();
        ResetState();
        RestoreEnvironmentLighting(); // 環境光を復元
    }

    private void ResetState()
    {
        avatarSettings.Clear();
        backgroundImage = null;
        stickerImages.Clear();
        stickerRawImages.Clear();
        stickerPositions.Clear();
        stickerScales.Clear();
        backgroundCanvas = null;
        backgroundImageComponent = null;
        scrollPosition = Vector2.zero;
        previewCamera = null;
        lights.Clear();
        lightTypes.Clear();
        isVideoRecording = false;
        followCamera = false;
        followOffset = new Vector3(0, 1.5f, -2f);
        initialCameraPos = Vector3.zero;
        saveFolderPath = "Assets/CaptureOutput";
        filterMode = FilterMode.Bilinear;
        recorderController = null;
        fitMode = CustomFitMode.FitBoth;
        currentLanguage = LanguageManager.Language.Japanese;
        previewTexture = null;
        maxStickerScale = 10f;
        resolutionOption = ResolutionOption.FHD;
        frameRateOption = FrameRateOption.FPS30;
        customWidth = 1920;
        customHeight = 1080;
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 言語選択
        EditorGUILayout.BeginVertical();
        currentLanguage = (LanguageManager.Language)EditorGUILayout.EnumPopup(LanguageManager.GetText("Language", currentLanguage), currentLanguage);
        EditorGUILayout.EndVertical();

        // プレビュー画面
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("Preview", currentLanguage), EditorStyles.boldLabel);
        if (previewTexture != null)
        {
            float aspect = (float)GetResolutionWidth() / GetResolutionHeight();
            previewRect = EditorGUILayout.GetControlRect(GUILayout.Height(200), GUILayout.Width(200 * aspect));
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
        }
        EditorGUILayout.EndVertical();

        // 解像度設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("Resolution Settings", currentLanguage), EditorStyles.boldLabel);
        resolutionOption = (ResolutionOption)EditorGUILayout.EnumPopup(LanguageManager.GetText("Resolution", currentLanguage), resolutionOption);
        if (resolutionOption == ResolutionOption.Custom)
        {
            customWidth = EditorGUILayout.IntField("Width", customWidth);
            customHeight = EditorGUILayout.IntField("Height", customHeight);
            customWidth = Mathf.Max(100, customWidth);
            customHeight = Mathf.Max(100, customHeight);
        }
        if (GUILayout.Button(LanguageManager.GetText("Apply Resolution", currentLanguage)))
        {
            SetupPreviewTexture();
            SetupRecorder();
        }
        EditorGUILayout.EndVertical();

        // アバターとアニメーション設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("TargetAvatars", currentLanguage), EditorStyles.boldLabel);
        for (int i = 0; i < avatarSettings.Count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            avatarSettings[i].avatar = (GameObject)EditorGUILayout.ObjectField(
                $"{LanguageManager.GetText("Avatar", currentLanguage)} {i + 1}",
                avatarSettings[i].avatar,
                typeof(GameObject),
                true
            );
            avatarSettings[i].poseAnimation = (AnimationClip)EditorGUILayout.ObjectField(
                $"{LanguageManager.GetText("PoseAnimation", currentLanguage)} {i + 1}",
                avatarSettings[i].poseAnimation,
                typeof(AnimationClip),
                false
            );
            avatarSettings[i].expressionAnimation = (AnimationClip)EditorGUILayout.ObjectField(
                $"{LanguageManager.GetText("ExpressionAnimation", currentLanguage)} {i + 1}",
                avatarSettings[i].expressionAnimation,
                typeof(AnimationClip),
                false
            );
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button(LanguageManager.GetText("AddAvatar", currentLanguage)))
        {
            avatarSettings.Add(new AvatarAnimationSettings());
        }
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastAvatar", currentLanguage)) && avatarSettings.Count > 0)
        {
            avatarSettings.RemoveAt(avatarSettings.Count - 1);
        }
        if (GUILayout.Button(LanguageManager.GetText("PlayAnimation", currentLanguage)))
        {
            PlayAnimation();
        }
        EditorGUILayout.EndVertical();

        // 背景設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("BackgroundSettings", currentLanguage), EditorStyles.boldLabel);
        backgroundImage = (Texture2D)EditorGUILayout.ObjectField(
            LanguageManager.GetText("BackgroundImage", currentLanguage),
            backgroundImage,
            typeof(Texture2D),
            false
        );
        if (backgroundImage != null && backgroundImageComponent != null)
        {
            backgroundImageComponent.texture = backgroundImage;
            fitMode = (CustomFitMode)EditorGUILayout.EnumPopup(LanguageManager.GetText("FitMode", currentLanguage), fitMode);
            UpdateBackgroundFitMode();
        }
        EditorGUILayout.EndVertical();

        // ステッカー設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("StickerImage", currentLanguage), EditorStyles.boldLabel);
        maxStickerScale = EditorGUILayout.FloatField(LanguageManager.GetText("Max Sticker Scale", currentLanguage), maxStickerScale);
        maxStickerScale = Mathf.Max(0.1f, maxStickerScale);
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
                EditorGUILayout.LabelField(string.Format(LanguageManager.GetText("StickerPosition", currentLanguage), i + 1));
                stickerPositions[i] = EditorGUILayout.Vector2Field("Position", stickerPositions[i]);
                stickerScales[i] = EditorGUILayout.Slider(
                    string.Format(LanguageManager.GetText("StickerScale", currentLanguage), i + 1),
                    stickerScales[i],
                    0.1f,
                    maxStickerScale
                );
                UpdateStickerTransform(i);
            }
        }
        if (GUILayout.Button(LanguageManager.GetText("AddSticker", currentLanguage))) AddSticker();
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastSticker", currentLanguage)) && stickerImages.Count > 0)
            RemoveSticker();
        EditorGUILayout.EndVertical();

        // ライト設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("LightSettings", currentLanguage), EditorStyles.boldLabel);
        for (int i = 0; i < lights.Count; i++)
        {
            EditorGUILayout.LabelField($"Light {i + 1}");
            lightTypes[i] = (LightType)EditorGUILayout.EnumPopup("Type", lightTypes[i]);
            lights[i].type = lightTypes[i];
            lights[i].color = EditorGUILayout.ColorField("Color", lights[i].color);
            lights[i].intensity = EditorGUILayout.Slider("Intensity", lights[i].intensity, 0f, 8f);
            lights[i].transform.position = EditorGUILayout.Vector3Field("Position", lights[i].transform.position);
            lights[i].transform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Rotation", lights[i].transform.rotation.eulerAngles));
            if (lightTypes[i] == LightType.Point || lightTypes[i] == LightType.Spot)
            {
                lights[i].range = EditorGUILayout.Slider("Range", lights[i].range, 0.1f, 100f);
            }
            if (lightTypes[i] == LightType.Spot)
            {
                lights[i].spotAngle = EditorGUILayout.Slider("Spot Angle", lights[i].spotAngle, 1f, 179f);
            }
            lights[i].shadows = (LightShadows)EditorGUILayout.EnumPopup("Shadows", lights[i].shadows);
            lights[i].cookie = (Texture)EditorGUILayout.ObjectField("Cookie", lights[i].cookie, typeof(Texture), false);
            lights[i].flare = (Flare)EditorGUILayout.ObjectField("Flare", lights[i].flare, typeof(Flare), false);
            lights[i].renderMode = (LightRenderMode)EditorGUILayout.EnumPopup("Render Mode", lights[i].renderMode);
            SerializedObject lightSerialized = new SerializedObject(lights[i]);
            SerializedProperty drawHaloProp = lightSerialized.FindProperty("m_DrawHalo");
            drawHaloProp.boolValue = EditorGUILayout.Toggle("Draw Halo", drawHaloProp.boolValue);
            lightSerialized.ApplyModifiedProperties();
            Debug.Log($"Updated Light {i + 1}: Type={lights[i].type}, Intensity={lights[i].intensity}, Position={lights[i].transform.position}, RenderMode={lights[i].renderMode}");
        }
        if (GUILayout.Button(LanguageManager.GetText("AddLight", currentLanguage))) AddLight();
        if (GUILayout.Button(LanguageManager.GetText("RemoveLastLight", currentLanguage)) && lights.Count > 0) RemoveLight();
        EditorGUILayout.EndVertical();

        // カメラ設定
        EditorGUILayout.BeginVertical();
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
        EditorGUILayout.EndVertical();

        // フィルター設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("FilterSettings", currentLanguage), EditorStyles.boldLabel);
        filterMode = (FilterMode)EditorGUILayout.EnumPopup(
            LanguageManager.GetText("TextureFilter", currentLanguage),
            filterMode
        );
        EditorGUILayout.EndVertical();

        // 動画設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("VideoSettings", currentLanguage), EditorStyles.boldLabel);
        frameRateOption = (FrameRateOption)EditorGUILayout.EnumPopup(LanguageManager.GetText("Frame Rate", currentLanguage), frameRateOption);
        followCamera = EditorGUILayout.Toggle(LanguageManager.GetText("FollowCamera", currentLanguage), followCamera);
        followOffset = EditorGUILayout.Vector3Field(LanguageManager.GetText("FollowOffset", currentLanguage), followOffset);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(LanguageManager.GetText("Video recording is only available in Play mode.", currentLanguage), MessageType.Info);
        }
        if (GUILayout.Button(isVideoRecording ? LanguageManager.GetText("StopRecording", currentLanguage) : LanguageManager.GetText("StartRecording", currentLanguage)))
        {
            if (Application.isPlaying)
            {
                if (isVideoRecording) StopRecording();
                else StartRecording();
            }
            else
            {
                Debug.LogWarning("Please enter Play mode to start recording.");
            }
        }
        EditorGUILayout.EndVertical();

        // 保存設定
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(LanguageManager.GetText("SaveSettings", currentLanguage), EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(LanguageManager.GetText("SaveFolder", currentLanguage), GUILayout.Width(100));
        EditorGUILayout.TextField(saveFolderPath);
        if (GUILayout.Button(LanguageManager.GetText("Browse", currentLanguage)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel(LanguageManager.GetText("Select Save Folder", currentLanguage), Application.dataPath, "");
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
        EditorGUILayout.EndVertical();

        // キャプチャボタン
        EditorGUILayout.BeginVertical();
        if (GUILayout.Button(LanguageManager.GetText("CaptureImage", currentLanguage))) CaptureImage();
        if (GUILayout.Button(LanguageManager.GetText("CaptureImageTransparent", currentLanguage))) CaptureImage(true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        if (previewCamera != null && followCamera && avatarSettings.Any(s => s.avatar != null))
        {
            var target = avatarSettings.First(s => s.avatar != null).avatar;
            previewCamera.transform.position = target.transform.position + followOffset;
        }
        if (previewTexture != null && previewCamera != null)
        {
            previewCamera.targetTexture = previewTexture;
            previewCamera.Render();
            previewCamera.targetTexture = null;
        }
        Repaint();
    }

    private void SetupPreviewCamera()
    {
        var camObj = new GameObject("PreviewCamera");
        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.Skybox;
        previewCamera.cullingMask = ~0; // Everything
        previewCamera.renderingPath = RenderingPath.Forward; // Forward rendering for multiple lights
        previewCamera.transform.position = new Vector3(0f, 0.2f, 0.5f);
        previewCamera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        initialCameraPos = previewCamera.transform.position;
        Debug.Log($"Setup PreviewCamera: CullingMask={previewCamera.cullingMask}, RenderingPath={previewCamera.renderingPath}");
    }

    private void SetupBackgroundCanvas()
    {
        var canvasObj = new GameObject("BackgroundCanvas");
        backgroundCanvas = canvasObj.AddComponent<Canvas>();
        backgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        backgroundCanvas.worldCamera = previewCamera;
        backgroundCanvas.planeDistance = previewCamera.farClipPlane;

        var bgImageObj = new GameObject("BackgroundImage");
        bgImageObj.transform.SetParent(backgroundCanvas.transform, false);
        backgroundImageComponent = bgImageObj.AddComponent<RawImage>();
        var rectTransform = backgroundImageComponent.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void SetupRecorder()
    {
        var settings = new RecorderControllerSettings
        {
            FrameRate = (int)frameRateOption
        };
        recorderController = new RecorderController(settings);
        Debug.Log($"Setup RecorderController with FrameRate={(int)frameRateOption}");
    }

    private void SetupPreviewTexture()
    {
        if (previewTexture != null) DestroyImmediate(previewTexture);
        int width = GetResolutionWidth();
        int height = GetResolutionHeight();
        previewTexture = new RenderTexture(width / 4, height / 4, 24);
        previewTexture.Create();
        Debug.Log($"Setup PreviewTexture: {width / 4}x{height / 4}");
    }

    private int GetResolutionWidth()
    {
        switch (resolutionOption)
        {
            case ResolutionOption.FHD: return 1920;
            case ResolutionOption.TwoK: return 2560;
            case ResolutionOption.FourK: return 3840;
            case ResolutionOption.Custom: return Mathf.Max(100, customWidth);
            default: return 1920;
        }
    }

    private int GetResolutionHeight()
    {
        switch (resolutionOption)
        {
            case ResolutionOption.FHD: return 1080;
            case ResolutionOption.TwoK: return 1440;
            case ResolutionOption.FourK: return 2160;
            case ResolutionOption.Custom: return Mathf.Max(100, customHeight);
            default: return 1080;
        }
    }

    private void UpdateBackgroundFitMode()
    {
        if (backgroundImageComponent == null || backgroundImage == null) return;

        var rectTransform = backgroundImageComponent.GetComponent<RectTransform>();
        float imageAspect = (float)backgroundImage.width / backgroundImage.height;
        float screenAspect = (float)GetResolutionWidth() / GetResolutionHeight();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        switch (fitMode)
        {
            case CustomFitMode.FitVertical:
                float scaleV = screenAspect / imageAspect;
                rectTransform.localScale = new Vector3(scaleV, 1f, 1f);
                break;
            case CustomFitMode.FitHorizontal:
                float scaleH = imageAspect / screenAspect;
                rectTransform.localScale = new Vector3(1f, scaleH, 1f);
                break;
            case CustomFitMode.FitBoth:
                rectTransform.localScale = Vector3.one;
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
        stickerObj.transform.SetAsLastSibling();
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
        light.intensity = 2f; // 視認しやすい強度
        light.color = Color.white;
        light.range = 10f;
        light.transform.position = new Vector3(0, 2, -1); // カメラの視野内に配置
        light.cullingMask = ~0; // Everything
        light.renderMode = LightRenderMode.Auto; // 適切なレンダリングモード
        light.enabled = true; // 明示的に有効化
        SerializedObject lightSerialized = new SerializedObject(light);
        SerializedProperty drawHaloProp = lightSerialized.FindProperty("m_DrawHalo");
        drawHaloProp.boolValue = true;
        lightSerialized.ApplyModifiedProperties();
        lights.Add(light);
        lightTypes.Add(LightType.Point);
        Debug.Log($"Added Light {lights.Count}: Type={light.type}, Intensity={light.intensity}, Position={light.transform.position}, CullingMask={light.cullingMask}, RenderMode={light.renderMode}");
    }

    private void RemoveLight()
    {
        if (lights.Count > 0)
        {
            Debug.Log($"Removing Light {lights.Count}: {lights[lights.Count - 1].name}");
            DestroyImmediate(lights[lights.Count - 1].gameObject);
            lights.RemoveAt(lights.Count - 1);
            lightTypes.RemoveAt(lightTypes.Count - 1);
        }
    }

    private void DisableEnvironmentLighting()
    {
        // 環境光を無効化してツールのライトのみを使用
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        Debug.Log("Disabled environment lighting.");
    }

    private void RestoreEnvironmentLighting()
    {
        // 環境光をデフォルト（グレー）に復元
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
        Debug.Log("Restored environment lighting.");
    }

    private AnimationClip CombineAnimationClips(AnimationClip poseClip, AnimationClip expressionClip, string avatarName)
    {
        if (poseClip == null && expressionClip == null)
        {
            Debug.LogWarning($"No animations to combine for {avatarName}.");
            return null;
        }

        AnimationClip combinedClip = new AnimationClip
        {
            name = $"Combined_{avatarName}_{System.DateTime.Now.Ticks}"
        };

        // ポーズ（または動きのある）アニメーションのすべてのカーブをコピー
        if (poseClip != null)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(poseClip))
            {
                var curve = AnimationUtility.GetEditorCurve(poseClip, binding);
                combinedClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                Debug.Log($"Copied pose curve: Path={binding.path}, Type={binding.type}, Property={binding.propertyName} for {avatarName}");
            }

            // ルートモーション設定をコピー
            var poseSettings = AnimationUtility.GetAnimationClipSettings(poseClip);
            var combinedSettings = AnimationUtility.GetAnimationClipSettings(combinedClip);
            combinedSettings.loopTime = poseSettings.loopTime;
            combinedSettings.loopBlendOrientation = poseSettings.loopBlendOrientation;
            combinedSettings.loopBlendPositionY = poseSettings.loopBlendPositionY;
            combinedSettings.loopBlendPositionXZ = poseSettings.loopBlendPositionXZ;
            combinedSettings.keepOriginalOrientation = poseSettings.keepOriginalOrientation;
            combinedSettings.keepOriginalPositionY = poseSettings.keepOriginalPositionY;
            combinedSettings.keepOriginalPositionXZ = poseSettings.keepOriginalPositionXZ;
            AnimationUtility.SetAnimationClipSettings(combinedClip, combinedSettings);
            Debug.Log($"Copied pose animation settings (loop={poseSettings.loopTime}, rootMotion={poseSettings.loopBlendPositionXZ}) for {avatarName}");
        }

        // 表情アニメーションのすべてのカーブをコピー（上書き優先）
        if (expressionClip != null)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(expressionClip))
            {
                var curve = AnimationUtility.GetEditorCurve(expressionClip, binding);
                combinedClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                Debug.Log($"Copied expression curve: Path={binding.path}, Type={binding.type}, Property={binding.propertyName} for {avatarName}");
            }

            // 表情アニメーションのルートモーション設定（通常は不要だが、念のため）
            var exprSettings = AnimationUtility.GetAnimationClipSettings(expressionClip);
            var combinedSettings = AnimationUtility.GetAnimationClipSettings(combinedClip);
            combinedSettings.loopTime |= exprSettings.loopTime; // ポーズと表情のループ設定を統合
            AnimationUtility.SetAnimationClipSettings(combinedClip, combinedSettings);
        }

        // アニメーションの長さを調整（最長のクリップに合わせる）
        float length = 0f;
        if (poseClip != null) length = Mathf.Max(length, poseClip.length);
        if (expressionClip != null) length = Mathf.Max(length, expressionClip.length);
        combinedClip.frameRate = (int)frameRateOption; // アニメーションのフレームレートを同期

        Debug.Log($"Created combined animation clip for {avatarName} with length {length}s, FrameRate={(int)frameRateOption}");
        return combinedClip;
    }

    private void PlayAnimation()
    {
        EditorApplication.update -= ForceAnimationUpdate;

        foreach (var setting in avatarSettings.Where(s => s.avatar != null))
        {
            var avatar = setting.avatar;
            var animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError($"Avatar {avatar.name} has no Animator component.");
                continue;
            }

            // 既存のコントローラーを破棄
            if (animator.runtimeAnimatorController != null)
            {
                DestroyImmediate(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = null;
                Debug.Log($"Destroyed existing AnimatorController for {avatar.name}.");
            }

            // アニメーションクリップを合成
            var combinedClip = CombineAnimationClips(setting.poseAnimation, setting.expressionAnimation, avatar.name);
            if (combinedClip == null)
            {
                Debug.LogWarning($"No valid animation clips to play for {avatar.name}.");
                continue;
            }

            // 新しいコントローラーを生成
            var controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            var baseLayer = controller.layers[0];
            baseLayer.defaultWeight = 1f;

            var stateMachine = baseLayer.stateMachine;
            var state = stateMachine.AddState(combinedClip.name);
            state.motion = combinedClip;
            stateMachine.defaultState = state;

            animator.runtimeAnimatorController = controller;
            Debug.Log($"Created new AnimatorController for {avatar.name} with combined clip: {combinedClip.name}.");

            // ルートモーションを有効化
            animator.applyRootMotion = true;
            Debug.Log($"Enabled root motion for {avatar.name}.");

            // Animator の状態をリセットして再生
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0); // 初期化を安定化
            animator.Play(state.nameHash, 0);
            Debug.Log($"Playing combined animation: {combinedClip.name} on {avatar.name} (Layer 0)");
        }

        EditorApplication.update += ForceAnimationUpdate;
    }

    private void ForceAnimationUpdate()
    {
        foreach (var setting in avatarSettings.Where(s => s.avatar != null))
        {
            var animator = setting.avatar.GetComponent<Animator>();
            if (animator != null && animator.enabled)
            {
                animator.Update(Time.deltaTime);
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

        bool canvasWasActive = backgroundCanvas.gameObject.activeSelf;
        backgroundCanvas.gameObject.SetActive(true);

        if (transparent)
        {
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = Color.clear;
            if (backgroundImageComponent != null) backgroundImageComponent.enabled = false;
            foreach (var sticker in stickerRawImages) sticker.enabled = false;
        }

        int width = GetResolutionWidth();
        int height = GetResolutionHeight();
        RenderTexture rt = new RenderTexture(width, height, 24);
        previewCamera.targetTexture = rt;
        previewCamera.Render();
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
            if (backgroundImageComponent != null) backgroundImageComponent.enabled = true;
            foreach (var sticker in stickerRawImages) sticker.enabled = true;
        }

        backgroundCanvas.gameObject.SetActive(canvasWasActive);
        Debug.Log($"Captured image to: {path}");
    }

    private void StartRecording()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Recording is only available in Play mode.");
            return;
        }

        if (string.IsNullOrEmpty(saveFolderPath))
        {
            Debug.LogError("Save folder path is not specified.");
            return;
        }

        // 新しいレコーダーをセットアップ
        SetupRecorder();

        string folder = Path.Combine(saveFolderPath, "Videos");
        Directory.CreateDirectory(folder);
        string fileName = $"Video_{System.DateTime.Now:yyyyMMddHHmmss}";
        string path = Path.Combine(folder, fileName);

        var movieRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieRecorder.name = "AvatarCaptureRecorder";
        movieRecorder.Enabled = true;
        movieRecorder.OutputFile = path;
        movieRecorder.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = GetResolutionWidth(),
            OutputHeight = GetResolutionHeight()
        };

        var settings = recorderController.Settings;
        settings.AddRecorderSettings(movieRecorder);
        settings.FrameRate = (int)frameRateOption;

        try
        {
            recorderController.PrepareRecording();
            recorderController.StartRecording();
            isVideoRecording = true;
            Debug.Log($"Started recording to: {path} at {GetResolutionWidth()}x{GetResolutionHeight()}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start recording: {e.Message}");
        }

        if (backgroundCanvas != null) backgroundCanvas.gameObject.SetActive(true);
    }

    private void StopRecording()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Recording is only available in Play mode.");
            return;
        }

        try
        {
            recorderController.StopRecording();
            isVideoRecording = false;
            Debug.Log($"Stopped recording. File saved to: {saveFolderPath}/Videos");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to stop recording: {e.Message}");
        }

        // レコーダーをリセット
        SetupRecorder();
        AssetDatabase.Refresh();
        if (backgroundCanvas != null) backgroundCanvas.gameObject.SetActive(true);
    }

    private void Cleanup()
    {
        if (recorderController != null && recorderController.IsRecording())
        {
            recorderController.StopRecording();
            Debug.Log("Stopped recording during cleanup.");
        }

        if (previewCamera != null)
        {
            if (previewCamera.targetTexture != null)
            {
                RenderTexture rt = previewCamera.targetTexture;
                previewCamera.targetTexture = null;
                DestroyImmediate(rt);
            }
            DestroyImmediate(previewCamera.gameObject);
        }
        if (backgroundCanvas != null) DestroyImmediate(backgroundCanvas.gameObject);
        if (previewTexture != null) DestroyImmediate(previewTexture);
        foreach (var light in lights) if (light != null) DestroyImmediate(light.gameObject);
        foreach (var sticker in stickerRawImages) if (sticker != null) DestroyImmediate(sticker.gameObject);
        lights.Clear();
        lightTypes.Clear();
        stickerRawImages.Clear();
        EditorApplication.update -= ForceAnimationUpdate;
        recorderController = null;

        // アバターのコントローラーもクリア
        foreach (var setting in avatarSettings.Where(s => s.avatar != null))
        {
            var animator = setting.avatar.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                DestroyImmediate(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = null;
            }
        }
        Debug.Log("Cleaned up all resources.");
    }
}
#endif