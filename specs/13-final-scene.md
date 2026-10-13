# 13 The final scene (`VRRocket_Final`)

The scene we ship: our game inside Richard's updated room, as one scene. Back to the [main spec](README.md).

| | |
|---|---|
| **Scene** | `Assets/Scenes/VRRocket_Final.unity` (the only scene in both build profiles) |
| **Made from** | `VRRocket_Main.unity` (our game) and `room.unity` (Richard's room, 13 October 2026). Both are kept unchanged |
| **Scripts** | `GameFlowController` (intro phase, abort alarm), `TransmissionBridge` (intro on demand), `WindowCurtain`, `AbortAlarm`, `BlueprintDisplay` (BACK and NEXT). Editor: `AbortScreenTools` (VR Rocket > Make Abort Screen), `WindowWallBuilder` |
| **Assets** | `Models/Environment/AlcoveWall_Window.asset`, `Models/Environment/Curtain_Panel.asset`, `Materials/Environment/Mat_Curtain.mat`, `Texture/Curtain/` (Poly Haven linen), `Texture/Screens/Abort_Screen.png`, `Materials/Screens/Mat_AbortScreen*.mat` |
| **Tests** | `FinalSceneTests` (12) and `GameFlowTests` (4 complete games), see [spec 10](10-testing-and-debugging.md) |

## 1. How it differs from `VRRocket_Main`

- **One scene.** The room's objects are part of this scene. There is no `RoomSceneLoader` or `RoomBindings`: everything is placed where it belongs and saved. `RoomLink` keeps only `XRRigAdapter` and `TransmissionBridge`.
- **Edit it directly.** `VRRocket_Final` is a normal scene now. Changes to `room.unity` or `VRRocket_Main` no longer reach it. One person edits it at a time.
- **Room:** Richard's new layout. The help board and our old front-room window are gone.
- **Posters:** the portrait rocket-launch poster hangs on the front room's right wall (x -3.9), centred between the corner TVs and the angled wall; the landscape moon-landing poster is centred on the alcove's right wall beside the console.
- **Window:** one large window on the alcove's left inner wall (x -1.98, z 0.45 to 3.55, y 1.0 to 3.4, two mullions), where the rocket poster was. The launch pad stands straight out from it at (48, 0.3, 2). 
- **Outside the window:** the floor and roof are split at z 0.25 and the back wall ends at x 2.3, so the window looks out and the room stays closed everywhere else.
- **Kept from our room:** the populated console (SUBMIT, LAUNCH, ABORT, the bin, and the console mirror that repeats the big TV), the blueprint panel and build-step monitor on the desk, the three back-wall portraits with plaques, the big TV and its side TVs.
- **Console monitor:** `ConsoleAddOn/ConsoleMonitor`, one wide monitor (0.94 x 0.30 m screen, dark housing) over the console's three centre screens; the two angled side screens stay as they are. The console mirror is laid out across it: the pad view on the left, title and clock on top right, the full status text under them. While the mirror is hidden (menu, intro) the monitor shows a black screen. The builder measures the three screens from the console's `Monitors` mesh.
- **Console side monitors:** two more monitors in the same style over the console's angled side screens (`ConsoleMonitorLeft`, `ConsoleMonitorRight`, driven by `ConsoleStatusScreens` on the left one). With all five native screens covered, the console's own `Monitors` mesh is switched off.
  - **Left, GO / NO-GO:** a launch readiness poll: FINS n/3, FLAPS n/3, NOSE CONE, MOTOR, MOTOR CAP, PAD, each GO, NO-GO or STBY (standby on the menu and in the intro), and a verdict line (HOLD BUILD IN PROGRESS, READY TO SUBMIT, ALL STATIONS GO, GO FOR LAUNCH, LIFTOFF). It counts fitted parts only; it never says whether the flaps are the right way round or the cap is locked, because finding that out is the game.
  - **Right, FLIGHT DATA:** RANGE STATUS while idle and building (weather, wind and range are scenery; pad state and the mission log of flights, successes and the last result are live), the T-minus during the countdown, live T+, altitude, speed and max altitude in flight, then FLIGHT n RESULT with the outcome.
- **Countdown clock:** `CountdownClock` covers the whole face of the top-left side TV, so none of the TV's own picture shows around it.
- **Console nameplate:** the small A-frame plate on top of Richard's console was plain black. `ConsoleAddOn/ConsoleNameplateLabel` lays a label screen on its player side reading LAUNCH STATION (Orbitron, the console monitor's title colour). Change the words in `LabelText`.
- **Teleport floor:** six areas that follow the new room (front, between the angled walls, both sides of the console, the alcove aisle).

## 2. The menu and the intro

The menu above the console has two buttons:

| Button | Calls | What happens |
|---|---|---|
| START WITH INTRO | `GameFlowController.StartGame` | phase `Intro`: Richard's intro plays (NASA logo, standby video, announcement, red alert, dimmed room, transmission video). Parts stay locked. Building starts when the intro ends. |
| SKIP INTRO | `GameFlowController.StartWithoutIntro` | straight to building, the intro never starts |

The same two buttons come back after a successful flight.

How it works: the room's `TransmissionController` has `playOnStart` off in this scene, so it waits. `TransmissionBridge.Play` starts it (`PlaySequenceNow`) and watches `sequenceFinished`. `TransmissionBridge.Stop` ends it cleanly at any time (lights back, alert off, logo on the big TV). `GameFlowController.SkipIntro` ends a running intro and starts building; it is not wired to a button today.

`VRRocket_Main` is unchanged: there the intro plays by itself under the menu and START ends it.

## 3. The window curtain

`AlcoveCurtain` (`WindowCurtain`): two pleated panels on a rail in front of the window, made from Poly Haven's CC0 "Rough Linen", darkened.

