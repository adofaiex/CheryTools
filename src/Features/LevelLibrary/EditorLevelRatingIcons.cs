using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CheryTools
{
    internal static class EditorLevelRatingIcons
    {
        private const string DefaultIconName = "ADOFAI";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite Get(EditorLevelLibraryEntry entry)
        {
            string iconName = GetIconName(entry);
            Sprite sprite = Load(iconName);
            return sprite != null ? sprite : Load(DefaultIconName);
        }

        private static string GetIconName(EditorLevelLibraryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.RatingCategory)) return DefaultIconName;
            string suffix = entry.RatingCategory == "U" && entry.RatingIsJ ? "J" : string.Empty;
            return entry.RatingCategory + entry.RatingLevel.ToString() + suffix;
        }

        private static Sprite Load(string iconName)
        {
            if (Sprites.TryGetValue(iconName, out Sprite cached))
            {
                if (cached != null && cached.texture != null) return cached;
                Sprites.Remove(iconName);
            }

            string directory = Main.ModEntry != null
                ? Path.Combine(Main.ModEntry.Path, "Resources", "Levelicon")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Levelicon");
            string path = FindIconPath(directory, iconName);
            if (string.IsNullOrEmpty(path)) return null;

            Texture2D texture = TextureManager.GetOrCreateTexture2D(path, 64f, 64f);
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "CheryTools_LevelRating_" + iconName;
            Sprites[iconName] = sprite;
            return sprite;
        }

        private static string FindIconPath(string directory, string iconName)
        {
            string png = Path.Combine(directory, iconName + ".png");
            if (File.Exists(png)) return png;
            string jpg = Path.Combine(directory, iconName + ".jpg");
            if (File.Exists(jpg)) return jpg;
            string jpeg = Path.Combine(directory, iconName + ".jpeg");
            return File.Exists(jpeg) ? jpeg : string.Empty;
        }
    }
}
