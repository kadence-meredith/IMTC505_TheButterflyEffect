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
        public bool IsExplorationMode { get; private set; }
        public bool ContextVisible { get; private set; }
        public bool TransientPromptVisible => IsExplorationMode && promptRemaining > 0f;
        [SerializeField] private Text title;
        [SerializeField] private Text speaker;
        [SerializeField] private Text dialogue;
        [SerializeField] private RectTransform choices;
        [SerializeField] private Canvas canvas;
        private readonly List<Button> activeButtons = new List<Button>();
        private readonly List<int> choiceIndices = new List<int>();
        private readonly Color ink = new Color(.032f, .082f, .105f, 1);
        private readonly Color cream = new Color(.94f, .95f, .88f, 1);
        private readonly Color cyan = new Color(.38f, .88f, .86f, 1);
        private GuidanceLine displayed;
        private int optionCount;
        private float promptRemaining;
        private string transientPrompt;
        private bool entryPromptShown;
        private bool savedLayout;
        private RectTransform card;
        private RectLayout cardLayout, dialogueLayout, choicesLayout;
        private bool speakerWasActive;
        private int originalFontSize, originalMinSize, originalMaxSize;
        private TextAnchor originalAlignment;

        private struct RectLayout
        {
            public RectTransform rect;
            public Vector2 min, max, offsetMin, offsetMax;

            public RectLayout(RectTransform value)
            {
                rect = value;
                min = value.anchorMin;
                max = value.anchorMax;
                offsetMin = value.offsetMin;
                offsetMax = value.offsetMax;
            }

            public void Restore()
            {
                if (rect == null) return;
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

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
            if (!IsExplorationMode && director != null && director.Current == null) director.Begin();
        }

        private void Update()
        {
            AdvancePrompt(Time.unscaledDeltaTime);
            if (IsExplorationMode) ApplyExplorationLayout(TransientPromptVisible);
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
            if (IsExplorationMode && (!ContextVisible || TransientPromptVisible)) return;
            if (displayed != null && displayed.choices != null && displayed.choices.Length > 0)
            {
                if (index < choiceIndices.Count) director.Choose(choiceIndices[index]);
            }
            else director.Advance();
        }

        public void Present(GuidanceLine line)
        {
            if (line == null || dialogue == null) return;
            displayed = line;
            if (IsExplorationMode)
            {
                PresentExploration(line);
                return;
            }
            if (InteractionLocked) return;
            dialogue.text = line.text;
            speaker.text = string.IsNullOrEmpty(line.speaker) ? "Butterfly" : line.speaker;
            bool forest = director != null && director.Location == "forest";
            bool opening = line.id == "campus.opening";
            if (title != null) title.text = forest ? "Camp" : "Campus";
            ClearChoices();
            int count = line.choices == null ? 0 : line.choices.Length;
            for (int i = 0; i < count; i++) choiceIndices.Add(i);
            optionCount = count > 0 ? count : string.IsNullOrEmpty(line.nextId) ? 0 : 1;
            RenderChoices(line, opening);
        }

        private void RenderChoices(GuidanceLine line, bool opening)
        {
            bool hasChoices = line.choices != null && line.choices.Length > 0;
            int columns = optionCount <= 2 ? Math.Max(1, optionCount) : Math.Min(4, optionCount);
            int rows = Mathf.CeilToInt(optionCount / (float)columns);
            for (int i = 0; i < optionCount; i++)
            {
                int index = i;
                string label = hasChoices ? line.choices[choiceIndices[i]].label : "Continue";
                if (opening && label == "Continue") label = "Begin";
                float x0 = i % columns / (float)columns;
                float x1 = (i % columns + 1) / (float)columns;
                float y0 = 1 - (i / columns + 1) / (float)Math.Max(1, rows);
                float y1 = 1 - (i / columns) / (float)Math.Max(1, rows);
                var button = MakeButton(choices, "Choice " + (i + 1), label,
                    new Vector2(x0, y0), new Vector2(x1, y1), () => Select(index), i == 0);
                var rect = (RectTransform)button.transform;
                float inset = IsExplorationMode ? 4 : rows > 1 ? 5 : 16;
                rect.offsetMin = new Vector2(i % columns == 0 ? 0 : 6, inset);
                rect.offsetMax = new Vector2(i % columns == columns - 1 ? 0 : -6, -inset);
                if (optionCount == 1 && !IsExplorationMode)
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
            if (IsExplorationMode)
            {
                InteractionLocked = false;
                return;
            }
            InteractionLocked = true;
            ClearChoices();
            optionCount = 0;
            if (dialogue != null) dialogue.text = message;
            if (speaker != null) speaker.text = "Butterfly";
        }

        public void ResumeDialogue()
        {
            InteractionLocked = false;
            var line = director != null && director.Current != null ? director.Current : displayed;
            if (line != null) Present(line);
        }

        public void SetExplorationMode(bool enabled)
        {
            if (IsExplorationMode == enabled)
            {
                if (enabled) ApplyExplorationLayout(TransientPromptVisible);
                return;
            }
            SaveLayout();
            IsExplorationMode = enabled;
            ContextVisible = false;
            InteractionLocked = false;
            promptRemaining = 0f;
            entryPromptShown = false;
            ClearChoices();
            optionCount = 0;
            if (enabled)
            {
                if (title != null) title.gameObject.SetActive(false);
                if (speaker != null) speaker.gameObject.SetActive(false);
                ApplyExplorationLayout(false);
                SetCardVisible(false);
            }
            else
            {
                cardLayout.Restore();
                dialogueLayout.Restore();
                choicesLayout.Restore();
                if (speaker != null) speaker.gameObject.SetActive(speakerWasActive);
                if (title != null) title.gameObject.SetActive(showDemoHeadings);
                if (dialogue != null)
                {
                    dialogue.fontSize = originalFontSize;
                    dialogue.resizeTextMinSize = originalMinSize;
                    dialogue.resizeTextMaxSize = originalMaxSize;
                    dialogue.alignment = originalAlignment;
                }
                SetCardVisible(true);
                ResumeDialogue();
            }
        }

        public void SetExplorationContext(bool visible)
        {
            if (ContextVisible == visible) return;
            ContextVisible = visible;
            if (!IsExplorationMode) return;
            var line = director != null && director.Current != null ? director.Current : displayed;
            if (line != null) Present(line);
            else if (!TransientPromptVisible) SetCardVisible(false);
        }

        public void ShowTransientPrompt(string message, float duration)
        {
            if (!IsExplorationMode || entryPromptShown || string.IsNullOrEmpty(message)
                || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f) return;
            entryPromptShown = true;
            transientPrompt = message;
            promptRemaining = duration;
            InteractionLocked = false;
            RenderTransientPrompt();
        }

        public void AdvancePrompt(float deltaTime)
        {
            if (!TransientPromptVisible || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            promptRemaining = Mathf.Max(0f, promptRemaining - deltaTime);
            if (promptRemaining > 0f) return;
            var line = director != null && director.Current != null ? director.Current : displayed;
            if (line != null) Present(line);
            else SetCardVisible(false);
        }

        private void PresentExploration(GuidanceLine line)
        {
            InteractionLocked = false;
            if (TransientPromptVisible)
            {
                RenderTransientPrompt();
                return;
            }
            ClearChoices();
            optionCount = 0;
            if (!ContextVisible || !IsObjectLine(line.id))
            {
                SetCardVisible(false);
                return;
            }
            if (line.choices != null)
            {
                for (int i = 0; i < line.choices.Length; i++)
                    if (IsObjectAction(line.choices[i])) choiceIndices.Add(i);
            }
            optionCount = choiceIndices.Count;
            dialogue.text = CompactText(line);
            ApplyExplorationLayout(false);
            SetCardVisible(!string.IsNullOrEmpty(dialogue.text) || optionCount > 0);
            RenderChoices(line, false);
        }

        private static bool IsObjectLine(string id)
        {
            return !string.IsNullOrEmpty(id) && (id.StartsWith("cigarette.", StringComparison.Ordinal)
                || id.StartsWith("campfire.", StringComparison.Ordinal)
                || id.StartsWith("bucket.", StringComparison.Ordinal)
                || id.StartsWith("fireworks.", StringComparison.Ordinal));
        }

        private static bool IsObjectAction(GuidanceChoice choice)
        {
            if (choice == null || string.IsNullOrEmpty(choice.action)) return false;
            if (choice.action == "bucket.pour" || choice.action == "bucket.stir-pour") return false;
            return choice.action.StartsWith("cigarette.", StringComparison.Ordinal)
                || choice.action.StartsWith("marshmallow.", StringComparison.Ordinal)
                || choice.action.StartsWith("bucket.", StringComparison.Ordinal)
                || choice.action.StartsWith("fireworks.", StringComparison.Ordinal);
        }

        private string CompactText(GuidanceLine line)
        {
            switch (line.id)
            {
                case "cigarette.hand": return "Your hand is full.";
                case "cigarette.ground": return "The cigarette is on the ground.";
                case "cigarette.picked": return "The cigarette is in your hand.";
                case "cigarette.bin": return "Cigarette disposal bin.";
                case "cigarette.bin-empty": return "Pick up the cigarette first.";
                case "cigarette.disposed": return "The cigarette is safely disposed.";
                case "campfire.approach": return "Campfire & marshmallows.";
                case "campfire.roasting": return RoastingText();
                case "campfire.eaten": return string.Empty;
                case "campfire.cold": return "The campfire is cold.";
                case "bucket.ready": return "Water bucket.";
                case "bucket.held": return string.Empty;
                case "bucket.embers": return "The embers are still hot.";
                case "bucket.cold": return "The embers are cold.";
                case "fireworks.approach": case "fireworks.again": return "Fireworks.";
                case "fireworks.watch": return "Watch the fireworks.";
                default: return line.text;
            }
        }

        private string RoastingText()
        {
            var bridge = GetComponent<GuidanceSceneBridge>();
            var item = bridge != null && bridge.setup != null ? bridge.setup.marshmallow : null;
            if (item == null || !item.IsHeld || float.IsNaN(item.cookedSeconds)) return string.Empty;
            if (item.cookedSeconds >= item.burntSeconds) return "It's burnt.";
            if (item.cookedSeconds >= item.goldenSeconds) return "It's ready. Would you like a bite?";
            return string.Empty;
        }

        private void RenderTransientPrompt()
        {
            ClearChoices();
            optionCount = 0;
            if (dialogue != null) dialogue.text = transientPrompt;
            ApplyExplorationLayout(true);
            SetCardVisible(true);
        }

        private void SaveLayout()
        {
            if (savedLayout) return;
            if (canvas == null) Build();
            if (dialogue == null || choices == null) return;
            card = dialogue.transform.parent as RectTransform;
            if (card == null) return;
            cardLayout = new RectLayout(card);
            dialogueLayout = new RectLayout(dialogue.rectTransform);
            choicesLayout = new RectLayout(choices);
            speakerWasActive = speaker != null && speaker.gameObject.activeSelf;
            originalFontSize = dialogue.fontSize;
            originalMinSize = dialogue.resizeTextMinSize;
            originalMaxSize = dialogue.resizeTextMaxSize;
            originalAlignment = dialogue.alignment;
            savedLayout = true;
        }

        private void ApplyExplorationLayout(bool prompt)
        {
            SaveLayout();
            if (card == null || dialogue == null || choices == null) return;
            bool world = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
            bool actionsOnly = !prompt && string.IsNullOrEmpty(dialogue.text) && optionCount > 0;
            SetRect(card,
                world ? new Vector2(prompt ? .25f : .18f, prompt ? .34f : .02f) : new Vector2(prompt ? .3f : .21f, .06f),
                world ? new Vector2(prompt ? .75f : .82f, prompt ? .61f : actionsOnly ? .38f : .7f)
                    : new Vector2(prompt ? .7f : .79f, prompt ? .13f : actionsOnly ? .16f : .24f));
            SetRect(dialogue.rectTransform, new Vector2(.05f, prompt ? .08f : .55f), new Vector2(.95f, .94f));
            SetRect(choices, new Vector2(.05f, .08f), new Vector2(.95f, actionsOnly ? .92f : .53f));
            dialogue.fontSize = 30;
            dialogue.resizeTextMinSize = 22;
            dialogue.resizeTextMaxSize = 30;
            dialogue.alignment = prompt ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (speaker != null) speaker.gameObject.SetActive(false);
            if (title != null) title.gameObject.SetActive(false);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void SetCardVisible(bool visible)
        {
            if (card == null) SaveLayout();
            if (card != null) card.gameObject.SetActive(visible);
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
            choiceIndices.Clear();
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