- The curtain follows how dark the intro has made the room (`TransmissionBridge.dimAmount`). It is fully drawn at 60 % dim and opens again as the lights come back.
- It moves at most 0.8 of its travel per second, so it slides rather than jumps.
- Open, each panel is gathered 0.3 m wide at a window end. Closed, each covers 1.85 m and they meet in the middle.
- With SKIP INTRO it never moves.

## 4. BACK and NEXT on the desk

Two push buttons at the player's edge of the assembly desk (z 2.65, 24 cm from the front edge, right of the kit, under the step text screen), so they are pressed without reaching across the desk. They call `BlueprintDisplay.ShowPreviousStep` and `ShowNextStep`: the panel and the build-step monitor show "VIEWING STEP n / 5" with that step's text and picture, stopping at step 1 and step 5. The next change to the build (a part attached or removed) returns both screens to the live step.

## 5. The abort alarm

Pressing ABORT during pre-launch or the countdown:

1. stops the countdown and the engine, clears the pad, and returns the same rocket to the bench, editable ([spec 02](02-game-engine.md));
2. `AbortAlarm` (child of `GameFlow`) shows a red ABORT panel over each of the four side TVs around the big TV and flashes them between a bright and a dim version, 2.5 flashes a second, while a two-tone siren (620 and 880 Hz, generated in code) sounds through its own 2D AudioSource. The top-left panel covers the countdown clock;
3. the big TV and the console mirror say LAUNCH ABORTED;
4. after 4 seconds the panels hide again, the siren stops, and the big TV shows the build again.

**Why panels and not the TVs' own material?** Each side TV is one mesh with one material: a picture put on that material wraps around the whole TV body. So the builder lays a quad (`AbortPanel_*`, saved switched off) on each TV's front face, measured from the TV mesh. The TVs are the intro's own `sideScreens`: if Richard changes them, rebuild the panels. To change the picture (1024 x 828, the TVs' shape), edit `AbortScreenTools` and run VR Rocket > Make Abort Screen.

Pressing ABORT outside pre-launch and the countdown only buzzes, as before.

## 6. The console's wiring (Yi-Xuan's cable puzzle)

Yi-Xuan's mini game (branch `test-hand-models`, merged 13 October 2026): the console is not operational until its wiring is connected. Her scripts are in `Assets/CablePuzzle` (`CablePairManager`, `PairSocket`, `CableConnector`, `VerletCable`).

**Where it is:** `PuzzleBox` sits in a well cut into the console's control panel between SUBMIT and LAUNCH. The panel's top is split into `PanelBody_Left`, `_Right`, `_Back`, `_Front` and `_UnderWell` around the opening (the original `PanelBody` is kept, switched off). The box walls are dark metal, so it reads as the inside of the console.

**The hatch:** `ConsoleAddOn/ControlPanel/WiringHatch`, a tread-plate cover (ambientCG DiamondPlate008D) with a bar grip (RuslanOz's Handles). `SlidingPanel` on `HatchGrip`: grab the grip and pull it toward LAUNCH; the cover slides into a slot under the panel surface (0.33 m of travel), the grip stays at the slot's mouth. Click and a controller pulse at each end. While the hatch is less than half open, every grabbable in `PuzzleBox` is switched off, except ones held or plugged in, so nothing is grabbed through the lid.

**The puzzle:** five pairs. Each pair has a plug (icvetlan's Schuko plug) on one cable and a socket (Artik's Plug Socket) on another, both in the pair's colour; `PairSocket` accepts only its own plug. The cables are round rubber tubes (`CableTube`, ambientCG Rubber004 tinted per pair) drawn along her Verlet points; her `LineRenderer` ribbons are switched off.

**The rule:** `GameFlowController` has **Require Mini Game** on in this scene. Until `MiniGameSolved()` is called, the console is offline: SUBMIT refuses the rocket (so LAUNCH and ABORT have nothing to act on), and the GO / NO-GO monitor shows WIRING NO-GO and CONSOLE OFFLINE · RESTORE WIRING. `CablePairManager.onAllConnected` calls `MiniGameSolved()` (wired in the scene). Once solved it stays solved; resetting it for a new play needs the puzzle to unplug itself first (TODO for Yi-Xuan in `GameFlowController`).

**Yi-Xuan's rig:** her hand-model rig (`RoomAnchor/Player`) and her physical-room calibration (`RoomLink_Calibration`: `RoomCalibrator`, `WalkableZone`) are in the scene, switched off. With them on, the player floated a metre up, part grabs failed and locomotion was off. Our `Player` and `RoomLink` run the game until her rig grabs parts and stands on the floor; then switch hers on and ours off.

## 7. Troubleshooting

| Problem | Cause and fix |
|---|---|
| The intro plays by itself under the menu | `TransmissionController.playOnStart` is on. Switch it off in this scene. |
| START WITH INTRO goes straight to building | `GameFlowController` has no Intro (should be `RoomLink`'s `TransmissionBridge`), or the room has no intro |
| The curtain never closes | `AlcoveCurtain`'s Intro is empty, or the intro does not dim the room (`currentDim` stays 0) |
| SUBMIT always refuses | the wiring is not connected yet (GO / NO-GO says CONSOLE OFFLINE), or `CablePairManager.onAllConnected` lost its call to `GameFlowController.MiniGameSolved` |
| The hatch does not move | grab `HatchGrip` itself (the bar), not the plate; check `SlidingPanel` has its Cover and Contents set |
| ABORT works but nothing flashes | `GameFlowController`'s Abort Alarm is empty, or `AbortAlarm` has no panels |
| The ABORT panels float off the TVs | a side TV moved: move the matching `AbortPanel_*` with it, or rebuild the scene |
