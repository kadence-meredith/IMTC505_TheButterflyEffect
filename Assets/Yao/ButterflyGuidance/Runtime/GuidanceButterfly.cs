using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ButterflyEffect.Guidance
{
    /// <summary>Offsets the visual child without moving the guide's navigation root.</summary>
    public sealed class GuidanceButterfly : MonoBehaviour
    {
        public bool floatGently = true;
        private Vector3 origin;
        private Quaternion orientation;
        private readonly List<Material> adaptedMaterials = new List<Material>();

        private void Start()
        {
            origin = transform.localPosition;
            orientation = transform.localRotation;
            AdaptMaterialForScene();
            foreach (var animation in GetComponentsInChildren<Animation>(true))
            {
                animation.wrapMode = WrapMode.Loop;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                if (animation.clip != null) animation.Play(animation.clip.name);
            }
        }

        private void AdaptMaterialForScene()
        {
            var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null || !pipeline.GetType().Name.Contains("Universal")) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.shader == shader) continue;
                    var texture = original.mainTexture;
                    var material = new Material(shader) { name = original.name + " (scene material)" };
                    material.SetTexture("_BaseMap", texture);
                    material.SetColor("_BaseColor", original.color);
                    bool cutout = original.IsKeywordEnabled("_ALPHATEST_ON");
                    material.SetFloat("_AlphaClip", cutout ? 1 : 0);
                    material.SetFloat("_Cutoff", .5f);
                    material.SetFloat("_Cull", cutout ? 0 : 2);
                    material.SetFloat("_Surface", 0);
                    material.SetFloat("_Metallic", 0);
                    material.SetFloat("_Smoothness", .08f);
                    if (cutout) material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)(cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
                    materials[i] = material;
                    adaptedMaterials.Add(material);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            foreach (var material in adaptedMaterials)
                if (material != null) Destroy(material);
        }

        private void Update()
        {
            if (!floatGently) return;
            transform.localPosition = origin + new Vector3(Mathf.Sin(Time.time * .6f) * .045f, Mathf.Sin(Time.time * 1.2f) * .065f, 0);
            transform.localRotation = orientation * Quaternion.Euler(0, Mathf.Sin(Time.time * .5f) * 5f, Mathf.Sin(Time.time * .8f) * 2f);
        }
    }
}
