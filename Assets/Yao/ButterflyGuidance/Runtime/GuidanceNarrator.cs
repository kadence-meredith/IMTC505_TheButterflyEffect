using System;
using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [DisallowMultipleComponent]
    public sealed class GuidanceNarrator : MonoBehaviour
    {
        public GuidanceNarrationLibrary library;
        public GuidanceView view;
        public AudioSource source;
        [Min(.1f)] public float minSubtitleSeconds = 2.5f;
        [Min(.1f)] public float subtitleCharactersPerSecond = 18f;
        [Min(0f)] public float audioStartupGraceSeconds = 1f;
        public bool IsPlaying { get; private set; }
        public bool AwaitingContinue { get; private set; }
        public bool TimedTextActive { get; private set; }
        public float SubtitleTimeRemaining { get; private set; }
        public string CurrentCueId => current != null ? current.id : string.Empty;
        public int CurrentSegmentIndex { get; private set; } = -1;
        public int CurrentSubtitlePage { get; private set; } = -1;
        public int SubtitlePageCount => subtitlePages.Length;
        public bool IntroductionReady { get; private set; }
        public bool AwaitingBegin => IntroductionReady;
        public string LastSubtitleText { get; private set; } = string.Empty;

        private GuidanceNarrationGroup current;
        private Action finished;
        private bool audioStarted;
        private float audioStartupElapsed;
        private int playbackVersion;
        private bool ownsPanel;
        private GuidanceSubtitleCue[] subtitlePages = Array.Empty<GuidanceSubtitleCue>();
        private bool introduction;
        private Action introductionReady;
        private Action introductionBegin;
        private Func<bool> canBeginIntroduction;
        private bool beginningIntroduction;

        private void Awake()
        {
            Configure();
        }

        public void Configure()
        {
            if (view == null) view = GetComponent<GuidanceView>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.pitch = 1f;
        }

        public void Play(string cueGroup, Action onFinished)
        {
            Stop();
            Configure();
            finished = onFinished;
            StartCue(cueGroup);
        }

        public void PlayIntroduction(string cueGroup, Action onReady, Action onBegin, Func<bool> canBegin = null)
        {
            Stop();
            Configure();
            introduction = true;
            introductionReady = onReady;
            introductionBegin = onBegin;
            canBeginIntroduction = canBegin;
            StartCue(cueGroup);
        }

        private void StartCue(string cueGroup)
        {
            current = library != null ? library.Find(cueGroup) : null;
            if (current == null || current.paragraphs == null || current.paragraphs.Length == 0)
            {
                Debug.LogWarning("No butterfly narration is assigned for " + cueGroup + ".", this);
                Complete();
                return;
            }
            IsPlaying = true;
            CurrentSegmentIndex = 0;
            PresentSegment();
        }

        public bool BeginIntroduction()
        {
            if (!IntroductionReady || beginningIntroduction
                || (canBeginIntroduction != null && !canBeginIntroduction())) return false;
            var callback = introductionBegin;
            int version = playbackVersion;
            beginningIntroduction = true;
            if (view != null) view.HideSequencePanel();
            ownsPanel = false;
            try
            {
                callback?.Invoke();
            }
            finally
            {
                if (version == playbackVersion)
                {
                    IntroductionReady = false;
                    introduction = false;
                    introductionReady = null;
                    introductionBegin = null;
                    canBeginIntroduction = null;
                    playbackVersion++;
                }
                beginningIntroduction = false;
            }
            return true;
        }

        public void CancelIntroduction()
        {
            if (introduction || IntroductionReady) Stop();
        }

        private void Update()
        {
            Tick();
        }

        public void Tick()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying || AudioListener.pause || !Finite(deltaTime) || deltaTime <= 0f) return;
            if (TimedTextActive)
            {
                SubtitleTimeRemaining = Mathf.Max(0f, SubtitleTimeRemaining - deltaTime);
                if (SubtitleTimeRemaining > 0f) return;
                if (CurrentSubtitlePage + 1 < subtitlePages.Length)
                {
                    CurrentSubtitlePage++;
                    StartTimedPage();
                }
                else AdvanceSegment();
                return;
            }
            if (source == null)
            {
                StartTimedText();
                return;
            }
            if (source.isPlaying)
            {
                audioStarted = true;
                UpdateAudioSubtitle();
                return;
            }
            if (audioStarted)
            {
                AdvanceSegment();
                return;
            }
            audioStartupElapsed += deltaTime;
            float grace = Finite(audioStartupGraceSeconds) && audioStartupGraceSeconds >= 0f ? audioStartupGraceSeconds : 1f;
            if (source.clip != null && source.clip.loadState != AudioDataLoadState.Failed && audioStartupElapsed < grace) return;
            StartTimedText();
        }

        public void Continue()
        {
            // Kept for callers from earlier versions; subtitles now advance automatically.
        }

        public void Stop()
        {
            playbackVersion++;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            IsPlaying = false;
            AwaitingContinue = false;
            TimedTextActive = false;
            SubtitleTimeRemaining = 0f;
            audioStartupElapsed = 0f;
            audioStarted = false;
            current = null;
            CurrentSegmentIndex = -1;
            CurrentSubtitlePage = -1;
            subtitlePages = Array.Empty<GuidanceSubtitleCue>();
            finished = null;
            IntroductionReady = false;
            LastSubtitleText = string.Empty;
            introduction = false;
            introductionReady = null;
            introductionBegin = null;
            canBeginIntroduction = null;
            beginningIntroduction = false;
            if (ownsPanel && view != null) view.HideSequencePanel();
            ownsPanel = false;
        }

        private void PresentSegment()
        {
            if (current == null || CurrentSegmentIndex >= current.paragraphs.Length)
            {
                Complete();
                return;
            }
            var segment = current.paragraphs[CurrentSegmentIndex];
            if (segment == null)
            {
                AdvanceSegment();
                return;
            }
            subtitlePages = segment.GetSubtitleCues();
            if (subtitlePages.Length == 0) subtitlePages = new[] { new GuidanceSubtitleCue { text = segment.text, startSeconds = 0f } };
            CurrentSubtitlePage = 0;
            source.Stop();
            source.clip = segment.audio;
            audioStarted = false;
            audioStartupElapsed = 0f;
            AwaitingContinue = false;
            TimedTextActive = false;
            SubtitleTimeRemaining = 0f;
            if (segment.audio == null || segment.audio.loadState == AudioDataLoadState.Failed)
            {
                StartTimedText();
                return;
            }
            ownsPanel = true;
            PresentSubtitle();
            if (source.isActiveAndEnabled)
            {
                if (segment.audio.loadState == AudioDataLoadState.Unloaded) segment.audio.LoadAudioData();
                source.Play();
            }
            audioStarted = source.isPlaying;
        }

        private void StartTimedText()
        {
            if (!IsPlaying || current == null || CurrentSegmentIndex < 0 || CurrentSegmentIndex >= current.paragraphs.Length) return;
            if (source != null) source.Stop();
            audioStarted = false;
            TimedTextActive = true;
            AwaitingContinue = false;
            ownsPanel = true;
            StartTimedPage();
        }

        private void StartTimedPage()
        {
            PresentSubtitle();
            float minimum = Finite(minSubtitleSeconds) && minSubtitleSeconds > 0f ? minSubtitleSeconds : 2.5f;
            float rate = Finite(subtitleCharactersPerSecond) && subtitleCharactersPerSecond > 0f ? subtitleCharactersPerSecond : 18f;
            float duration = LastSubtitleText.Length / rate;
            if (!Finite(duration)) duration = LastSubtitleText.Length / 18f;
            SubtitleTimeRemaining = Mathf.Max(minimum, duration);
        }

        private void PresentSubtitle()
        {
            if (CurrentSubtitlePage < 0 || CurrentSubtitlePage >= subtitlePages.Length) return;
            string text = subtitlePages[CurrentSubtitlePage] != null ? subtitlePages[CurrentSubtitlePage].text ?? string.Empty : string.Empty;
            LastSubtitleText = text;
            view?.ShowSequencePanel(text, Array.Empty<string>(), null);
        }

        private void UpdateAudioSubtitle()
        {
            if (source == null || source.clip == null || subtitlePages.Length < 2) return;
            int totalCharacters = 0;
            foreach (var page in subtitlePages) totalCharacters += page != null && page.text != null ? page.text.Length : 0;
            int precedingCharacters = 0;
            int nextPage = 0;
            float previousStart = 0f;
            for (int i = 0; i < subtitlePages.Length; i++)
            {
                var page = subtitlePages[i];
                float automaticStart = totalCharacters > 0 ? source.clip.length * precedingCharacters / totalCharacters : 0f;
                float start = page != null && page.startSeconds >= 0f ? page.startSeconds : automaticStart;
                start = Mathf.Clamp(Mathf.Max(previousStart, start), 0f, source.clip.length);
                if (source.time >= start) nextPage = i;
                previousStart = start;
                precedingCharacters += page != null && page.text != null ? page.text.Length : 0;
            }
            if (nextPage == CurrentSubtitlePage) return;
            CurrentSubtitlePage = nextPage;
            PresentSubtitle();
        }

        private void AdvanceSegment()
        {
            if (!IsPlaying) return;
            AwaitingContinue = false;
            CurrentSegmentIndex++;
            PresentSegment();
        }

        private void Complete()
        {
            if (introduction)
            {
                ReadyIntroduction();
                return;
            }
            var callback = finished;
            finished = null;
            IsPlaying = false;
            AwaitingContinue = false;
            TimedTextActive = false;
            SubtitleTimeRemaining = 0f;
            current = null;
            CurrentSegmentIndex = -1;
            CurrentSubtitlePage = -1;
            subtitlePages = Array.Empty<GuidanceSubtitleCue>();
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            if (view != null) view.HideSequencePanel();
            ownsPanel = false;
            callback?.Invoke();
        }

        private void ReadyIntroduction()
        {
            if (!introduction || IntroductionReady) return;
            IsPlaying = false;
            AwaitingContinue = false;
            TimedTextActive = false;
            SubtitleTimeRemaining = 0f;
            IntroductionReady = true;
            if (current != null && current.paragraphs != null && current.paragraphs.Length > 0)
                CurrentSegmentIndex = current.paragraphs.Length - 1;
            if (subtitlePages.Length > 0)
            {
                CurrentSubtitlePage = subtitlePages.Length - 1;
                var page = subtitlePages[CurrentSubtitlePage];
                LastSubtitleText = page != null ? page.text ?? string.Empty : string.Empty;
            }
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            ownsPanel = true;
            int version = playbackVersion;
            view?.ShowSequencePanel(LastSubtitleText, new[] { "Begin" }, _ =>
            {
                if (version == playbackVersion) BeginIntroduction();
            });
            var callback = introductionReady;
            introductionReady = null;
            callback?.Invoke();
        }

        private void OnDisable()
        {
            Stop();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
