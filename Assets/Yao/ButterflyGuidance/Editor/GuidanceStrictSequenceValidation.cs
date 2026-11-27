using System;
using System.Linq;
using System.Reflection;
using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceStrictSequenceValidation
    {
        [MenuItem("Tools/Butterfly Effect/Validate Strict Guided Sequence")]
        public static void Validate()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run guided-sequence checks outside Play mode.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Main Scenes/0_CampusStart_Main.unity", OpenSceneMode.Single);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            using (var policy = new PolicySnapshot())
            {
                try
                {
                    PrototypeInteractionPolicy.Reset();
                    CheckPhysicalEntryPoints(scene);
                    CheckVRLocomotion(scene);
                    CheckArrivalAndBegin(scene);
                    CheckTentExitGate(scene);
                    CheckOrderedSequence(scene);
                    CheckRoastingCompletionGate(scene);
                    CheckRoastingCounterReadiness(scene);
                    CheckRoastingChoicesAndFreshServing(scene);
                    CheckKeepRoastingAndBurntChoices(scene);
                    CheckRawServingChoiceAndXRRefresh(scene);
                    CheckServingChoiceOwnership(scene);
                    CheckCampfireTableAccess(scene);
                    CheckWaterBucketArrivalAndChoices(scene);
                    CheckFireworksNaturalExit(scene);
                    CheckSafeDisposal(scene);
                    CheckOutOfOrderState(scene);
                    CheckHeldCigaretteReturn(scene);
                    CheckStoryIntroduction(scene, GuidanceSceneRole.CampusStart, GuidanceEnding.Thriving, "campus.opening");
                    CheckStoryIntroduction(scene, GuidanceSceneRole.CampusEnding, GuidanceEnding.Thriving, "ending.good");
                    CheckStoryIntroduction(scene, GuidanceSceneRole.CampusEnding, GuidanceEnding.Burning, "ending.bad");
                    CheckNarration(scene);
                    Debug.Log("Strict guided sequence passed: shared physical gates, arrival and Begin boundaries, "
                        + "ordered choices, narration completion, roasting, return permission and retained physical score.");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void CheckPhysicalEntryPoints(Scene scene)
        {
            using (var f = new Fixture(scene, false))
            using (var policy = new PolicySnapshot())
            {
                f.Setup.player.DropHeld(false);
                f.Session.state.RecordCigarette(CigaretteCondition.Dropped);
                int actions = f.Session.state.totalActions;
                Vector3 position = f.Setup.marshmallow.transform.position;
                PrototypeInteractionPolicy.GrabHandler = item => false;
                PrototypeInteractionPolicy.UseHandler = item => false;
                PrototypeInteractionPolicy.PressHandler = button => false;
                PrototypeInteractionPolicy.LaunchHandler = station => false;
                PrototypeInteractionPolicy.MoveHandler = (from, to) => false;
                PrototypeInteractionPolicy.ReturnHandler = () => false;
                f.Setup.player.Grab(f.Setup.marshmallow);
                Require(f.Setup.player.Held == null && !f.Setup.marshmallow.IsHeld
                    && f.Setup.marshmallow.transform.position == position,
                    "A locked desktop grab moved or selected a future item.");
                f.Setup.cigarette.Grabbed(true);
                Require(!f.Setup.cigarette.IsHeld && f.Session.state.cigarette == CigaretteCondition.Dropped,
                    "A rejected XR grab still changed cigarette state.");
                f.Setup.marshmallow.Grabbed(true);
                f.Setup.marshmallow.Use();
                Require(!f.Setup.marshmallow.IsHeld && f.Session.state.marshmallowsEaten == 0,
                    "A direct item method bypassed the active-station restriction.");
                var button = f.Child("Future firework button").AddComponent<PrototypeButton>();
                int pressed = 0;
                button.onPressed.AddListener(() => pressed++);
                button.Press();
                f.Setup.fireworks.Launch();
                Require(pressed == 0 && f.Session.state.fireworksLaunched == 0 && !f.Setup.fireworks.IsLaunching,
                    "A physical button or direct firework launch bypassed the sequence.");
                Require(f.Session.state.totalActions == actions,
                    "Blocked physical inputs changed the real action score.");
                Require(!PrototypeInteractionPolicy.CanMove(f.User.position, f.FireworksPoint.position)
                    && !PrototypeInteractionPolicy.CanReturn(),
                    "Shared movement or portal policy ignored its restriction.");
                var actualPortal = f.Child("Team return portal").AddComponent<CampusReturnPortal>();
                Require(!actualPortal.CanReturnNow && !PrototypeInteractionPolicy.TryBeginReturn(),
                    "The team's portal bypassed the shared completion condition.");

                PrototypeInteractionPolicy.GrabHandler = item => true;
                f.Setup.player.Grab(f.Setup.marshmallow);
                Require(f.Setup.player.Held == f.Setup.marshmallow, "The physical release test has no held item.");
                PrototypeInteractionPolicy.GrabHandler = item => false;
                f.Setup.player.DropHeld(false);
                Require(f.Setup.player.Held == null && !f.Setup.marshmallow.IsHeld,
                    "Changing station eligibility trapped an item in the player's hand.");

                var xr = f.Setup.bucket.gameObject.AddComponent<XRGrabInteractable>();
                var bridge = f.Setup.bucket.gameObject.AddComponent<XrItemBridge>();
                Invoke(bridge, "Awake");
                Require(!bridge.Process(null, xr), "The XR selection filter permits a locked item before selection.");
            }
        }

        private static void CheckVRLocomotion(Scene scene)
        {
            using (var f = new Fixture(scene, false))
            using (var policy = new PolicySnapshot())
            {
                var rig = f.Child("XR locomotion rig");
                var origin = rig.AddComponent<XROrigin>();
                origin.Origin = rig;
                var head = f.Child("Tracked head", rig.transform).AddComponent<Camera>();
                head.transform.localPosition = new Vector3(.1f, 1.6f, .15f);
                head.transform.localRotation = Quaternion.Euler(7f, 15f, 0f);
                origin.Camera = head;
                var move = rig.AddComponent<ContinuousMoveProvider>();
                var original = move.transformation;
                var constraint = rig.AddComponent<GuidanceLocomotionConstraint>();
                constraint.Configure(origin);
                constraint.RefreshProviders();
                Require(!ReferenceEquals(move.transformation, original),
                    "The XR continuous movement provider did not receive the shared permission check.");
                Vector3 before = rig.transform.position;
                Vector3 headPosition = head.transform.localPosition;
                Quaternion headRotation = head.transform.localRotation;
                PrototypeInteractionPolicy.MoveHandler = (from, to) => false;
                move.transformation.motion = new Vector3(1f, -.25f, 2f);
                move.transformation.Apply(new XRMovableBody(origin, null));
                Require(Vector3.Distance(rig.transform.position, before + Vector3.down * .25f) < .001f,
                    "Blocked controller movement changed XZ or suppressed the vertical gravity motion.");
                Require(head.transform.localPosition == headPosition
                    && Quaternion.Angle(head.transform.localRotation, headRotation) < .001f,
                    "The locomotion boundary rewrote the tracked head pose.");
                var teleport = rig.AddComponent<GuidanceTeleportationProvider>();
                teleport.origin = origin;
                var request = new TeleportRequest { destinationPosition = before + Vector3.forward * 20f };
                Require(!teleport.CanQueueTo(request) && !teleport.QueueTeleportRequest(request),
                    "A blocked teleport entered the locomotion queue.");
                PrototypeInteractionPolicy.MoveHandler = (from, to) => true;
                Require(teleport.CanQueueTo(request), "The teleport permission still rejects an unlocked route.");
                before = rig.transform.position;
                move.transformation.motion = new Vector3(1f, 0f, 2f);
                move.transformation.Apply(new XRMovableBody(origin, null));
                Require(Vector3.Distance(rig.transform.position, before + new Vector3(1f, 0f, 2f)) < .001f,
                    "Allowed controller movement no longer applies the original locomotion transformation.");
                constraint.enabled = false;
                Invoke(constraint, "OnDisable");
                Require(ReferenceEquals(move.transformation, original),
                    "Disabling the sequence did not restore the original movement provider.");
            }
        }

        private static void CheckArrivalAndBegin(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                Require(f.Sequence != null && f.Sequence.IsInitialized && f.Sequence.Phase == CampsiteSequencePhase.Intro,
                    "The campsite did not start with its tent narration.");
                Require(f.Narrator.IsPlaying && f.Narrator.CurrentCueId == "camp.tent"
                    && !f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage(),
                    "A station can start before the arrival narration is finished.");
                f.Sequence.Tick(30f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Intro && f.Narrator.CurrentSegmentIndex == 0,
                    "Sequence progression alone bypassed the narration playback boundary.");
                f.CompleteNarration();
                Require(f.Sequence.Phase == CampsiteSequencePhase.Travelling
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Cigarette,
                    "The first guided route does not lead to the cigarette.");
                f.ReturnPortal.Arm();
                f.MoveUser(f.ReturnPoint);
                Require(f.ReturnPortal.IsArmed && !f.ReturnPortal.TryEnter(),
                    "An armed return portal can leave before the guided route is complete.");
                f.Bridge.HandleChoice(new GuidanceChoice { action = "marshmallow.pickup" });
                Require(!f.Setup.marshmallow.IsHeld, "Calling a future UI choice directly bypassed station order.");
                Require(!f.Sequence.BeginCurrentStage() && !f.Sequence.FinishCurrentStage()
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow)
                    && !PrototypeInteractionPolicy.CanLaunch(f.Setup.fireworks),
                    "A future station can be started or used while travelling.");

                f.MoveUser(f.CigarettePoint);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.CanBegin, "The user's arrival alone unlocked Begin before the butterfly.");
                f.MoveUser(f.AwayPoint);
                f.Guide.transform.position = f.CigarettePoint.position + Vector3.up * f.Guide.flightHeight;
                f.Guide.Tick(.1f);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.CanBegin, "The butterfly's arrival alone unlocked Begin without the user.");
                f.MoveUser(f.CigarettePoint);
                f.Guide.Tick(.1f);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Narrating && f.Narrator.IsPlaying
                    && !f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage()
                    && !Buttons(f.View).Any(button => Label(button) == "Begin")
                    && !string.IsNullOrWhiteSpace(DialogueText(f.View)),
                    "Arrival showed an empty Begin before the area's introduction.");
                Require(!PrototypeInteractionPolicy.CanGrab(f.Setup.cigarette)
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow)
                    && !PrototypeInteractionPolicy.CanUse(f.Setup.bucket)
                    && !PrototypeInteractionPolicy.CanLaunch(f.Setup.fireworks),
                    "The introduction left physical interactions enabled.");
                f.ReadIntroduction(true, true, "Continue");
                Require(f.Sequence.CanBegin && f.Sequence.Phase == CampsiteSequencePhase.AwaitingBegin
                    && Buttons(f.View).Length == 1 && Label(Buttons(f.View)[0]) == "Continue"
                    && DialogueText(f.View) == f.Narrator.LastSubtitleText,
                    "The final cigarette introduction subtitle did not retain its Continue button.");
                CheckBeginBelowText(f.View, "Continue");
                Require(!PrototypeInteractionPolicy.CanGrab(f.Setup.cigarette),
                    "Finishing the introduction unlocked interaction before Begin was selected.");
                f.MoveUser(f.AwayPoint);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.BeginCurrentStage(), "A stale Begin can start after the user walks away.");
                f.Arrive(f.CigarettePoint);
                Require(!f.Narrator.IsPlaying && f.Sequence.CanBegin,
                    "Returning to the same introduction replayed its narration.");
                f.Press("Continue");
                Require(f.Sequence.Phase == CampsiteSequencePhase.Interacting
                    && f.Sequence.IsStageStarted(CampsiteGuidanceStage.Cigarette)
                    && !f.Sequence.BeginCurrentStage() && !f.Narrator.IsPlaying && !f.Narrator.AwaitingBegin,
                    "Begin replayed the introduction instead of directly releasing interaction.");
                f.MoveUser(f.CigarettePoint);
                for (int i = 0; i < 30; i++) f.Sequence.Tick(.1f);
                f.MoveUser(f.AwayPoint);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Cigarette
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Cigarette),
                    "Visiting and leaving still completes a station without a decision.");
                Require(!PrototypeInteractionPolicy.CanMove(f.CigarettePoint.position, f.FireworksPoint.position),
                    "Movement eligibility lets the player bypass the remaining route to the fireworks.");
            }
        }

        private static void CheckTentExitGate(Scene scene)
        {
            using (var f = new Fixture(scene, true, true))
            {
                Transform exit = f.Sequence.tentExit;
                Vector3 inside = exit.position - exit.forward * .2f + Vector3.up * 1.6f;
                Vector3 outside = exit.position + exit.forward * .2f + Vector3.up * 1.6f;
                f.User.position = outside;
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.HasExitedTent && f.Narrator.CurrentCueId == "camp.tent",
                    "Walking across the exit skipped the opening tent narration.");
                f.User.position = inside;
                f.Guide.transform.position = f.CigarettePoint.position + Vector3.up * f.Guide.flightHeight;
                f.Guide.Tick(.1f);
                f.CompleteNarration();
                Require(f.Sequence.Phase == CampsiteSequencePhase.Travelling && !f.Sequence.HasExitedTent
                    && !f.Narrator.IsPlaying && !f.Sequence.CanBegin && Buttons(f.View).Length == 0,
                    "The cigarette introduction began while the user was still inside the tent.");
                for (int i = 0; i < 10; i++) f.Sequence.Tick(1f);
                Require(!f.Sequence.HasExitedTent && !f.Narrator.IsPlaying
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.Cigarette)
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow),
                    "Waiting inside the tent or the butterfly's arrival unlocked the cigarette stop.");
                Require(PrototypeInteractionPolicy.CanMove(inside, outside),
                    "The opening movement boundary prevents walking out of the tent.");
                Quaternion rotation = f.User.rotation;
                string state = JsonUtility.ToJson(f.Session.state);
                f.User.position = outside;
                f.Setup.player.RefreshHeldPose();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.HasExitedTent && f.Sequence.Phase == CampsiteSequencePhase.Narrating
                    && f.Narrator.CurrentCueId == "camp.cigarette" && f.Narrator.IsPlaying
                    && !f.Sequence.CanBegin && Buttons(f.View).Length == 0,
                    "Leaving the tent did not start the cigarette introduction before Continue.");
                Require(f.User.position == outside && Quaternion.Angle(f.User.rotation, rotation) < .001f
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Detecting the tent exit moved the user's head or changed a physical choice.");
                f.ReadIntroduction(true, true, "Continue");
                Require(!f.Sequence.IsStageStarted(CampsiteGuidanceStage.Cigarette),
                    "Exiting the tent started cigarette interaction without Continue.");

                f.User.position = inside;
                f.Session.state = new ChoiceState();
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.HasExitedTent && f.Sequence.Phase == CampsiteSequencePhase.Intro
                    && f.Narrator.CurrentCueId == "camp.tent" && !f.Sequence.CanBegin,
                    "Restarting the camp retained the previous run's tent-exit permission.");
                f.CompleteNarration();
                Require(!f.Sequence.HasExitedTent && !f.Narrator.IsPlaying
                    && f.Sequence.Phase == CampsiteSequencePhase.Travelling,
                    "A restarted run replayed the cigarette introduction before leaving the tent.");
            }
        }

        private static void CheckOrderedSequence(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Session.state.cigarette == CigaretteCondition.Dropped
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "An unsafe cigarette decision did not advance to the campfire.");
                Require(!f.Sequence.IsStageStarted(CampsiteGuidanceStage.Campfire)
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow),
                    "The next object unlocked before its own arrival and Begin.");
                f.StartStation(f.FirePoint, CampsiteGuidanceStage.Campfire);
                Require(PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow),
                    "The campfire narration finished but the physical marshmallow stayed locked.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                f.Sequence.Tick(.2f);
                Require(f.Setup.player.Held == f.Setup.marshmallow && f.Setup.marshmallow.IsHeld
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Picking up the physical marshmallow prematurely completed roasting.");
                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.goldenSeconds;
                f.Sequence.Tick(.2f);
                Require(VisibleText(f.View).IndexOf("ready", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The actual ready marshmallow state is no longer reflected in its interaction.");
                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.burntSeconds;
                f.Sequence.Tick(.2f);
                Require(VisibleText(f.View).IndexOf("burnt", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The actual burnt marshmallow state is no longer reflected in its interaction.");
                f.Setup.marshmallow.Use();
                f.Sequence.Tick(.2f);
                Require(f.Session.state.marshmallowsEaten == 1 && f.Session.state.burntMarshmallowsEaten == 1
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Eating changed the real roast score or ended roasting without a finish decision.");
                f.MoveUser(f.AwayPoint);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.IsRoastingFinished && !f.Sequence.BucketPromptAvailable,
                    "Walking away automatically skipped the explicit end-of-roasting decision.");
                f.MoveUser(f.FirePoint);
                f.Sequence.Tick(.2f);
                f.Press("Finish roasting");
                f.Sequence.Tick(.2f);
                Require(f.Sequence.IsRoastingFinished && f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && f.Setup.player.Held == null && !f.Setup.marshmallow.IsHeld,
                    "Explicit finish did not release the desktop marshmallow and lead to the bucket.");
                Require(f.Session.state.campfire == FireCondition.Burning,
                    "Completing roasting fabricated an extinguished campfire.");
                Require(!f.Sequence.CanBegin && !f.Sequence.BucketPromptAvailable,
                    "The bucket prompt appeared before its separate arrival boundary.");
                f.StartStation(f.BucketPoint, CampsiteGuidanceStage.WaterBucket);
                Require(f.Sequence.BucketPromptAvailable && PrototypeInteractionPolicy.CanGrab(f.Setup.bucket),
                    "The bucket did not become usable when both user and guide arrived after roasting.");
                f.Setup.player.Grab(f.Setup.bucket);
                Require(f.Setup.player.Held == f.Setup.bucket && f.Setup.bucket.IsHeld,
                    "The guided bucket choice did not grab the physical bucket.");
                Require(!VisibleText(f.View).Contains("Hold R"),
                    "A keyboard pouring instruction returned to the VR interaction.");
                f.Setup.player.DropHeld(false);
                f.Press("Leave");
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && f.Session.state.campfire == FireCondition.Burning,
                    "The unsafe water decision changed fire state or did not continue to fireworks.");
                f.StartStation(f.FireworksPoint, CampsiteGuidanceStage.Fireworks);
                Require(PrototypeInteractionPolicy.CanLaunch(f.Setup.fireworks),
                    "The fireworks remain locked after arrival, Begin and narration.");
                f.LeaveFireworksNaturally();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal && !f.Sequence.CanReturn,
                    "The return portal opened before its own arrival and narration.");
                f.StartStation(f.ReturnPoint, CampsiteGuidanceStage.ReturnPortal);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Complete && f.Sequence.CanReturn
                    && PrototypeInteractionPolicy.CanReturn(),
                    "Completing the guided return did not open the shared portal boundary.");
                var actualPortal = f.Child("Completed team return portal").AddComponent<CampusReturnPortal>();
                Require(actualPortal.CanReturnNow && PrototypeInteractionPolicy.TryBeginReturn()
                    && PrototypeInteractionPolicy.ReturnInProgress && !actualPortal.CanReturnNow
                    && !PrototypeInteractionPolicy.TryBeginReturn(),
                    "Two portal paths can begin competing scene transitions.");
                PrototypeInteractionPolicy.CancelReturn();
                Require(f.Session.state.cigarette == CigaretteCondition.Dropped
                    && f.Session.state.campfire == FireCondition.Burning
                    && f.Session.state.marshmallowsEaten == 1 && !f.Session.state.fireworksEverLaunched,
                    "Narration or ordered progress replaced the player's physical choices.");
                Require(f.ReturnPortal.ResolveEndingScene() == "3_CampusFire_Main",
                    "The ordered tour overrode the actual two unresolved hazards.");
                f.Session.CaptureFinalResults();
                int missed = f.Session.MissedChoices;
                int total = f.Session.ActionsPerformed;
                f.Session.CaptureFinalResults();
                Require(f.Session.hasFinalResult && f.Session.MissedChoices == missed
                    && f.Session.ActionsPerformed == total && missed == 2,
                    "Capturing the result more than once replaced the physical score.");
            }
        }

        private static void CheckRoastingCompletionGate(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                Require(!f.Sequence.RoastingReady && !f.Sequence.CanFinishRoasting
                    && !Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "Finish roasting is visible immediately after the campfire Begin.");
                Require(!f.Sequence.FinishCurrentStage(), "The generic finish method skips an unroasted marshmallow.");
                f.Sequence.FinishRoasting();
                f.Sequence.MarkExplored(CampsiteGuidanceStage.Campfire);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire && !f.Sequence.IsRoastingFinished,
                    "A direct roasting finish or legacy completion callback bypasses actual cooking.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                f.Setup.marshmallow.Use();
                f.Sequence.Tick(.2f);
                Require(f.Session.state.rawMarshmallowsEaten == 1 && !f.Sequence.RoastingReady
                    && !f.Sequence.CanFinishRoasting && !f.Sequence.FinishCurrentStage()
                    && !Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "Eating a raw serving unlocked or completed roasting.");
                f.Sequence.FinishRoasting();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Eating a raw serving lets the direct roasting method skip the station.");

                f.Setup.player.DropHeld(false);
                f.Setup.marshmallow.ResetPrototype();
                f.Setup.player.Grab(f.Setup.marshmallow);
                f.Setup.marshmallow.cookedSeconds = Mathf.Max(0f, f.Setup.marshmallow.goldenSeconds - .01f);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.RoastingReady && !f.Sequence.CanFinishRoasting,
                    "A serving below the actual cooked threshold unlocked Finish roasting.");
                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.goldenSeconds;
                f.Sequence.Tick(.2f);
                Require(f.Sequence.RoastingReady && f.Sequence.CanFinishRoasting
                    && Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "A physically cooked serving did not unlock Finish roasting.");
                f.Setup.marshmallow.Use();
                f.Setup.player.DropHeld(false);
                f.Setup.marshmallow.ResetPrototype();
                f.Sequence.Tick(.2f);
                Require(f.Setup.marshmallow.cookedSeconds == 0f && !f.Setup.marshmallow.IsHeld
                    && f.Sequence.RoastingReady && f.Sequence.CanFinishRoasting
                    && Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "Eating, replenishing or dropping a cooked serving erased roasting completion eligibility.");
                f.Press("Finish roasting");
                Require(f.Sequence.IsRoastingFinished && f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket,
                    "A previously cooked serving cannot finish after it has been eaten or put down.");
            }
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.Setup.marshmallow.cookedSeconds = f.Setup.marshmallow.burntSeconds;
                f.Sequence.Tick(.2f);
                Require(f.Sequence.RoastingReady && f.Sequence.CanFinishRoasting
                    && Buttons(f.View).Any(button => Label(button) == "Finish roasting"),
                    "A burnt serving did not count as having completed roasting.");
                var staleFinish = Buttons(f.View).Single(button => Label(button) == "Finish roasting").onClick;
                f.Setup.marshmallow.ResetPrototype();
                f.Session.state = new ChoiceState { cigarette = CigaretteCondition.Held, campfire = FireCondition.Burning };
                f.Sequence.Tick(.2f);
                staleFinish.Invoke();
                f.Sequence.FinishRoasting();
                Require(f.Sequence.Phase == CampsiteSequencePhase.Intro && !f.Sequence.RoastingReady
                    && !f.Sequence.CanFinishRoasting && !f.Sequence.IsRoastingFinished,
                    "Resetting the session retained a previous run's roasting readiness.");
            }
        }

        private static void CheckRoastingCounterReadiness(Scene scene)
        {
            foreach (bool burnt in new[] { false, true })
            using (var f = new Fixture(scene))
            {
                f.Session.state.goldenMarshmallowsEaten = 1;
                f.Session.state.burntMarshmallowsEaten = 1;
                f.OpenCampfire();
                Require(!f.Sequence.RoastingReady,
                    "Cooked-serving counters from before this campfire Begin unlocked Finish roasting.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                f.Setup.marshmallow.cookedSeconds = burnt ? f.Setup.marshmallow.burntSeconds : f.Setup.marshmallow.goldenSeconds;
                f.Setup.marshmallow.Use();
                f.Setup.player.DropHeld(false);
                f.Setup.marshmallow.ResetPrototype();
                f.Sequence.Tick(.2f);
                Require(f.Sequence.RoastingReady && f.Sequence.CanFinishRoasting,
                    "A cooked serving eaten between sequence ticks was lost when the next serving reset.");
                Require(f.Sequence.FinishCurrentStage() && f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket,
                    "The direct finish method rejected a recorded cooked serving.");
            }
        }

        private static void CheckRoastingChoicesAndFreshServing(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.Setup.player.Grab(f.Setup.marshmallow);
                var item = f.Setup.marshmallow;
                item.cookedSeconds = item.goldenSeconds;
                f.Sequence.Tick(.2f);
                RequireLabels(f.View, "Keep roasting", "Take a bite", "Roast another", "Finish roasting");
                CheckRoastingChoiceLayouts(f);
                var eat = Buttons(f.View).Single(button => Label(button) == "Take a bite").onClick;
                var oldFinish = Buttons(f.View).Single(button => Label(button) == "Finish roasting").onClick;
                int serving = item.ServingRevision;
                eat.Invoke();
                f.Sequence.Tick(.2f);
                Require(item.IsServingConsumed && item.ServingRevision > serving && !item.marshmallow.enabled
                    && f.Session.state.marshmallowsEaten == 1 && f.Session.state.goldenMarshmallowsEaten == 1
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "Take a bite did not consume one actual golden serving while staying at the campfire.");
                string eatenState = JsonUtility.ToJson(f.Session.state);
                eat.Invoke();
                item.Use();
                Require(JsonUtility.ToJson(f.Session.state) == eatenState,
                    "A repeated or stale bite counted the same serving more than once.");
                Require(DialogueText(f.View) == f.Director.library.Find("campfire.eaten").text,
                    "Eating a golden serving lost the authored invitation to roast another.");
                RequireLabels(f.View, "Roast another", "Finish roasting");
                Require(item.RequireServingChoice, "Guided roasting still allows an automatic replacement serving.");
                item.roastPoint = f.Child("Roast sample outside the heat").transform;
                item.roastPoint.position = f.Root.transform.position + Vector3.up * 100f;
                ExpireServingDelay(item);
                Invoke(item, "Update");
                Require(item.IsServingConsumed && !item.marshmallow.enabled,
                    "The consumed serving automatically returned before Roast another was chosen.");

                var fresh = Buttons(f.View).Single(button => Label(button) == "Roast another").onClick;
                Vector3 position = item.transform.position;
                Quaternion rotation = item.transform.rotation;
                Transform parent = item.transform.parent;
                bool kinematic = item.Body.isKinematic;
                bool gravity = item.Body.useGravity;
                bool collisions = item.Body.detectCollisions;
                serving = item.ServingRevision;
                fresh.Invoke();
                f.Sequence.Tick(.2f);
                Require(!item.IsServingConsumed && item.ServingRevision > serving && item.cookedSeconds == 0f
                    && item.marshmallow.enabled && item.IsHeld && !item.HeldInXR && f.Setup.player.Held == item,
                    "Roast another did not replace the consumed serving with a held fresh marshmallow.");
                Require(item.transform.parent == parent && item.transform.position == position
                    && Quaternion.Angle(item.transform.rotation, rotation) < .001f
                    && item.Body.isKinematic == kinematic && item.Body.useGravity == gravity
                    && item.Body.detectCollisions == collisions && JsonUtility.ToJson(f.Session.state) == eatenState,
                    "Refreshing a held serving changed its pose owner, physics or interaction score.");
                RequireFreshWhite(item);
                RequireLabels(f.View, "Finish roasting");
                Require(string.IsNullOrEmpty(DialogueText(f.View)) && f.Sequence.RoastingReady,
                    "A fresh raw serving was mislabeled ready or erased the previous roasting completion.");
                serving = item.ServingRevision;
                item.cookedSeconds = .5f;
                fresh.Invoke();
                eat.Invoke();
                oldFinish.Invoke();
                Require(item.ServingRevision == serving && item.cookedSeconds == .5f && !item.IsServingConsumed
                    && JsonUtility.ToJson(f.Session.state) == eatenState
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "A stale serving choice reset, consumed or finished the newer raw serving.");
                var finish = Buttons(f.View).Single(button => Label(button) == "Finish roasting").onClick;
                finish.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket && f.Setup.player.Held == null,
                    "Only the current Finish roasting choice should advance to the bucket.");
                f.StartStation(f.BucketPoint, CampsiteGuidanceStage.WaterBucket);
                string waterState = JsonUtility.ToJson(f.Session.state);
                fresh.Invoke(); eat.Invoke(); oldFinish.Invoke(); finish.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && JsonUtility.ToJson(f.Session.state) == waterState,
                    "Old roasting choices affected the following bucket station.");

                item.ResetPrototype();
                f.Session.state = new ChoiceState { cigarette = CigaretteCondition.Held, campfire = FireCondition.Burning };
                f.Sequence.Tick(.2f);
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                Require(f.Sequence.FinishCurrentStage(), "The new-run fixture could not leave its cigarette station.");
                f.StartStation(f.FirePoint, CampsiteGuidanceStage.Campfire);
                f.Setup.player.Grab(item);
                item.cookedSeconds = item.goldenSeconds;
                f.Sequence.Tick(.2f);
                string newState = JsonUtility.ToJson(f.Session.state);
                serving = item.ServingRevision;
                fresh.Invoke(); eat.Invoke(); oldFinish.Invoke(); finish.Invoke();
                Require(item.ServingRevision == serving && !item.IsServingConsumed
                    && JsonUtility.ToJson(f.Session.state) == newState
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "A previous run's roasting choices acted on the new run's serving.");
            }
        }

        private static void CheckKeepRoastingAndBurntChoices(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.Setup.player.Grab(f.Setup.marshmallow);
                var item = f.Setup.marshmallow;
                item.cookedSeconds = item.goldenSeconds;
                f.Sequence.Tick(.2f);
                var keep = Buttons(f.View).Single(button => Label(button) == "Keep roasting").onClick;
                var oldFinish = Buttons(f.View).Single(button => Label(button) == "Finish roasting").onClick;
                string state = JsonUtility.ToJson(f.Session.state);
                keep.Invoke();
                f.Sequence.Tick(.2f);
                RequireLabels(f.View, "Finish roasting");
                Require(string.IsNullOrEmpty(DialogueText(f.View)) && item.cookedSeconds == item.goldenSeconds
                    && item.IsHeld && item.enabled && item.marshmallow.enabled
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Keep roasting stopped cooking, reset the serving or changed the physical score.");
                item.cookedSeconds = Mathf.Min(item.burntSeconds - .1f, item.goldenSeconds + .5f);
                f.Sequence.Tick(.2f);
                RequireLabels(f.View, "Finish roasting");
                Require(string.IsNullOrEmpty(DialogueText(f.View)),
                    "The dismissed ready prompt returned while the same serving kept roasting.");
                item.cookedSeconds = item.burntSeconds;
                Invoke(item, "ApplyColour");
                f.Sequence.Tick(.2f);
                Require(DialogueText(f.View).IndexOf("burnt", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Continuing to cook did not reveal the actual burnt state.");
                RequireLabels(f.View, "Roast another", "Finish roasting");
                keep.Invoke(); oldFinish.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire
                    && item.cookedSeconds == item.burntSeconds,
                    "A stale ready choice dismissed or finished the later burnt state.");
                f.Press("Roast another");
                f.Sequence.Tick(.2f);
                Require(item.cookedSeconds == 0f && item.IsHeld && f.Setup.player.Held == item
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Replacing a burnt marshmallow counted it as eating or dropped the stick.");
                RequireFreshWhite(item);
                RequireLabels(f.View, "Finish roasting");
            }
        }

        private static void CheckRawServingChoiceAndXRRefresh(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.Setup.player.Grab(f.Setup.marshmallow);
                var item = f.Setup.marshmallow;
                Require(Buttons(f.View).Length == 0, "A first raw serving still exposes cooking choices.");
                item.Use();
                f.Sequence.Tick(.2f);
                Require(item.IsServingConsumed && !f.Sequence.RoastingReady && !f.Sequence.CanFinishRoasting,
                    "Eating a raw serving incorrectly completed roasting.");
                RequireLabels(f.View, "Roast another");
                string state = JsonUtility.ToJson(f.Session.state);
                f.Press("Roast another");
                f.Sequence.Tick(.2f);
                Require(!item.IsServingConsumed && item.cookedSeconds == 0f && item.IsHeld
                    && Buttons(f.View).Length == 0 && !f.Sequence.RoastingReady
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "A raw bite left the user stuck, fabricated eating statistics or unlocked Finish roasting.");
                RequireFreshWhite(item);
                item.cookedSeconds = item.goldenSeconds;
                f.Sequence.Tick(.2f);
                RequireLabels(f.View, "Keep roasting", "Take a bite", "Roast another", "Finish roasting");
                Require(f.Session.state.marshmallowsEaten == 1 && f.Session.state.rawMarshmallowsEaten == 1
                    && f.Session.state.goldenMarshmallowsEaten == 0,
                    "Refreshing and cooking another serving counted it as a second bite.");
            }
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                var item = f.Setup.marshmallow;
                item.Grabbed(true);
                var hand = f.Child("XR hand pose owner").transform;
                hand.position = f.User.position;
                item.transform.SetParent(hand, true);
                item.transform.SetPositionAndRotation(f.User.position + new Vector3(.2f, -.3f, .6f), Quaternion.Euler(15f, 35f, 5f));
                item.Body.isKinematic = true;
                item.Body.useGravity = false;
                item.Body.position = item.transform.position;
                item.Body.rotation = item.transform.rotation;
                item.cookedSeconds = item.burntSeconds;
                Invoke(item, "ApplyColour");
                f.Sequence.Tick(.2f);
                RequireLabels(f.View, "Roast another", "Finish roasting");
                Vector3 position = item.transform.position;
                Quaternion rotation = item.transform.rotation;
                string state = JsonUtility.ToJson(f.Session.state);
                f.Press("Roast another");
                f.Sequence.Tick(.2f);
                Require(item.IsHeld && item.HeldInXR && f.Setup.player.Held == null
                    && item.transform.parent == hand && item.transform.position == position
                    && Quaternion.Angle(item.transform.rotation, rotation) < .001f
                    && item.Body.isKinematic && !item.Body.useGravity && item.cookedSeconds == 0f
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Refreshing an XR-held serving changed its hand owner, pose, physics or score.");
                RequireFreshWhite(item);
            }
        }

        private static void CheckServingChoiceOwnership(Scene scene)
        {
            using (var f = new Fixture(scene, false))
            {
                var item = f.Setup.marshmallow;
                item.roastPoint = f.Child("Legacy roast point").transform;
                item.roastPoint.position = f.Root.transform.position + Vector3.up * 100f;
                Require(!item.RequireServingChoice, "A standalone item unexpectedly requires the guided serving menu.");
                item.Use();
                ExpireServingDelay(item);
                Invoke(item, "Update");
                Require(!item.IsServingConsumed && item.marshmallow.enabled && item.cookedSeconds == 0f,
                    "The standalone item's legacy automatic serving no longer works.");
            }
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                Require(f.Setup.marshmallow.RequireServingChoice,
                    "The guided scene did not take ownership of explicit serving choices.");
                Invoke(f.Sequence, "OnDisable");
                Require(!f.Setup.marshmallow.RequireServingChoice,
                    "Disabling guidance did not restore the item's previous serving policy.");
            }
        }

        private static void ExpireServingDelay(MarshmallowItem item)
        {
            typeof(MarshmallowItem).GetField("nextServingTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(item, Time.time - 1f);
        }

        private static void RequireFreshWhite(MarshmallowItem item)
        {
            var block = new MaterialPropertyBlock();
            item.marshmallow.GetPropertyBlock(block);
            Require(item.marshmallow.enabled && item.cookedSeconds == 0f
                && Vector4.Distance(block.GetColor(Shader.PropertyToID("_BaseColor")), Color.white) < .001f
                && Vector4.Distance(block.GetColor(Shader.PropertyToID("_Color")), Color.white) < .001f,
                "The replacement marshmallow was not immediately restored to a visible white serving.");
        }

        private static void RequireLabels(GuidanceView view, params string[] expected)
        {
            Require(Buttons(view).Select(Label).SequenceEqual(expected),
                "Unexpected roasting choices: " + string.Join(", ", Buttons(view).Select(Label)));
        }

        private static void CheckRoastingChoiceLayouts(Fixture f)
        {
            var canvas = f.View.GetComponentInChildren<Canvas>(true);
            var camera = f.User.GetComponent<Camera>();
            var target = new RenderTexture(1600, 900, 0);
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                f.View.ApplyNaturalStyle();
                CheckFourChoiceLayout(f.View, false);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.DestroyImmediate(target);
            }
            var panel = f.Root.AddComponent<GuidanceVRPanel>();
            panel.Configure(camera, f.View);
            CheckFourChoiceLayout(f.View, true);
        }

        private static void CheckFourChoiceLayout(GuidanceView view, bool world)
        {
            Canvas.ForceUpdateCanvases();
            var buttons = Buttons(view);
            Require(buttons.Length == 4, "The roasting card does not contain four ready choices.");
            var bounds = buttons.Select(button =>
            {
                var corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Transform parent = button.transform.parent;
                Vector3 min = parent.InverseTransformPoint(corners[0]);
                Vector3 max = parent.InverseTransformPoint(corners[2]);
                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }).ToArray();
            Require(Mathf.Abs(bounds[0].center.y - bounds[1].center.y) < 1f
                && Mathf.Abs(bounds[2].center.y - bounds[3].center.y) < 1f
                && bounds[0].center.y > bounds[2].center.y
                && bounds[0].center.x < bounds[1].center.x && bounds[2].center.x < bounds[3].center.x,
                "The four roasting choices are not arranged in two clear rows of two.");
            for (int i = 0; i < bounds.Length; i++)
            {
                Require(bounds[i].width > 0f && bounds[i].height > 0f, "A roasting choice has no usable target area.");
                for (int j = i + 1; j < bounds.Length; j++)
                    Require(!bounds[i].Overlaps(bounds[j]), "Two roasting choice targets overlap.");
                if (world)
                    Require(buttons[i].GetComponent<RectTransform>().rect.height * Mathf.Abs(buttons[i].transform.lossyScale.y) >= .06f,
                        "A roasting choice is too small for the world-space controller target.");
            }
            CheckVisibleTextFits(view, world ? "world-space roasting choices" : "desktop roasting choices");
        }

        private static void CheckCampfireTableAccess(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "The table-access regression did not reach the campfire stage.");
                f.FirePoint.position = f.Root.transform.position + new Vector3(-1.0197f, 0f, 2.3921f);
                f.Setup.campfire.transform.position = f.FirePoint.position;
                var table = f.Child("Fixed marshmallow table").transform;
                table.position = f.Root.transform.position + new Vector3(.5414f, .7f, 3.1720f);
                f.Setup.marshmallowTable = table;
                Vector3 stickPosition = f.Root.transform.position + new Vector3(.2614f, .8f, 3.0520f);
                f.Setup.marshmallow.transform.position = stickPosition;
                f.Setup.marshmallow.Body.position = stickPosition;
                f.Setup.player.reach = 3f;
                Vector3 tableSide = f.Root.transform.position + new Vector3(.8f, 1.6f, 2.4f);
                Require(HorizontalDistance(tableSide, f.FirePoint.position) > f.Sequence.visitRadius
                    && HorizontalDistance(tableSide, table.position) < f.Sequence.visitRadius
                    && Vector3.Distance(tableSide, stickPosition) < f.Setup.player.reach,
                    "The regression no longer represents the authored table edge outside the approach marker.");

                f.Arrive(f.FirePoint);
                f.ReadIntroduction();
                f.User.position = tableSide;
                f.Sequence.Tick(.2f);
                int pickups = f.Session.state.itemsPickedUp;
                Require(!f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage()
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow),
                    "The table's interaction area unlocked arrival or grabbing before the campfire Begin.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                Require(f.Setup.player.Held == null && f.Session.state.itemsPickedUp == pickups,
                    "The desktop grab bypassed the campfire Begin boundary at the table.");
                f.Arrive(f.FirePoint);
                f.Press("Begin");
                f.User.position = tableSide;
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Interacting
                    && f.Sequence.CanInteractWith(CampsiteGuidanceStage.Campfire)
                    && PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow),
                    "A valid stick at the far side of its table remained locked after Begin.");
                Require(!PrototypeInteractionPolicy.CanGrab(f.Setup.bucket)
                    && !PrototypeInteractionPolicy.CanLaunch(f.Setup.fireworks),
                    "Expanding the campfire table area unlocked later stations.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                Require(f.Setup.player.Held == f.Setup.marshmallow && f.Setup.marshmallow.IsHeld
                    && PrototypeInteractionPolicy.CanUse(f.Setup.marshmallow),
                    "The in-reach desktop grab or held-item use is blocked at the authored table edge.");
                f.Setup.marshmallow.Use();
                Require(f.Session.state.rawMarshmallowsEaten == 1,
                    "Using the held marshmallow at the table edge did not reach the real item method.");
                f.Setup.player.DropHeld(false);
                Vector3 fixedTablePosition = table.position;
                Vector3 outside = f.Root.transform.position + new Vector3(5f, 1.6f, 3f);
                f.User.position = outside;
                f.Setup.marshmallow.transform.position = outside + Vector3.down * .8f;
                f.Setup.marshmallow.Body.position = f.Setup.marshmallow.transform.position;
                f.Sequence.Tick(.2f);
                Require(HorizontalDistance(outside, f.FirePoint.position) > f.Sequence.visitRadius
                    && HorizontalDistance(outside, table.position) > f.Sequence.visitRadius
                    && !f.Sequence.CanInteractWith(CampsiteGuidanceStage.Campfire)
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow)
                    && !PrototypeInteractionPolicy.CanUse(f.Setup.marshmallow),
                    "Moving the portable stick moved the allowed campfire area outside its fixed anchors.");
                f.Setup.player.Grab(f.Setup.marshmallow);
                Require(f.Setup.player.Held == null && table.position == fixedTablePosition,
                    "A nearby stick can be grabbed outside both fixed interaction circles.");
                f.Setup.marshmallow.transform.position = stickPosition;
                f.Setup.marshmallow.Body.position = stickPosition;
                f.User.position = tableSide;
                f.Sequence.Tick(.2f);
                Require(PrototypeInteractionPolicy.CanGrab(f.Setup.marshmallow)
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire && !f.Sequence.IsRoastingFinished,
                    "Returning to the fixed table did not restore access or changed the guided stage.");
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void CheckWaterBucketArrivalAndChoices(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.MakeRoastingReady();
                var oldRoast = Buttons(f.View).Single(button => Label(button) == "Finish roasting").onClick;
                oldRoast.Invoke();
                f.Sequence.Tick(.2f);
                var card = f.View.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Butterfly dialogue card");
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && f.Sequence.Phase == CampsiteSequencePhase.Travelling && !card.gameObject.activeSelf
                    && Buttons(f.View).Length == 0 && !f.Sequence.BucketPromptAvailable
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.bucket)
                    && !f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage(),
                    "Finishing roasting exposed a blank bucket Begin or unlocked the bucket before arrival.");
                f.Guide.transform.position = f.BucketPoint.position + Vector3.up * f.Guide.flightHeight;
                f.Guide.Tick(.1f);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Travelling
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.WaterBucket)
                    && !PrototypeInteractionPolicy.CanGrab(f.Setup.bucket),
                    "The butterfly's arrival alone unlocked the bucket.");
                f.Guide.transform.position = f.FirePoint.position + Vector3.up * f.Guide.flightHeight;
                f.MoveUser(f.BucketPoint);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Travelling
                    && !f.Sequence.BucketPromptAvailable && !PrototypeInteractionPolicy.CanGrab(f.Setup.bucket)
                    && !f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage(),
                    "The user's arrival alone unlocked the bucket.");
                f.Guide.transform.position = f.BucketPoint.position + Vector3.up * f.Guide.flightHeight;
                f.Guide.Tick(.1f);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Interacting
                    && f.Sequence.IsStageStarted(CampsiteGuidanceStage.WaterBucket)
                    && f.Sequence.BucketPromptAvailable && PrototypeInteractionPolicy.CanGrab(f.Setup.bucket)
                    && !f.Sequence.CanBegin && !f.Sequence.BeginCurrentStage()
                    && !f.Narrator.IsPlaying && !f.Narrator.AwaitingBegin,
                    "Both arrivals did not directly open the physical bucket interaction.");
                CheckBucketPanel(f, "bucket.ready");
                oldRoast.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.WaterBucket),
                    "A stale Finish roasting callback completed the newly opened bucket station.");
                f.Setup.player.Grab(f.Setup.bucket);
                f.Sequence.Tick(.2f);
                Require(f.Setup.player.Held == f.Setup.bucket && f.Setup.bucket.IsHeld
                    && PrototypeInteractionPolicy.CanUse(f.Setup.bucket),
                    "The bucket cannot be grabbed and used after automatic arrival.");
                CheckBucketPanel(f, "bucket.held");
                f.Setup.bucket.waterLeft = 0f;
                f.Setup.bucket.refillPoint = f.Child("Actual refill point").transform;
                f.Setup.bucket.refillPoint.position = f.Setup.bucket.transform.position;
                int refills = f.Session.state.bucketRefills;
                f.Setup.bucket.Use();
                Require(f.Setup.bucket.waterLeft == f.Setup.bucket.capacity
                    && f.Session.state.bucketRefills == refills + 1,
                    "The arrival gate blocked the bucket's real refill/use method.");
                Require(f.Setup.campfire.ReceiveWater(f.Setup.campfire.transform.position, FireProgress.FlameThreshold),
                    "The real fire did not accept its first water stage.");
                f.Sequence.Tick(.2f);
                Require(f.Session.state.campfire == FireCondition.HotEmbers
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket,
                    "Hot embers were treated as a finished cold fire.");
                CheckBucketPanel(f, "bucket.embers");
                int actions = f.Session.state.totalActions;
                var oldLeave = Buttons(f.View).Single(button => Label(button) == "Leave").onClick;
                oldLeave.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && f.Session.state.campfire == FireCondition.HotEmbers
                    && f.Session.state.totalActions == actions,
                    "Leaving the bucket changed the player's unsafe fire decision or physical action score.");
                f.Setup.player.DropHeld(false);
                f.StartStation(f.FireworksPoint, CampsiteGuidanceStage.Fireworks);
                oldLeave.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && f.Sequence.Phase == CampsiteSequencePhase.Interacting
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks),
                    "A stale bucket Leave callback completed the later fireworks station.");
                f.LeaveFireworksNaturally();
                oldLeave.Invoke();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal && !f.Sequence.CanReturn
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.ReturnPortal),
                    "A stale bucket Leave callback skipped the guided return boundary.");
            }
            using (var f = new Fixture(scene))
            {
                f.OpenCampfire();
                f.MakeRoastingReady();
                f.Sequence.FinishRoasting();
                Require(f.Setup.campfire.ReceiveWater(f.Setup.campfire.transform.position, FireProgress.ColdThreshold),
                    "The real fire did not reach its physically cold state.");
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.WaterBucket
                    && f.Sequence.Phase == CampsiteSequencePhase.Travelling,
                    "A cold fire skipped the separate bucket arrival boundary.");
                int actions = f.Session.state.totalActions;
                f.Arrive(f.BucketPoint);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && f.Session.state.campfire == FireCondition.Cold
                    && f.Session.state.UnsafeStations(EndingRule.CampsiteFinal) == 1
                    && f.Session.state.totalActions == actions && !f.Session.hasFinalResult,
                    "A physically cold fire did not automatically advance while retaining the real score.");
                Require(!f.Narrator.IsPlaying && !Buttons(f.View).Any(button => Label(button) == "Begin"),
                    "An already cold bucket stop still exposed a blank Begin.");
                f.Session.state = new ChoiceState { cigarette = CigaretteCondition.Held, campfire = FireCondition.Burning };
                f.Sequence.Tick(.2f);
                Require(f.Sequence.Phase == CampsiteSequencePhase.Intro
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.WaterBucket)
                    && !f.Sequence.BucketPromptAvailable && !PrototypeInteractionPolicy.CanGrab(f.Setup.bucket),
                    "Resetting the route left the automatically opened bucket unlocked.");
            }
        }

        private static void CheckBucketPanel(Fixture f, string lineId)
        {
            var line = f.Director.library.Find(lineId);
            Require(line != null && DialogueText(f.View) == line.text
                && Buttons(f.View).Length == 1 && Label(Buttons(f.View)[0]) == "Leave"
                && !string.IsNullOrWhiteSpace(DialogueText(f.View)),
                "The bucket panel lost its authored status text or Leave choice: " + lineId);
            Require(!VisibleText(f.View).Contains("Hold R")
                && !Buttons(f.View).Any(button => Label(button) == "Begin" || Label(button) == "Continue"),
                "The bucket restored a blank Begin/Continue or a keyboard instruction.");
        }

        private static void CheckFireworksNaturalExit(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.OpenFireworksRoute();
                f.Arrive(f.FireworksPoint);
                f.ReadIntroduction();
                string state = JsonUtility.ToJson(f.Session.state);
                f.MoveOutsideFireworks();
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.Fireworks)
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Walking away before the fireworks Begin skipped the required guided stop.");
                f.StartStation(f.FireworksPoint, CampsiteGuidanceStage.Fireworks);
                Require(Buttons(f.View).Length == 0 && string.IsNullOrEmpty(DialogueText(f.View)),
                    "The active fireworks station still shows a blank Leave card.");
                for (int i = 0; i < 20; i++) f.Sequence.Tick(.5f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks),
                    "The unlit fireworks stop completed while the user remained in the area.");
                f.LeaveFireworksNaturally();
                Require(!f.Session.state.fireworksEverLaunched && f.Session.state.fireworksLaunched == 0
                    && JsonUtility.ToJson(f.Session.state) == state,
                    "Leaving without launching fireworks fabricated an action or changed the hazard score.");
            }
            using (var f = new Fixture(scene))
            {
                f.OpenFireworksRoute();
                f.StartStation(f.FireworksPoint, CampsiteGuidanceStage.Fireworks);
                var launching = typeof(FireworkStation).GetProperty("IsLaunching", BindingFlags.Instance | BindingFlags.Public);
                launching.SetValue(f.Setup.fireworks, true);
                f.Session.state.RecordFirework();
                f.Session.Changed();
                string launchedState = JsonUtility.ToJson(f.Session.state);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks,
                    "A launch completed the guided stop before its animation finished.");
                f.MoveOutsideFireworks();
                f.Sequence.Tick(10f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && JsonUtility.ToJson(f.Session.state) == launchedState,
                    "Walking out during an active fireworks show skipped its completion boundary.");
                launching.SetValue(f.Setup.fireworks, false);
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal
                    && f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && f.Session.state.fireworksEverLaunched && f.Session.state.fireworksLaunched == 1
                    && JsonUtility.ToJson(f.Session.state) == launchedState,
                    "The finished fireworks show did not continue from outside the area or lost its real outcome.");
            }
        }

        private static void CheckSafeDisposal(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                Require(PrototypeInteractionPolicy.CanMove(f.User.position, f.BinPoint.position),
                    "The current cigarette station cannot reach its disposal bin.");
                f.MoveUser(f.BinPoint);
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Setup.cigarette.IsDisposed && f.Session.state.cigaretteDisposals == 1
                    && f.Sequence.CurrentStage == CampsiteGuidanceStage.Campfire,
                    "A real safe disposal does not finish the same station as an unsafe cigarette choice.");
                f.Session.state.RecordCampfire(FireCondition.Cold);
                Require(f.ReturnPortal.ResolveEndingScene() == "5_CampusEnd_Main",
                    "The strict route overrode safe physical state with a forced ending.");
            }
        }

        private static void CheckOutOfOrderState(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.Session.state.RecordFirework();
                f.Session.state.poursStarted++;
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(!f.Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && !f.Sequence.IsExplored(CampsiteGuidanceStage.WaterBucket),
                    "Out-of-order physical state pre-completed future stations.");
                f.StartStation(f.FirePoint, CampsiteGuidanceStage.Campfire);
                f.MakeRoastingReady();
                f.Sequence.FinishRoasting();
                f.StartStation(f.BucketPoint, CampsiteGuidanceStage.WaterBucket);
                f.Press("Leave");
                f.Sequence.Tick(.2f);
                Require(f.Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.Fireworks),
                    "An earlier firework state skipped its required arrival and Begin.");
                Require(f.Session.state.fireworksLaunched == 1,
                    "Enforcing guided order erased the physical session's recorded actions.");
            }
        }

        private static void CheckHeldCigaretteReturn(Scene scene)
        {
            using (var f = new Fixture(scene))
            {
                f.CompleteNarration();
                f.StartStation(f.CigarettePoint, CampsiteGuidanceStage.Cigarette);
                Require(f.Sequence.FinishCurrentStage(), "The held-cigarette choice cannot continue as an unsafe decision.");
                f.StartStation(f.FirePoint, CampsiteGuidanceStage.Campfire);
                f.MakeRoastingReady();
                f.Sequence.FinishRoasting();
                f.StartStation(f.BucketPoint, CampsiteGuidanceStage.WaterBucket);
                f.Press("Leave");
                f.StartStation(f.FireworksPoint, CampsiteGuidanceStage.Fireworks);
                f.LeaveFireworksNaturally();
                f.ReturnPortal.Arm();
                f.Arrive(f.ReturnPoint);
                Require(f.Narrator.CurrentCueId == "camp.return-held" && !f.Sequence.CanBegin,
                    "The return introduction ignored the cigarette or exposed Begin too early.");
                f.ReadIntroduction(false);
                string warning = DialogueText(f.View);
                f.Press("Begin");
                Require(!f.Sequence.BeginCurrentStage() && !f.Sequence.IsStageStarted(CampsiteGuidanceStage.ReturnPortal)
                    && f.Narrator.IntroductionReady && DialogueText(f.View) == warning
                    && !f.Narrator.IsPlaying && !f.Sequence.CanReturn && !PrototypeInteractionPolicy.CanReturn(),
                    "The return warning consumed Begin or permitted carrying a cigarette back to campus.");
                var actualPortal = f.Child("Team return portal").AddComponent<CampusReturnPortal>();
                Require(!actualPortal.CanReturnNow, "The physical trigger bypasses the held-cigarette return rule.");
                Require(f.ReturnPortal.IsArmed && !f.ReturnPortal.TryEnter(),
                    "The distance portal bypasses the held-cigarette return rule.");
                f.Setup.player.DropHeld(false);
                f.Sequence.Tick(.2f);
                Require(f.Narrator.CurrentCueId == "camp.return" && !f.Sequence.CanReturn,
                    "Releasing the cigarette skipped the final return narration.");
                f.ReadIntroduction();
                Require(!f.Sequence.CanReturn && f.Sequence.CanBegin,
                    "The return introduction opened the portal before its final Begin.");
                f.Press("Begin");
                Require(!f.Narrator.IsPlaying && f.Sequence.CanReturn && actualPortal.CanReturnNow,
                    "The two portal paths do not share the same completed return permission.");
            }
        }

        private static void CheckStoryIntroduction(Scene scene, GuidanceSceneRole role, GuidanceEnding ending, string cue)
        {
            using (var f = new Fixture(scene, false))
            {
                f.Bridge.role = role;
                f.Bridge.ending = ending;
                f.Bridge.scriptedNarration = true;
                var flow = f.Root.AddComponent<GuidanceStoryFlow>();
                flow.bridge = f.Bridge;
                flow.narrator = f.Narrator;
                f.Bridge.storyFlow = flow;
                if (role == GuidanceSceneRole.CampusStart || ending == GuidanceEnding.Thriving)
                {
                    var portal = f.Child("Story portal").AddComponent<GuidanceCampusPortal>();
                    portal.transform.position = f.ReturnPoint.position;
                    portal.user = f.User;
                    portal.playerRoot = f.Setup.player.transform;
                    flow.storyPortal = portal;
                }
                flow.Initialize(f.Bridge);
                Vector3 position = f.User.position;
                Require(f.Narrator.IsPlaying && f.Narrator.CurrentCueId == cue && !flow.HasStarted
                    && !flow.Begin() && !flow.CanEnterPortal
                    && !Buttons(f.View).Any(button => Label(button) == "Begin"),
                    cue + " displayed Begin before its introduction.");
                f.CompleteNarration();
                Require(!flow.HasStarted && !flow.StoryFinished && f.Narrator.IntroductionReady
                    && DialogueText(f.View) == f.Narrator.LastSubtitleText,
                    cue + " advanced before its final introduction Begin.");
                CheckBeginBelowText(f.View);
                f.Press("Begin");
                Require(flow.HasStarted && flow.StoryFinished && !f.Narrator.IsPlaying
                    && !f.Narrator.IntroductionReady && !flow.Begin() && f.User.position == position,
                    cue + " Begin replayed narration or moved the player instead of advancing once.");
            }
        }

        private static void CheckNarration(Scene scene)
        {
            using (var f = new Fixture(scene, false))
            {
                var panel = f.Root.AddComponent<GuidanceVRPanel>();
                panel.Configure(f.User.GetComponent<Camera>(), f.View);
                int completed = 0;
                f.Narrator.Play("camp.campfire", () => completed++);
                Require(f.Narrator.IsPlaying && f.Narrator.TimedTextActive && !f.Narrator.AwaitingContinue
                    && Buttons(f.View).Length == 0 && f.Narrator.SubtitleTimeRemaining > 0f,
                    "Missing narration audio did not start automatic text playback without Continue.");
                int segment = f.Narrator.CurrentSegmentIndex;
                int page = f.Narrator.CurrentSubtitlePage;
                float duration = f.Narrator.SubtitleTimeRemaining;
                float expected = Mathf.Max(f.Narrator.minSubtitleSeconds,
                    DialogueText(f.View).Length / f.Narrator.subtitleCharactersPerSecond);
                Require(Mathf.Abs(duration - expected) < .01f,
                    "A subtitle does not receive its configured minimum and text-length reading time.");
                f.Narrator.Continue();
                Require(f.Narrator.CurrentSegmentIndex == segment && f.Narrator.CurrentSubtitlePage == page
                    && Mathf.Abs(f.Narrator.SubtitleTimeRemaining - duration) < .001f,
                    "The compatibility Continue method still skips timed subtitles.");
                bool paused = AudioListener.pause;
                try
                {
                    AudioListener.pause = true;
                    f.Narrator.Tick(100f);
                    Require(f.Narrator.SubtitleTimeRemaining == duration && f.Narrator.CurrentSubtitlePage == page,
                        "Paused narration advanced its automatic reading timer.");
                }
                finally { AudioListener.pause = paused; }
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) f.Narrator.Tick(invalid);
                Require(f.Narrator.SubtitleTimeRemaining == duration,
                    "An invalid playback step advanced the subtitle timer.");
                f.Narrator.Tick(duration - .01f);
                Require(completed == 0 && f.Narrator.CurrentSegmentIndex == segment && f.Narrator.CurrentSubtitlePage == page,
                    "The first subtitle advanced before its full reading duration.");
                f.Narrator.Tick(.02f);
                Require(completed == 0 && (f.Narrator.CurrentSegmentIndex != segment || f.Narrator.CurrentSubtitlePage != page),
                    "The automatic timer did not advance to the next subtitle.");
                AssertNoNarrationContinue(f.View);
                f.Narrator.Stop();
                f.Narrator.Tick(1000f);
                f.Narrator.Continue();
                Require(completed == 0 && !f.Narrator.IsPlaying && !f.Narrator.TimedTextActive
                    && !f.View.SequencePanelActive && f.Narrator.source.clip == null,
                    "Stopping narration left its timer, callback, panel or audio active.");
                f.Narrator.Play("camp.campfire", () => completed++);
                Require(f.Narrator.CurrentSegmentIndex == 0 && f.Narrator.CurrentSubtitlePage == 0,
                    "Replaying narration retained an old page or elapsed time.");
                f.CompleteNarration();
                f.Narrator.Tick(1000f);
                Require(completed == 1, "Automatic narration completed more than once.");

                int pagedCompleted = 0;
                f.Narrator.Play("camp.fireworks", () => pagedCompleted++);
                Require(f.Narrator.SubtitlePageCount > 1 && f.Narrator.CurrentSubtitlePage == 0,
                    "The long fireworks paragraph was not divided into compact subtitles.");
                f.Narrator.Tick(f.Narrator.SubtitleTimeRemaining + 100f);
                Require(f.Narrator.CurrentSegmentIndex == 0 && f.Narrator.CurrentSubtitlePage == 1
                    && f.Narrator.SubtitleTimeRemaining > 0f && pagedCompleted == 0,
                    "A delayed frame skipped unread subtitle pages instead of giving the next page its own reading time.");
                f.CompleteNarration();
                Require(pagedCompleted == 1, "A paged paragraph did not complete automatically exactly once.");
                foreach (var group in f.NarrationLibrary.groups)
                {
                    foreach (var paragraph in group.paragraphs)
                        Require(string.Concat(paragraph.GetSubtitleCues().Select(cue => cue.text)) == paragraph.text,
                            "Subtitle pages changed the exact narration text in " + group.id);
                    f.Narrator.Play(group.id, null);
                    int ticks = 0;
                    while (f.Narrator.IsPlaying && ticks++ < 200)
                    {
                        CheckVisibleTextFits(f.View, group.id);
                        AssertNoNarrationContinue(f.View);
                        f.Narrator.Tick(Mathf.Max(.1f, f.Narrator.SubtitleTimeRemaining + .01f));
                    }
                    Require(!f.Narrator.IsPlaying, "Automatic narration did not finish group " + group.id);
                }
                Require(f.Root.GetComponents<AudioSource>().Length == 1,
                    "Repeated narration created overlapping audio sources.");
                CheckMixedAudioFallback(f);
                CheckIntroductionNarrator(f);
            }
        }

        private static void CheckMixedAudioFallback(Fixture f)
        {
            var previousGroups = f.NarrationLibrary.groups;
            var clip = AudioClip.Create("Unavailable narration playback", 160, 1, 16000, false);
            bool enabled = f.Narrator.source.enabled;
            var mixed = new GuidanceNarrationGroup
            {
                id = "validation.mixed-audio",
                paragraphs = new[]
                {
                    new GuidanceNarrationSegment { text = "A short opening paragraph." },
                    new GuidanceNarrationSegment
                    {
                        text = "This paragraph has an assigned audio clip that cannot start. Its subtitles must still play automatically, one readable page at a time.",
                        audio = clip
                    },
                    new GuidanceNarrationSegment { text = "The final paragraph still plays automatically." }
                }
            };
            f.NarrationLibrary.groups = previousGroups.Concat(new[] { mixed }).ToArray();
            try
            {
                f.Narrator.source.enabled = false;
                int completed = 0;
                f.Narrator.Play(mixed.id, () => completed++);
                f.Narrator.Tick(f.Narrator.SubtitleTimeRemaining + .01f);
                Require(f.Narrator.CurrentSegmentIndex == 1 && !f.Narrator.TimedTextActive
                    && f.Narrator.source.clip == clip && completed == 0,
                    "Assigned audio did not enter its startup boundary after a text-only paragraph.");
                f.Narrator.Tick(f.Narrator.audioStartupGraceSeconds * .5f);
                Require(f.Narrator.CurrentSegmentIndex == 1 && !f.Narrator.TimedTextActive,
                    "Unavailable audio skipped its startup grace period.");
                f.Narrator.Tick(f.Narrator.audioStartupGraceSeconds + .01f);
                Require(f.Narrator.CurrentSegmentIndex == 1 && f.Narrator.TimedTextActive
                    && f.Narrator.SubtitleTimeRemaining > 0f && completed == 0,
                    "Unavailable audio did not fall back to readable automatic text.");
                AssertNoNarrationContinue(f.View);
                f.CompleteNarration();
                Require(completed == 1, "Mixed text and unavailable audio did not complete exactly once.");

                int cancelled = 0;
                int replacement = 0;
                f.Narrator.Play(mixed.id, () => cancelled++);
                f.Narrator.Tick(f.Narrator.SubtitleTimeRemaining + .01f);
                f.Narrator.Play("camp.return", () => replacement++);
                f.CompleteNarration();
                Require(cancelled == 0 && replacement == 1 && f.Root.GetComponents<AudioSource>().Length == 1,
                    "Replacing unavailable audio retained its old callback or created a second voice source.");
                int ready = 0;
                int began = 0;
                f.Narrator.PlayIntroduction(mixed.id, () => ready++, () => began++);
                f.CompleteNarration();
                Require(ready == 1 && began == 0 && f.Narrator.IntroductionReady
                    && Label(Buttons(f.View).Single()) == "Begin",
                    "An unavailable audio introduction did not wait for its final readable text before Begin.");
                f.Press("Begin");
                Require(began == 1 && !f.Narrator.IntroductionReady,
                    "The mixed-audio introduction began more than once or failed to begin.");
            }
            finally
            {
                f.Narrator.Stop();
                f.Narrator.source.enabled = enabled;
                f.NarrationLibrary.groups = previousGroups;
                Object.DestroyImmediate(clip);
            }
        }

        private static void CheckIntroductionNarrator(Fixture f)
        {
            int ready = 0;
            int started = 0;
            bool allowed = false;
            f.Narrator.PlayIntroduction("camp.campfire", () => ready++, () => started++, () => allowed);
            Require(f.Narrator.IsPlaying && !f.Narrator.IntroductionReady && f.Narrator.TimedTextActive
                && Buttons(f.View).Length == 0,
                "Begin or Continue appeared before the automatically played introduction finished.");
            f.CompleteNarration();
            string finalText = f.Narrator.LastSubtitleText;
            Require(ready == 1 && started == 0 && f.Narrator.IntroductionReady && f.Narrator.AwaitingBegin
                && !f.Narrator.IsPlaying && !string.IsNullOrWhiteSpace(finalText)
                && DialogueText(f.View) == finalText && Label(Buttons(f.View).Single()) == "Begin",
                "Introduction completion did not retain its final subtitle and Begin.");
            CheckBeginBelowText(f.View);
            for (int i = 0; i < 10; i++) f.Narrator.Tick(10f);
            Require(ready == 1 && started == 0 && !f.Narrator.BeginIntroduction()
                && f.Narrator.IntroductionReady && DialogueText(f.View) == finalText,
                "A guarded Begin started early, repeated readiness, or cleared the introduction.");
            var stale = Buttons(f.View).Single().onClick;
            allowed = true;
            f.Press("Begin");
            stale.Invoke();
            Require(started == 1 && ready == 1 && !f.Narrator.IntroductionReady
                && !f.Narrator.IsPlaying && !f.Narrator.BeginIntroduction(),
                "Begin replayed narration or invoked the start callback more than once.");

            f.Narrator.PlayIntroduction("camp.return", () => ready++, () => started++);
            Require(f.Narrator.IsPlaying && !f.Narrator.IntroductionReady && Buttons(f.View).Length == 0,
                "The final introduction page exposed Begin without its reading duration.");
            float remaining = f.Narrator.SubtitleTimeRemaining;
            f.Narrator.Tick(remaining - .01f);
            Require(f.Narrator.IsPlaying && !f.Narrator.IntroductionReady && ready == 1
                && !Buttons(f.View).Any(button => Label(button) == "Begin"),
                "The final introduction subtitle completed before its reading duration.");
            f.Narrator.Tick(.02f);
            Require(f.Narrator.IntroductionReady && !f.Narrator.IsPlaying && ready == 2,
                "The final subtitle did not expose Begin after automatic reading completed.");
            var stopped = Buttons(f.View).Single().onClick;
            f.Narrator.Stop();
            stopped.Invoke();
            Require(started == 1 && !f.Narrator.BeginIntroduction() && !f.View.SequencePanelActive,
                "Stopping an introduction left a stale Begin callback active.");

            f.Narrator.PlayIntroduction("camp.return", null, () => started++);
            f.CompleteNarration();
            var replaced = Buttons(f.View).Single().onClick;
            f.Narrator.PlayIntroduction("camp.campfire", () => ready++, () => started++);
            replaced.Invoke();
            Require(f.Narrator.IsPlaying && !f.Narrator.IntroductionReady
                && f.Narrator.CurrentCueId == "camp.campfire" && started == 1,
                "Replacing an introduction let the old Begin activate the new area.");
            f.CompleteNarration();
            f.Press("Begin");
            Require(started == 2 && !f.Narrator.IsPlaying,
                "The replacement introduction did not start its area exactly once.");
        }

        private static void AssertNoNarrationContinue(GuidanceView view)
        {
            Require(!view.GetComponentsInChildren<Button>(true)
                .Any(button => button.gameObject.activeInHierarchy && Label(button) == "Continue"),
                "Automatic narration still shows a Continue button.");
        }

        private static void CheckVisibleTextFits(GuidanceView view, string context)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                var rect = text.rectTransform.rect;
                var settings = text.GetGenerationSettings(new Vector2(rect.width, rect.height));
                settings.resizeTextForBestFit = false;
                settings.fontSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                using (var generator = new TextGenerator())
                {
                    float height = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                    Require(rect.width > 0f && height <= rect.height + 2f,
                        context + "/" + text.name + " narration does not fit its VR panel.");
                }
            }
        }

        private static string DialogueText(GuidanceView view) => view.GetComponentsInChildren<Text>(true)
            .First(text => text.name == "Dialogue").text;

        private static void CheckBeginBelowText(GuidanceView view, string label = "Begin")
        {
            Canvas.ForceUpdateCanvases();
            var text = view.GetComponentsInChildren<Text>(true).First(value => value.name == "Dialogue");
            var begin = Buttons(view).Single(button => Label(button) == label);
            Require(!string.IsNullOrWhiteSpace(text.text), label + " is shown without its introduction text.");
            var textCorners = new Vector3[4];
            var buttonCorners = new Vector3[4];
            text.rectTransform.GetWorldCorners(textCorners);
            begin.GetComponent<RectTransform>().GetWorldCorners(buttonCorners);
            Transform card = text.transform.parent;
            Require(card.InverseTransformPoint(textCorners[0]).y >= card.InverseTransformPoint(buttonCorners[1]).y - 1f,
                "Begin overlaps the introduction instead of appearing below it.");
        }

        private static string Label(Button button) => button.GetComponentInChildren<Text>(true)?.text ?? string.Empty;
        private static Button[] Buttons(GuidanceView view) => view.GetComponentsInChildren<Button>(true)
            .Where(button => button.isActiveAndEnabled && button.interactable).ToArray();
        private static string VisibleText(GuidanceView view) => string.Join("\n", view.GetComponentsInChildren<Text>(true)
            .Where(text => text.isActiveAndEnabled).Select(text => text.text));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Strict guided sequence: " + message);
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
            public readonly GuidanceNarrator Narrator;
            public readonly GuidanceNarrationLibrary NarrationLibrary;
            public readonly Transform User, CigarettePoint, FirePoint, FireworksPoint, ReturnPoint, BinPoint, BucketPoint, AwayPoint;
            private readonly PrototypeSession previousSession;
            private readonly GuidanceLibrary library;
            private readonly PolicySnapshot policy = new PolicySnapshot();
            private static readonly PropertyInfo InstanceProperty = typeof(PrototypeSession)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);

            public Fixture(Scene scene, bool initialize = true, bool tentGate = false)
            {
                previousSession = PrototypeSession.Instance;
                PrototypeInteractionPolicy.Reset();
                Root = new GameObject("Strict guided sequence fixture");
                SceneManager.MoveGameObjectToScene(Root, scene);
                Root.SetActive(false);
                Root.transform.position = new Vector3(5000f, 0f, 5000f);
                Session = Child("Session").AddComponent<PrototypeSession>();
                Session.state = new ChoiceState { cigarette = CigaretteCondition.Held, campfire = FireCondition.Burning };
                InstanceProperty.SetValue(null, Session);
                var events = Child("EventSystem").AddComponent<EventSystem>();
                events.gameObject.AddComponent<XRUIInputModule>();
                library = GuidanceLibrary.CreateDefault();
                Director = Root.AddComponent<GuidanceDirector>();
                Director.library = library;
                View = Root.AddComponent<GuidanceView>();
                View.director = Director;
                View.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Yao/ButterflyGuidance/Art/GuidanceSans.ttf");
                View.Build();
                NarrationLibrary = ScriptableObject.CreateInstance<GuidanceNarrationLibrary>();
                NarrationLibrary.groups = GuidanceNarrationBuilder.DefaultGroups();
                Narrator = Root.AddComponent<GuidanceNarrator>();
                Narrator.library = NarrationLibrary;
                Narrator.view = View;
                Narrator.Configure();
                User = Child("Player camera").transform;
                User.gameObject.AddComponent<Camera>();
                User.localPosition = new Vector3(-4f, 1.6f, 0f);
                CigarettePoint = Marker("Cigarette approach", Vector3.zero);
                FirePoint = Marker("Campfire approach", Vector3.right * 12f);
                FireworksPoint = Marker("Fireworks approach", Vector3.right * 24f);
                ReturnPoint = Marker("Return approach", Vector3.right * 36f);
                BinPoint = Marker("Disposal bin", new Vector3(-8f, 0f, -8f));
                AwayPoint = Marker("Open ground", new Vector3(-20f, 0f, 20f));
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
                var bin = Child("Actual disposal bin");
                bin.transform.position = BinPoint.position + Vector3.up;
                bin.AddComponent<BoxCollider>().size = new Vector3(3f, 4f, 3f);
                Setup.disposalBin = bin.AddComponent<DisposalBin>();
                Setup.cigarette = Item<CigaretteItem>("Actual cigarette", CigarettePoint.position);
                Setup.marshmallow = Item<MarshmallowItem>("Actual marshmallow", FirePoint.position);
                Setup.marshmallow.marshmallow = Child("Marshmallow visual", Setup.marshmallow.transform).AddComponent<MeshRenderer>();
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
                Session.state.itemsPickedUp = Session.state.totalActions = 0;
                if (tentGate)
                {
                    var sequence = Root.AddComponent<GuidanceCampsiteSequence>();
                    sequence.tentExit = Marker("Tent exit", Vector3.left);
                    sequence.tentExit.rotation = Quaternion.Euler(0f, 90f, 0f);
                }
                if (initialize) Bridge.Initialize();
                Sequence = Root.GetComponent<GuidanceCampsiteSequence>();
            }

            public void OpenCampfire()
            {
                CompleteNarration();
                StartStation(CigarettePoint, CampsiteGuidanceStage.Cigarette);
                Setup.player.DropHeld(false);
                Sequence.Tick(.2f);
                StartStation(FirePoint, CampsiteGuidanceStage.Campfire);
            }
            public void MakeRoastingReady()
            {
                Setup.marshmallow.cookedSeconds = Setup.marshmallow.goldenSeconds;
                Sequence.Tick(.2f);
                Require(Sequence.RoastingReady && Sequence.CanFinishRoasting,
                    "The fixture's physically cooked serving did not unlock roasting completion.");
            }
            public void OpenFireworksRoute()
            {
                OpenCampfire();
                MakeRoastingReady();
                Sequence.FinishRoasting();
                StartStation(BucketPoint, CampsiteGuidanceStage.WaterBucket);
                Press("Leave");
                Require(Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks,
                    "The fixture could not reach the required fireworks route.");
            }
            public void MoveOutsideFireworks()
            {
                Vector3 position = FireworksPoint.position
                    + Vector3.left * (Mathf.Max(.1f, Sequence.leaveRadius) + .2f) + Vector3.up * 1.6f;
                Require(PrototypeInteractionPolicy.CanMove(User.position, position),
                    "The route boundary prevents the user from naturally leaving the fireworks area.");
                User.position = position;
                Setup.player.RefreshHeldPose();
                Sequence.Tick(.2f);
            }
            public void LeaveFireworksNaturally()
            {
                Require(Sequence.CurrentStage == CampsiteGuidanceStage.Fireworks
                    && Sequence.Phase == CampsiteSequencePhase.Interacting,
                    "The natural fireworks exit was attempted before its guided interaction began.");
                string state = JsonUtility.ToJson(Session.state);
                MoveOutsideFireworks();
                Require(Sequence.CurrentStage == CampsiteGuidanceStage.ReturnPortal
                    && Sequence.IsExplored(CampsiteGuidanceStage.Fireworks)
                    && JsonUtility.ToJson(Session.state) == state,
                    "Walking away from the fireworks failed to continue or changed physical choices.");
            }
            public void MoveUser(Transform point)
            {
                User.position = point.position + Vector3.up * 1.6f;
                Setup.player.RefreshHeldPose();
            }
            public void Arrive(Transform point)
            {
                MoveUser(point);
                Guide.transform.position = point.position + Vector3.up * Guide.flightHeight;
                Guide.Tick(.1f);
                Sequence.Tick(.2f);
            }
            public void CompleteNarration()
            {
                int ticks = 0;
                while (Narrator.IsPlaying && ticks++ < 500)
                {
                    AssertNoNarrationContinue(View);
                    Require(!Narrator.AwaitingContinue
                        && !Buttons(View).Any(button => Label(button) == "Begin"),
                        "A manual Continue or early Begin interrupted automatic narration.");
                    float deltaTime = Narrator.TimedTextActive ? Narrator.SubtitleTimeRemaining + .01f
                        : Mathf.Max(.1f, Narrator.audioStartupGraceSeconds + .01f);
                    Narrator.Tick(deltaTime);
                    Sequence?.Tick(deltaTime);
                }
                Require(!Narrator.IsPlaying, "Automatic narration did not complete within its reading duration.");
                bool cigaretteStart = Sequence != null && Sequence.CurrentStage == CampsiteGuidanceStage.Cigarette
                    && Sequence.Phase == CampsiteSequencePhase.AwaitingBegin;
                if (!cigaretteStart) AssertNoNarrationContinue(View);
                Sequence?.Tick(.2f);
            }
            public void ReadIntroduction(bool beginAllowed = true, bool hasIntroductionText = true, string label = "Begin")
            {
                CompleteNarration();
                Require(Sequence.Phase == CampsiteSequencePhase.AwaitingBegin && Sequence.CanBegin == beginAllowed,
                    "The completed introduction has the wrong Begin eligibility.");
                Require(Buttons(View).Length == 1 && Label(Buttons(View)[0]) == label,
                    "The completed introduction did not expose exactly one " + label + " choice.");
                if (hasIntroductionText)
                {
                    Require(!string.IsNullOrWhiteSpace(DialogueText(View)),
                        "The area's introduction did not retain its final text with Begin.");
                    CheckBeginBelowText(View, label);
                }
            }
            public void StartStation(Transform point, CampsiteGuidanceStage expected)
            {
                Require(Sequence.CurrentStage == expected, "Expected next station " + expected + ", got " + Sequence.CurrentStage);
                Arrive(point);
                if (expected == CampsiteGuidanceStage.WaterBucket)
                {
                    Require(Sequence.Phase == CampsiteSequencePhase.Interacting
                        && Sequence.IsStageStarted(expected) && Sequence.BucketPromptAvailable
                        && !Sequence.CanBegin && !Sequence.BeginCurrentStage()
                        && !Narrator.IsPlaying && !Narrator.AwaitingBegin,
                        "The arrived bucket stop did not open directly without Begin or narration.");
                    CheckBucketPanel(this, Setup.bucket.IsHeld ? "bucket.held" : "bucket.ready");
                    return;
                }
                Vector3 position = User.position;
                Quaternion rotation = User.rotation;
                Require(!Sequence.IsStageStarted(expected), "Arrival marked " + expected + " as started before Begin.");
                Require(Sequence.Phase == CampsiteSequencePhase.Narrating || Sequence.Phase == CampsiteSequencePhase.AwaitingBegin,
                    "The " + expected + " introduction did not begin automatically on arrival.");
                PrototypeItem current = expected == CampsiteGuidanceStage.Cigarette ? Setup.cigarette
                    : expected == CampsiteGuidanceStage.Campfire ? (PrototypeItem)Setup.marshmallow
                    : expected == CampsiteGuidanceStage.WaterBucket ? Setup.bucket : null;
                Require(current == null || !PrototypeInteractionPolicy.CanGrab(current),
                    "The " + expected + " object is usable before introduction and Begin are complete.");
                Require(expected != CampsiteGuidanceStage.Fireworks || !PrototypeInteractionPolicy.CanLaunch(Setup.fireworks),
                    "Fireworks can launch before their introduction and Begin.");
                string label = expected == CampsiteGuidanceStage.Cigarette ? "Continue" : "Begin";
                ReadIntroduction(true, true, label);
                Require(current == null || !PrototypeInteractionPolicy.CanGrab(current),
                    "The " + expected + " object unlocked while awaiting its introduction choice.");
                Press(label);
                Require(Sequence.IsStageStarted(expected), "Begin did not mark " + expected + " as started.");
                Require(!Sequence.BeginCurrentStage() && !Narrator.IsPlaying && !Narrator.AwaitingBegin,
                    "The " + expected + " Begin replayed or restarted its introduction.");
                Require(User.position == position && Quaternion.Angle(User.rotation, rotation) < .001f,
                    "Starting or narrating a station moved the VR player's head.");
                Require(Sequence.Phase == CampsiteSequencePhase.Interacting || Sequence.Phase == CampsiteSequencePhase.Complete,
                    "The " + expected + " Begin did not directly release interaction.");
                if (expected == CampsiteGuidanceStage.Cigarette || expected == CampsiteGuidanceStage.Fireworks)
                    Require(Buttons(View).Length == 0 && string.IsNullOrWhiteSpace(DialogueText(View)),
                        "The active " + expected + " station still shows a blank Leave card.");
            }
            public void Press(string label)
            {
                Button button = Buttons(View).FirstOrDefault(value => Label(value) == label);
                Require(button != null, "Missing interactive choice '" + label + "'. Current text: " + VisibleText(View));
                button.onClick.Invoke();
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
            public GameObject Child(string name, Transform parent = null)
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
                Object.DestroyImmediate(NarrationLibrary);
                policy.Dispose();
            }
        }

        private sealed class PolicySnapshot : IDisposable
        {
            private readonly Func<PrototypeItem, bool> grab = PrototypeInteractionPolicy.GrabHandler;
            private readonly Func<PrototypeItem, bool> use = PrototypeInteractionPolicy.UseHandler;
            private readonly Func<PrototypeButton, bool> press = PrototypeInteractionPolicy.PressHandler;
            private readonly Func<FireworkStation, bool> launch = PrototypeInteractionPolicy.LaunchHandler;
            private readonly Func<Vector3, Vector3, bool> move = PrototypeInteractionPolicy.MoveHandler;
            private readonly Func<bool> returns = PrototypeInteractionPolicy.ReturnHandler;
            public void Dispose()
            {
                PrototypeInteractionPolicy.CancelReturn();
                PrototypeInteractionPolicy.GrabHandler = grab;
                PrototypeInteractionPolicy.UseHandler = use;
                PrototypeInteractionPolicy.PressHandler = press;
                PrototypeInteractionPolicy.LaunchHandler = launch;
                PrototypeInteractionPolicy.MoveHandler = move;
                PrototypeInteractionPolicy.ReturnHandler = returns;
            }
        }
    }
}
