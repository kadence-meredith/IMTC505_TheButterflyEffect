using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    /// <summary>Checks the original dialogue, rendered controls, and real moving guide together.</summary>
    public static class GuidedDialogueValidation
    {
        [MenuItem("Butterfly Effect/Validate Guided Dialogue")]
        public static void Validate()
        {
            ValidateCigaretteToMarshmallow();
            ValidateSpecificPrefixesAndRetargeting();
            ValidateFallbackAndMinimalView();
            Debug.Log("Guided dialogue validation passed: original cigarette-to-campfire choice; "
                + "travel input lock; user catch-up before options; original marshmallow dialogue; "
                + "same-station interactions; longest-prefix destinations; latest-line retargeting; "
                + "missing-target fallback; dialogue-only UI; user transform preserved.");
        }

        private static void ValidateCigaretteToMarshmallow()
        {
            using (var fixture = new Fixture("Cigarette to marshmallow"))
            {
                fixture.Director.State.Cigarette = CigaretteStatus.Ground;
                fixture.Director.Show("cigarette.ground");
                fixture.Until(() => !fixture.Coordinator.IsTravelling, 5, .01f,
                    "The initial station did not recognize the already-near butterfly and user.");
                Require(!fixture.Coordinator.IsTravelling && !fixture.View.InteractionLocked,
                    "An already-reached station should display its dialogue immediately.");
                int initialRouteCount = fixture.FinishedRoutes;
                fixture.AssertOriginalLine("cigarette.ground");
                var visitButton = fixture.FindChoice("Visit the campfire");
                var staleMouseAction = visitButton.onClick;
                visitButton.onClick.Invoke();

                Require(fixture.Director.Current.id == "campfire.approach", "The original campfire choice no longer opens campfire dialogue.");
                fixture.AssertTravellingTo(fixture.Campfire, "campfire.approach");
                Require(fixture.ActiveButtons().Length == 0, "Destination choices remained clickable while travelling.");

                // Both keyboard selection and a stale mouse callback must obey the view's lock.
                InvokePrivate(fixture.View, "Select", 0);
                staleMouseAction.Invoke();
                Require(fixture.Director.Current.id == "campfire.approach" && fixture.Director.State.MarshmallowsEaten == 0,
                    "An input callback advanced destination dialogue during travel.");
                Require(fixture.ActiveButtons().Length == 0, "Travel input rendered new active buttons.");

                // Walk part way: the butterfly can reach the station, but the user is still outside its arrival radius.
                fixture.User.position = new Vector3(0, 1.7f, 1.5f);
                fixture.Until(() => fixture.Guide.IsAtPoint, 100, .1f, "Butterfly did not reach the campfire marker.");
                Require(fixture.Coordinator.IsTravelling && fixture.View.InteractionLocked && fixture.FinishedRoutes == initialRouteCount,
                    "Options unlocked when only the butterfly had arrived.");
                Require(fixture.ActiveButtons().Length == 0, "Campfire options appeared before user arrival.");

                fixture.User.position = fixture.Campfire.position + Vector3.up * 1.7f;
                fixture.Until(() => !fixture.Coordinator.IsTravelling, 8, .05f, "User arrival did not unlock campfire dialogue.");
                Require(!fixture.View.InteractionLocked && fixture.Coordinator.CurrentStation == fixture.Campfire,
                    "The coordinator did not record arrival at the campfire.");
                fixture.AssertOriginalLine("campfire.approach");
                Require(fixture.FinishedRoutes == initialRouteCount + 1, "A single station transfer should complete one guide route.");

                fixture.ClickChoice("Pick up a marshmallow");
                fixture.AssertOriginalLine("campfire.roasting");
                Require(!fixture.Coordinator.IsTravelling && !fixture.Guide.IsRunning,
                    "Roasting at the same station restarted travel.");
                fixture.ClickChoice("Take a bite");
                fixture.AssertOriginalLine("campfire.eaten");
                for (int i = 0; i < 5; i++) fixture.Tick(.1f);
                Require(fixture.Director.State.MarshmallowsEaten == 1 && fixture.FinishedRoutes == initialRouteCount + 1
                    && !fixture.Coordinator.IsTravelling && !fixture.Guide.IsRunning,
                    "Same-station choices lost their original behavior or restarted the guide.");
            }
        }

        private static void ValidateSpecificPrefixesAndRetargeting()
        {
            using (var fixture = new Fixture("Specific station and latest pending line"))
            {
                fixture.Director.State.Cigarette = CigaretteStatus.Holding;
                fixture.Director.Show("cigarette.bin");
                fixture.AssertTravellingTo(fixture.Bin, "cigarette.bin");
                Require(fixture.Guide.TargetPoint != fixture.Cigarette,
                    "The general cigarette prefix overrode the specific disposal-bin prefix.");
                fixture.Tick(.2f);

                fixture.Director.State.Cigarette = CigaretteStatus.Ground;
                fixture.Director.Show("cigarette.ground");
                fixture.AssertTravellingTo(fixture.Cigarette, "cigarette.ground");
                fixture.FollowUntilArrived();
                Require(fixture.Coordinator.CurrentStation == fixture.Cigarette,
                    "The old pending station won after a retarget.");
                fixture.AssertOriginalLine("cigarette.ground");
                Require(!fixture.View.InteractionLocked && fixture.FinishedRoutes == 1,
                    "Retargeting failed to restore only the latest pending dialogue.");
            }
        }

        private static void ValidateFallbackAndMinimalView()
        {
            using (var fixture = new Fixture("Fallback and minimal dialogue UI"))
            {
                fixture.Director.Show("forest.hub");
                Require(!fixture.Coordinator.IsTravelling && !fixture.View.InteractionLocked,
                    "An unmapped dialogue line should retain its original controls.");
                fixture.AssertOriginalLine("forest.hub");
                fixture.AssertDialogueOnlyUI();

                fixture.Coordinator.stations = new[]
                {
                    new GuidanceStationBinding { name = "Missing fireworks station", target = null,
                        dialoguePrefixes = new[] { "fireworks." } }
                };
                fixture.Director.Show("fireworks.approach");
                Require(!fixture.Coordinator.IsTravelling && !fixture.View.InteractionLocked,
                    "A missing station target locked the dialogue indefinitely.");
                fixture.AssertOriginalLine("fireworks.approach");

                fixture.Coordinator.stations = new[]
                {
                    new GuidanceStationBinding { name = "Campfire", target = fixture.Campfire,
                        dialoguePrefixes = new[] { "campfire." } }
                };
                fixture.Director.State.Cigarette = CigaretteStatus.Disposed;
                fixture.Director.Show("campfire.approach");
                fixture.AssertTravellingTo(fixture.Campfire, "campfire.approach");
                Object.DestroyImmediate(fixture.Campfire.gameObject);
                fixture.Coordinator.RefreshRoute();
                Require(!fixture.Coordinator.IsTravelling && !fixture.View.InteractionLocked,
                    "Refreshing a destroyed target failed to release the dialogue lock.");
                fixture.AssertOriginalLine("campfire.approach");
                fixture.AssertDialogueOnlyUI();
            }
        }

        private static void InvokePrivate(object instance, string name, params object[] arguments)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "Missing input/lifecycle method: " + name);
            method.Invoke(instance, arguments);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Guided dialogue: " + message);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GuidanceLibrary library;
            private readonly HashSet<EventSystem> previousEventSystems;
            public readonly GuidanceDirector Director;
            public readonly GuidanceView View;
            public readonly ButterflyPointGuide Guide;
            public readonly GuidanceRouteCoordinator Coordinator;
            public readonly Transform User;
            public readonly Transform Cigarette;
            public readonly Transform Campfire;
            public readonly Transform Bin;
            public int FinishedRoutes;

            public Fixture(string name)
            {
                previousEventSystems = new HashSet<EventSystem>(Resources.FindObjectsOfTypeAll<EventSystem>());
                root = new GameObject("Guided dialogue validation — " + name) { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                var dialogueObject = new GameObject("Dialogue");
                dialogueObject.transform.SetParent(root.transform, false);
                Director = dialogueObject.AddComponent<GuidanceDirector>();
                library = GuidanceLibrary.CreateDefault();
                Director.library = library;
                View = dialogueObject.AddComponent<GuidanceView>();
                View.director = Director;
                View.bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                View.headingFont = View.bodyFont;
                View.showDemoHeadings = false;
                View.Build();

                User = Point("User", new Vector3(0, 1.7f, 0));
                Cigarette = Point("Cigarette marker", Vector3.zero);
                Campfire = Point("Marshmallow marker", new Vector3(0, 0, 4));
                Bin = Point("Disposal-bin marker", new Vector3(4, 0, 0));
                var guideObject = new GameObject("Butterfly guide");
                guideObject.transform.SetParent(root.transform, false);
                Guide = guideObject.AddComponent<ButterflyPointGuide>();
                Guide.user = User;
                Guide.playOnStart = false;
                Guide.moveSpeed = 2;
                Guide.flightHeight = 1.4f;
                Guide.maxLeadDistance = 3;
                Guide.resumeLeadDistance = 2.2f;
                Guide.userArrivalDistance = .6f;
                Guide.transform.position = Cigarette.position + Vector3.up * Guide.flightHeight;
                Guide.onRouteFinished.AddListener(() => FinishedRoutes++);

                Coordinator = dialogueObject.AddComponent<GuidanceRouteCoordinator>();
                Coordinator.director = Director;
                Coordinator.view = View;
                Coordinator.guide = Guide;
                Coordinator.stations = new[]
                {
                    new GuidanceStationBinding { name = "Cigarette", target = Cigarette,
                        dialoguePrefixes = new[] { "cigarette." } },
                    new GuidanceStationBinding { name = "Marshmallows", target = Campfire,
                        dialoguePrefixes = new[] { "campfire." } },
                    new GuidanceStationBinding { name = "Disposal bin", target = Bin,
                        dialoguePrefixes = new[] { "cigarette.bin" } }
                };
                root.SetActive(true);
                // Editor checks do not rely on a Play Mode frame to install the view's event subscription.
                InvokePrivate(View, "OnDisable");
                InvokePrivate(View, "OnEnable");
                Coordinator.RefreshRoute();
            }

            private Transform Point(string name, Vector3 position)
            {
                var point = new GameObject(name).transform;
                point.SetParent(root.transform, false);
                point.position = position;
                return point;
            }

            public Button[] ActiveButtons()
            {
                return View.GetComponentsInChildren<Button>(true)
                    .Where(button => button != null && button.gameObject.activeInHierarchy && button.interactable).ToArray();
            }

            public Button FindChoice(string label)
            {
                var button = ActiveButtons().FirstOrDefault(item => item.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == label));
                Require(button != null, "The original choice is missing: " + label);
                return button;
            }

            public void ClickChoice(string label) { FindChoice(label).onClick.Invoke(); }

            public void AssertOriginalLine(string id)
            {
                var line = library.Find(id);
                Require(line != null && Director.Current.id == id, "Wrong original dialogue line: " + id);
                var text = View.GetComponentsInChildren<Text>(true).FirstOrDefault(item => item.gameObject.name == "Dialogue");
                Require(text != null && text.gameObject.activeInHierarchy && text.text == line.text,
                    "The original dialogue text was not restored for " + id);
                int expectedChoices = line.choices != null && line.choices.Length > 0 ? line.choices.Length
                    : string.IsNullOrEmpty(line.nextId) ? 0 : 1;
                Require(ActiveButtons().Length == expectedChoices, "Incorrect rendered choice count for " + id);
                if (line.choices != null)
                    foreach (var choice in line.choices) FindChoice(choice.label);
            }

            public void AssertTravellingTo(Transform target, string pendingLine)
            {
                Require(Coordinator.IsTravelling && View.InteractionLocked,
                    "Travel did not lock dialogue interaction for " + pendingLine);
                Require(Guide.TargetPoint == target && Coordinator.PendingLineId == pendingLine,
                    "The guide or pending dialogue points at the wrong destination.");
                Require(ActiveButtons().Length == 0, "Travelling retained active destination options.");
            }

            public void AssertDialogueOnlyUI()
            {
                var card = View.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(item => item.gameObject.name == "Butterfly dialogue card");
                Require(card != null, "The original dialogue card is missing.");
                foreach (var text in View.GetComponentsInChildren<Text>(true))
                    if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                        Require(text.transform.IsChildOf(card), "Unexpected active HUD/header/footer text: " + text.text);
            }

            public void Tick(float deltaTime)
            {
                var position = User.position;
                var rotation = User.rotation;
                Guide.Tick(deltaTime);
                Require(Vector3.Distance(User.position, position) < .0001f && Quaternion.Angle(User.rotation, rotation) < .001f,
                    "The guided dialogue moved the user's transform.");
            }

            public void Until(Func<bool> condition, int steps, float deltaTime, string message)
            {
                for (int i = 0; i < steps && !condition(); i++) Tick(deltaTime);
                Require(condition(), message);
            }

            public void FollowUntilArrived()
            {
                for (int i = 0; i < 150 && Coordinator.IsTravelling; i++)
                {
                    User.position = new Vector3(Guide.transform.position.x, 1.7f, Guide.transform.position.z - .1f);
                    Tick(.1f);
                }
                Require(!Coordinator.IsTravelling, "Retargeted route did not finish after user catch-up.");
            }

            public void Dispose()
            {
                if (root != null) Object.DestroyImmediate(root);
                if (library != null) Object.DestroyImmediate(library);
                foreach (var events in Resources.FindObjectsOfTypeAll<EventSystem>())
                    if (events != null && !previousEventSystems.Contains(events) && events.gameObject.scene.IsValid())
                        Object.DestroyImmediate(events.gameObject);
            }
        }
    }
}
