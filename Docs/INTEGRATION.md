# Integrating with the rocket

For Yi-Xuan and Richard: how the rocket connects to the rest of the experience, what it tells you, and how to replace the working versions of your systems with your own. Behaviour is specified in `Docs/SPEC.md`; how the port sits on your room is in `Docs/PORTING.md`.

## 1. Where the rocket lives

- `Assets/VRRocket/Prefabs/RocketWorkstation.prefab`, placed in `VRRocket_Main` on your assembly desk at (−0.30, 1.125, 2.76). Its origin is the desk top. It needs about 1.6 × 0.9 m of clear desk around the origin: the parts lie to the left (−x) and the respawn pad to the right (+x).
- **Rig.** The rocket works with your `Player` rig (XRI Starter Assets, `NearFarInteractor` hands). `XRRigAdapter` makes the changes it needs at runtime, so neither the rig prefab nor `room.unity` is edited:
  - generic hover/select buzz off on all its `SimpleHapticFeedback`;
  - the `RocketPart` interaction layer on the hands;
  - Jump off;
  - the rig onto the floor.

  Rocket parts are near-grab only: a far ray cannot pick them up.
- **No dependency on the room.** Nothing in `Assets/VRRocket/Scripts` outside `Runtime/Room` references the environment.
- **Tuning.** `rocketScale` in `Assets/VRRocket/Settings/AssemblyTuning.asset` scales the whole rocket (1.0 = real size, 34 mm parts), and every other tunable of SPEC 10.4 is in that asset too. Every sound and vibration value is in `Settings/FeedbackLibrary.asset`. Swap your final clips in there; no code changes are needed.
- **Haptics per controller.** `FeedbackLibrary.deviceTunings` lifts the soft pulses for HTC Vive wands and Valve Index controllers; Touch Plus plays the table as designed.

## 2. The interface: `RocketAssembly` on the workstation root

```csharp
public enum AssemblyState { BuildingAirframe, AirframeComplete, MotorFitted, PrototypeComplete, Submitted }
public enum ReturnMode { Editable, InspectOnly }

AssemblyState State { get; }
event Action<AssemblyState> StateChanged;      // also UnityEvent onStateChanged
event Action PartAttached;                     // also onPartAttached
event Action PartRemoved;                      // also onPartRemoved
event Action<BuildReport> Submitted;           // also onSubmitted
                                               // plus onPrototypeComplete (the rocket is ready to carry)
BuildReport GetReport();                       // valid from PrototypeComplete onward
bool TrySubmit();                              // the bin calls this: freezes the rocket, raises Submitted
void ReturnPrototype(ReturnMode mode, Transform at);
void BeginNewBuild();                          // fresh tube and fresh parts on the desk
void SetRocketVisible(bool visible);           // helper for a bin animation
bool interactionEnabled { get; set; }          // the flow locks the parts outside the Assembly phase
RocketPart tube;                               // the working rocket's body tube (carries all attached parts as children)
RocketPart inspectPrototype;                   // the frozen rocket returned for inspection, if any
```

`BuildReport` (SPEC section 7) has these fields:

- `flapPlacement[3]`: slot name, `Base`/`Midpoint` and `Up`/`Down` for each flap;
- `capLocked` and `FlapError`;
- `outcome`: `Success`, `MotorRetentionLoss` or `UnstableFlight`;
- `failedComponents`, such as `{ "MotorCap", "WingFlap_2" }`, and `FailedCount`.

## 3. Expected flow

- **Assembly.**
  - The tube lies on the desk with the parts. The user holds it in one hand and fits parts with the other, in any order (the cap needs the motor first).
  - Every attached part is a child of the tube, so grabbing the tube carries the whole rocket. A resting rocket is grabbed as a whole; parts come off only while the rocket is held in the other hand, by a deliberate pull.
  - At `PrototypeComplete` you get `onPrototypeComplete`.
- **Submit.** The bin's trigger finds the loose complete rocket (`RocketPart` with `partType == BodyTube`, `assembly.State == PrototypeComplete`). The Submit button calls `TrySubmit()`, which returns false if the rocket is still held or not complete.
- **Launch.** Read `outcome` from the report in `Submitted` and play the matching launch. `failedComponents` feeds the failure screen.
- **Failed launch.** Call `ReturnPrototype(InspectOnly, binReturnSpot)`, then `BeginNewBuild()`. The old rocket comes back as a frozen object the user can pick up but not alter, and a new kit appears.
- **Abort.** Call `ReturnPrototype(Editable, null)`. The same rocket goes back to the desk and can be corrected.
- **Dropped things.**
  - A dropped loose part respawns on the pad.
  - A dropped rocket returns to its desk slot.
  - The bin and the console shelf count as in bounds, so nothing jumps back from there.

