using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Deathwing's own ability icons, loaded from PNGs embedded in the assembly (or dropped into an
    /// <c>Icons</c> folder beside the plugin, so they can be swapped without a rebuild). Every lookup
    /// falls back to the chassis' icon, which is why a build without the art still shows readable
    /// skill slots rather than blanks.
    /// </summary>
    internal static class DeathwingIcons
    {
        internal const string moltenFlame = "MoltenFlame";
        internal const string lavaBurst = "LavaBurst";
        internal const string onslaught = "Onslaught";
        internal const string incinerate = "Incinerate";
        internal const string dragonflight = "Dragonflight";
        internal const string cataclysm = "Cataclysm";
        internal const string earthShatter = "EarthShatter";
        internal const string destroyer = "Destroyer";
        internal const string worldBreaker = "WorldBreaker";
        internal const string aspectOfDeath = "AspectofDeath";
        internal const string formSwitch = "FormSwitch";

        private const string resourcePrefix = "Deathwing.Icons.";
        private const string folderName = "Icons";

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>The named icon, or <paramref name="fallback"/> when its art is not installed.</summary>
        internal static Sprite Get(string name, Sprite fallback = null)
        {
            if (sprites.TryGetValue(name, out Sprite cached))
            {
                return cached ? cached : fallback;
            }

            Sprite sprite = Load(name);
            sprites[name] = sprite;
            if (!sprite)
            {
                Log.Warning($"Icon '{name}' is not installed; using the chassis' icon for that slot.");
            }

            return sprite ? sprite : fallback;
        }

        /// <summary>The named icon as a texture, for the places the game wants one (the body portrait).</summary>
        internal static Texture GetTexture(string name, Texture fallback = null)
        {
            Sprite sprite = Get(name);
            return sprite ? sprite.texture : fallback;
        }

        private static Sprite Load(string name)
        {
            byte[] png = ReadIcon(name);
            if (png == null)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "texDeathwingIcon" + name,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!texture.LoadImage(png))
            {
                Log.Warning($"Icon '{name}' could not be decoded.");
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            sprite.name = "spriteDeathwingIcon" + name;
            return sprite;
        }

        private static byte[] ReadIcon(string name)
        {
            try
            {
                string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(directory))
                {
                    string path = Path.Combine(Path.Combine(directory, folderName), name + ".png");
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not read the icon '{name}' from disk: {exception.Message}");
            }

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePrefix + name + ".png"))
            {
                if (stream == null)
                {
                    return null;
                }

                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int chunk = stream.Read(bytes, read, bytes.Length - read);
                    if (chunk <= 0)
                    {
                        break;
                    }

                    read += chunk;
                }

                return bytes;
            }
        }
    }
}
