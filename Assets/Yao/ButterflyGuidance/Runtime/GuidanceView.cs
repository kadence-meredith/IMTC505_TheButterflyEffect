using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ButterflyEffect.Guidance
{
    /// <summary>Reusable dialogue canvas. Its content comes entirely from the director/library.</summary>
    public sealed class GuidanceView : MonoBehaviour
    {
        public GuidanceDirector director;
        public Font headingFont;
        public Font bodyFont;
        public bool showDemoHeadings = false;
        public Material uiMaterial;
        public int choiceFontSize = 16;
        public bool useNumberKeys = true;
        public bool InteractionLocked { get; private set; }
        [SerializeField] private Text title;
        [SerializeField] private Text speaker;
        [SerializeField] private Text dialogue;
        [SerializeField] private RectTransform choices;
        [SerializeField] private Canvas canvas;
        private readonly List<Button> activeButtons = new List<Button>();
        private readonly Color ink = new Color(.032f, .082f, .105f, 1);
        private readonly Color cream = new Color(.94f, .95f, .88f, 1);
        private readonly Color cyan = new Color(.38f, .88f, .86f, 1);
        private GuidanceLine displayed;
        private int optionCount;

        private void Awake()
        {
            if (canvas == null) Build();
            if (director == null) director = GetComponent<GuidanceDirector>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("Guidance EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            if (title != null) title.gameObject.SetActive(showDemoHeadings);
        }

        private void OnEnable()
        {
            if (director == null) director = GetComponent<GuidanceDirector>();
            if (director == null) return;
            director.LineChanged += Present;
            if (director.Current != null) Present(director.Current);
        }

        private void OnDisable()
        {
            if (director != null) director.LineChanged -= Present;
        }

        private void Start()
        {
            if (director != null && director.Current == null) director.Begin();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame && optionCount == 1) Select(0);
                if (!useNumberKeys) return;
                if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
                if (keyboard.digit4Key.wasPressedThisFrame) Select(3);
                if (keyboard.digit5Key.wasPressedThisFrame) Select(4);
                if (keyboard.digit6Key.wasPressedThisFrame) Select(5);
                if (keyboard.digit7Key.wasPressedThisFrame) Select(6);
                if (keyboard.digit8Key.wasPressedThisFrame) Select(7);
            }
#endif
        }

        private void Select(int index)
        {
            if (InteractionLocked || index < 0 || index >= optionCount || director == null) return;
            if (displayed.choices != null && displayed.choices.Length > 0) director.Choose(index);
            else director.Advance();
        }

        public void Present(GuidanceLine line)
        {
            if (line == null || dialogue == null) return;
            displayed = line;
            if (InteractionLocked) return;
            dialogue.text = line.text;
            speaker.text = string.IsNullOrEmpty(line.speaker) ? "Butterfly" : line.speaker;
            bool forest = director != null && director.Location == "forest";
            bool opening = line.id == "campus.opening";
            if (title != null) title.text = forest ? "Camp" : "Campus";
            ClearChoices();
            int count = line.choices == null ? 0 : line.choices.Length;
            optionCount = count > 0 ? count : string.IsNullOrEmpty(line.nextId) ? 0 : 1;
            int columns = optionCount <= 2 ? Math.Max(1, optionCount) : Math.Min(4, optionCount);
            int rows = Mathf.CeilToInt(optionCount / (float)columns);
            for (int i = 0; i < optionCount; i++)
            {
                int index = i;
                string label = count > 0 ? line.choices[i].label : "Continue";
                if (opening && label == "Continue") label = "Begin";
                float x0 = i % columns / (float)columns;
                float x1 = (i % columns + 1) / (float)columns;
                float y0 = 1 - (i / columns + 1) / (float)Math.Max(1, rows);
                float y1 = 1 - (i / columns) / (float)Math.Max(1, rows);
                var button = MakeButton(choices, "Choice " + (i + 1), label,
                    new Vector2(x0, y0), new Vector2(x1, y1), () => Select(index), i == 0);
                var rect = (RectTransform)button.transform;
                rect.offsetMin = new Vector2(i % columns == 0 ? 0 : 6, rows > 1 ? 5 : 16);
                rect.offsetMax = new Vector2(i % columns == columns - 1 ? 0 : -6, rows > 1 ? -5 : -16);
                if (optionCount == 1)
                {
                    rect.anchorMin = new Vector2(.72f, 0);
                    rect.anchorMax = Vector2.one;
                }
                activeButtons.Add(button);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowTravelPrompt(string message)
        {
            InteractionLocked = true;
            ClearChoices();
            optionCount = 0;
            if (dialogue != null) dialogue.text = message;
            if (speaker != null) speaker.text = "Butterfly";
        }

        public void ResumeDialogue()
        {
            InteractionLocked = false;
            var line = director != null ? director.Current : displayed;
            if (line != null) Present(line);
        }

        private void ClearChoices()
        {
            foreach (var button in activeButtons)
            {
                button.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(button.gameObject);
                else DestroyImmediate(button.gameObject);
            }
            activeButtons.Clear();
        }

        /// <summary>Used by the editor builder to save an editable Canvas into the prefab.</summary>
        public void Build()
        {
            if (canvas != null) return;
            if (bodyFont == null) bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (headingFont == null) headingFont = bodyFont;
            var canvasObject = new GameObject("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;
            if (showDemoHeadings)
                title = MakeText(root, "Location title", "Campus", 40, cream, new Vector2(.05f, .87f), new Vector2(.45f, .95f), headingFont);
            var card = MakeRect(root, "Butterfly dialogue card", new Vector2(.045f, .065f), new Vector2(.955f, .385f));
            MakeImage(card, "Card surface", new Color(.027f, .073f, .092f, .97f), Vector2.zero, Vector2.one);
            MakeImage(card, "Cyan accent", cyan, new Vector2(0, .99f), Vector2.one);
            speaker = MakeText(card, "Speaker", "Butterfly", 14, cyan, new Vector2(.035f, .8f), new Vector2(.965f, .94f));
            dialogue = MakeText(card, "Dialogue", "", 27, cream, new Vector2(.035f, .43f), new Vector2(.965f, .79f));
            dialogue.alignment = TextAnchor.UpperLeft;
            dialogue.lineSpacing = 1.08f;
            dialogue.resizeTextForBestFit = true;
            dialogue.resizeTextMinSize = 20;
            dialogue.resizeTextMaxSize = 27;
            choices = MakeRect(card, "Choices", new Vector2(.035f, .055f), new Vector2(.965f, .43f));
        }

        private RectTransform MakeRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Image MakeImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var rect = MakeRect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            if (uiMaterial != null) image.material = uiMaterial;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text MakeText(Transform parent, string name, string value, int size, Color color, Vector2 min, Vector2 max, Font font = null)
        {
            var rect = MakeRect(parent, name, min, max);
            var text = rect.gameObject.AddComponent<Text>();
            if (uiMaterial != null) text.material = uiMaterial;
            text.font = font == null ? bodyFont : font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Action action, bool primary, int size = 16)
        {
            var rect = MakeRect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            if (uiMaterial != null) image.material = uiMaterial;
            image.color = primary ? cyan : new Color(.09f, .19f, .22f, 1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = primary ? new Color(.8f, 1, .93f) : new Color(.67f, .92f, .9f);
            colors.pressedColor = new Color(.57f, .76f, .74f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = MakeText(rect, "Label", label, choiceFontSize, primary ? ink : cream, new Vector2(.035f, 0), new Vector2(.965f, 1));
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Min(16, choiceFontSize);
            text.resizeTextMaxSize = choiceFontSize;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.35f, .6f, .6f, .23f);
            outline.effectDistance = new Vector2(1, -1);
            return button;
        }
    }
}
