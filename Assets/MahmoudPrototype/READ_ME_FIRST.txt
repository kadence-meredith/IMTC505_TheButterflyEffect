MAHMOUD PROTOTYPE - DIRECT SCRIPT REPAIR (V3)

This is a direct file replacement for the previously imported prototype.
Use Windows File Explorer for this repair.

1. In Unity's Project window, right-click Assets/MahmoudPrototype and choose
   Show in Explorer. This locates the correct folder on your computer.
2. Close the Unity Editor. Leave File Explorer open.
3. Extract this ZIP into a separate folder, for example on your Desktop.
4. Copy the THREE folders from the extracted ZIP: Runtime, Editor, Tests.
5. Paste those three folders INTO your existing project folder:
   Assets/MahmoudPrototype
   Choose Replace the files in the destination when Windows asks.
   The result must have paths such as:
   Assets/MahmoudPrototype/Runtime/DesktopPlayer.cs
   Assets/MahmoudPrototype/Editor/PrototypeSceneBuilder.cs
   Assets/MahmoudPrototype/Tests/PlayMode/DesktopCarryTests.cs
6. Reopen the project through Unity Hub and wait for compilation.
7. Open the Console and clear the old messages. The two CS1061 errors should
   be resolved because the runtime files now match the tests.
8. Choose Tools > Butterfly Effect > Repair Existing Prototype.
   This also saves backups of your generated scene and prefabs.
9. Game tab: Scale 1x. Press Play, click in the Game view, and try WASD.
   You START with the cigarette in hand. E puts it down. Aim and tap E once
   to pick it up again; you do not need to hold E while walking.

Keep the existing Generated folder. It holds your scene and prefabs.
This repair archive contains no Generated folder, no scene or prefab assets,
no project settings, and no changes to other team folders.

The .meta files inside the three folders preserve the existing script IDs.
The scripts remain under Assets/MahmoudPrototype after the replacement.

This archive already contains the code files themselves. Do not also copy
an old Source backup from the first ZIP into your Assets folder.

Source syntax, archive bytes, required members, and preserved script IDs
were checked. Unity and Quest execution were not available in the authoring
environment, so the actual compile and carry test must run on your device.
