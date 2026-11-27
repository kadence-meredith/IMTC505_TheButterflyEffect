using System;
using System.Collections.Generic;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [Serializable]
    public sealed class GuidanceSubtitleCue
    {
        [TextArea(1, 4)] public string text;
        [Tooltip("Seconds after this paragraph's audio begins. -1 uses proportional text timing.")]
        [Min(-1f)] public float startSeconds = -1f;
    }

    [Serializable]
    public sealed class GuidanceNarrationSegment
    {
        [TextArea(2, 8)] public string text;
        public AudioClip audio;
        public GuidanceSubtitleCue[] subtitleCues = Array.Empty<GuidanceSubtitleCue>();

        public GuidanceSubtitleCue[] GetSubtitleCues()
        {
            if (subtitleCues != null && subtitleCues.Length > 0) return subtitleCues;
            return CreateSubtitleCues(text);
        }

        public static GuidanceSubtitleCue[] CreateSubtitleCues(string text, int maxCharacters = 110)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<GuidanceSubtitleCue>();
            maxCharacters = Mathf.Max(20, maxCharacters);
            var result = new List<GuidanceSubtitleCue>();
            int cursor = 0;
            while (cursor < text.Length)
            {
                int limit = Mathf.Min(text.Length, cursor + maxCharacters);
                int end = limit;
                if (limit < text.Length)
                {
                    int wordBreak = -1;
                    int sentenceBreak = -1;
                    for (int i = cursor; i < limit; i++)
                    {
                        if (char.IsWhiteSpace(text[i])) wordBreak = i + 1;
                        bool sentenceEnd = text[i] == '.' || text[i] == '!' || text[i] == '?';
                        if (sentenceEnd && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])
                            && i + 1 - cursor >= maxCharacters * .45f)
                        {
                            int boundary = i + 1;
                            while (boundary < limit && char.IsWhiteSpace(text[boundary])) boundary++;
                            sentenceBreak = boundary;
                        }
                    }
                    if (sentenceBreak > cursor) end = sentenceBreak;
                    else if (wordBreak > cursor) end = wordBreak;
                    else
                    {
                        // Keep an unusually long word intact rather than cutting it between pages.
                        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
                        while (end < text.Length && char.IsWhiteSpace(text[end])) end++;
                    }
                }
                result.Add(new GuidanceSubtitleCue
                {
                    text = text.Substring(cursor, end - cursor),
                    startSeconds = result.Count == 0 ? 0f : -1f
                });
                cursor = end;
            }
            return result.ToArray();
        }
    }

    [Serializable]
    public sealed class GuidanceNarrationGroup
    {
        public string id;
        public GuidanceNarrationSegment[] paragraphs = Array.Empty<GuidanceNarrationSegment>();
    }

    [CreateAssetMenu(fileName = "ButterflyNarration", menuName = "Butterfly Effect/Narration Library")]
    public sealed class GuidanceNarrationLibrary : ScriptableObject
    {
        public GuidanceNarrationGroup[] groups = Array.Empty<GuidanceNarrationGroup>();

        public GuidanceNarrationGroup Find(string id)
        {
            if (groups == null || string.IsNullOrEmpty(id)) return null;
            foreach (var group in groups)
                if (group != null && string.Equals(group.id, id, StringComparison.Ordinal)) return group;
            return null;
        }
    }
}
