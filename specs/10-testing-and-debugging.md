# 10 Testing and debugging

How to run the automated tests, what each one checks, how they work, and a playbook for finding faults. Back to the [main spec](README.md).

| | |
|---|---|
| **Test code** | `Assets/Scripts/Tests/Editor/` (edit mode, assembly `VRRocket.Tests.Editor`) and `Assets/Scripts/Tests/PlayMode/` (play mode, assembly `VRRocket.Tests.PlayMode`) |
| **Totals** | 40 edit-mode tests, 44 play-mode tests. All passed on 14 October 2026. |
| **Run them** | Window > General > **Test Runner** |

## 1. Running the tests

1. Open **Window > General > Test Runner**.
2. **EditMode** tab: click **Run All**. These take a few seconds: they test the plain C# logic without a scene.
3. **PlayMode** tab: click **Run All**. These load real scenes and take a few minutes. Do not click in the Editor while they run.
4. Green tick = passed, red cross = failed. Click a failed test to see its message at the bottom. Right-click a test (or a class) > **Run** to run just that one.

Run both tabs before you push any change to VR Rocket's scripts, prefabs, scenes or settings, and after any change to `VRRocket_Final`.

**From the command line** (with the Editor closed), Unity's standard test runner works:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe" -batchmode -projectPath "<path to the project>" -runTests -testPlatform EditMode -testResults editmode.xml
```

Use `-testPlatform PlayMode` for the play-mode suite. The results are NUnit XML.

## 2. What the tests check

### Edit mode (39)

| Class | Tests | Checks |
|---|---|---|
| `AssemblyStateMachineTests` | 7 | forward path through the states, backward moves, gating with the strict order on and off, removal rules, reset |
| `BuildReportTests` | 5 | the outcome rule: locked and correct is Success, a wrong flap is UnstableFlight, an unlocked cap is MotorRetentionLoss (and wins), every failed part is listed, an upside-down flap is wrong |
| `CapTwistLogicTests` | 7 | the ratchet: progress kept across grabs, never unwinds, a tick every 30°, locks exactly at 180°, lock reported once, reset on pull-off |
| `GuideMathTests` | 7 | rail geometry, clamping, nose magnetism, break-away, orientation tolerance, seated rotations of nose, fin and flap |
| `AttachPointGeneratorTests` | 1 | the generator makes the 12 points of `attach_points.json` correctly, and running it twice adds no duplicates |
| `RocketFlightModelTests` | 4 | success lifts off and climbs, the mass falls during the burn, unstable flight tilts, tumbles and explodes, retention loss never leaves the pad |
| `StaticResetsTests` | 2 | `AssemblyEvents` and our singletons are cleared when Play mode starts (the project enters Play mode without a domain reload) |
| `FeedbackDeviceTuningTests` | 2 | Vive gets strength and duration floors and the runtime frequency; Index keeps the frequency; Touch Plus is untouched |
| `BlueprintDisplayTests` | 3 | every build stage shows its step text and the matching picture on the build-step monitor; BACK and NEXT browse the steps (stopping at 1 and 5) until the build changes; without a screen only the text changes |
| `ScreenTextTests` | 1 | the big screen's "first sentence" cut at ". " and " \| " |

### Play mode (44)

Most use `Dev_Interactions.unity` (a grey test bench with only the kit and a rig); the last two classes use `VRRocket_Final`, the scene we ship. `VRRocket_Main` has no tests since it was retired on 13 October 2026.

| Class | Tests | Checks |
|---|---|---|
| `FinFlapMotorTests` | 4 | fins in any order, snapped straight, removable; flaps in all six slots either way up, placements recorded, the same feedback for right and wrong; motor refused before the airframe (strict order) and nozzle up, then seated nozzle down; cap refused before the motor; free order accepts the motor early |
| `ConsoleFeedbackTests` | 11 | a plug dropped out of the box comes back (no guard keeps it out); LAUNCH and ABORT on a dead console say CONSOLE FAULT; with nothing on the pad they say so and the screen comes back after 4 s; a refused press gives the pressing hand the double buzz; SUBMIT with an empty bin or a rocket in the hand says so; an unfinished rocket comes back to the workbench with the missing parts listed and is not a flight; the failed rocket in the bin is not submitted; the cap before the battery says BATTERY FIRST once; a plug at the wrong socket says WRONG SOCKET; the launch vehicle has the player's flaps where and how they were put; two games back to back, the first with the cap left unscrewed (the battery blows out), the second with the fins one slot round and a flap upside down in a Low slot: the vehicle matches each build, fins at full size, the battery its own size |
| `SlotGlowTests` | 6 | a fin's and a flap's own glow is a band that shows on both faces, with a double-sided material; a held flap or fin lights only the nearest slot that fits it (Mid and Low flap slots, fin slots), brighter as it nears and full on the track; held between two slots the light does not flicker, and moves once the other is clearly nearer; the slot strips are wider than a part's edge |
| `NoseGuideTests` | 2 | the nose's approach, capture, slide, break-away, seat and removal; engaging and breaking away never jump |
| `CapTwistTests` | 6 | the twist hint: no arrow at once, then the arrow (travelling the locking way) and the ghost cap; while twisting the ring lights a segment per detent; let go early the ring keeps the progress and the arrow returns; the lock flashes all six segments, then the hint goes; the cap seats unlocked with its gap, ratchets clockwise and locks; letting go early keeps the seat and progress; pulling off resets |
| `FullFlowTests` | 5 | build, carry, submit, failed launch and new kit; submit then abort returns it editable; attached parts follow the carried rocket and dropped parts respawn; parts do not lag behind the rocket frame by frame; a dropped rocket returns to the desk |
| `BatteryInsertTests` | 9 | not caught before its + end is in the base (close below, off to the side, leaning); caught barely in and leaning 40 degrees, and deep in, straightening without moving in or out, then seats straight; stays on the axis through 15 cm of drift and a 45 degree lean of the hand; a tube turned 30 degrees mid-insert still seats it straight; let go outside the base it drops; pulled back out it comes free; the wrong way up it is kept out; its glow ring is on the + end |
| `NearOnlyGrabTests` | 1 | parts refuse a far hand and accept a near one |
| `RespawnSafeZoneTests` | 1 | a part resting in a safe zone below the floor line stays put |
| `GameFlowTests` | 4 | complete games in `VRRocket_Final`, started with SKIP INTRO: an unlocked cap fails, comes back for inspection and a new kit appears; a locked cap flies past orbit altitude (live flight data on the side monitor) and returns to the menu with a new kit; ABORT during the countdown puts the engine out, clears the pad and returns the same rocket editable (with the alarm); buttons pressed in the wrong phase do nothing |
| `LaunchEffectsTests` | 7 | ignition lights under the rocket and rolls smoke along the ground; liftoff climbs on its flame with a trail while the pad fire dies; one wrong flap veers off and crashes in the window's view; two or more break up in the air and a reset clears it; every vehicle part is 36 times its kit part; a blown-out battery and cap come back at their own size; ABORT during ignition puts everything out |
| `FinalSceneTests` | 13 | one scene with one rig, EventSystem, manager and AudioListener, no loader, help board or old window, a curtain; the rig is adapted (generic buzz off, Jump off, floor tracking, capsule, locomotion, hands that grab parts); the menu offers START WITH INTRO and SKIP INTRO; SKIP goes straight to building and switches the game displays on; START WITH INTRO plays the intro, the curtain closes and reopens, building starts when it ends; SKIP during the intro; the ABORT panels sit on the four side TVs, flash with the siren and hide again; the countdown clock covers its whole TV; the side monitors poll the build (GO / NO-GO, never revealing the cap lock) and show range status, with the console's own screens hidden; console and step buttons wired and pressed by a hand; only the window glass between the player and the rocket; teleport areas cover the floor but not the furniture |

## 3. How the play-mode tests work

All play-mode tests share `DevSceneTestBase`:

1. It loads the test's scene (`Dev_Interactions` unless the class overrides `scenePath`). A scene with a `RoomSceneLoader` would also be waited for; none has one now.
2. It takes over the rig's **right controller**: it switches off the controller's tracking (`TrackedPoseDriver`) and XRI's input modality manager, and makes the hand's attach point follow rigidly.
3. Tests then **move the controller by setting its position** and grab and release through the interaction manager, exactly as a real hand would trigger XRI.
4. Instead of a second hand, the body tube is parked **upright above its tray slot and frozen**, which counts as "held in the other hand".
5. Every `AssemblyEvents` moment is logged, so tests can assert that a guide engaged, a part seated, a detent ticked.

Haptics and how things feel can only be checked in a headset.

**Tests that depend on the room** (`VRRocket_Final` tests): they use the room's objects in that scene: the side TVs and the intro (`TransmissionController`, `Incoming_Transmission`, `Transmission_Video`, `stand_by`), the console's `Monitors` mesh, the window, the floor and desk. Changes Richard makes to `room.unity` no longer reach `VRRocket_Final`; room changes are made in `VRRocket_Final` itself, then run these tests.

## 4. Debugging playbook

### Read the Console first

Our scripts log clear messages with a prefix:

| Message starts with | Meaning | Spec |
|---|---|---|
| `RoomBindings:` (warning) | a room object is missing or moved | [01](01-scenes-and-room.md) |
| `RoomSceneLoader: could not load` (error) | the room scene is not in the build profile, or its path changed | [01](01-scenes-and-room.md) |
| `XRRigAdapter: no XR Origin` (warning) | the room has no rig, or it failed to load | [08](08-xr-rig-and-input.md) |
| `RocketAssembly.BeginNewBuild: the workstation has no kit prefabs` (error) | a part prefab field on `RocketWorkstation` is empty | [03](03-rocket-assembly.md) |
| `GuideGrabTransformer needs a RocketGrabInteractable` (error) | a part uses a plain `XRGrabInteractable` | [03](03-rocket-assembly.md) |

### Tools that help

- **XR Interaction Debugger** (Window > Analysis > XR Interaction Debugger): in Play mode, shows every interactor and interactable, what each hand hovers and selects, and their interaction layers. The first stop for "I cannot grab it".
- **Physics Debugger** (Window > Analysis > Physics Debugger): shows colliders. Use it for the rocket's compound colliders (`Compound_*` under the tube), the console colliders, the bin trigger and the teleport boxes.
- **Screen previews**: the ⋮ menu of `LaunchScreenController` on `GameFlow` > Preview > ..., to see any screen stage without playing ([spec 05](05-screens-and-displays.md)).
- **Slow motion**: Edit > Project Settings > Time > Time Scale 0.2 (put it back to 1 afterwards). The countdown, flight and every timer slow down with it.
- **The screens as a status display**: the big screen's title shows the phase and the build state (for example ASSEMBLY | MOTOR FITTED), the blueprint panel shows the counts of attached parts. `GameFlowController.phase` and `RocketAssembly.State` are properties, so the Inspector does not show them.
- **The Scene view in Play mode**: watch the pad from close by, select `LaunchSite/LaunchVehicle` during a flight, or check where `ConsoleAddOn` ended up.

### Things that work differently in this project

- **No domain reload on Play.** Edit > Project Settings > Editor > Enter Play Mode Settings is set to reload neither the domain nor the scene, so Play starts fast. Static fields keep their values from the last Play session unless reset. Our statics reset themselves with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`. If something works the first time you press Play and breaks the second time, look for a static that is not reset.
- **Objects saved switched off on purpose**: `ConsoleAddOn` and `WindowWall` ([spec 01](01-scenes-and-room.md)). Saving them on causes unpressable buttons or double walls.
- **Two scenes:** an object you cannot find in `VRRocket_Main`'s Hierarchy in Play mode may be in `room.unity`, listed under its own scene header in the Hierarchy.

