using System.Collections;
using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class FireworkStation : MonoBehaviour, IPrototypeResettable
    {
        public Transform rocket;
        public TrailRenderer trail;
        public ParticleSystem burst;
        public FireTarget grassFire;
        public float launchHeight = 7f;
        public bool igniteGrassOnLaunch = true;
        public bool IsLaunching { get; private set; }
        private Vector3 rocketOrigin;
        private Color[] colours = { new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.78f, 0.2f) };
        private int colourIndex;

        private void Awake() { rocketOrigin = rocket.localPosition; rocket.gameObject.SetActive(false); }
        public void Launch()
        {
            PrototypeSession session = PrototypeSession.Instance;
            if (IsLaunching) { if (session != null) session.Say("Look up. The show is still running."); return; }
            if (session != null) { session.state.RecordFirework(); session.Say("Look up. The show's starting."); }
            StartCoroutine(LaunchSequence());
        }

        public void LeaveUnlit()
        {
            PrototypeSession session = PrototypeSession.Instance;
            if (session != null) session.Say(session.state.fireworksEverLaunched
                ? "You leave the remaining fireworks alone. Your earlier launch is still recorded."
                : "You leave the fireworks unlit. No sparks land in the grass.");
        }

        private IEnumerator LaunchSequence()
        {
            IsLaunching = true;
            rocket.localPosition = rocketOrigin;
            rocket.gameObject.SetActive(true); trail.Clear();
            float elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                rocket.localPosition = rocketOrigin + Vector3.up * (launchHeight * Mathf.Clamp01(elapsed / 1.2f));
                yield return null;
            }
            burst.transform.position = rocket.position;
            var main = burst.main; main.startColor = colours[colourIndex++ % colours.Length];
            burst.Play(); rocket.gameObject.SetActive(false);
            if (igniteGrassOnLaunch && grassFire != null) grassFire.Ignite();
            yield return new WaitForSeconds(2.7f);
            IsLaunching = false;
            if (PrototypeSession.Instance != null) PrototypeSession.Instance.Say("That was beautiful. Launch another, or leave the fireworks alone.");
        }

        public void ResetPrototype()
        {
            StopAllCoroutines(); IsLaunching = false; colourIndex = 0;
            rocket.localPosition = rocketOrigin; rocket.gameObject.SetActive(false);
            trail.Clear(); burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
