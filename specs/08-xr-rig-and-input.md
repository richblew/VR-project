# 08 XR rig and controls

The player rig, the controls on each headset, interaction layers, UI input and teleporting, and what we change on the room's rig at runtime. Back to the [main spec](README.md).

| | |
|---|---|
| **The rig** | `Player` in `room.unity` (the room team's): an instance of XRI 3.6.1's Starter Assets `XR Origin (XR Rig)` prefab |
| **Our side** | `VRRocket_Main` → `RoomLink` (`XRRigAdapter`), `XR Interaction Manager`, `GameFlow/EventSystem`, `RoomFit/TeleportFloor` |
| **Scripts** | `Assets/Scripts/Runtime/Room/XRRigAdapter.cs`; the part filters `NearOnlyGrabFilter.cs` and `AssemblyGrabGate.cs` ([spec 03](03-rocket-assembly.md)) |
| **Settings** | Edit > Project Settings > XR Plug-in Management (OpenXR), XR Interaction Toolkit (interaction layers) |
| **Tests** | `RoomIntegrationTests` (`Room_Loads_Once_With_One_Rig_EventSystem_And_Manager`, `Rig_Is_Adapted`, `Teleport_Areas_Cover_The_Floor_But_Not_The_Furniture`), `NearOnlyGrabTests` |

## 1. The rig

The player is the room team's `Player` object: XRI's ready-made **XR Origin (XR Rig)** from the Starter Assets sample (`Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/`). It stands at (−0.4, 2.4, 0.78), turned to face the big TV (−Z).

What it brings:

- a head camera and two tracked controllers;
- on each hand a **`NearFarInteractor`**: one interactor that grabs things within reach and casts a ray at things further away (menus, far objects);
- locomotion: smooth move, snap turn, teleport, and jump;
- `SimpleHapticFeedback` on the hands: a generic buzz on hover and select.

We never edit the rig or `room.unity`. Our needs are applied at runtime by `XRRigAdapter`.

## 2. `XRRigAdapter`: our changes to the rig at runtime

On `RoomLink`. When the room has loaded, it finds the first `XROrigin` and applies:

| Setting | Value | What it does | Why |
|---|---|---|---|
| Silence Generic Haptics | on | turns off hover and select buzzes on every `SimpleHapticFeedback` in the rig | the rocket plays its own designed vibrations for every event ([spec 09](09-feedback-audio-haptics.md)); the generic buzz would muddy them |
| Required Interaction Layers | Default, RocketPart | adds these layers to both `NearFarInteractor`s | rocket parts are on the RocketPart layer; without it the hands cannot touch them |
| Snap To Floor | on | casts a ray down from 1 m above the rig (up to 5 m, ignoring the rig's own colliders) and puts the rig on what it hits | `Player` is placed 2.4 m up in the room, so the player would otherwise drop at the start |
| Floor Mask | Everything | layers the floor ray can hit | |
| Enable Teleport | on | switches on every `TeleportationProvider` in the rig | |
| Force Floor Tracking | on | sets the rig's tracking origin to Floor and its camera Y offset to 0 | the room's rig asks for "Not Specified", which on Quest can become Device mode, where the rig adds its 1.36 m camera offset on top of the real head height: the player stood far too tall and had to bend to reach the desks (fixed 12 October 2026) |
| Enable Locomotion | on | switches the rig's `Locomotion` object back on if the room saved it off | the room's `Player` was saved with locomotion off (move, turn, teleport and gravity all stopped) |
| Fix Character Controller | on | resets a body capsule taller than 2.2 m, or one reaching below the feet, to 1.36 m standing on the feet | the room set the capsule to 3 m around a 0.76 m centre, so it reached 0.74 m under the floor and could lift the player |
| Log Rig State | on | 3 s after start, logs the rig's height, tracking mode, eye height and capsule (`adb logcat -s Unity` on Quest) | to diagnose height problems from a headset session |
| Disable Jump | on | switches off every `JumpProvider` | Jump is on the primary button (A on Quest). Vive wands do not have it, and it is easy to press by accident while building. |

Calling `Apply()` again is safe. `isApplied` tells you it ran. The test `Rig_Is_Adapted` checks every row of this table.

## 3. Controls

The Starter Assets rig binds these (`XRI Default Input Actions` in the sample folder). On this rig the **left** stick moves smoothly and the **right** stick teleports and turns.

| Action | Meta Quest 3 (Touch Plus) | HTC Vive wand | Valve Index |
|---|---|---|---|
| Grab a part or the rocket (near only) | grip | grip | grip |
| Point at the menu | the ray from either controller | same | same |
| Click the menu (START) | trigger | trigger | trigger |
| Press a console button | push it with the controller, from above | same | same |
| Twist the motor cap | grip the cap and turn the wrist or the hand around the rocket | same | same |
| Move | left thumbstick | left trackpad | left thumbstick |
| Teleport | right thumbstick forward, aim, release | right trackpad up, aim, release | right thumbstick forward |
| Snap turn | right thumbstick left or right | right trackpad left or right | right thumbstick left or right |
| Jump | A (switched off by `XRRigAdapter`) | (none) | A (switched off) |

When teleporting, the direction of the stick or trackpad sets which way you face on arrival.

The help board in the room shows a short version of this ([spec 05](05-screens-and-displays.md)).

## 4. Interaction layers

XRI interaction layers decide which interactor may touch which interactable. They are set in Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit (`Assets/XRI/Settings/Resources/InteractionLayerSettings.asset`):

| Index | Name | Used by |
|---|---|---|
| 0 | Default | most interactables, the console buttons, the menu |
| 1 | RocketPart | every rocket part (parts are on Default and RocketPart) |
| 31 | Teleport | the teleport areas |

These are XRI interaction layers, separate from Unity's physics layers.

## 5. Grabbing parts: near only

Rocket parts can only be grabbed with a hand within 12 cm. The full rules (who may grab what, the two-handed removal) are in [spec 03](03-rocket-assembly.md), section 3. In short:

- `NearOnlyGrabFilter` on each part refuses any hand further than 0.12 m from the part's collider, and refuses rays outright. A layer cannot do this, because one `NearFarInteractor` does both near and far.
- `AssemblyGrabGate` on each part asks `RocketAssembly.CanGrab`, which follows the game phase and the build rules.

## 6. UI input: clicking the menu

| Piece | Where | Job |
|---|---|---|
| `EventSystem` + `XRUIInputModule` | `GameFlow/EventSystem` | turns controller rays and trigger presses into UI clicks. There must be exactly one EventSystem; the room has none. |
| `TrackedDeviceGraphicRaycaster` | on `GameFlow/MenuCanvas` | lets the controller rays hit that canvas. Any new world-space canvas needs one too. |

The rig's `NearFarInteractor`s have UI interaction on, so their rays highlight and click the menu with the trigger ([spec 06](06-menu.md)).

## 7. The interaction manager

XRI needs one `XRInteractionManager`. The room scene has none, so `VRRocket_Main` has the root object `XR Interaction Manager`. The rig's interactors and all our interactables find it at runtime. `Room_Loads_Once_With_One_Rig_EventSystem_And_Manager` checks there is exactly one, plus one rig, one EventSystem and one AudioListener.

(`Dev_Interactions.unity` has its own manager for the same reason: without an explicit one, XRI creates one that can survive between tests.)

## 8. Teleporting

You can teleport onto `RoomFit/TeleportFloor`: six thin boxes (`TeleportArea_0` and `_2` to `_6`; `_1` was removed when the room was shortened) that cover the room's floor but leave out the console and the desk, so you cannot teleport inside furniture.

| `TeleportationArea` setting | Value |
|---|---|
| Interaction layer | Teleport |
| Match Orientation | World Space Up |
| Match Directional Input | on (the stick or trackpad direction chooses your facing) |
| Teleport Trigger | On Select Exited (teleport when you let go) |

`RoomBindings` can also turn room floors into teleport areas (its Teleport Floors list), but that list is empty: our own boxes are used. The test `Teleport_Areas_Cover_The_Floor_But_Not_The_Furniture` checks the spawn, the desk front and the window are covered and the console and desk are not.

If the room's layout changes (new furniture, a different floor), resize or add boxes under `RoomFit/TeleportFloor`, keeping them on the Teleport layer.

## 9. OpenXR: which controllers are supported

Edit > Project Settings > XR Plug-in Management > OpenXR. Enabled today:

| Platform | Features and interaction profiles |
|---|---|
| Windows (PC VR) | HTC Vive Controller, Valve Index Controller, Oculus Touch Controller, Meta Quest Touch Plus Controller |
| Android (Quest) | Meta Quest Support, Oculus Touch Controller, Meta Quest Touch Plus Controller |

A controller without a matching profile binds nothing, so add its profile here before testing a new headset. On PC, the headset's OpenXR runtime must be set: SteamVR for Vive and Index, the Meta Quest Link app for a Quest on Link. See [spec 11](11-builds-and-deploy.md).

## 10. Testing without a headset

- **The automated tests** drive simulated controllers, so they run on any computer ([spec 10](10-testing-and-debugging.md)).
- **XR Device Simulator:** the sample is imported (`Assets/Samples/XR Interaction Toolkit/3.6.1/XR Device Simulator`) but not switched on. To use it, tick Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit > **Use XR Device Simulator in scenes**, then press Play. This is a shared project setting: switch it off again before you commit.

## 11. Troubleshooting

| Symptom | Check |
|---|---|
| The player falls at the start | `XRRigAdapter` Snap To Floor is on and the floor has a collider. |
| The player is far too tall or too short | Force Floor Tracking must be on; in the headset, reset the floor height in the Quest's boundary settings if it is still off. |
| The hands cannot grab parts | Do the `NearFarInteractor`s have the RocketPart layer at runtime? Select a hand in Play mode (`Player/Camera Offset/Left Controller/...`) and look at Interaction Layer Mask. |
| The ray cannot click START | One `EventSystem` with `XRUIInputModule`; `TrackedDeviceGraphicRaycaster` on the canvas. |
| Every hover buzzes | Silence Generic Haptics must be on. |
| Pressing A makes the player jump | Disable Jump must be on. |
| Vive or Index controllers do nothing | Their interaction profile must be enabled for Windows, and SteamVR must be the active OpenXR runtime. |
| Teleport lands inside the console | A teleport box covers furniture: resize the boxes under `RoomFit/TeleportFloor`. |
