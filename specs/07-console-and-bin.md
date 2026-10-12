# 07 Console buttons and the submission bin

The three push buttons (SUBMIT, LAUNCH, ABORT), the submission bin, and the invisible colliders we add to the room's console. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Main` → `ConsoleAddOn` (saved switched off, see section 1) |
| **Scripts** | `Assets/Scripts/Runtime/SubmissionBin.cs`, `ButtonPressHaptics.cs`; the button itself is XRI's example `XRPushButton` (`Assets/ThirdParty/XRI_Examples/UI_3D/Scripts/XRPushButton.cs`) |
| **Prefab** | `Assets/Prefabs/Console/PushButton.prefab` |
| **Tests** | `RoomIntegrationTests` (`Console_Buttons_And_Start_Are_Wired`, `Console_Buttons_Press_With_The_Hand`), `GameFlowTests`, `FullFlowTests`, `RespawnSafeZoneTests` |

## 1. `ConsoleAddOn`

The room's console (`Environment/Mission Control Desk/hq`) is a model with no colliders and no buttons. `ConsoleAddOn` adds everything the game needs on top of it:

```
ConsoleAddOn                 follows the room's console (RoomBindings), saved switched off
├─ ControlPanel              the tilted button panel on the shelf
│  ├─ PanelBody, PanelTrim   the panel itself (with colliders)
│  ├─ SubmitButton           PushButton.prefab → SubmissionBin.Submit
│  ├─ LaunchButton           PushButton.prefab, green → GameFlowController.Launch
│  ├─ AbortButton            PushButton.prefab, red   → GameFlowController.Abort
│  └─ SubmitButtonLabel, LaunchButtonLabel, AbortButtonLabel   text on the panel in front of each button
├─ BinOpening                the bin's look: black hole, green rim, shaft walls (with colliders), "SUBMISSION BIN" label
├─ SubmissionBin             trigger box + SubmissionBin + AudioSource
│  └─ ReturnSpot             where a failed rocket comes back
├─ ConsoleZone               trigger box over the shelf: a "safe zone" for parts
├─ ConsoleColliders          invisible boxes: console body, keyboard, the shelf around the bin
└─ ConsoleMirror             the console monitor (spec 05)
```

**Why it is saved switched off.** XRI's push button remembers its world position in `Start`. `RoomBindings` first moves `ConsoleAddOn` onto the room's console (wherever the room team put it), then switches it on, so the buttons start in their final place ("Activate After Placing", [spec 01](01-scenes-and-room.md)). If you edit it, **switch it off again before saving** the scene.

