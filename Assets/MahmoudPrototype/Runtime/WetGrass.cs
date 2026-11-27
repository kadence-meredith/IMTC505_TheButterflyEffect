using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(TerrainCollider)), DisallowMultipleComponent]
    public sealed class WetGrass : MonoBehaviour, IPrototypeResettable
    {
        [SerializeField] private Shader wetGrassShader;

        private const int MaxPatches = 40;
        private const int Segments = 8;
        private const float Reach = 0.78f;
        private const float MergeDistance = 0.22f;
        private const float SaturationVolume = 4f;
        private const float DryAfter = 30f;
        private const float DryDuration = 15f;
        private static readonly int WetAmount = Shader.PropertyToID("_WetAmount");
        private static readonly int Impact = Shader.PropertyToID("_Impact");
        private readonly List<Patch> patches = new List<Patch>();
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * (Segments + 1)];
        private readonly Vector2[] uv = new Vector2[(Segments + 1) * (Segments + 1)];
        private MaterialPropertyBlock properties;
        private TerrainData terrain;
        private Material material;

        private sealed class Patch
        {
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector2 center;
            public float water;
            public float lastWaterTime;
            public Vector2 impactPosition;
        }

        public static void WaterGround(TerrainCollider ground, Vector3 hitPoint, float amount)
        {
            if (!ground.TryGetComponent(out WetGrass grass))
            {
                // A campsite already open during a script update can still have its original hierarchy.
                if (!ground.TryGetComponent<CampsiteGround>(out _)) return;
                grass = ground.gameObject.AddComponent<WetGrass>();
            }
            if (grass.isActiveAndEnabled) grass.AddWater(hitPoint, amount);
        }

        private void InitializeSurface()
        {
            if (terrain == null) terrain = GetComponent<TerrainCollider>().terrainData;
            if (wetGrassShader == null) wetGrassShader = Shader.Find("Butterfly Effect/Wet Grass");
            if (material == null && wetGrassShader != null) material = new Material(wetGrassShader);
            if (properties == null) properties = new MaterialPropertyBlock();
        }

        public void AddWater(Vector3 hitPoint, float amount)
        {
            if (amount <= 0f) return;
            InitializeSurface();
            if (terrain == null || material == null) return;
            Vector3 local = transform.InverseTransformPoint(hitPoint);
            Vector2 center = new Vector2(local.x, local.z);
            // Tree colliders can be part of the terrain as well.
            if (Mathf.Abs(local.y - GroundHeight(center)) > 0.08f) return;

            Patch nearest = null;
            float nearestSquared = MergeDistance * MergeDistance;
            foreach (Patch patch in patches)
            {
                float distance = (center - patch.center).sqrMagnitude;
                if (patch.water > 0f && distance < nearestSquared)
                {
                    nearest = patch;
                    nearestSquared = distance;
                }
            }
            if (nearest == null) nearest = StartPatch(center);
            nearest.water = Mathf.Min(1f, nearest.water + amount / SaturationVolume);
            nearest.lastWaterTime = Time.time;
            nearest.impactPosition = new Vector2(hitPoint.x, hitPoint.z);
            SetAppearance(nearest);
        }

        private Patch StartPatch(Vector2 center)
        {
            Patch slot = null;
            foreach (Patch patch in patches)
            {
                if (patch.water <= 0f) { slot = patch; break; }
                if (slot == null || patch.lastWaterTime < slot.lastWaterTime) slot = patch;
            }
            if (slot == null || (slot.water > 0f && patches.Count < MaxPatches))
            {
                var obj = new GameObject("Wet grass patch");
                obj.transform.SetParent(transform, false);
                slot = new Patch { mesh = new Mesh { name = "Wet grass surface" }, renderer = obj.AddComponent<MeshRenderer>() };
                obj.AddComponent<MeshFilter>().sharedMesh = slot.mesh;
                slot.renderer.sharedMaterial = material;
                slot.renderer.shadowCastingMode = ShadowCastingMode.Off;
                slot.renderer.receiveShadows = false;
                patches.Add(slot);
            }
            slot.center = center;
            slot.water = 0f;
            FitToTerrain(slot);
            slot.renderer.gameObject.SetActive(true);
            return slot;
        }

        private float GroundHeight(Vector2 point)
        {
            return terrain.GetInterpolatedHeight(point.x / terrain.size.x, point.y / terrain.size.z);
        }

        private void FitToTerrain(Patch patch)
        {
            int width = Segments + 1;
            Vector3 size = terrain.size;
            for (int z = 0; z < width; z++)
            for (int x = 0; x < width; x++)
            {
                Vector2 offset = new Vector2(2f * x / Segments - 1f, 2f * z / Segments - 1f) * Reach;
                Vector2 point = patch.center + offset;
                point.x = Mathf.Clamp(point.x, 0f, size.x);
                point.y = Mathf.Clamp(point.y, 0f, size.z);
                int i = z * width + x;
                vertices[i] = new Vector3(point.x, GroundHeight(point) + 0.009f, point.y);
                uv[i] = (point - patch.center) / Reach;
            }
            patch.mesh.vertices = vertices;
            patch.mesh.uv = uv;
            if (patch.mesh.subMeshCount == 0 || patch.mesh.GetIndexCount(0) == 0)
            {
                var indices = new int[Segments * Segments * 6];
                int i = 0;
                for (int z = 0; z < Segments; z++)
                for (int x = 0; x < Segments; x++)
                {
                    int a = z * width + x;
                    indices[i++] = a; indices[i++] = a + width; indices[i++] = a + 1;
                    indices[i++] = a + 1; indices[i++] = a + width; indices[i++] = a + width + 1;
                }
                patch.mesh.triangles = indices;
            }
            patch.mesh.RecalculateBounds();
        }

        private void SetAppearance(Patch patch)
        {
            properties.SetFloat(WetAmount, patch.water);
            properties.SetVector(Impact, new Vector4(patch.impactPosition.x, patch.impactPosition.y, patch.lastWaterTime, 0f));
            patch.renderer.SetPropertyBlock(properties);
        }

        private void Update()
        {
            foreach (Patch patch in patches)
            {
                if (patch.water <= 0f || Time.time - patch.lastWaterTime <= DryAfter) continue;
                patch.water = Mathf.Max(0f, patch.water - Time.deltaTime / DryDuration);
                if (patch.water > 0f) SetAppearance(patch);
                else patch.renderer.gameObject.SetActive(false);
            }
        }

        public void ResetPrototype()
        {
            foreach (Patch patch in patches)
            {
                patch.water = 0f;
                patch.renderer.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            foreach (Patch patch in patches) Destroy(patch.mesh);
            if (material != null) Destroy(material);
        }
    }
}
