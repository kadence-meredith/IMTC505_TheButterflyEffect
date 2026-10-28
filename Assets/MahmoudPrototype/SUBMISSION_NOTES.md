# Implementation 1 notes

Fill in the repository and recording links after committing and recording your own tested build.

- Team repository: [add the actual repository URL]
- Recording(s): [add accessible, unlisted video URL(s)]

## Interactions demonstrated

1. **Cigarette handling and disposal.** The player can hold, drop, throw, recover, and safely dispose of a lit cigarette. A predicted throw trajectory helps aim. Leaving it on dry grass starts a small fire; collecting the cigarette does not erase an existing grass fire. This implements the forest cigarette decision and the need to free a hand for another activity.

2. **Campfire and marshmallow roasting.** The player uses a lighter to ignite the campfire, holds a marshmallow over the heat, watches it turn golden or burn, and takes a bite. The interaction supports repeated servings and an explicit choice to leave the fire unlit. This implements the campsite temptation from the pitch and the refined roasting sequence.

3. **Water bucket and extinguishing.** The player carries and tips a bucket, aims a visible water stream, and uses a limited supply of water. The fire changes from flames to hot embers, then to cold. Stopping too early allows reignition; the bucket can be refilled. This implements the refined water-bucket interaction and the hot-embers debrief.

4. **Fireworks activation.** The player can leave fireworks unlit or trigger a rocket and coloured burst. Repeated launches are supported, while the choice tracker counts one unsafe fireworks station rather than penalizing every launch. This implements the pitch's fireworks temptation and repeated experimentation.

The scene uses primitive shapes so the interaction logic can be developed before the team's environment and final models are finished. The four stations are generated as separate prefabs and share choice data with the game manager. The desktop prototype uses keyboard and mouse controls; XR event bridges are included for later headset integration.

## Before submitting

- Verify every claim above in the version you actually record; edit the notes if your final behaviour differs.
- Commit scripts, scene, prefabs, materials, and all corresponding `.meta` files.
- Confirm the repository includes the scene, rather than only the scene-builder script.
- Confirm that every video link opens without signing into your own account.
- State the selected ending rule. OriginalPitch is the package default; RemainingHazards is an alternative for the refined narrative.
- Mention any known bugs or incomplete headset integration accurately.
