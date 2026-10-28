# Mahmoud's interaction prototype

Version 2 updates desktop carrying and simplifies the presentation. Held props now receive an explicit camera-relative pose every frame with interpolation and collisions disabled during the hold. Their previous physics settings and XR availability are restored on release. Labels are shortened, fitted to physical size limits, and hidden at a distance. Full statistics are available through Tab instead of a large permanent screen panel.

## Updating the version you already imported

1. Stop Play mode by clicking the blue square button at the top of Unity.
2. Download the updated ZIP and extract it. Import `Mahmoud_Interactions_v2.unitypackage` through **Assets > Import Package > Custom Package**. Leave its files selected and click Import. They update the existing scripts; their asset IDs are unchanged.
3. Wait for compilation. Choose **Tools > Butterfly Effect > Repair Existing Prototype**. This updates the generated scene and prefabs and saves copies of their previous versions under `Generated/Backups`.
4. In the Game tab, set the **Scale** slider to **1x** for a clear view.
5. Press Play and click the Game view. You already have the cigarette in your hand: try WASD before pressing E. It should stay in front of you as you walk and look around. E puts it down. To pick it up again, aim at it and tap E once; keeping E held is not required.

The correct import folder is **Assets/MahmoudPrototype**, next to **Mahmoud Playground**. The package supplies this path. It does not use the folder that was selected when you clicked Import, and it does not need to be moved.

The first ZIP contained some optional extras:

| Original ZIP item | What to do with it |
| --- | --- |
| The `.unitypackage` | Import this to install the prototype |
| `START_HERE.md` | Read it for the walkthrough; it is not an additional import |
| `SUBMISSION_NOTES.md` | Optional submission-description notes to edit after testing |
| `Source` folder | A spare copy of the same scripts. Leave it outside Unity; importing it again would duplicate the installed code |

The updated ZIP contains just the v2 import package and a short read-me. The full source and detailed guide are already installed under Assets/MahmoudPrototype by the import package.

This package creates a separate, playable desktop test scene inside the existing Butterfly Effect Unity project. It uses cubes, cylinders, spheres, and simple effects. The scene builder also saves four station prefabs for the final campsite.

Target project: Unity 6000.6.2f1, XR Interaction Toolkit 3.6.1, Input System 1.20.0, URP. These dependencies already appear in the supplied project. The package does not install or upgrade dependencies.

## 1. Import and open

1. Extract the downloaded ZIP.
2. In your existing Unity project, exit Play mode if it is active.
3. Choose **Assets > Import Package > Custom Package**.
4. Select **Mahmoud_Interactions_v2.unitypackage** from the extracted folder.
5. Leave the package's files selected and click **Import**.
6. Wait for script compilation to finish. Open the Console and check for red errors.
7. From the top menu, choose **Tools > Butterfly Effect > Create or Open Interaction Prototype**.
8. If Unity asks whether to save the open campsite, save it if it has changes you want to keep.
9. Click the **Game** tab, press **Play**, and click inside the Game view so it receives keyboard input.

The menu builds the scene the first time. Later, it opens the existing scene without rebuilding it. All generated files go inside `Assets/MahmoudPrototype/Generated`. Existing team scenes and project settings are not edited by the builder.

The generated scene is:

`Assets/MahmoudPrototype/Generated/Scenes/Mahmoud_InteractionPrototype.unity`

You begin with a lit cigarette in your hand. Objects can also be picked up from their tables or from the ground. One object can be held at a time.

## 2. Controls

| Control | Action |
| --- | --- |
| W / A / S / D | Walk |
| Hold right mouse and move mouse | Look around and aim the centre crosshair |
| Release right mouse | Free the cursor for on-screen buttons |
| Tap E once | Pick up the object under the crosshair, or drop the object in your hand; release the key before walking |
| Hold Q, then release Q | Preview a throw trajectory, then throw the held object |
| F | Use the held object, or press an aimed interaction button with an empty hand |
| Hold R | Tip a held bucket to pour water |
| 1 / 2 / 3 / 4 | Move to the cigarette / campfire / bucket / fireworks station |
| Tab | Open or close the choices and ending preview |
| Backspace | Reset the entire run |
| Escape | Release the cursor |

The on-screen station buttons do the same thing as keys 1-4. Jumping between stations keeps the item in your hand, which is useful when carrying the bucket to the fire. Tap E to grab, walk with WASD, and tap E again to drop; you do not need to keep E pressed.

## 3. Interaction 1: cigarette disposal

**Purpose:** implement the story's free-hand dilemma, recoverable litter, and safe disposal.

1. Start with the cigarette in hand. Press E to drop it.
2. Look down at it and press E to pick it up again.
3. Walk beside the blue disposal bin. Position the cigarette above its opening and press E to release it inside.
4. The cigarette disappears and the status changes to **Disposed safely**.

