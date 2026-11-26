using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    public static class LowPolyButterflyBuilder
    {
        public const string ArtRoot = "Assets/Yao/ButterflyGuidance/Art/LowPoly";
        // Keeping this prefab's GUID and root IDs preserves the nested references in the group scenes.
        public const string PrefabPath = UlyssesModelBake.PrefabPath;
        public const float DisplaySpan = .8f;

        private static readonly Color[] Palette =
        {
            new Color(.29f, .29f, .25f), new Color(.36f, .55f, .59f),
            new Color(.52f, .66f, .65f), new Color(.28f, .48f, .51f),
            new Color(.84f, .75f, .51f), new Color(.66f, .51f, .31f),
            new Color(.44f, .38f, .30f), new Color(.67f, .59f, .45f),
            new Color(.24f, .25f, .22f), new Color(.44f, .56f, .54f),
            new Color(.74f, .65f, .47f), new Color(.43f, .59f, .59f)
        };

        [MenuItem("Butterfly Effect/Build Low-Poly Butterfly")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtRoot);
            AssetDatabase.Refresh();
            var material = MakeMaterial();
            var rightMesh = Store(WingMesh(1f), ArtRoot + "/RightWing.asset");
            var leftMesh = Store(WingMesh(-1f), ArtRoot + "/LeftWing.asset");
            var bodyMesh = Store(BodyMesh(), ArtRoot + "/Body.asset");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Low-poly Butterfly");
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                foreach (var component in root.GetComponents<Component>())
                    if (!(component is Transform)) Object.DestroyImmediate(component);

                root.name = "Low-poly Butterfly";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var wings = root.AddComponent<LowPolyButterflyWings>();
                wings.leftWing = AddMesh(root.transform, "Left wing", leftMesh, material);
                wings.rightWing = AddMesh(root.transform, "Right wing", rightMesh, material);
                AddMesh(root.transform, "Body and antennae", bodyMesh, material);
                wings.Pose(0f);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save the low-poly butterfly prefab.");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            Debug.Log("Low-poly butterfly saved with native flat-shaded meshes and a matte palette: " + PrefabPath);
        }

        public static int NormalizeInstances(GameObject root, float span = DisplaySpan)
        {
            int count = 0;
            foreach (var wings in root.GetComponentsInChildren<LowPolyButterflyWings>(true))
            {
                NormalizeModel(wings, span);
                count++;
            }
            return count;
        }

        public static void NormalizeModel(LowPolyButterflyWings wings, float span = DisplaySpan)
        {
            if (wings == null) return;
            wings.name = "Low-poly Butterfly";
            var model = wings.transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            model.localScale = Vector3.one * (span / .81f);
            EditorUtility.SetDirty(model);
            EditorUtility.SetDirty(wings.gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(model))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wings.gameObject);
            }
        }

        private static Transform AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go.transform;
        }

        private static Material MakeMaterial()
        {
            var atlas = new Texture2D(Palette.Length, 1, TextureFormat.RGBA32, false)
            {
                name = "Butterfly earth and blue palette", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
            };
            atlas.SetPixels(Palette);
            atlas.Apply();
            atlas = Store(atlas, ArtRoot + "/Palette.asset");
            var pipeline = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            bool universal = pipeline != null && pipeline.GetType().Name.Contains("Universal");
            var shader = Shader.Find(universal ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) throw new InvalidOperationException("Cannot find the scene's lit shader for the butterfly.");
            var material = new Material(shader) { name = "Butterfly matte palette", enableInstancing = true };
            material.mainTexture = atlas;
            material.color = Color.white;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .08f);
            return Store(material, ArtRoot + "/ButterflyMatte.mat");
        }

        private static Mesh WingMesh(float side)
        {
            var geometry = new Geometry();
            AddLobe(geometry, new[]
            {
                new Vector2(.012f, .07f), new Vector2(.055f, .195f), new Vector2(.165f, .275f),
                new Vector2(.295f, .315f), new Vector2(.405f, .255f), new Vector2(.365f, .10f),
                new Vector2(.275f, -.025f), new Vector2(.12f, -.075f), new Vector2(.012f, -.035f)
            }, new Vector2(.19f, .10f), side, false);
            AddLobe(geometry, new[]
            {
                new Vector2(.013f, -.025f), new Vector2(.14f, -.04f), new Vector2(.26f, -.09f),
                new Vector2(.295f, -.18f), new Vector2(.26f, -.255f), new Vector2(.17f, -.305f),
                new Vector2(.095f, -.285f), new Vector2(.04f, -.21f), new Vector2(.013f, -.12f)
            }, new Vector2(.14f, -.16f), side, true);
            return geometry.Mesh(side < 0f ? "Left faceted wing" : "Right faceted wing");
        }

        private static void AddLobe(Geometry geometry, Vector2[] outline, Vector2 center, float side, bool hind)
        {
            var top = Point(center, .024f, side);
            var bottom = Point(center, -.009f, side);
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                var a = Point(outline[i], .002f, side);
                var b = Point(outline[next], .002f, side);
                var innerA = Point(Vector2.Lerp(outline[i], center, .12f), .01f, side);
                var innerB = Point(Vector2.Lerp(outline[next], center, .12f), .01f, side);
                int facet = hind ? (i % 3 == 0 ? 5 : i % 2 == 0 ? 11 : 3) : i % 3 == 0 ? 2 : i % 3 == 1 ? 1 : 3;
                geometry.HorizontalTriangle(top, innerA, innerB, facet, true);
                geometry.HorizontalTriangle(a, b, innerB, 0, true);
                geometry.HorizontalTriangle(a, innerB, innerA, 0, true);
                var undersideA = a - Vector3.up * .008f;
                var undersideB = b - Vector3.up * .008f;
                geometry.HorizontalTriangle(bottom, undersideA, undersideB, i % 3 == 0 ? 10 : 9, false);
                if (side > 0f)
                {
                    geometry.Triangle(a, undersideA, undersideB, 0);
                    geometry.Triangle(a, undersideB, b, 0);
                }
                else
                {
                    geometry.Triangle(a, undersideB, undersideA, 0);
                    geometry.Triangle(a, b, undersideB, 0);
                }

                // Small sand-coloured facets stay within the wing surface, above the flat triangle.
                if ((hind && i >= 3 && i <= 6) || (!hind && (i == 2 || i == 3)))
                {
                    var p = Vector3.Lerp(top, Vector3.Lerp(innerA, innerB, .28f), .86f);
                    var q = Vector3.Lerp(top, Vector3.Lerp(innerA, innerB, .70f), .86f);
                    var r = Vector3.Lerp(top, Vector3.Lerp(innerA, innerB, .50f), .65f);
                    geometry.HorizontalTriangle(p + Vector3.up * .0006f, q + Vector3.up * .0006f,
                        r + Vector3.up * .0006f, hind ? 4 : 10, true);
                }
            }
        }

        private static Vector3 Point(Vector2 point, float height, float side)
        {
            return new Vector3(point.x * side, height, point.y);
        }

        private static Mesh BodyMesh()
        {
            var geometry = new Geometry();
            AddDiamond(geometry, new Vector3(0f, .006f, -.09f), new Vector3(.025f, .028f, .13f), 6, 7);
            AddDiamond(geometry, new Vector3(0f, .011f, .055f), new Vector3(.038f, .035f, .078f), 6, 7);
            AddDiamond(geometry, new Vector3(0f, .017f, .15f), new Vector3(.030f, .030f, .031f), 6, 7);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                AddDiamond(geometry, new Vector3(sign * .024f, .026f, .159f), Vector3.one * .008f, 8, 8);
                var a = new Vector3(sign * .014f, .034f, .173f);
                var b = new Vector3(sign * .052f, .054f, .238f);
                var c = new Vector3(sign * .075f, .048f, .255f);
                AddStem(geometry, a, b, .0036f);
                AddStem(geometry, b, c, .0028f);
                AddDiamond(geometry, c, new Vector3(.006f, .006f, .008f), 5, 7);
            }
            return geometry.Mesh("Faceted body and antennae");
        }

        private static void AddDiamond(Geometry geometry, Vector3 center, Vector3 size, int dark, int light)
        {
            var front = center + Vector3.forward * size.z;
            var back = center - Vector3.forward * size.z;
            const int sides = 6;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                var p = center + new Vector3(Mathf.Cos(a) * size.x, Mathf.Sin(a) * size.y, 0f);
                var q = center + new Vector3(Mathf.Cos(b) * size.x, Mathf.Sin(b) * size.y, 0f);
                geometry.Triangle(front, p, q, i < 3 ? light : dark);
                geometry.Triangle(back, q, p, i < 3 ? light : dark);
            }
        }

        private static void AddStem(Geometry geometry, Vector3 start, Vector3 end, float radius)
        {
            var direction = (end - start).normalized;
            var u = Vector3.Cross(direction, Vector3.up).normalized * radius;
            var v = Vector3.Cross(direction, u).normalized * radius;
            var corners = new[] { u, v, -u, -v };
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                geometry.Triangle(start + corners[i], end + corners[next], end + corners[i], 6);
                geometry.Triangle(start + corners[i], start + corners[next], end + corners[next], 6);
            }
        }

        private static T Store<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) AssetDatabase.CreateAsset(value, path);
            else
            {
                EditorUtility.CopySerialized(value, existing);
                Object.DestroyImmediate(value);
                EditorUtility.SetDirty(existing);
                value = existing;
            }
            return value;
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Vector2> uv = new List<Vector2>();

            public void HorizontalTriangle(Vector3 a, Vector3 b, Vector3 c, int color, bool up)
            {
                if ((Vector3.Cross(b - a, c - a).y > 0f) != up)
                    Triangle(a, c, b, color);
                else Triangle(a, b, c, color);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, int color)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                var swatch = new Vector2((color + .5f) / Palette.Length, .5f);
                uv.Add(swatch); uv.Add(swatch); uv.Add(swatch);
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
