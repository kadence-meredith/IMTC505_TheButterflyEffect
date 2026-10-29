using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class WorldLabel : MonoBehaviour
    {
        public float maxWidth = 2.7f;
        public float maxHeight = 0.16f;
        public float visibilityDistance = 5.5f;
        [SerializeField] private bool configured;
        private Renderer textRenderer;
        private int fitFrames;

        private void Awake()
        {
            textRenderer = GetComponent<Renderer>();
            if (!configured) ApplyReadableDefaults();
            fitFrames = 8;
        }

        // Also used by the repair command on scenes and prefab assets made with version 1.
        public void ApplyReadableDefaults()
        {
            TextMesh text = GetComponent<TextMesh>();
            if (text == null) return;
            int newline = text.text.IndexOf('\n');
            if (newline >= 0) text.text = text.text.Substring(0, newline);
            text.fontSize = 64;
            text.characterSize = 0.02f;
            bool heading = text.text.Length > 2 && char.IsDigit(text.text[0]) && text.text.Contains("/");
            maxWidth = heading ? 2.7f : 0.95f;
            maxHeight = heading ? 0.16f : 0.1f;
            visibilityDistance = 5.5f;
            transform.localScale = Vector3.one;
            configured = true;
            textRenderer = GetComponent<Renderer>();
            FitToBounds();
            fitFrames = 8; // TextMesh can update its native bounds a frame after changing the font/text.
        }

        public bool FitToBounds()
        {
            if (textRenderer == null) textRenderer = GetComponent<Renderer>();
            if (textRenderer == null) return false;
            Vector3 size = textRenderer.localBounds.size;
            if (size.x < 0.0001f || size.y < 0.0001f) return false;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float scale = Mathf.Min(maxWidth / (size.x * Mathf.Max(0.001f, Mathf.Abs(parentScale.x))),
                maxHeight / (size.y * Mathf.Max(0.001f, Mathf.Abs(parentScale.y))));
            transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.001f, 100f);
            return true;
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            transform.rotation = camera.transform.rotation;
            if (fitFrames > 0 && FitToBounds()) fitFrames--;
            float distance = Vector3.Distance(transform.position, camera.transform.position);
            if (textRenderer != null) textRenderer.enabled = distance <= visibilityDistance && distance >= 0.65f;
        }
    }
}
