using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RoR2;
using UnityEngine;
using Path = System.IO.Path;

namespace Deathwing.Modules
{
    /// <summary>
    /// Builds the real Deathwing model at runtime from a converted mesh/skeleton/animation payload
    /// (see tools/convert.py), instead of an AssetBundle. A bundle has to be produced by the Unity
    /// editor and pinned to the exact editor version the game shipped with; everything here -
    /// meshes, skinning, textures and legacy animation clips - can be created from plain data by the
    /// running game, which keeps the mod a single file and independent of any editor.
    ///
    /// The payload is looked for next to the plugin so it can be updated without a rebuild, and
    /// falls back to a copy embedded in the assembly. When neither is present the survivor keeps the
    /// placeholder chassis model, so a distribution without the art still works.
    /// </summary>
    internal static class DeathwingModel
    {
        internal const string payloadFileName = "deathwing.dwm";
        private const string embeddedPayloadName = "Deathwing.deathwing.dwm";
        private const uint payloadMagic = 0x314D5744; // "DWM1"

        private static bool loaded;
        private static Payload payload;
        private static Material material;

        /// <summary>True once the art payload has been found and decoded.</summary>
        internal static bool available => Load() != null;

        /// <summary>
        /// Instantiates the model: a bone hierarchy, one skinned renderer per geoset and an
        /// <see cref="Animation"/> carrying every clip, driven by <see cref="DeathwingAnimator"/>.
        /// </summary>
        internal static GameObject Build(string name)
        {
            Payload data = Load();
            if (data == null)
            {
                return null;
            }

            GameObject root = new GameObject(name);
            root.transform.localScale = Vector3.one * Tuning.realModelScale.Value;
            root.layer = LayerIndex.defaultLayer.intVal;

            Transform[] bones = BuildSkeleton(data, root.transform);
            Matrix4x4[] bindPoses = BindPoses(bones, root.transform);
            Material shared = ResolveMaterial(data);

            foreach (MeshData meshData in data.meshes)
            {
                GameObject rendererObject = new GameObject(meshData.name);
                rendererObject.transform.SetParent(root.transform, false);

                SkinnedMeshRenderer renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = BuildMesh(meshData, bindPoses);
                renderer.bones = bones;
                renderer.rootBone = bones.Length > 0 ? bones[0] : root.transform;
                renderer.sharedMaterial = shared;
                // The mesh deforms far outside its bind-pose bounds (wings, tail), and a dragon that
                // pops out of existence when its bind bounds leave the frustum reads as a bug.
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rendererObject.layer = root.layer;
            }

            Animation animation = root.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (KeyValuePair<string, AnimationClip> clip in BuildClips(data, bones))
            {
                animation.AddClip(clip.Value, clip.Key);
            }

            root.AddComponent<DeathwingAnimator>();
            return root;
        }

        /// <summary>The jaw bone, which Molten Breath is emitted from.</summary>
        internal static Transform FindMouth(Transform root)
        {
            if (!root)
            {
                return null;
            }

            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == DeathwingClips.jawBoneName)
                {
                    return bone;
                }
            }

