using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RuneRush.Editor
{
    public static class RuneRushApplicationIcon
    {
        public const string IconPath = "Assets/_RuneRush/Art/AppIcon/RuneRushIcon.png";
        private const string BackgroundPath = "Assets/_RuneRush/Art/AppIcon/AdaptiveBackground.png";

        [MenuItem("Rune Rush/PICO/Apply Game Application Icon")]
        public static void Configure()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) throw new InvalidOperationException("The Rune Rush game icon is missing: " + IconPath);
            if (!File.Exists(BackgroundPath))
            {
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.08f, .10f, .16f, 1f);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(BackgroundPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(BackgroundPath);
            }
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (PlatformIcon slot in icons)
                {
                    if (kind.ToString().StartsWith("Adaptive", StringComparison.Ordinal))
                        slot.SetTextures(new[] { background, icon });
                    else slot.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RUNE_RUSH_APPLICATION_ICON_OK: " + IconPath + "; default/legacy/round/adaptive configured");
        }
    }
}
