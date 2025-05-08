#if UNITY_EDITOR
using UnityEngine;

public static class FilterManager
{
    public static void ApplyFilter(Texture2D texture, FilterMode filterMode)
    {
        texture.filterMode = filterMode;
        texture.Apply();
    }
}
#endif