            return null;
        }

        private static Transform[] BuildSkeleton(Payload data, Transform root)
        {
            Transform[] bones = new Transform[data.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                BoneData bone = data.bones[i];
                GameObject boneObject = new GameObject(bone.name);
                Transform parent = bone.parent >= 0 ? bones[bone.parent] : root;
                boneObject.transform.SetParent(parent, false);
                boneObject.transform.localPosition = bone.localPosition;
                boneObject.transform.localRotation = Quaternion.identity;
                bones[i] = boneObject.transform;
            }

            return bones;
        }

        /// <summary>
        /// The rest pose is the bind pose: every bone sits on its pivot unrotated, and the mesh is
        /// authored in that same space, so each bind matrix is simply the bone's inverse rest
        /// transform relative to the model root.
        /// </summary>
        private static Matrix4x4[] BindPoses(Transform[] bones, Transform root)
        {
            Matrix4x4[] poses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                poses[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }

            return poses;
        }

        private static Mesh BuildMesh(MeshData data, Matrix4x4[] bindPoses)
        {
            Mesh mesh = new Mesh
            {
                name = data.name,
                // Assets built at load time are reachable only through the body prefab; without this
                // the scene change into a stage can unload them, which empties the renderer.
                hideFlags = HideFlags.DontUnloadUnusedAsset,
                vertices = data.vertices,
                normals = data.normals,
                uv = data.uv,
                boneWeights = data.boneWeights,
                bindposes = bindPoses
            };

            mesh.SetTriangles(data.triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The model's own diffuse and additive emission maps, drawn with the game's own character
        /// shader where it can be found, so Deathwing lights, dithers and takes overlays like any
        /// other survivor. A plain lit fallback keeps him visible if the shader lookup ever fails.
        /// </summary>
        private static Material ResolveMaterial(Payload data)
        {
            if (material)
            {
                return material;
            }

            Texture2D diffuse = data.textures.TryGetValue("diffuse", out Texture2D d) ? d : null;
            Texture2D emissive = data.textures.TryGetValue("emissive", out Texture2D e) ? e : null;

            Material template = ChassisMaterial();
            if (template)
            {
                material = UnityEngine.Object.Instantiate(template);
                material.name = "matDeathwing";
                material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                material.SetTexture("_MainTex", diffuse);
                material.SetColor("_Color", Color.white);
                if (material.HasProperty("_EmTex"))
                {
                    material.SetTexture("_EmTex", emissive);
                    material.SetColor("_EmColor", new Color(1f, 0.45f, 0.12f));
                    material.SetFloat("_EmPower", Tuning.modelEmission.Value);
                }

                if (material.HasProperty("_NormalTex"))
                {
                    material.SetTexture("_NormalTex", null);
                    material.SetFloat("_NormalStrength", 0f);
                }
            }
            else
            {
                material = new Material(Shader.Find("Standard"))
                {
                    name = "matDeathwing",
                    hideFlags = HideFlags.DontUnloadUnusedAsset
                };
                material.mainTexture = diffuse;
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", emissive);
                    material.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.12f));
                }
            }

            return material;
        }

        /// <summary>A lit character material from the chassis, used purely as a source of its shader.</summary>
        private static Material ChassisMaterial()
        {
            GameObject commandoBody = DeathwingAssets.Load<GameObject>(DeathwingAssets.commandoBodyKey);
            if (!commandoBody)
            {
                return null;
            }

            CharacterModel characterModel = commandoBody.GetComponentInChildren<CharacterModel>(true);
            if (!characterModel || characterModel.baseRendererInfos == null)
            {
                return null;
            }

            foreach (CharacterModel.RendererInfo info in characterModel.baseRendererInfos)
            {
                if (info.defaultMaterial && info.defaultMaterial.HasProperty("_MainTex"))
                {
                    return info.defaultMaterial;
                }
            }

            return null;
        }

        private static Dictionary<string, AnimationClip> BuildClips(Payload data, Transform[] bones)
        {
            string[] paths = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                paths[i] = BonePath(data, i);
            }

            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            foreach (ClipData clipData in data.clips)
            {
                AnimationClip clip = new AnimationClip
                {
                    name = clipData.name,
                    // Legacy clips are the only kind that can be authored at runtime; the
                    // AnimatorController a modern clip needs cannot be built outside the editor.
                    legacy = true,
                    wrapMode = WrapMode.Loop,
                    hideFlags = HideFlags.DontUnloadUnusedAsset
                };

                foreach (TrackData track in clipData.tracks)
                {
                    string path = paths[track.bone];
                    SetCurves(clip, path, "localPosition", track.position, 3);
                    SetCurves(clip, path, "localRotation", track.rotation, 4);
                    SetCurves(clip, path, "localScale", track.scale, 3);
                }

                clip.EnsureQuaternionContinuity();
                clips[clipData.name] = clip;
            }

            return clips;
        }

        private static readonly string[] components = { ".x", ".y", ".z", ".w" };

        private static void SetCurves(AnimationClip clip, string path, string property, Keys keys, int count)
        {
            if (keys == null || keys.times.Length == 0)
            {
                return;
            }

            for (int component = 0; component < count; component++)
            {
                Keyframe[] frames = new Keyframe[keys.times.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i] = new Keyframe(keys.times[i], keys.values[i * count + component]);
                }

                // Linear tangents: the source keys were authored for linear interpolation, and
                // Unity's default (flat) tangents would ease in and out of every one of them.
                for (int i = 0; i < frames.Length - 1; i++)
                {
                    float span = frames[i + 1].time - frames[i].time;
                    float slope = span > 0f ? (frames[i + 1].value - frames[i].value) / span : 0f;
                    frames[i].outTangent = slope;
                    frames[i + 1].inTangent = slope;
                }

                clip.SetCurve(path, typeof(Transform), property + components[component], new AnimationCurve(frames));
            }
        }

        private static string BonePath(Payload data, int index)
        {
            string path = data.bones[index].name;
            for (int parent = data.bones[index].parent; parent >= 0; parent = data.bones[parent].parent)
            {
                path = data.bones[parent].name + "/" + path;
            }

            return path;
        }

        private static Payload Load()
        {
            if (loaded)
            {
                return payload;
            }

            loaded = true;

            byte[] bytes = ReadPayloadBytes();
            if (bytes == null)
            {
                Log.Info($"No '{payloadFileName}' found beside the plugin or embedded; keeping the placeholder model.");
                return null;
            }

            try
            {
                payload = Decode(bytes);
                Log.Info($"Deathwing model loaded: {payload.bones.Length} bones, {payload.meshes.Count} meshes, "
                    + $"{payload.clips.Count} animations.");
            }
            catch (Exception exception)
            {
                Log.Error($"Could not decode '{payloadFileName}': {exception}");
                payload = null;
            }

            return payload;
        }

        private static byte[] ReadPayloadBytes()
        {
            try
            {
                string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(directory))
                {
                    string path = Path.Combine(directory, payloadFileName);
                    if (File.Exists(path))
                    {
                        Log.Info($"Reading the Deathwing model from '{path}'.");
                        return File.ReadAllBytes(path);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not read '{payloadFileName}' from disk: {exception.Message}");
            }

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPayloadName))
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

        private static Payload Decode(byte[] bytes)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (reader.ReadUInt32() != payloadMagic)
                {
                    throw new InvalidDataException("not a DWM1 payload");
                }

                reader.ReadUInt32(); // format revision, unused while there is only one

                Payload data = new Payload();

                int boneCount = (int)reader.ReadUInt32();
                data.bones = new BoneData[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    data.bones[i] = new BoneData
                    {
                        name = ReadString(reader),
                        parent = reader.ReadInt32(),
                        localPosition = ReadVector3(reader)
                    };
                }

                int textureCount = (int)reader.ReadUInt32();
                for (int i = 0; i < textureCount; i++)
                {
                    string name = ReadString(reader);
                    TextureFormat format = reader.ReadUInt32() == 1 ? TextureFormat.DXT1 : TextureFormat.DXT5;
                    int width = (int)reader.ReadUInt32();
                    int height = (int)reader.ReadUInt32();
                    int mips = (int)reader.ReadUInt32();
                    byte[] pixels = reader.ReadBytes((int)reader.ReadUInt32());

                    // The blocks ship in the compressed layout the GPU wants, mip chain included, so
                    // they can be handed over without a decode step.
                    Texture2D texture = new Texture2D(width, height, format, mips > 1)
                    {
                        name = "texDeathwing_" + name,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.DontUnloadUnusedAsset
                    };
                    texture.LoadRawTextureData(pixels);
                    texture.Apply(false, true);
                    data.textures[name] = texture;
                }

                int meshCount = (int)reader.ReadUInt32();
                for (int i = 0; i < meshCount; i++)
                {
                    data.meshes.Add(ReadMesh(reader));
                }

                int clipCount = (int)reader.ReadUInt32();
                for (int i = 0; i < clipCount; i++)
                {
                    data.clips.Add(ReadClip(reader));
                }

                return data;
            }
        }

        private static MeshData ReadMesh(BinaryReader reader)
        {
            MeshData mesh = new MeshData { name = ReadString(reader) };
            int count = (int)reader.ReadUInt32();

            mesh.vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                mesh.vertices[i] = ReadVector3(reader);
            }

            mesh.normals = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                mesh.normals[i] = ReadVector3(reader);
            }

            mesh.uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                mesh.uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }

            int[] boneIndices = new int[count * 4];
            for (int i = 0; i < boneIndices.Length; i++)
            {
                boneIndices[i] = reader.ReadUInt16();
            }

            mesh.boneWeights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                mesh.boneWeights[i] = new BoneWeight
                {
                    boneIndex0 = boneIndices[i * 4],
                    boneIndex1 = boneIndices[i * 4 + 1],
                    boneIndex2 = boneIndices[i * 4 + 2],
                    boneIndex3 = boneIndices[i * 4 + 3],
                    weight0 = reader.ReadSingle(),
                    weight1 = reader.ReadSingle(),
                    weight2 = reader.ReadSingle(),
                    weight3 = reader.ReadSingle()
                };
            }

            int indexCount = (int)reader.ReadUInt32();
            mesh.triangles = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
            {
                mesh.triangles[i] = reader.ReadUInt16();
            }

            return mesh;
        }

        private static ClipData ReadClip(BinaryReader reader)
        {
            ClipData clip = new ClipData
            {
                name = ReadString(reader),
                duration = reader.ReadSingle()
            };

            int trackCount = (int)reader.ReadUInt32();
            for (int i = 0; i < trackCount; i++)
            {
                clip.tracks.Add(new TrackData
                {
                    bone = (int)reader.ReadUInt32(),
                    position = ReadKeys(reader, 3),
                    rotation = ReadKeys(reader, 4),
                    scale = ReadKeys(reader, 3)
                });
            }

            return clip;
        }

        private static Keys ReadKeys(BinaryReader reader, int components)
        {
            int count = (int)reader.ReadUInt32();
            Keys keys = new Keys
            {
                times = new float[count],
                values = new float[count * components]
            };

            for (int i = 0; i < count; i++)
            {
                keys.times[i] = reader.ReadSingle();
                for (int component = 0; component < components; component++)
                {
                    keys.values[i * components + component] = reader.ReadSingle();
                }
            }

            return keys;
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static string ReadString(BinaryReader reader)
        {
            return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
        }

        private class Payload
        {
            internal BoneData[] bones;
            internal readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
            internal readonly List<MeshData> meshes = new List<MeshData>();
            internal readonly List<ClipData> clips = new List<ClipData>();
        }

        private struct BoneData
        {
            internal string name;
            internal int parent;
            internal Vector3 localPosition;
        }

        private class MeshData
        {
            internal string name;
            internal Vector3[] vertices;
            internal Vector3[] normals;
            internal Vector2[] uv;
            internal BoneWeight[] boneWeights;
            internal int[] triangles;
        }

        private class ClipData
        {
            internal string name;
            internal float duration;
            internal readonly List<TrackData> tracks = new List<TrackData>();
        }

        private class TrackData
        {
            internal int bone;
            internal Keys position;
            internal Keys rotation;
            internal Keys scale;
        }

        private class Keys
        {
            internal float[] times;
            internal float[] values;
        }
    }
}