## 4. The working versions in `VRRocket_Main` (yours to replace)

The whole loop is playable on your room today. Each piece is a separate root, listed with coordinates in `Docs/ENVIRONMENT.md`.

| Your system (SPEC 9) | Working version | Replace it by |
|---|---|---|
| Start menu and flow | `GameFlow`: `GameFlowController` (Menu, Assembly, PreLaunch, Countdown, Launch, Result) and `MenuCanvas` (START calls `GameFlowController.StartGame`). | Keeping the `RocketAssembly` calls of section 3. `GameFlowController.PhaseChanged` and `onPhaseChanged` tell you the phase. |
| Intro video | Yours: `TransmissionController` in `room.unity`. `TransmissionBridge` lets it play under the menu and ends it on START. | Nothing to do. If you rename the class, update `TransmissionBridge.m_ControllerTypeName`. |
| Submission bin | `ConsoleAddOn/SubmissionBin`: a trigger over the opening; `Submit()` calls `TrySubmit()` and sinks the rocket; `returnSpot` is where a failed prototype comes back. | Calling `TrySubmit()` and the return sequences from your bin; keep the Submit button wired to it. |
| Console buttons | `ConsoleAddOn/ControlPanel`: XRI example push buttons. `onPress` calls `SubmissionBin.Submit`, `GameFlowController.Launch` and `GameFlowController.Abort` (plus `ButtonPressHaptics.OnPress`). | Wiring your buttons to the same three methods in the Inspector. |
| Launch screen | `LaunchScreen` (overlay on your big TV), `CountdownClock` (top-left corner TV) and the console mirror, driven by `LaunchScreenController` on `GameFlow`. | Deleting those roots and pointing your screen at `GameFlowController.lastReport` or `RocketAssembly.Submitted`. The controller ignores missing references. |
| Launch | `LaunchSite`: `LaunchSequence` with the 36× vehicle, effects, pad camera and audio. `PlaceVehicleOnPad(growSeconds)` grows the prototype to full size; `Liftoff(outcome, onFinished)` plays one of the three outcomes. | Calling your own launch from `GameFlowController`, or replacing `LaunchSite` and keeping the method names. |
| Wire puzzle | Not built. `Launch()` is accepted as soon as the vehicle is on the pad. | Gating `GameFlowController.Launch` on your puzzle. |
| Blueprint | `BlueprintPanel` with `BlueprintDisplay` (step text). | Calling `BlueprintDisplay.ShowState` / `ShowText` from your display, or replacing it. |

`Configure(...)` methods on every component take all references, so the objects can be rebuilt by script; the Inspector works too. If you drop the flow, leave `RocketAssembly.interactionEnabled` at true (its default).

## 5. Things to know

- **Attach mechanic.** All parts share one guided-attach mechanic (`GuideGrabTransformer`).
  - Rocket parts never collide with each other.
  - Attached parts are re-pinned to their seats every frame (`AttachedPartSync`), so a carried rocket moves as one piece.
  - The attach points are generated from `Assets/VRRocket/Models/attach_points.json` by the menu item VR Rocket > Generate Attach Points On Selected BodyTube; do not hand-edit them.
- **Static rocket prefabs.** `Prefabs/AssembledRocket_Static.prefab` is a plain assembled rocket for animations. `Prefabs/Launch/LaunchVehicle.prefab` is its 36× copy used on the pad.
- **Tests.** Run Window > General > Test Runner after changing anything under `Assets/VRRocket`. See `Docs/PORTING.md` section 4 for what they cover.
- **Stubs.** `Prefabs/Stubs/MissionControlStubs.prefab` is only in `Dev_Interactions.unity`. It holds a bin and readout stub from before the working versions existed; delete it with the dev scene when you no longer need either.
- **Headset checks.** Feel, haptics and frame rate are listed in SPEC 12.2 and `Docs/PORTING.md` section 5, and are Praise's to tick.
