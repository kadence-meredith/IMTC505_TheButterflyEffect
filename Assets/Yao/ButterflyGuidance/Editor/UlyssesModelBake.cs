using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>
    /// Converts the downloaded glTF import into a self-contained Unity prefab.
    /// The temporary glTF importer is needed only while running this editor command.
    /// </summary>
    public static class UlyssesModelBake
    {
        private const string SourcePath = "Assets/_UlyssesImport/Ulysses.glb";
        private const string ArtRoot = "Assets/Yao/ButterflyGuidance/Art/Ulysses";
        private const string TexturePath = ArtRoot + "/Textures/Ulysses_BaseColor.png";
        public const string PrefabPath = ArtRoot + "/UlyssesButterfly.prefab";

        [MenuItem("Butterfly Effect/Bake Ulysses Butterfly")]
        public static void Bake()
        {
            Directory.CreateDirectory(ArtRoot + "/Meshes");
            Directory.CreateDirectory(ArtRoot + "/Animations");
            Directory.CreateDirectory(ArtRoot + "/Materials");
            AssetDatabase.Refresh();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("Import the original Ulysses GLB at " + SourcePath + " before baking.");

            var texture = PrepareTexture();
            bool builtInPipeline = GraphicsSettings.currentRenderPipeline == null;
            var material = MakeMaterial(texture, builtInPipeline);
            var importedClips = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            if (importedClips.Length == 0)
                throw new InvalidOperationException("The original Ulysses import has no animation clips; a static replacement is not accepted.");

            var nativeClips = new List<AnimationClip>();
            for (int i = 0; i < importedClips.Length; i++)
            {
                var imported = importedClips[i];
                if (AnimationUtility.GetCurveBindings(imported).Length == 0)
                    throw new InvalidOperationException("Ulysses animation has no editable transform curves: " + imported.name);
                var clip = Object.Instantiate(imported);
                clip.name = "Ulysses_" + SafeName(imported.name);
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                nativeClips.Add(StoreNative(clip, ArtRoot + "/Animations/" + i.ToString("00") + "_" + clip.name + ".anim"));
            }

            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (instance == null) instance = Object.Instantiate(source);
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // Keep every imported transform, including the armature and the root's
                // initial orientation/scale. Only replace asset references and playback.
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("The Ulysses import has no renderers.");
                var meshCopies = new Dictionary<Mesh, Mesh>();
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                        filter.sharedMesh = CopyMesh(filter.sharedMesh, meshCopies, builtInPipeline);
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null)
                        throw new InvalidOperationException("Ulysses skinned renderer has no mesh: " + skin.name);
                    skin.sharedMesh = CopyMesh(skin.sharedMesh, meshCopies, builtInPipeline);
                    skin.updateWhenOffscreen = true;
                }
                if (meshCopies.Count == 0)
                    throw new InvalidOperationException("No Ulysses mesh was copied.");

                foreach (var renderer in renderers)
                {
                    int slots = Math.Max(1, renderer.sharedMaterials.Length);
                    renderer.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
                    var meshRenderer = renderer as MeshRenderer;
                    if (meshRenderer != null && meshRenderer.additionalVertexStreams != null)
                        meshRenderer.additionalVertexStreams = CopyMesh(meshRenderer.additionalVertexStreams, meshCopies, builtInPipeline);
                }

                var animationRoots = instance.GetComponentsInChildren<Animator>(true)
                    .Select(a => a.gameObject).Distinct().ToArray();
                if (animationRoots.Length == 0) animationRoots = new[] { instance };

                // Imported MonoBehaviours may retain glTF data. This prefab only needs
                // native render/transform/legacy-animation components to play its wings.
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    if (component is Transform || component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer)
                        continue;
                    Object.DestroyImmediate(component);
                }
                foreach (var animationRoot in animationRoots)
                {
                    var animation = animationRoot.AddComponent<Animation>();
                    foreach (var clip in nativeClips) animation.AddClip(clip, clip.name);
                    animation.clip = nativeClips[0];
                    animation.playAutomatically = true;
                    animation.wrapMode = WrapMode.Loop;
                    animation.cullingType = AnimationCullingType.AlwaysAnimate;
                }

                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Unity could not save " + PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

                var dependencies = AssetDatabase.GetDependencies(PrefabPath, true);
                foreach (var dependency in dependencies)
                {
                    if (dependency.StartsWith("Assets/_UlyssesImport/", StringComparison.OrdinalIgnoreCase)
                        || dependency.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
                        || dependency.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)
                        || dependency.IndexOf("gltfast", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("Baked Ulysses still depends on temporary importer data: " + dependency);
                    if (dependency.StartsWith("Assets/", StringComparison.Ordinal) && !dependency.StartsWith(ArtRoot + "/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Baked Ulysses has an unexpected external asset dependency: " + dependency);
                }

                Debug.Log("Ulysses native model baked: " + PrefabPath
                    + "\nRenderers: " + renderers.Length + "; native meshes: " + meshCopies.Count
                    + "; world bounds center: " + bounds.center.ToString("F4") + "; size: " + bounds.size.ToString("F4")
                    + "\nAnimation clips: " + string.Join(", ", nativeClips.Select(c => c.name + " (" + c.length.ToString("F3") + " s)"))
                    + "\nNative asset dependencies:\n" + string.Join("\n", dependencies));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        private static Texture2D PrepareTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Extract the original Ulysses base-color PNG to " + TexturePath + " before baking.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material MakeMaterial(Texture2D texture, bool builtInPipeline)
        {
            string shaderName = builtInPipeline ? "Standard" : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Cannot find native Unity shader: " + shaderName);
            var material = new Material(shader) { name = "Ulysses_OriginalColor", doubleSidedGI = true };
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetFloat("_Cull", 0);
            material.SetFloat("_Cutoff", .5f);
            material.SetFloat("_Metallic", 0);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.EnableKeyword("_ALPHATEST_ON");
            if (builtInPipeline)
            {
                material.SetFloat("_Mode", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1);
                material.SetFloat("_Glossiness", .35f);
                material.SetColor("_Color", Color.white);
                material.SetTexture("_MainTex", texture);
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Surface", 0);
                material.SetFloat("_Smoothness", .35f);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", texture);
            }
            return StoreNative(material, ArtRoot + "/Materials/Ulysses_OriginalColor.mat");
        }

        private static Mesh CopyMesh(Mesh source, IDictionary<Mesh, Mesh> copies, bool doubleSidedGeometry)
        {
            Mesh saved;
            if (copies.TryGetValue(source, out saved)) return saved;
            var mesh = Object.Instantiate(source);
            mesh.name = "Ulysses_" + SafeName(source.name);
            if (doubleSidedGeometry)
            {
                // Built-in Standard hardcodes back-face culling. Reverse-index copies
                // retain all original vertices, weights and bind poses while letting
                // the original thin wing surface remain visible from either side.
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    int[] front = mesh.GetIndices(submesh);
                    var bothSides = new int[front.Length * 2];
                    Array.Copy(front, bothSides, front.Length);
                    for (int index = 0; index < front.Length; index += 3)
                    {
                        bothSides[front.Length + index] = front[index];
                        bothSides[front.Length + index + 1] = front[index + 2];
                        bothSides[front.Length + index + 2] = front[index + 1];
                    }
                    mesh.SetIndices(bothSides, MeshTopology.Triangles, submesh, false);
                }
            }
            saved = StoreNative(mesh, ArtRoot + "/Meshes/" + copies.Count.ToString("00") + "_" + mesh.name + ".asset");
            copies.Add(source, saved);
            return saved;
        }

        private static T StoreNative<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(value, existing);
                Object.DestroyImmediate(value);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Model";
            return new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        }
    }
}
