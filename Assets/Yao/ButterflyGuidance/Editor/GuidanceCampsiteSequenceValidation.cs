using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ButterflyEffect.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceCampsiteSequenceValidation
    {
        [MenuItem("Tools/Butterfly Effect/Validate Free Exploration")]
        public static void Validate() { Run(); }

        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            Scene previousScene = default;
            Scene temporary = default;
            EventSystem previousEvents = EventSystem.current;
            PrototypeSession previousSession = PrototypeSession.Instance;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                    "Exit Play mode before running free-exploration validation.");
                if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                    EditorSceneManager.OpenScene("Assets/Main Scenes/0_CampusStart_Main.unity", OpenSceneMode.Single);
                previousScene = SceneManager.GetActiveScene();
                temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporary);
                CheckEntryAndIndependentActions(temporary);
                CheckRoastingReadinessAndWaterBucket(temporary);
                CheckEarlyBucketPhysicalChoice(temporary);
                CheckOutOfOrderExploration(temporary);
                CheckVisitAndLeaveWithoutIgnition(temporary);
                CheckSafeDisposalAndEndingState(temporary);
                CheckBucketAfterCampfireCold(temporary);
                report.passed = true;
                report.summary = "Free exploration passed: automatic ordered guidance, one timed entry prompt, "
                    + "independent action choices, early exploration, visit-and-leave progression, "
                    + "physical roasting readiness and burning, explicit roasting completion before water-bucket guidance, "
                    + "safe and unsafe cigarette decisions, independent cold-campfire bucket access, "
                    + "unchanged player pose, and physical ending state.";
                Debug.Log(report.summary);
            }
            catch (Exception error)
            {
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                if (previousEvents != null) EventSystem.current = previousEvents;
                typeof(PrototypeSession).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .SetValue(null, previousSession);
                string path = Environment.GetEnvironmentVariable("BUTTERFLY_SEQUENCE_REPORT");
                if (string.IsNullOrEmpty(path)) path = Path.Combine(Path.GetTempPath(), "ButterflyCampsiteSequenceValidation.json");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
        }

        [MenuItem("Tools/Butterfly Effect/Validate All Guidance")]
        public static void RunAll()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            try
            {
                Validate();
                GuidanceMainSceneValidation.Validate();
                GuidancePlayerModeValidation.Validate();
                report.passed = true;
                report.summary = "Sequential free exploration, main-scene integration, and keyboard/XR mode regressions passed.";
                Debug.Log(report.summary);
            }
            catch (Exception error)
            {
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                string path = Environment.GetEnvironmentVariable("BUTTERFLY_ALL_GUIDANCE_REPORT");
                if (string.IsNullOrEmpty(path)) path = Path.Combine(Path.GetTempPath(), "ButterflyAllGuidanceValidation.json");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
        }

        public static void OpenCampsiteForPreview()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before opening the campsite.");
            EditorSceneManager.OpenScene("Assets/Main Scenes/1_Campsite_Main.unity", OpenSceneMode.Single);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static void CheckEntryAndIndependentActions(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                Require(f.Sequence.IsInitialized && f.Sequence.CurrentStage == CampsiteGuidanceStage.Cigarette,
                    "Entering camp did not automatically begin with the cigarette.");
                Require(f.Guide.DestinationPoint == f.CigarettePoint,
                    "The first suggested destination is not the cigarette.");
                Require(f.View.IsExplorationMode && f.View.TransientPromptVisible && Buttons(f.View).Length == 0,
                    "Camp entry showed destination choices instead of the single entry prompt.");
                Require(VisibleText(f.View).Contains("Follow the butterfly."), "The entry follow prompt is missing.");
                Vector3 playerPose = f.User.position;
                f.View.AdvancePrompt(3.1f);
                f.Sequence.Tick(.2f);
                Require(!f.View.TransientPromptVisible, "The entry prompt did not expire.");
                f.Bridge.Initialize();
                Require(!f.View.TransientPromptVisible, "Repeated binding replayed the entry prompt.");
                f.View.ShowTravelPrompt("I'll wait here. Come a little closer.");
                Require(!f.View.InteractionLocked && !VisibleText(f.View).Contains("I'll wait here"),
                    "Travel restored a blocking or repeated waiting prompt.");
                Require(f.User.position == playerPose, "Automatic guidance moved the user.");

                // Walk straight to campfire while the guide is still going to the cigarette.
                f.MoveUser(f.FirePoint);
                f.Sequence.Tick(.2f);
                Require(f.Guide.DestinationPoint == f.CigarettePoint,
                    "A player's independent visit replaced the butterfly's suggested order.");
                Require(f.View.ContextVisible && !f.View.InteractionLocked,
                    "A player away from the butterfly cannot use a nearby physical object.");
                Button[] active = Buttons(f.View);
                Require(active.Length > 0 && active.All(b => !NavigationLabels.Contains(Label(b))),
                    "The contextual card contains destination or continue/leave navigation.");
                Button drop = active.FirstOrDefault(b => Label(b) == "Drop it");
                Require(drop != null, "The held cigarette has no local drop choice at the independently visited campfire.");
                Vector3 beforeClick = f.User.position;
                drop.onClick.Invoke();
                Require(f.Setup.player.Held == null && f.Session.state.cigarette == CigaretteCondition.Dropped,
                    "The filtered drop button selected the wrong original choice or was blocked by guide travel.");
                f.Sequence.Tick(.2f);
                Require(f.Sequence.IsExplored(CampsiteGuidanceStage.Cigarette)
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "An unsafe cigarette decision failed to advance exploration.");
                Require(f.User.position == beforeClick, "A contextual choice teleported the player.");
                Button marshmallow = Buttons(f.View).FirstOrDefault(button => Label(button) == "Pick up a marshmallow");
                Require(marshmallow != null, "The campfire lost its physical marshmallow pickup choice.");
                marshmallow.onClick.Invoke();
                Require(f.Setup.player.Held == f.Setup.marshmallow && f.Setup.marshmallow.IsHeld,
                    "The marshmallow choice did not grab the physical roasting item.");
                var body = f.View.GetComponentsInChildren<Text>(true).First(text => text.name == "Dialogue");
                Require(string.IsNullOrEmpty(body.text) && !VisibleText(f.View).ToLowerInvariant().Contains("golden"),
                    "Roasting still displays a readiness or golden-status instruction.");
                Require(Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "The player has no explicit way to finish the roasting session.");
                f.MoveUser(f.AwayPoint);
                f.Sequence.Tick(.2f);
                Require(!f.View.ContextVisible && Buttons(f.View).Length == 0,
                    "The contextual panel stayed visible after leaving all objects.");
            }
        }

        private static void CheckRoastingReadinessAndWaterBucket(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                f.MoveUser(f.FirePoint);
                f.Sequence.Tick(.2f);
                Button pickup = Buttons(f.View).FirstOrDefault(button => Label(button) == "Pick up a marshmallow");
                Require(pickup != null, "There is no physical marshmallow pickup at the campfire.");
                pickup.onClick.Invoke();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Campfire)
                    && f.Guide.DestinationPoint == f.FirePoint,
                    "The guide left the campfire as soon as a marshmallow was picked up.");

                f.MoveUser(f.BucketPoint);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.BucketPromptAvailable
                    && (f.Director.Current == null || !f.Director.Current.id.StartsWith("bucket.", StringComparison.Ordinal))
                    && !VisibleText(f.View).Contains("Water bucket"),
                    "The bucket prompt appeared before the player chose to finish roasting.");
                f.MoveUser(f.FirePoint);
                f.Sequence.Tick(.2f);
                var body = f.View.GetComponentsInChildren<Text>(true).First(text => text.name == "Dialogue");
                f.Setup.marshmallow.cookedSeconds = Mathf.Max(0f, f.Setup.marshmallow.goldenSeconds - .1f);
                f.Sequence.Tick(.2f);
                Require(string.IsNullOrEmpty(body.text), "An uncooked marshmallow displayed a readiness message.");
                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.goldenSeconds;
                f.Sequence.Tick(.2f);
                Require(body.text.IndexOf("ready", StringComparison.OrdinalIgnoreCase) >= 0
                    && body.text.IndexOf("bite", StringComparison.OrdinalIgnoreCase) >= 0,
                    "A physically ready marshmallow did not offer the ready-and-bite prompt.");
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Reaching the ready threshold advanced the tour before the player's decision.");

                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.burntSeconds;
                f.Sequence.Tick(.2f);
                Require(body.text.IndexOf("burnt", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The burnt threshold did not replace the ready prompt with a burnt message.");
                Button bite = Buttons(f.View).FirstOrDefault(button => Label(button) == "Take a bite");
                Require(bite != null, "The player cannot choose to taste the roasted marshmallow.");
                bite.onClick.Invoke();
                f.Sequence.Tick(.2f);
                Require(f.Session.state.marshmallowsEaten == 1 && f.Session.state.burntMarshmallowsEaten == 1,
                    "The taste choice did not use the actual physical marshmallow state.");
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Eating one marshmallow automatically ended roasting without a finish decision.");

                Button finish = Buttons(f.View).FirstOrDefault(button => Label(button) == "Finish roasting");
                Require(finish != null, "The eaten-marshmallow choices have no finish decision.");
                Vector3 userPosition = f.User.position;
                finish.onClick.Invoke();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.IsRoastingFinished && f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && f.Guide.DestinationPoint == f.BucketPoint,
                    "Finishing roasting did not lead to the water bucket before fireworks.");
                Require(f.Setup.player.Held == null && !f.Setup.marshmallow.IsHeld && f.User.position == userPosition,
                    "Finishing roasting did not release the desktop item or moved the player.");
                Require(!f.Sequence.BucketPromptAvailable && !VisibleText(f.View).Contains("Water bucket"),
                    "The bucket instructions appeared while the user was still at the campfire.");
                f.MoveUser(f.BucketPoint);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.BucketPromptAvailable && f.Director.Current != null
                    && f.Director.Current.id == "bucket.ready"
                    && Buttons(f.View).Any(button => Label(button) == "Pick up"),
                    "The nearby bucket prompt did not appear after the roasting session was finished.");
                Require(!VisibleText(f.View).Contains("Hold R"), "A keyboard pouring prompt returned in the VR flow.");
                Require(f.Session.state.campfire == FireCondition.Burning,
                    "Finishing or tasting a marshmallow silently extinguished the real fire.");
            }
        }

        private static void CheckOutOfOrderExploration(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.MoveUser(f.FireworksPoint);
                f.Session.state.RecordFirework();
                f.Session.Changed();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Cigarette,
                    "Early fireworks activity was lost or changed the suggested first station.");
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "The suggested route did not continue to campfire after the cigarette decision.");
                f.MoveUser(f.FirePoint);
                f.Setup.player.Grab(f.Setup.marshmallow);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.IsExplored(CampsiteGuidanceStage.Campfire)
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Picking up a marshmallow skipped the player's roasting decision.");
                Button finish = Buttons(f.View).FirstOrDefault(button => Label(button) == "Finish roasting");
                Require(finish != null, "An early fireworks visit removed the later roasting finish choice.");
                finish.onClick.Invoke();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket,
                    "An early fireworks visit skipped the required post-roasting bucket stop.");
                f.VisitAndLeave(f.BucketPoint);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal,
                    "An already explored fireworks station was demanded again after the bucket visit.");
                Require(f.Guide.DestinationPoint == f.ReturnPoint,
                    "After the explored stations and bucket visit, the butterfly did not suggest the return portal.");
                Require(f.Session.state.fireworksLaunched == 1 && !f.Setup.marshmallow.IsHeld,
                    "Exploration progress replaced real item or fireworks state.");
            }
        }

        private static void CheckEarlyBucketPhysicalChoice(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                f.MoveUser(f.BucketPoint);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.BucketPromptAvailable && !VisibleText(f.View).Contains("Water bucket"),
                    "An early bucket visit displayed its guided instructions before roasting was finished.");
                f.Setup.player.Grab(f.Setup.bucket);
                f.Sequence.Tick(.2f);
                Require(f.Setup.player.Held == f.Setup.bucket && f.Setup.bucket.IsHeld
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "The suggested route blocked an independent physical bucket pickup.");
                Require(!f.Sequence.IsExplored(CampsiteGuidanceStage.WaterBucket),
                    "Early bucket use silently removed the later guided bucket stop.");
                f.Sequence.FinishRoasting();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && f.Guide.DestinationPoint == f.BucketPoint,
                    "Finishing roasting skipped the bucket because it had been picked up earlier.");
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks,
                    "A post-finish physical bucket decision failed to continue toward fireworks.");
                Require(f.Session.state.campfire == FireCondition.Burning,
                    "A bucket visit changed fire state without water being poured on the fire.");
            }
        }

        private static void CheckVisitAndLeaveWithoutIgnition(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.VisitAndLeave(f.CigarettePoint);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Looking at the cigarette then leaving could not advance.");
                f.VisitAndLeave(f.FirePoint);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket,
                    "Campfire exploration required a physical action or safe result.");
                f.VisitAndLeave(f.BucketPoint);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks,
                    "The water-bucket stop could not be visited without changing a physical fire decision.");
                f.VisitAndLeave(f.FireworksPoint);
                Require(f.Sequence.ExploredCount == 4 && f.Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal,
                    "Leaving fireworks unlit prevented completing exploration.");
                Require(!f.Session.state.fireworksEverLaunched && f.Session.state.fireworksLaunched == 0
                    && f.Session.state.totalActions == 0,
                    "Visit-only exploration fabricated an interaction or ignited fireworks.");
                Require(f.Session.state.campfire == FireCondition.Burning
                    && f.Session.state.cigarette == CigaretteCondition.Held,
                    "Visiting a station changed the player's unresolved decisions.");
                Require(f.ReturnPortal.ResolveEndingScene() == "3_CampusFire_Main",
                    "Visit-only exploration overrode the actual two remaining hazards.");
            }
        }

        private static void CheckSafeDisposalAndEndingState(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.MoveUser(f.BinPoint);
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Setup.cigarette.IsDisposed && f.Session.state.cigaretteDisposals == 1,
                    "The real disposal-bin boundary did not retain a safe cigarette decision.");
                Require(f.Sequence.IsExplored(CampsiteGuidanceStage.Cigarette),
                    "Safe cigarette disposal did not count as exploration.");
                f.Session.state.RecordCampfire(FireCondition.Cold);
                f.Session.state.fireworksEverLaunched = false;
                Require(f.ReturnPortal.ResolveEndingScene() == "5_CampusEnd_Main",
                    "Exploration progress overrode a safe physical result.");
                f.Session.state.cigaretteDroppedOutsideBin = true;
                Require(f.ReturnPortal.ResolveEndingScene() == "2_CampusFire_Main",
                    "Prior unsafe choices disappeared when guidance moved on.");
            }
        }

        private static void CheckBucketAfterCampfireCold(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Ready();
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                f.Setup.campfire.progress.condition = FireCondition.Cold;
                f.Session.state.RecordCampfire(FireCondition.Cold);
                f.Sequence.FinishRoasting();
                f.MoveUser(f.BucketPoint);
                f.Sequence.Tick(.2f);
                Require(f.Director.Current != null && f.Director.Current.id == "bucket.ready",
                    "A cold campfire hid the independently usable water bucket.");
                Button pickup = Buttons(f.View).FirstOrDefault(button => Label(button) == "Pick up");
                Require(pickup != null, "The bucket pickup choice disappeared after the campfire became cold.");
                pickup.onClick.Invoke();
                Require(f.Setup.player.Held == f.Setup.bucket && f.Setup.bucket.IsHeld,
                    "The cold-campfire bucket choice did not grab the actual bucket.");
                var card = f.View.GetComponentsInChildren<RectTransform>(true)
                    .First(rect => rect.name == "Butterfly dialogue card");
                Require(!card.gameObject.activeSelf && !VisibleText(f.View).Contains("Hold R"),
                    "The held bucket still shows a keyboard instruction or an empty instruction card.");
                Require(Buttons(f.View).All(button => Label(button) != "Pour water"
                    && Label(button) != "Stir and pour again"),
                    "A wait-only pouring button was retained in free exploration.");
            }
        }

        private static readonly HashSet<string> NavigationLabels = new HashSet<string>
        {
            "The cigarette", "Campfire & marshmallows", "Water bucket", "Canada Day fireworks",
            "Look for the disposal bin", "Return portal", "Visit the campfire", "Explore the campsite",
            "Leave", "Continue", "Begin", "Keep holding it"
        };
        private static string Label(Button button) => button.GetComponentInChildren<Text>(true)?.text ?? string.Empty;
        private static Button[] Buttons(GuidanceView view) => view.GetComponentsInChildren<Button>(true)
            .Where(button => button.isActiveAndEnabled && button.interactable).ToArray();
        private static string VisibleText(GuidanceView view) => string.Join("\n", view.GetComponentsInChildren<Text>(true)
            .Where(text => text.isActiveAndEnabled).Select(text => text.text));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Free exploration: " + message);
        }
        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly PrototypeSession Session;
            public readonly CampsiteSetup Setup;
            public readonly GuidanceDirector Director;
            public readonly GuidanceView View;
            public readonly GuidanceRouteCoordinator Coordinator;
            public readonly ButterflyPointGuide Guide;
            public readonly GuidanceSceneBridge Bridge;
            public readonly GuidanceCampsiteSequence Sequence;
            public readonly GuidanceReturnPortal ReturnPortal;
            public readonly Transform User, CigarettePoint, FirePoint, FireworksPoint, ReturnPoint, BinPoint, BucketPoint, AwayPoint;
            private readonly PrototypeSession previousSession;
            private readonly GuidanceLibrary library;
            private static readonly PropertyInfo InstanceProperty = typeof(PrototypeSession)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);

            public Fixture(Scene scene)
            {
                previousSession = PrototypeSession.Instance;
                Root = new GameObject("Free exploration validation");
                SceneManager.MoveGameObjectToScene(Root, scene);
                Root.SetActive(false);
                Root.transform.position = new Vector3(5000f, 0f, 5000f);
                Session = Child("Session").AddComponent<PrototypeSession>();
                Session.state = new ChoiceState { cigarette = CigaretteCondition.Held, campfire = FireCondition.Burning };
                InstanceProperty.SetValue(null, Session);
                var events = Child("EventSystem").AddComponent<EventSystem>();
                events.gameObject.AddComponent<XRUIInputModule>();
                EventSystem.current = events;
                library = GuidanceLibrary.CreateDefault();
                Director = Root.AddComponent<GuidanceDirector>();
                Director.library = library;
                View = Root.AddComponent<GuidanceView>();
                View.director = Director;
                View.Build();
                User = Child("Player camera").transform;
                User.gameObject.AddComponent<Camera>();
                User.localPosition = new Vector3(-4f, 1.6f, 0f);
                CigarettePoint = Marker("Cigarette approach", Vector3.zero);
                FirePoint = Marker("Campfire and marshmallow approach", Vector3.right * 12f);
                FireworksPoint = Marker("Fireworks approach", Vector3.right * 24f);
                ReturnPoint = Marker("Return portal approach", Vector3.right * 36f);
                BinPoint = Marker("Disposal bin approach", new Vector3(-8f, 0f, -8f));
                AwayPoint = Marker("Unrelated open ground", new Vector3(-20f, 0f, 20f));
                BucketPoint = Marker("Water bucket approach", new Vector3(12f, 0f, 5f));
                Guide = Child("Butterfly guide").AddComponent<ButterflyPointGuide>();
                Guide.user = User;
                Guide.transform.position = Root.transform.position + new Vector3(-3f, 1.4f, 0f);
                Coordinator = Root.AddComponent<GuidanceRouteCoordinator>();
                Coordinator.director = Director;
                Coordinator.view = View;
                Coordinator.guide = Guide;
                Coordinator.stations = new[]
                {
                    Binding("Cigarette", CigarettePoint, "cigarette."),
                    Binding("Campfire", FirePoint, "campfire."),
                    Binding("Fireworks", FireworksPoint, "fireworks."),
                    Binding("Water bucket", BucketPoint, "bucket."),
                    Binding("Disposal bin", BinPoint, "cigarette.bin"),
                    Binding("Return portal", ReturnPoint, "return.")
                };
                Coordinator.routeEdges = new[]
                {
                    new GuidanceRouteEdge { from = CigarettePoint, to = FirePoint },
                    new GuidanceRouteEdge { from = FirePoint, to = BucketPoint },
                    new GuidanceRouteEdge { from = BucketPoint, to = FireworksPoint },
                    new GuidanceRouteEdge { from = FireworksPoint, to = ReturnPoint }
                };
                Setup = Child("Physical props").AddComponent<CampsiteSetup>();
                var bin = Child("Actual bin");
                bin.transform.position = BinPoint.position + Vector3.up;
                bin.AddComponent<BoxCollider>().size = new Vector3(3f, 4f, 3f);
                Setup.disposalBin = bin.AddComponent<DisposalBin>();
                Setup.cigarette = Item<CigaretteItem>("Actual cigarette", CigarettePoint.position);
                Setup.marshmallow = Item<MarshmallowItem>("Actual marshmallow", FirePoint.position);
                Setup.marshmallow.marshmallow = Child("Marshmallow visual", Setup.marshmallow.transform)
                    .AddComponent<MeshRenderer>();
                Setup.bucket = Item<WaterBucketItem>("Actual bucket", BucketPoint.position);
                Setup.campfire = Child("Actual campfire").AddComponent<FireTarget>();
                Setup.campfire.transform.position = FirePoint.position;
                Setup.campfire.source = FireSource.Campfire;
                Setup.campfire.progress.condition = FireCondition.Burning;
                Setup.fireworks = Child("Actual fireworks").AddComponent<FireworkStation>();
                Setup.fireworks.transform.position = FireworksPoint.position;
                Setup.fireworks.rocket = Child("Rocket", Setup.fireworks.transform).transform;
                var player = Child("Desktop player").AddComponent<DesktopPlayer>();
                User.SetParent(player.transform, true);
                player.view = User.GetComponent<Camera>();
                Setup.player = player;
                player.startingCigarette = Setup.cigarette;
                ReturnPortal = Child("Actual return portal").AddComponent<GuidanceReturnPortal>();
                ReturnPortal.transform.position = ReturnPoint.position;
                ReturnPortal.user = User;
                Bridge = Root.AddComponent<GuidanceSceneBridge>();
                Bridge.role = GuidanceSceneRole.Campsite;
                Bridge.director = Director;
                Bridge.view = View;
                Bridge.coordinator = Coordinator;
                Bridge.setup = Setup;
                Bridge.session = Session;
                Bridge.returnPortal = ReturnPortal;
                Bridge.cigaretteGroundAnchor = CigarettePoint;
                Root.SetActive(true);
                Invoke(View, "OnDisable");
                Invoke(View, "OnEnable");
                Invoke(Setup.cigarette, "Awake");
                player.GrabStartingCigarette();
                Bridge.Initialize();
                Sequence = Root.GetComponent<GuidanceCampsiteSequence>();
                Require(Sequence != null, "The actual campsite adapter did not initialize sequential exploration.");
            }

            public void Ready()
            {
                View.AdvancePrompt(3.1f);
                Sequence.Tick(.3f);
                Sequence.Tick(.2f);
            }
            public void MoveUser(Transform point)
            {
                User.position = point.position + Vector3.up * 1.6f;
                Setup.player.RefreshHeldPose();
            }
            public void VisitAndLeave(Transform point)
            {
                MoveUser(point);
                for (int i = 0; i < 23; i++) Sequence.Tick(.1f);
                MoveUser(AwayPoint);
                Sequence.Tick(.2f);
            }
            private T Item<T>(string name, Vector3 position) where T : PrototypeItem
            {
                var obj = Child(name);
                obj.transform.position = position;
                obj.AddComponent<Rigidbody>();
                var item = obj.AddComponent<T>();
                Invoke(item, "Awake");
                return item;
            }
            private GameObject Child(string name, Transform parent = null)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent != null ? parent : Root.transform, false);
                return child;
            }
            private Transform Marker(string name, Vector3 localPosition)
            {
                var marker = Child(name).transform;
                marker.localPosition = localPosition;
                return marker;
            }
            private static GuidanceStationBinding Binding(string name, Transform target, params string[] prefixes)
                => new GuidanceStationBinding { name = name, target = target, dialoguePrefixes = prefixes };
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                InstanceProperty.SetValue(null, previousSession);
                Object.DestroyImmediate(library);
            }
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string summary;
            public string failure;
            public string utc;
        }
    }
}