## 5. Common problems

| Symptom | Likely cause | Fix |
|---|---|---|
| Nothing can be grabbed | still in the menu, or the hands lack the RocketPart layer | press START; see [spec 08](08-xr-rig-and-input.md) |
| A part refuses to attach | wrong way round, slot taken, or the rules refuse it (cap before motor) | [spec 03](03-rocket-assembly.md) section 6 |
| SUBMIT does nothing | rocket incomplete, still held, or not in the bin's trigger | [spec 07](07-console-and-bin.md) |
| LAUNCH buzzes | vehicle still growing, or not in PreLaunch | [spec 02](02-game-engine.md) |
| Console buttons do not press | `ConsoleAddOn` saved switched on | [spec 07](07-console-and-bin.md) |
| Room dark, sky grey | `VRRocket_Main` lighting changed, or the room became the active scene | [spec 01](01-scenes-and-room.md) |
| Intro plays on after START | `TransmissionController` or its fields renamed | [spec 01](01-scenes-and-room.md) |
| Big screen black during launch | pad camera or its render texture unset | [spec 04](04-launch-simulator.md) |
| Text blurry in the headset | MSAA off in the URP asset | [spec 11](11-builds-and-deploy.md) |
| Works in the Editor, not in a build | the room scene missing from the build profile | [spec 11](11-builds-and-deploy.md) |
| A test fails only when another Unity Editor is open | timing; close the other Editor and rerun | |

## 6. Only a headset can check

Keep testing these on the real hardware (also listed in `Docs/PORTING.md` section 5):

- Haptics: are the soft ticks felt on Vive wands (device tuning)? Is there no generic buzz on hover?
- A full build with the binary grip of Vive wands, and trackpad locomotion.
- Legibility of the big screen from the console, the corner clock, the console monitor and the menu.
- Heights: the buttons at 0.90 m on the tilted panel, the 1.125 m assembly desk.
- Is the vehicle fully visible through the window from the console?
- Frame rate: 90 Hz held through the countdown and flight, on PC VR and on Quest 3.