**Where things are** (world positions, facing the big TV; +x is the player's left):

| Thing | Position |
|---|---|
| Control panel | root at (0.00, 0.72, −0.23), tilted 20° toward the player; its back edge rests on the shelf |
| SUBMIT | x −0.55 |
| LAUNCH | x 1.05 |
| ABORT | x 1.40 |
| Button height | about 0.90 m |
| Bin opening | (−1.05, 0.78, −0.30), 0.46 m square, on the shelf at the right end |
| Return spot | (−1.62, 0.80, −0.30), on the shelf right of the bin |

## 2. The push buttons

Each button is an instance of `PushButton.prefab`:

```
PushButton                XRPushButton, ButtonPressHaptics
├─ Feedback Effects       XRI affordance system: tints the button and plays a sound on hover and press
└─ PushButton Visuals     the button model (PushButton.fbx from the XRI examples)
```

**How a press works** (`XRPushButton`):

- Any hand that is **not a ray** can push it: the player pokes it with the controller. Rays are ignored.
- The hand must come from **above**, within the button's area (Button Size 0.1), and push the top down more than **Press Distance** (0.03 m). Then **On Press** fires once. Letting it come back up fires **On Release**.
- The settings on the prefab: Press Distance 0.03, Press Buffer 0.01, Button Offset 0.14, Button Size 0.1, Toggle off. Interaction layer: Default.

**What each button calls** (its **On Press** list in the Inspector):

| Button | On Press, entry 1 | On Press, entry 2 | Colour |
|---|---|---|---|
| `SubmitButton` | `ButtonPressHaptics.OnPress` | `ConsoleAddOn/SubmissionBin` → `SubmissionBin.Submit` | the prefab's own |
| `LaunchButton` | `ButtonPressHaptics.OnPress` | `GameFlow` → `GameFlowController.Launch` | `Mat_AccentGreen` |
| `AbortButton` | `ButtonPressHaptics.OnPress` | `GameFlow` → `GameFlowController.Abort` | `Mat_AccentRed` |

On Release calls `ButtonPressHaptics.OnRelease`.

**`ButtonPressHaptics`** sends a crisp click to the hand that is pressing (strength 0.6 for 30 ms on press, 0.25 for 20 ms on release, from `FeedbackLibrary`, [spec 09](09-feedback-audio-haptics.md)). It finds the hand among the button's hovering or selecting interactors.

Pressing a button at the wrong time is harmless: LAUNCH and ABORT buzz, SUBMIT is refused ([spec 02](02-game-engine.md)).

## 3. The submission bin

**What the player does:** carry the finished rocket to the console, drop it into the bin on the right end of the shelf, let go, and press SUBMIT.

**How it works** (`SubmissionBin` on `ConsoleAddOn/SubmissionBin`):

1. The object has a **trigger box** (0.44 × 0.60 × 0.44 m) filling the bin's opening and shaft.
2. When a collider of a body tube enters the trigger, the bin remembers that rocket. When it leaves, it forgets it.
3. **Submit** (from the SUBMIT button) checks:
   - a rocket is inside, its object is active, and **nobody is holding it**;
   - the rocket's state is **PrototypeComplete** ([spec 03](03-rocket-assembly.md)).

   If not, it raises `onRefused` and stops.
4. It calls `RocketAssembly.TrySubmit()`. If that refuses too, `onRefused`. Otherwise `onAccepted`, and the rocket is frozen and the game engine moves to PreLaunch.
5. The bin plays the door-close sound and **sinks** the rocket 0.45 m down over 1.2 s, then hides it. If the player aborts during the sink, it stops.

**Fields:**

| Field | Value |
|---|---|
| Assembly | `RocketWorkstation` (`RocketAssembly`); found automatically if empty |
| Return Spot | `SubmissionBin/ReturnSpot` |
| Sink Depth | 0.45 m |
| Sink Seconds | 1.2 s |
| Audio | its own `AudioSource` |
| Close Clip | `Assets/Audio/Kenney/doorClose_000.ogg` (on submit) |
| Open Clip | `Assets/Audio/Kenney/doorOpen_000.ogg` (when a rocket comes back) |
| On Accepted, On Refused | UnityEvents, empty today: hook a light or a sound here if you like |

**Coming back:** on ABORT the same rocket returns to the desk; after a failed launch it returns at `ReturnSpot` as the inspection prototype ([spec 03](03-rocket-assembly.md)). Both play the bin's open sound (`PlayOpen`).

**Safe zones:** the bin's trigger and `ConsoleZone` (a 4.0 × 0.4 × 0.6 m trigger over the shelf) are registered with `PartRespawner` as safe zones, so a rocket or part resting there never jumps back to the desk, even though the bin shaft is lower than the desk.

## 4. The invisible console colliders

`hq` has no colliders, so without help the player's hands, the teleport ray and dropped parts would pass through it. `ConsoleAddOn/ConsoleColliders` adds box colliders, no meshes:

| Collider | Covers |
|---|---|
| `HQ_Body` | the console's main body |
| `HQ_Keyboard` | the keyboard |
| `HQ_ShelfLeft`, `HQ_ShelfRight` | the shelf on either side of the bin |
| `HQ_ShelfBinFront`, `HQ_ShelfBinBack` | the shelf strips in front of and behind the bin opening |

If the room team changes the console model, these boxes may need resizing: select them and use the box collider's **Edit Collider** button, with `room.unity` open additively.

## 5. Recipes

**Wire a button to something else.** Select the button (for example `ConsoleAddOn/ControlPanel/LaunchButton`), find **On Press ()** on `XRPushButton`, and change the second entry's object and method. Keep the first entry (`ButtonPressHaptics.OnPress`) for the click in the hand. Then update `Console_Buttons_And_Start_Are_Wired` in `RoomIntegrationTests`, which checks the wiring.

**Add a new button.** Drag `Assets/Prefabs/Console/PushButton.prefab` under `ControlPanel`, place it on the panel (its up axis must point out of the panel), and wire **On Press**. Add a label like the others. Switch `ConsoleAddOn` off before saving.

**Make the buttons easier or harder to press.** Change Press Distance on the prefab (all buttons) or on one instance.

**Replace the bin with your own.** Your bin must call `RocketAssembly.TrySubmit()` with a complete rocket that nobody holds, and keep the SUBMIT button wired to it. Keep a return spot for failed rockets and pass it to the game engine (`GameFlowController.Bin` uses `returnSpot` and `PlayOpen`). See `Docs/INTEGRATION.md` section 4.

## 6. Troubleshooting

| Symptom | Check |
|---|---|
| The buttons cannot be pressed | `ConsoleAddOn` was saved switched on, or moved after it started: save it off. Push from above with the controller, not the ray. `Console_Buttons_Press_With_The_Hand` tests this. |
| SUBMIT does nothing | The rocket must be complete (cap seated), let go of, and inside the bin's trigger. The trigger only reacts to the body tube's collider. |
| The rocket bounces out of the bin | The shaft walls and the rim have colliders; check they are still there after editing `BinOpening`. |
| The rocket jumps back to the desk from the bin | The bin's trigger must be in `PartRespawner`'s **Safe Zones** on the `RocketWorkstation` instance. |
| No click in the hand on press | `ButtonPressHaptics` needs an `AssemblyFeedback` in the scene (on `RocketWorkstation`) and a non-zero `hapticButtonPress` in `FeedbackLibrary`. |
| Hands or the teleport ray go through the console | `ConsoleColliders` is missing or out of place (the room's console changed). |