To demonstrate throwing, hold Q while carrying the cigarette, aim, and release Q. The blue line predicts the unobstructed ballistic path; collisions can change the final landing position.

Optional unsafe branch: drop the cigarette on the yellow dry-grass patch for about four seconds. A small grass fire starts. Recovering the cigarette prevents further cigarette contact, but does not automatically extinguish grass that has already caught fire. The bucket can extinguish that fire separately.

Use Backspace for a fresh run before recording the safe-disposal branch.

## 4. Interaction 2: campfire and marshmallow

**Purpose:** implement a tempting activity, timed roasting, visible overcooking, and repeated eating.

1. Dispose of or drop the cigarette so your hand is free.
2. Press 2 to approach the campfire.
3. Look toward the table to the left. Pick up the red lighter with E.
4. Bring it near the campfire and press F to ignite the logs.
5. Press E to drop the lighter. Pick up the marshmallow stick with E.
6. Hold its marshmallow end above the fire. Pressing 2 while holding the stick places you at a useful roasting position; adjust your aim if necessary.
7. The marshmallow gradually changes from white to golden after about four seconds in the heat.
8. Press F to take a bite. The eaten count increases, and a new raw marshmallow appears on the stick after a short delay.
9. Repeat, or keep roasting past nine seconds to demonstrate the burnt state.

The blue **Leave unlit** button represents respecting the fire ban. Aim at it with an empty hand and press F. Lighting the fire remains recorded even if the player later extinguishes it.

## 5. Interaction 3: water bucket and embers

**Purpose:** implement physical pouring, finite water, refilling, and a fire that is not safe merely because its flames disappear.

1. Light the campfire first, then drop the lighter or stick.
2. Press 3. Pick up the bucket from its table with E.
3. Press 2 while still holding the bucket to return to the campfire.
4. Hold R to tilt the bucket. A blue stream appears below its rim. Move or adjust your aim until the stream lands on the fire patch.
5. The flames go out first. The status becomes **Hot embers - keep pouring**.
6. Keep pouring until the orange embers disappear and the status becomes **Cold / extinguished**.
7. To refill, carry the bucket back to the blue tank at station 3 and press F beside it.

If pouring stops while embers remain hot, the fire reignites after about twelve seconds. These amounts and timings are simplified game mechanics, not real-world extinguishing instructions.

The same bucket also works on the cigarette and fireworks grass fires. Pouring away from any fire still consumes water.

## 6. Interaction 4: fireworks

**Purpose:** implement activation, a timed effect, repeated use, and a single station result.

1. Drop the held item and press 4.
2. Aim at the orange launch button and press F.
3. Look up to follow the rocket and see its burst.
4. Wait for the display to finish, then launch another. Successive launches use different colours.
5. The launch counter increases, but the fireworks station contributes at most one unsafe result to the ending.
6. The blue **Leave unlit** button records the alternative choice as feedback. It does not undo an earlier launch.

By default, fireworks ignite a nearby dry-grass patch. The station's Inspector has an `Ignite Grass On Launch` setting for the team to adjust later.

## 7. Choice tracking and the refined story

The prototype retains BOTH action history and current hazard conditions. This preserves the difference between the original pitch and the newer narrative.

Select **Prototype Session** in the Hierarchy before entering Play mode. Its **Ending Rule** can be:

- **Original Pitch** (default): dropping the cigarette outside the bin, lighting the campfire, or launching any fireworks remains unsafe after cleanup. A cigarette that has not been disposed of is also unresolved.
- **Remaining Hazards**: evaluates the undischarged cigarette, cigarette grass fire, hot campfire/embers, and fireworks grass fire at the time of the preview. Cleanup can improve this result.

The original-pitch setting aligns with the submitted pitch's rule that lighting a campfire remains unsafe even when later put out. The remaining-hazards option supports the refined debrief's emphasis on what the player leaves behind. The team should select the final rule before integrating the ending.

The four preview titles use this configurable prototype mapping:

| Unsafe / unresolved stations | Preview title |
| --- | --- |
| 0 | A Place Protected |
| 1 | A Fragile Future |
| 2 | The Spark Came Home |
| 3 | What We Leave Behind |

The refined text does not itself specify numeric thresholds. The mapping here is a prototype default, not a claim that the team has finalized those thresholds. No campus destruction scene, portal, butterfly path, or narration is generated by this package.

## 8. Recording and submitting

Record the Game view after checking each interaction in Play mode. Desktop recording is acceptable under the assignment instructions you supplied.

A useful combined recording is roughly 3-5 minutes:

1. Show the cigarette drop, pickup, trajectory preview, and bin disposal.
2. Show lighting the campfire, a golden marshmallow, eating, and a second serving. Briefly show burning if time allows.
3. Show the bucket stream, flames becoming hot embers, and continued pouring until cold. Briefly demonstrate a refill.
4. Show two fireworks with different burst colours. Open the choices panel to show that repeat launches do not add extra unsafe stations.
5. Reset and show the safe alternatives if needed.

