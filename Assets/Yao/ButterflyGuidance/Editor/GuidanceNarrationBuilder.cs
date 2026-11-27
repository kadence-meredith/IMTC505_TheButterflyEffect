using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceNarrationBuilder
    {
        public const string LibraryPath = "Assets/Yao/ButterflyGuidance/Data/ButterflyNarration_English.asset";
        public const string AudioFolder = "Assets/Yao/ButterflyGuidance/Art/Narration";

        [MenuItem("Tools/Butterfly Effect/Build Narration Library")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static GuidanceNarrationLibrary Build()
        {
            var library = AssetDatabase.LoadAssetAtPath<GuidanceNarrationLibrary>(LibraryPath);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<GuidanceNarrationLibrary>();
            var previous = new Dictionary<string, GuidanceNarrationGroup>(StringComparer.Ordinal);
            foreach (var group in library.groups ?? Array.Empty<GuidanceNarrationGroup>())
                if (group != null && !string.IsNullOrEmpty(group.id)) previous[group.id] = group;
            var groups = DefaultGroups();
            foreach (var group in groups)
            {
                previous.TryGetValue(group.id, out var old);
                for (int i = 0; i < group.paragraphs.Length; i++)
                {
                    var paragraph = group.paragraphs[i];
                    if (old != null && old.paragraphs != null && i < old.paragraphs.Length
                        && old.paragraphs[i] != null && old.paragraphs[i].text == paragraph.text)
                    {
                        paragraph.audio = old.paragraphs[i].audio;
                        if (old.paragraphs[i].subtitleCues != null && old.paragraphs[i].subtitleCues.Length > 0)
                            paragraph.subtitleCues = old.paragraphs[i].subtitleCues;
                    }
                    if (paragraph.audio == null) paragraph.audio = FindAudio(group.id, i);
                }
            }
            library.name = "Butterfly Narration English";
            library.groups = groups;
            if (created) AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static string AudioPath(string cueGroup, int paragraphIndex)
        {
            return AudioFolder + "/" + cueGroup + "_" + (paragraphIndex + 1).ToString("00") + ".wav";
        }

        private static AudioClip FindAudio(string cueGroup, int paragraphIndex)
        {
            string path = AudioPath(cueGroup, paragraphIndex);
            foreach (string extension in new[] { ".wav", ".mp3", ".ogg" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(System.IO.Path.ChangeExtension(path, extension));
                if (clip != null) return clip;
            }
            return null;
        }

        public static GuidanceNarrationGroup[] DefaultGroups()
        {
            return new[]
            {
                Group("campus.opening",
                    "Hello there! Welcome to the Virtual UBCO Okanagan campus! I'm Betsy the Butterfly, here to guide your experience.",
                    "One perk of attending this school is how close it is to many campsites and provincial parks.",
                    "I'd like to show you one, but first, I want to mention an important button on your controller.",
                    "The Grip button can be used to pick up and drop items.",
                    "There's a table over here where you can practice.",
                    "Whenever you're ready, follow me into this portal to be transported into the Okanagan outdoors!"),
                Group("camp.tent",
                    "Don't be shy! Come on out of that tent!"),
                Group("camp.cigarette",
                    "Oh! I didn't know you smoked! There might be somewhere to put it out when you're done with that cigarette. Or just toss it, I don't care!"),
                Group("camp.campfire",
                    "Anyways, a big part of camping is the campfire, so I started one up for us.",
                    "The best part of campfires is roasting MARSHMALLOWS!! If you'd like, there's a stick and some marshmallows on the table you can use."),
                Group("camp.fireworks",
                    "On special occasions, fireworks and festivities can bring your camping trip to the next level. And being away from the city, this seems like the perfect place to light some up! If you want, you can get close to the box and press your right Trigger button to get them going."),
                Group("camp.return-held",
                    "Alright I'm ready to go back whenever you are. But! My portal doesn't go to a designated smoking area on campus. So you can't bring that cigarette with you."),
                Group("camp.return",
                    "What a fun time! Follow me back to campus whenever you are ready."),
                Group("ending.good",
                    "And we are back! Your actions in the campsite kept the campus safe.",
                    "But it very easily could have been different.",
                    "Did you know that around 40% of BC forest fires are caused by humans? This includes fires from people discarding burning items like cigarettes, not extinguishing campfires properly, and lighting fireworks, all especially during firebans.",
                    "You avoided risks and even helped fix some of the careless decisions I made! Thank you!",
                    "Enter this last portal to see what could have happened to our campus if you were less responsible."),
                Group("ending.bad",
                    "And we are back! But something has changed... GASP! Look around!",
                    "Did you know that around 40% of BC forest fires are caused by humans? This includes fires from people discarding burning items like cigarettes, not extinguishing campfires properly, and lighting fireworks, all especially during firebans.",
                    "We totally did some of that on our camping trip. Thank goodness this is just virtual reality and our developers can clean this up!",
                    "But next time you go camping for real, be responsible. Keep our campus and city safe with each small decision you make.",
                    "Ok, I'm going to leave now, but you can can stay here with the consequences of your actions. Toodles!")
            };
        }

        private static GuidanceNarrationGroup Group(string id, params string[] paragraphs)
        {
            var group = new GuidanceNarrationGroup
            {
                id = id,
                paragraphs = new GuidanceNarrationSegment[paragraphs.Length]
            };
            for (int i = 0; i < paragraphs.Length; i++)
                group.paragraphs[i] = new GuidanceNarrationSegment
                {
                    text = paragraphs[i],
                    subtitleCues = GuidanceNarrationSegment.CreateSubtitleCues(paragraphs[i])
                };
            return group;
        }
    }
}
