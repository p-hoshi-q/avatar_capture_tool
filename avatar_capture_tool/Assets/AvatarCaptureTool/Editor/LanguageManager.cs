#if UNITY_EDITOR
using System.Collections.Generic;

public static class LanguageManager
{
    public enum Language { Japanese, English }

    private static readonly Dictionary<Language, Dictionary<string, string>> translations = new Dictionary<Language, Dictionary<string, string>>
    {
        {
            Language.Japanese, new Dictionary<string, string>
            {
                { "TargetAvatars", "対象アバター" },
                { "Avatar", "アバター" },
                { "AddAvatar", "アバターを追加" },
                { "RemoveLastAvatar", "最後のアバターを削除" },
                { "BackgroundSettings", "背景設定" },
                { "BackgroundImage", "背景画像" },
                { "StickerImage", "ステッカー画像" },
                { "FitMode", "フィットモード" },
                { "LightSettings", "ライト設定" },
                { "LightColor", "ライト {0} 色" },
                { "LightIntensity", "ライト {0} 強度" },
                { "AddLight", "ライトを追加" },
                { "RemoveLastLight", "最後のライトを削除" },
                { "CameraSettings", "カメラ設定" },
                { "CameraPosition", "カメラ位置" },
                { "CameraRotation", "カメラ回転" },
                { "AnimationSettings", "アニメーション設定" },
                { "PoseAnimation", "ポーズアニメーション" },
                { "ExpressionAnimation", "表情アニメーション" },
                { "PlayAnimation", "アニメーション再生" },
                { "FilterSettings", "フィルター設定" },
                { "TextureFilter", "テクスチャフィルター" },
                { "VideoSettings", "動画設定" },
                { "FollowCamera", "カメラ追従" },
                { "FollowOffset", "追従オフセット" },
                { "StartRecording", "録画開始" },
                { "StopRecording", "録画停止" },
                { "SaveSettings", "保存設定" },
                { "SaveFolder", "保存フォルダ" },
                { "CaptureImage", "画像キャプチャ" },
                { "CaptureImageTransparent", "画像キャプチャ（透過）" },
                { "StickerPosition", "ステッカー {0} 位置" },
                { "StickerScale", "ステッカー {0} スケール" },
                { "AddSticker", "ステッカーを追加" },
                { "RemoveLastSticker", "最後のステッカーを削除" }
            }
        },
        {
            Language.English, new Dictionary<string, string>
            {
                { "TargetAvatars", "Target Avatars" },
                { "Avatar", "Avatar" },
                { "AddAvatar", "Add Avatar" },
                { "RemoveLastAvatar", "Remove Last Avatar" },
                { "BackgroundSettings", "Background Settings" },
                { "BackgroundImage", "Background Image" },
                { "StickerImage", "Sticker Image" },
                { "FitMode", "Fit Mode" },
                { "LightSettings", "Light Settings" },
                { "LightColor", "Light {0} Color" },
                { "LightIntensity", "Light {0} Intensity" },
                { "AddLight", "Add Light" },
                { "RemoveLastLight", "Remove Last Light" },
                { "CameraSettings", "Camera Settings" },
                { "CameraPosition", "Camera Position" },
                { "CameraRotation", "Camera Rotation" },
                { "AnimationSettings", "Animation Settings" },
                { "PoseAnimation", "Pose Animation" },
                { "ExpressionAnimation", "Expression Animation" },
                { "PlayAnimation", "Play Animation" },
                { "FilterSettings", "Filter Settings" },
                { "TextureFilter", "Texture Filter" },
                { "VideoSettings", "Video Settings" },
                { "FollowCamera", "Follow Camera" },
                { "FollowOffset", "Follow Offset" },
                { "StartRecording", "Start Recording" },
                { "StopRecording", "Stop Recording" },
                { "SaveSettings", "Save Settings" },
                { "SaveFolder", "Save Folder" },
                { "CaptureImage", "Capture Image" },
                { "CaptureImageTransparent", "Capture Image (Transparent)" },
                { "StickerPosition", "Sticker {0} Position" },
                { "StickerScale", "Sticker {0} Scale" },
                { "AddSticker", "Add Sticker" },
                { "RemoveLastSticker", "Remove Last Sticker" }
            }
        }
    };

    public static string GetText(string key, Language language)
    {
        if (translations.TryGetValue(language, out var dict) && dict.TryGetValue(key, out var text))
        {
            return text;
        }
        return key; // フォールバック
    }
}
#endif