For GitHub, commit the new `Assets/MahmoudPrototype` folder AND its `.meta` files, including the generated scene, materials, and station prefabs. A reasonable commit summary is `Add modular cigarette, roasting, water bucket and fireworks prototypes`.

Submit the team's repository URL and the accessible video link(s). Set the videos to **Unlisted**, not Private, and verify that a person without your account can open the links.

## 9. Integrating into the team's scenes

The scene builder creates these prefabs under `Generated/Prefabs`:

- `01_CigaretteStation.prefab`
- `02_CampfireAndMarshmallowStation.prefab`
- `03_WaterBucketStation.prefab`
- `04_FireworksStation.prefab`

Drag the station prefabs into the final campsite, move the roots to the desired locations, and replace the child primitive visuals with the final models. Keep each item's Rigidbody, collider, behaviour, and referenced tip/spout/roast transforms. Preserve the scale of the physics root; resize visual children where possible.

Add exactly one `PrototypeSession` component in the integrated scene if the team wants these components to record choices. Its `onStateChanged` UnityEvent can notify the final game manager; the choice data is available through `PrototypeSession.Instance.state`. The prototype state currently lives in the scene. For separate campus and forest scenes, the final game manager must retain/copy it across scene transitions.

The water bucket discovers the active fire targets when it starts. Keep the station objects present from scene startup, or refresh that target list if the final game spawns fire targets dynamically.

### XR preparation

The grabbable props already have `XRGrabInteractable` and an `XrItemBridge`; the buttons have `XRSimpleInteractable` and an `XrButtonBridge`. Desktop and XR inputs call the same gameplay methods. Desktop holding temporarily disables the item's XRGrabInteractable and physics interpolation, then restores them when released. This gives the prop a single pose owner. A held bucket pours when physically tipped in XR. The controller's Activate action uses held items, and Select presses a station button.

The generated scene is desktop-first. It does not create an XR rig, configure a Quest/OpenXR runtime, attach the initial cigarette to an XR hand, or convert its screen HUD to world-space UI. For headset integration, disable the Desktop Player, add the team's configured XR Origin, connect its input actions, and provide world-space prompts. Preserve or recreate the initial cigarette-in-hand event through the XR interaction manager. Test all XR grabbing, activation, throwing, tipping, and feedback on the actual headset. The desktop trajectory guide belongs to the desktop controller; it is not a VR throwing predictor.

## 10. Checks and troubleshooting

- **No Tools menu:** wait for compilation, then inspect the first red Console error. Target dependencies must be installed. Share the full error if it persists.
- **Keyboard does nothing:** click inside the Game view. The project uses the new Input System.
- **OpenXR startup errors with no headset:** for desktop testing, open Project Settings > XR Plug-in Management, select the desktop platform tab, and temporarily disable Initialize XR on Startup. Restore it before testing with the headset. This is a local testing choice; check any resulting settings change before committing it to the team repository.
- **Cannot use a button:** empty your hand, aim at the button, and press F. F otherwise operates the held item.
- **Hard to pick up a tiny prop:** move closer and aim at its centre. Small-item targeting has a little tolerance.
- **Marshmallow stays white:** the campfire must be burning, and the marshmallow tip must be inside the heat area. Move the tip closer to the flames.
- **Water does not extinguish:** check where the blue stream lands. It must reach the fire's ground patch. Refill if empty.
- **No flame when starting:** the campfire begins unlit. Use the lighter or choose to respect the fire ban.
- **Fire returns:** the embers were still hot. Continue pouring until the cold state.
- **Old scene still has oversized labels:** stop Play mode and use Tools > Butterfly Effect > Repair Existing Prototype. The Create or Open command preserves an existing scene; it does not rebuild it.
- **Game view is zoomed or cropped:** move the Game view's Scale slider to 1x. This is an Editor viewing setting, separate from the scene's object scales.

Source syntax and archive integrity were checked outside Unity. The Unity Editor and a Quest were not available in the authoring environment, so actual compilation, Play-mode behaviour, rendering, XR behaviour, and video capture still require validation on your device.

Included EditMode tests cover all eight station-choice combinations, repeat fireworks counting, cleanup/history differences, persistent grass hazards, and the flames/embers transition. PlayMode regression tests cover carrying across movement/rotation, physics/XR restoration on drop, and tipping a carried bucket. Run the corresponding tests through **Window > General > Test Runner**. They have not been executed in Unity here.

Primary API references used for this implementation:

- https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@3.6/manual/xr-grab-interactable.html
- https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@3.6/api/UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-linearVelocity.html
- https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/rigidbody/rigidbody-interpolation
