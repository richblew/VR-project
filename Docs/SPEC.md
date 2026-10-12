# VR Rocket: Project Specification

Version 1.0, 8 October 2026. Written from the submitted treatment (VR Rocket Treatment, Wu, Blewett, Jaravani) plus measurements of the actual model files.

> **In the team project (12 October 2026).** This spec came over with the VR Rocket port onto the `interactions` branch. It still describes the rocket and its interactions exactly. Where it talks about rooms, rigs or scenes, read `Docs/PORTING.md` for how they map onto the team's room: the experience runs in `Assets/VRRocket/Scenes/VRRocket_Main.unity`, which loads `Assets/Scenes/room.unity` at runtime and uses its `Player` rig. Decision D12 below records the ported working versions of the team's systems.

## 0. How to use this document

- This is the working source of truth for the build. Where it disagrees with the treatment, this document wins, and every such difference is listed in section 13 so the team can overrule it.
- Each area is tagged with an owner:
  - **[PRAISE]** The rocket model and every assembly interaction. This is the agent's job and must reach 100% done.
  - **[TEAM]** Built by teammates. The agent builds only the stub or interface named here, so the rocket can be tested end to end and later plugged into their work.
- The ownership split is Praise's stated assignment (model and interactions) extended by assumption to the rest. If the team split is different, change the tags, not the behaviour.
- "Must" is required. "Should" is expected unless it threatens a "must". "Stub" means the simplest thing that lets the rocket be tested.
- Numbers marked *tunable* live in one settings asset (section 10.4) and are starting values to be adjusted in the headset.

## 1. Project summary

| | |
|---|---|
| Course | UCT VR course, Assignment 2: build a virtual environment focused on a multi-component object that the user assembles into a functional object |
| Marking emphasis | Interaction over environment. Must use visuals, audio and touch (controller input and vibration). Design principles must be applied on purpose |
| Concept | The user hand-assembles a small model rocket, submits it, and watches a full-size version launch. A wrongly built rocket fails in a way that reflects the mistake, the prototype is returned, and the user rebuilds |
| Platform | PC VR (Windows, OpenXR: HTC Vive and Valve Index through SteamVR, Quest through Link) and Meta Quest 3 standalone (Android, OpenXR). Controllers, not hand tracking. *(Was Quest-only before the port.)* |
| Engine | Unity 6000.6.2f1, XR Interaction Toolkit 3.6.1, OpenXR 1.18, URP 17.6 (the team project). *(The rocket was built in 6000.3 / XRI 3.4.0.)* |
| Deadline | Final presentation at the end of October 2026 |
| Team | Praise Jaravani (rocket model and interactions), Yi-Xuan Wu and Richard Blewett (everything else, see section 0) |

Educational aim from the treatment: teach how a rocket is assembled, plus some history of space missions through an introductory video.

## 2. Experience flow [TEAM owns the flow, PRAISE owns the Assembly state]

Recreated from treatment Figure 2.3.

| State | What happens | Exits |
|---|---|---|
| Tutorial | User spawns in the room. An introductory video plays | Video watched: go to Assembly |
| Assembly | User builds the rocket at the workstation | All parts fitted and the cap seated: go to Completed assembly |
| Completed assembly | Rocket can be lifted out of its stand and carried | Rocket placed in the submission bin and Submit pressed: go to Pre-launch |
| Pre-launch | User solves the wire-matching puzzle. Launch is disabled until it is solved | Puzzle solved and Launch pressed: go to Launch. Abort pressed: back to Assembly |
| Launch | Countdown, then one of three launch animations on the big screen | Launch succeeded: End. Launch failed or Abort pressed: back to Assembly, with failure feedback and the prototype returned |

The red Abort button works at any time after submission.

The agent implements the Assembly and Completed assembly states fully, and exposes the hooks in section 8.4 so the other states can drive the rocket.

## 3. Environment [TEAM]

### 3.1 Final room (teammates deliver this)

A single room. No chairs: the user stands. Movement is limited to the space between two desks, so no locomotion system is needed beyond walking in the play area.

Layout, recreated from treatment Figure 1.2. The user spawns in the middle facing the workstation.

```
        +--------------------------------------------------+
        |   [            LAUNCH SCREEN (front wall)      ] |
 WINDOW |                                                  |
 (left) |  +--------------------------------------------+  |
        |  | [BIN]      MISSION CONTROL DESK    (o) (o) |  |
        |  +--------------------------------------------+  |
  EXIT  |                                                  | HELP WALL
  DOOR  |                  (user spawns)                   | (tips)
        |                                                  |
        |  +--------------------------------------------+  |
        |  | <> <> <>          WORKSTATION      [STAND] |  |
        |  | <> <> <>                           [ PAD ] |  |
        |  +--------------------------------------------+  |
        +--------------------------------------------------+
   <> = rocket parts laid out    [STAND]/[PAD] = "Spawn Rocket" area
   [BIN] = submission bin        (o) (o) = Launch (green) and Abort (red)
```

- **Workstation (back):** rocket parts laid out on one side, the "Spawn Rocket" area on the other. A blueprint display with step buttons sits here.
- **Help wall:** tips on building the rocket correctly.
- **Mission control desk (front):** submission bin (a hole in the desk), Submit button, puzzle panel, green Launch button, red Abort button.
- **Front wall:** large screen for the countdown and launch.
- **Window:** long window with an outside view and natural light. Ceiling lights. Exit door to leave the simulation.
- **Mood:** real mission control and a clean workshop. Palette from the mood board: white, dark grey, black, green, red.

### 3.2 Development scene [PRAISE]

The dev room is greybox primitives and stays that way. Environment art belongs to the teammates (section 3.1); no art kit is a dependency of this project (decision of 8 Oct 2026).

- `Assets/VRRocket/Scenes/Dev_Interactions.unity`: a floor, one desk-height surface for the workstation and a second for the mission control stubs, built from Unity primitives.
- Use the XR rig prefab `Assets/XRI_Examples/Global/Prefabs/Complete XR Origin Set Up Variant.prefab`. Teleport and continuous move are disabled in this scene.
- Keep the dev room cheap: one directional light, no post-processing that the Quest cannot afford.
- Everything rocket-related lives in one prefab (section 8.1) so it can be dropped into the teammates' final scene unchanged. Nothing in the rocket code may reference environment assets.
- `Dev_Interactions` is first in the build scene list so Build And Run opens it on the headset.

## 4. The rocket [PRAISE]

### 4.1 Overview

Based on an Estes BT-55 model rocket airframe. Six component types, eleven objects: one body tube, three tail fins, three wing flaps, one nose cone, one motor, one motor cap. Assembled, it measures about 361 mm from the bottom of the cap to the nose tip.

Kit-derived meshes come from "Model Rocket Kit" by ellipticaloptician (printables.com/model/157400-model-rocket-kit), BSD 2-Clause. Credit the author in the project's credits.

### 4.2 Model files

All files are in `SourceArt/VR_Rocket_Kit_Parts_v2/` and must be copied into `Assets/VRRocket/Models/`. They replace the four files from the first pass (`BodyTube_StandIn.fbx` is obsolete, delete it).

| File | Triangles | Materials | Size (mm) | Source |
|---|---|---|---|---|
| `BodyTube.fbx` | 2,100 | Rocket_White, Rocket_Cardboard | 33.7 diameter x 300 | Modelled. Nine slots and an internal motor mount |
| `TailFin.fbx` | 425 | Rocket_Orange | root 58.6, 55.9 out, 2.0 thick | Cut from the kit's five-fin base. Used three times |
| `WingFlap.fbx` | 28 | Rocket_Orange | root 78, 42 out, 2.0 thick | Modelled. Used three times |
| `NoseCone.fbx` | 2,702 | Rocket_White, Rocket_Orange | 34 diameter, 49.4 above the tube rim | Kit, cleaned up, orange tip |
| `Motor.fbx` | 768 | Rocket_Cardboard, Rocket_Black, Rocket_White | 24 diameter x 70 | Modelled |
| `MotorCap.fbx` | 3,000 | Rocket_Black | 34 diameter, 18.4 deep | Kit, cleaned up |

Whole rocket: about 9,900 triangles.

Also in that folder: `rocket_kit_parts.blend` (the correct build, assembled), `build_kit.py` (regenerates everything, every dimension is a named constant at the top), `attach_points.json` (section 4.4 in machine-readable form).

### 4.3 Conventions for every part

- Units are metres at real size. Import with scale factor 1.
- Local **+Y** is the rocket axis, pointing at the nose.
- Fins and flaps: local **+Z** points away from the tube (the slide axis), thickness is along X.
- Origins sit on the attachment point:

| Part | Origin | Expected mesh bounds in Unity, local space (mm) |
|---|---|---|
| BodyTube | Centre of the bottom rim | X and Z within 16.85 of the axis, Y 0 to 300 |
| TailFin | On the tube surface at the middle of the root edge | X -1 to 1, Y -29.3 to 29.3, Z -2 to 55.9 |
| WingFlap | On the tube surface at the middle of the root edge | X -1 to 1, Y -39 to 39, Z -2 to 42 |
| NoseCone | Centre of the step where the shoulder meets the base (the tube's top rim) | Y -17.6 to 49.4 |
| Motor | Centre of the nozzle face. The black nozzle is at Y = 0 | Y 0 to 70 |
| MotorCap | Centre of the face that meets the tube's bottom rim | Y -12 to 6.4 |

The negative Z on fins and flaps is the 2 mm tab that sits inside the slot.

**Import check (must do first).** The exports were verified by re-importing into Blender, not Unity. After import, confirm each mesh's bounds match the table. If a part arrives rotated or mirrored, correct it with FBX import settings or a parent pivot in the prefab. Do not edit the attach numbers to compensate.

**Wing flap orientation.** The flap is deliberately not symmetrical: its top edge sweeps back 30 mm and its bottom edge only 8 mm. "Up" means the strongly swept edge is toward the nose, which is how the mesh is modelled (local +Y up). The tab is centred, so the flap fits its slot either way up.

### 4.4 Attach points

Positions are in the BodyTube's local space. A point at yaw `a` sits at `(R sin a, height, R cos a)` with `R = 0.01685`, and a part placed there with local rotation `(0, a, 0)` has its +Z pointing outward.

| Name | Accepts | Yaw (degrees) | Height (m) | Position (m) |
|---|---|---|---|---|
| FinSlot_1 | Tail fin | 0 | 0.0353 | (0, 0.0353, 0.01685) |
| FinSlot_2 | Tail fin | 120 | 0.0353 | (0.01459, 0.0353, -0.00842) |
| FinSlot_3 | Tail fin | 240 | 0.0353 | (-0.01459, 0.0353, -0.00843) |
| FlapSlot_Mid_1 | Wing flap | 0 | 0.150 | (0, 0.150, 0.01685) |
| FlapSlot_Mid_2 | Wing flap | 120 | 0.150 | (0.01459, 0.150, -0.00842) |
| FlapSlot_Mid_3 | Wing flap | 240 | 0.150 | (-0.01459, 0.150, -0.00843) |
| FlapSlot_Low_1 | Wing flap | 60 | 0.109 | (0.01459, 0.109, 0.00843) |
| FlapSlot_Low_2 | Wing flap | 180 | 0.109 | (0, 0.109, -0.01685) |
| FlapSlot_Low_3 | Wing flap | 300 | 0.109 | (-0.01459, 0.109, 0.00843) |
| NoseSeat | Nose cone | any | 0.300 | (0, 0.300, 0) |
| MotorSeat | Motor | any | -0.011 | (0, -0.011, 0) |
| CapSeat | Motor cap | any | 0 | (0, 0, 0) |

- The **Mid** flap slots are the correct position. The **Low** slots are the wrong position and sit 60 degrees round from the fins (see decision D1).
- The seated motor sticks out 11 mm below the tube until the cap covers it. Inside the tube a thrust ring at 59 mm stops it.
- Generate the attach transforms from `attach_points.json` with a small editor script, so nothing is typed by hand.

### 4.5 Colliders and physics

- No concave mesh colliders. Fins and flaps: box colliders padded to at least 8 mm thick so they are easy to grab. Nose cone and motor: capsules. Cap: a short cylinder approximation or convex mesh. Tube: capsule.
- Loose parts are non-kinematic rigidbodies with gravity and continuous collision detection, so thin plates do not fall through the desk.
- A part that is being guided or is attached ignores collisions with the rest of the rocket.

## 5. Assembly interactions [PRAISE]

### 5.1 Design principles the implementation must show

These come from the treatment section 2.3 and are what the marker will look for.

- **Tolerance.** Precise placement is hard in VR (hand tremor, no physical support). Every attach point snaps the part into place once it is brought within a radius and held roughly the right way round.
- **Constraint.** Where real geometry would guide a part, the part's motion is restricted to that axis while it is being placed.
- **Feedback at three moments.** Approach (the point glows), commit (snap, click, vibration), and rejection (nothing at all, so a refused placement is distinguishable without text).
- **Silent failure points.** Two mistakes are possible and nothing during assembly reveals them. Do not add warnings, colours or hints for them.

Interaction technique: direct grab with the controller (a simple virtual hand), near interaction only. Rocket parts must not be grabbable by ray from a distance.

### 5.2 The guided attach mechanic (shared by all parts)

Every part uses the same mechanic with different settings.

1. **Free.** The part is held by an `XRGrabInteractable` with dynamic attach on, so it is grabbed wherever the hand touches it.
2. **Approach.** When a held part's origin is within `glowRadius` of an attach point that currently accepts it, that point glows. Several points can glow at once.
3. **Guided.** When the part's origin is within `captureRadius` and its orientation is within tolerance, the guide engages: the part's rotation locks to the attach point's rotation and its position is the hand position projected onto the attach axis, clamped between seated (0) and `guideLength`. The part can slide in and out but not sideways. A light vibration tick marks engagement.
4. **Break away.** If the hand moves more than `breakRadius` sideways from the axis, or beyond the end of the guide, the guide releases and the part follows the hand freely again.
5. **Seat.** Releasing the part while guided moves it to the seated position over `seatDuration`, then plays the click and vibration and marks it attached.
6. **Remove.** Grabbing an attached part re-enters the guide at depth 0. Pulling it out past `guideLength` detaches it. Exceptions are listed in 5.5.

Attach axis: for fins and flaps, the slot's outward direction (local +Z of the attach point). For nose cone, motor and cap, the tube axis, with the guide extending away from the tube.

Implementation route (required unless a spike proves it unworkable, in which case write up why in `Docs/PROGRESS.md` before changing course):

- Implement the guide as a custom grab transformer deriving from `XRBaseGrabTransformer` and overriding `Process(XRGrabInteractable, XRInteractionUpdateOrder.UpdatePhase, ref Pose targetPose, ref Vector3 localScale)`. `Assets/Samples/XR Interaction Toolkit/3.4.0/Starter Assets/Scripts/RotationAxisLockGrabTransformer.cs` is a working example of the pattern.
- Do **not** use `XRSocketInteractor` for the rocket's attach points. A socket only takes an object on release and shows a ghost preview, which fights the constrained slide. Attach points are our own component.
- Attached parts are parented under the tube with their rigidbody kinematic.

### 5.3 Per-part behaviour

| Part | Treatment verb | Accepted at | Orientation rule | On release | Can it be wrong? |
|---|---|---|---|---|---|
| Tail fin (x3) | Slide | Any free FinSlot, any order | Outward axis roughly aligned. Any roll is accepted and corrected to the seated orientation | Snaps flush, click | No |
| Wing flap (x3) | Slide | Any free FlapSlot, Low or Mid | Outward axis roughly aligned, and either way up. Whichever way up it is held is kept | Snaps flush, click, identical in every slot and orientation | **Yes** |
| Nose cone | Press | NoseSeat | Roughly upright. Roll is free | Drawn down onto the shoulder, soft click | No |
| Motor | Insert | MotorSeat | Nozzle end down. Presented nozzle-up it is rejected: no glow, no snap | Travels up into the mount by itself over about 0.3 s, then click | No |
| Motor cap | Twist | CapSeat | Lugs toward the tube | Seats unlocked and rests in place. Must then be twisted to lock | **Yes** |

Specifics:

- **Fins.** All three slots are identical. A fin can never end up wrong.
- **Flaps.** All six slots accept any flap. Position (Low or Mid) and orientation (Up or Down) are recorded at seating and are never corrected or signalled. Glow, click and vibration must be exactly the same for right and wrong placements.
- **Nose cone.** While guided it is pulled toward the seat (the displayed distance is a fraction of the hand's distance, `noseMagnetism`; 0.5 shows the nose at half the hand's distance), so it feels drawn down. The pull fades in along the guide: at the guide's outer end the nose follows the hand exactly, and the fraction reaches the full `noseMagnetism` value at the seat. There must be no visible jump when the guide engages or breaks away. It is the last airframe part in the blueprint order, but this is not enforced.
- **Motor.** The reversed case is the only rejection the user is likely to meet. It must be completely silent.

### 5.4 Motor cap twist

The only fully custom interaction and the highest risk. Build it early.

1. The cap is guided to CapSeat like any other part and, on release, rests **seated but unlocked**. Play a soft seat sound. No lock click, no strong vibration.
2. Gripping the seated cap does not move it. Instead the cap rotates about the rocket axis following the twist of the hand about that axis.
3. Rotation is clockwise only, viewed from below the rocket. It works like a ratchet: turning back does not unwind the cap and makes no clicks, so the user can re-grip with wrist motion or by letting go and grabbing again. Progress is kept between grabs.
4. Every `capDetentAngle` of new progress plays one ratchet tick and one light vibration, so the user hears and feels the thread engaging.
5. When total progress reaches `capLockAngle` the rotation stops, a firm click plays, one strong vibration fires, and `capLocked` becomes true. A locked cap can no longer be turned or removed.
6. The user may let go at any point before that. The cap stays seated and nothing warns them.
7. **Visible difference.** An unlocked cap sits `capUnlockedGap` (2 mm) proud of the tube. Locking closes the gap. This is the "almost identical" look from the treatment and is what lets a user diagnose the returned prototype.
8. An unlocked cap can be pulled straight off (hand moves more than `capPullOff` along the axis away from the tube while gripping). Its progress resets to zero.

`Assets/XRI_Examples/UI_3D/Scripts/XRKnob.cs` shows how to follow a hand's twist past 180 degrees (it extends `XRBaseInteractable` and reads the interactor's attach transform in `ProcessInteractable`). Borrow the technique into our own script. Do not modify or subclass theirs.

### 5.5 Assembly states and gating

Recreated from treatment Figure 2.4.

| State | Entered when | What can be attached |
|---|---|---|
| BuildingAirframe | Start | Fins, flaps, nose cone, in any order |
| AirframeComplete | 3 fins, 3 flaps and the nose cone are attached | Motor |
| MotorFitted | Motor is seated | Motor cap |
| PrototypeComplete | Cap is seated (locked or not) | Nothing. Cap can still be twisted |
| Submitted | Rocket accepted by the submission bin and Submit pressed | Nothing. Rocket is frozen |

Rules:

- A point that is gated off behaves as a rejection: no glow, no snap.
- States move backward when parts are removed (for example pulling a fin in AirframeComplete returns to BuildingAirframe).
- Removal exceptions: the motor cannot be removed while the cap is seated. A locked cap cannot be removed. Airframe parts cannot be removed once the motor is seated.
- The gating order is a setting (`enforceOrder`). Default changed to **off** on 11 Oct 2026 (Praise's decision after the headset test: the ordered build felt limiting). With it off any part attaches in any order and comes off at any time; the only rule is that the cap needs the motor, and the motor cannot come out while the cap is on. The states of the table still describe progress and drive the screen text.
- `capLocked` and the flap placements are read at the moment of submission, not at PrototypeComplete, because the user may finish twisting the cap late.

### 5.6 Handling the rocket

- (Changed 9 Oct 2026, Praise's decision after the first headset test: no assembly stand.) The body tube starts flat on the bench like every other part and can be grabbed at any time. The user holds it in one hand and fits parts with the other, or works on it while it rests on the bench. Attach points work in any orientation.
- Every attached part is a kinematic child of its attach point, and its colliders are mirrored onto the tube's own rigidbody (compound collider), so the rocket is one physical unit: it rests on its fins, is pushed as one piece, and grabbing the tube anywhere carries the whole rocket at any stage of the build.
- A resting rocket is grabbed as a whole: pointing at it never picks an attached part. Parts come off two-handed: while the rocket is held in the other hand, grab the part and pull it along its guide (the cap can also be twisted this way). This follows how people actually build in VR, base in one hand, parts with the other (Praise, 11 Oct 2026). Rocket parts never collide with each other, so a held part cannot shove the tube or knock parts off; parts still collide with the bench and the room.
- At PrototypeComplete there is a sound plus a short vibration on both controllers; nothing else changes.

## 6. Feedback [PRAISE]

Every event below needs all three channels where a value is given. Haptic values are amplitude (0 to 1) and duration, *tunable*.

| Event | Visual | Audio | Haptic (holding hand) |
|---|---|---|---|
| Part near an accepting point | Point glows, brightness rising with closeness | None | None |
| Guide engages | Part locks to the axis | Soft tick | 0.15 for 0.02 s |
| Part seats (fin, flap, motor) | Glow off, part flush | Click | 0.5 for 0.08 s |
| Nose cone seats | Glow off | Soft click | 0.4 for 0.08 s |
| Cap seats unlocked | Glow off, 2 mm gap remains | Soft seat sound, not the click | 0.2 for 0.04 s |
| Cap detent | Cap turns | Ratchet tick | 0.2 for 0.02 s |
| Cap locks | Gap closes | Firm click | 1.0 for 0.20 s |
| Cap released early | Nothing | Nothing | Nothing |
| Rejected placement | Nothing | Nothing | Nothing |
| Part removed | Part slides out | Soft unseat | 0.2 for 0.04 s |
| Prototype complete | Nothing moves; the rocket is ready to carry | Completion chime (the old stand-release clip) | Both hands 0.3 for 0.10 s |
| Part hits the desk | | Dull impact, volume by speed | None |
| Part hits another part | | Sharp impact, volume by speed | None |
| Part respawns | Part appears on the pad | Soft pop | None |

Glow:

- Each attach point has its own glow mesh: a thin frame around the slot for fins and flaps, a ring at the tube's top rim for the nose, a ring at the bottom rim for motor and cap.
- Should: the matching feature on the held part (its tab or shoulder) glows too, since the treatment says "the points that connect will start to glow".
- Use an unlit emissive material. Do not rely on bloom or other post-processing.

Audio:

- All clips are referenced through one library asset so the teammates' final sounds can be swapped in without touching code.
- Use placeholders now: `Assets/XRI_Examples/Global/Audio/Button_22_Click.wav`, `HoverSound.wav`, `Button_14_Hover.wav`, and `Assets/Samples/XR Interaction Toolkit/3.4.0/Starter Assets/DemoAssets/Audio/Button Pop.wav`. Reference them, do not move them.
- Sounds are 3D and play from the attach point or the part.

Haptics: sent through the controller's `HapticImpulsePlayer` as amplitude, duration and frequency (confirmed in the XRI 3.4.0 source; `OpenXRHapticImpulseChannel` passes all three). Designed for the Quest 3 Touch Plus controllers, whose voice-coil actuators honour frequency (Quest 2 ignores it): low frequency (80 to 120 Hz) for heavy events such as locks, clunks and heft; high frequency (200 to 250 Hz) for fine events such as ticks and textures; durations of 10 to 100 ms, with a strong lock at 200 ms; an optional second "settle" pulse after a gap. All values are in `FeedbackLibrary` and are starting points for headset tuning.

Immersion haptics added on 9 Oct 2026 beyond the table above (all tunable, holding hand unless stated):

| Event | Haptic | Why |
|---|---|---|
| Hand comes within reach of a loose part | 0.08 for 0.012 s at 250 Hz, at most every 0.25 s per part | "you can take this", replaces the rig's generic hover buzz |
| Loose part picked up | 0.3 for 0.03 s at 160 Hz | contact |
| Rocket or inspection prototype picked up | 0.55 for 0.05 s at 90 Hz, then 0.2 for 0.08 s | heft |
| Loose part let go | 0.12 for 0.015 s at 180 Hz | release |
| Rocket let go | 0.25 for 0.03 s at 110 Hz | release |
| Guided travel | 0.07 for 0.01 s at 220 Hz per 4 mm of travel | the slot's texture, the constraint principle made tangible |
| Cap turning between detents | 0.08 for 0.012 s at 200 Hz per 6 degrees of new progress | thread engaging; detent frames skip it |
| Rocket lifted out of the stand, snapped back into the stand | retired with the stand on 9 Oct 2026; the values stay in the feedback library unused | |
| Submission accepted | both hands 0.5 for 0.12 s at 100 Hz, then 0.3 for 0.15 s | the bin takes it |
| Fresh kit arrives | both hands 0.15 for 0.04 s at 200 Hz | new build |
| Push button down / up | 0.6 for 0.03 s at 200 Hz / 0.25 for 0.02 s at 180 Hz, on the pressing hand | mechanical click |

Unchanged on purpose: rejected placement, break-away, early let-go of the cap and respawn stay silent; impacts of loose parts stay audio only. The spec table's own values gained a frequency and, for seats, the lock and the stand release, a settle pulse. The rig's `SimpleHapticFeedback` hover and select buzz is switched off on the hands in both scenes so these are the only haptics the user feels.

## 7. Build record and launch outcome [PRAISE produces, TEAM consumes]

### 7.1 Data

```csharp
public enum FlapPosition { Base, Midpoint }      // Base = a Low slot
public enum FlapOrientation { Up, Down }
public enum LaunchOutcome { Success, MotorRetentionLoss, UnstableFlight }

public struct FlapPlacement {
    public int flapId;            // 1 to 3
    public string slotName;       // e.g. "FlapSlot_Low_2"
    public FlapPosition position;
    public FlapOrientation orientation;
    public bool IsCorrect => position == FlapPosition.Midpoint && orientation == FlapOrientation.Up;
}

public sealed class BuildReport {
    public FlapPlacement[] flapPlacement;   // always 3 entries
    public bool capLocked;
    public bool FlapError;                  // any flap not correct
    public LaunchOutcome outcome;
    public string[] failedComponents;       // e.g. { "MotorCap", "WingFlap_2" }
    public int FailedCount;                 // failedComponents.Length
}
```

### 7.2 Outcome rule

| capLocked | Any flap wrong | Outcome | What the launch shows |
|---|---|---|---|
| true | no | Success | Clean launch |
| true | yes | UnstableFlight | Lifts off, cannot hold its heading, yaws and corkscrews |
| false | no | MotorRetentionLoss | Motor ignites and is driven out of the base. Rocket never leaves the pad |
| false | yes | MotorRetentionLoss | Same animation, because a rocket that never leaves the pad never has its stability tested. The flap error is still listed in `failedComponents` |

`failedComponents` lists the cap when it is unlocked plus every individual wrong flap. This feeds the failure screen in the treatment section 5.2: a 2D image of the rocket with the failed components outlined in red and the failed count beside it.

## 8. Workstation rig and integration [PRAISE]

### 8.1 One prefab

`Assets/VRRocket/Prefabs/RocketWorkstation.prefab` contains everything in this section. Its root sits on the workstation desk surface. Dropping it into any scene with an XR rig must give a fully working assembly task. A single child, `ScaledRoot`, holds all rocket geometry so that `rocketScale` can enlarge the whole kit without touching any other number.

### 8.2 Assembly stand (removed 9 Oct 2026)

- There is no stand. The tube lies on the tray with the other parts (its slot is part of the tray layout) and is a normal loose part. `standClearance` in the tuning asset is unused.
- The "Spawn Rocket" area of the workstation is where the tube lies; a returned or dropped rocket comes back to that slot at rest.

### 8.3 Parts tray and respawn

- The body tube and the nine loose parts start laid out flat on the desk in clear groups: tube, three fins, three flaps, nose cone, motor, cap. Nothing overlaps and everything is within easy reach.
- A part that touches the floor, or stays outside the workstation bounds for `respawnDelay`, reappears on the respawn pad. A dropped rocket returns to the tube's slot on the bench. The submission bin and the console top count as inside the bounds.
- Respawned objects arrive at rest, never falling from a height.

### 8.4 Interface for the rest of the project

One component on the prefab root, `RocketAssembly`, is the only thing other systems talk to.

```csharp
public enum AssemblyState { BuildingAirframe, AirframeComplete, MotorFitted, PrototypeComplete, Submitted }
public enum ReturnMode { Editable, InspectOnly }

public AssemblyState State { get; }
public event Action<AssemblyState> StateChanged;
public event Action PartAttached;            // also raised with details through UnityEvents
public event Action PartRemoved;
public event Action<BuildReport> Submitted;

public BuildReport GetReport();              // valid from PrototypeComplete onward
public bool TrySubmit();                     // called by the bin: freezes the rocket, raises Submitted
public void ReturnPrototype(ReturnMode mode, Transform at);
public void BeginNewBuild();                 // fresh tube and fresh parts on the tray
```

Expected use by the flow owner:

- **Submit.** The bin detects the complete rocket inside it. The Submit button calls `TrySubmit()`. The bin's animation then takes the rocket away.
- **Launch.** The launch sequence reads `outcome` from the report and plays the matching animation.
- **Failed launch.** Call `ReturnPrototype(InspectOnly, binAnchor)` and `BeginNewBuild()`. The old rocket comes back through the bin as a frozen object the user can pick up, turn over and examine, but not alter. New parts appear on the tray. Only one inspection prototype exists at a time.
- **Abort.** Call `ReturnPrototype(Editable, null)`. The same rocket returns to its bench slot and can be corrected.

Every event is also exposed as a UnityEvent so teammates can wire things in the Inspector.

### 8.5 Stubs the agent builds for testing

- `SubmissionZoneStub`: a marked box on the second desk. Releasing the complete rocket inside it and pressing a stub button calls `TrySubmit()`.
- `OutcomeReadoutStub`: a world-space panel showing the report (outcome, failed components, count), with two buttons that call the failed-launch and abort sequences above.
- Stub buttons may reuse `Assets/XRI_Examples/UI_3D/Prefabs/PushButton.prefab` as a base (copy it into our folder first).

Mark every stub clearly in the hierarchy. They are deleted when the real systems arrive.

Update 9 Oct 2026: beyond the stubs, a working version of the section 9 systems the rocket plugs into (menu, flow, bin, launch screen, launch outside) exists in `Env_ControlRoom.unity` under the `GameFlow` and `LaunchSite` scene objects, so the whole loop can be played and tested before the teammates' versions arrive. Ownership is unchanged; see Docs/INTEGRATION.md section 6.

## 9. Other systems [TEAM]

Recreated from the treatment so the agent knows what the rocket plugs into. The agent does not build these.

- **Submission bin.** A hole in the mission control desk. The rocket must collide with the desk normally and fall through only at the bin. Closing and opening sounds. One animation for taking the rocket, one for returning it.
- **Pre-launch puzzle.** A panel showing coloured wires. The user presses a button on the left, then its partner on the right, until all pairs are matched. Button presses only. Launch stays disabled until solved.
- **Launch and Abort buttons.** Green Launch glows when available and starts the countdown. Red Abort works at any time.
- **Launch screen.** Countdown video, then one of three animations: success, loss of motor retention, unstable flight. After a failure, the failure screen from section 7.2.
- **Blueprint display.** Shows the assembly steps with the required action for each (for example a twist arrow for the cap), in the style of furniture or brick-set instructions. Buttons step through them one at a time. It must show the correct flap position and orientation, since nothing else does.
- **Help wall.** Short tips on building the rocket correctly.
- **Tutorial video** before assembly, covering the history of space missions.
- **Final audio** (treatment asset list): twist, click, part on table, part on part, bin close, bin open, button click, launch countdown, rocket launch, explosion, mission successful, mission failed, aborted, and ambient room noise (computer fans, engine hum, lights).
- **Models:** two desks, buttons, screens, lights, submission bin, puzzle panel.

## 10. Technical design [PRAISE]

### 10.1 Layout

```
Assets/VRRocket/
  Models/        the six FBX files
  Materials/     Rocket_White, Rocket_Orange, Rocket_Black, Rocket_Cardboard, Glow, particle and screen materials
  Prefabs/       RocketWorkstation, one prefab per part, stubs, Launch/ (LaunchVehicle, Explosion)
  Scenes/        Dev_Interactions.unity, Env_ControlRoom.unity
  Scripts/
    Runtime/     VRRocket.Runtime.asmdef, namespace VRRocket
    Editor/      attach point generator
    Tests/       edit mode and play mode tests
  Settings/      AssemblyTuning.asset, FeedbackLibrary.asset
  Audio/Kenney/  CC0 clips used by the feedback library and the launch
  Textures/      particle sprites
  UI/            fonts (with TMP font assets) and sprites for the menu and screens
  Environment/   the greybox room, its materials, textures and prefabs (Docs/ENVIRONMENT.md)
```

Read-only folders: `Assets/XRI_Examples` and `Assets/Samples`. Copy from them, never edit them.

### 10.2 Scripts and responsibilities

| Script | Responsibility |
|---|---|
| `RocketPart` | Part type and id, current state (free, guided, attached), references to its grab interactable and rigidbody |
| `AttachPoint` | What it accepts, its axis and guide settings, whether it is occupied, whether gating currently allows it, its glow |
| `GuideGrabTransformer` | The guided attach mechanic of section 5.2 |
| `MotorCapTwist` | Section 5.4 |
| `RocketAssembly` | State machine of section 5.5, the interface of section 8.4, building the report |
| `BuildReport` and enums | Section 7. Plain C#, no Unity dependencies, so it is unit-testable |
| `PartRespawner` | Section 8.3 |
| `AssemblyFeedback` | Turns events into glow, audio and haptics using the two settings assets |
| `ImpactAudio` | Collision sounds |
| `GameFlowController`, `LaunchSequence`, `RocketFlightModel`, `LaunchScreenController`, `SubmissionBin`, `MenuPanel`, `BlueprintDisplay` | Working version of the section 9 systems (added 9 Oct 2026, replaceable; Docs/INTEGRATION.md section 6). `RocketFlightModel` is plain C# with unit tests |

Keep the outcome logic and the state machine free of scene references so they can be tested without a headset.

### 10.3 XR Interaction Toolkit notes

Checked against this project's own files:

- Interactable types are in `UnityEngine.XR.Interaction.Toolkit.Interactables`, interactors in `...Interactors`, grab transformers in `...Transformers`.
- `XRBaseGrabTransformer.Process` has the signature given in section 5.2.
- The XR Interaction Simulator sample is imported, so behaviour can be exercised in Play mode without a headset. `XRDeviceSimulatorSettings` auto-instantiates it in the Editor only.
- **Near-only grab, as built.** The rig's hands are single `NearFarInteractor`s (sphere caster for near, curve caster for far) sharing one interaction layer mask, so an interaction layer cannot separate near from far. Instead every grabbable rocket object carries `NearOnlyGrabFilter`, an `IXRSelectFilter` and `IXRHoverFilter` on the interactable that refuses hover and select when the interactor's own transform is further than `nearGrabDistance` (0.12 m, tunable) from the object's colliders, and always refuses ray and socket interactors. Blocking hover means a part pointed at from far away shows no highlight, no ray cursor and no glow. The filter measures from the interactor transform, not its attach transform, because `NearFarInteractor` parks the attach transform at the far-cast hit point. An existing selection is never cancelled by the filter. The same component and rule apply to the complete rocket once it is grabbable and to the seated cap. Parts also carry the `RocketPart` interaction layer (index 1) alongside `Default` so the prefab works in any scene without rig edits.
- Haptics: `XRBaseInputInteractor.SendHapticImpulse(float amplitude, float duration)` on the interactor holding the part (wraps `HapticImpulsePlayer`). Confirmed in the 3.4.0 package source.
- `XRGrabInteractable` adds its default `XRGeneralGrabTransformer` only if no single transformer is registered by the first update, and it unparents a held object, restoring the grab-time parent on drop. `GuideGrabTransformer` accounts for both.

### 10.4 Tuning asset

`AssemblyTuning` (a ScriptableObject) holds every tunable. Starting values:

| Setting | Value | Meaning |
|---|---|---|
| rocketScale | 1.0 | Uniform scale of the whole kit. Try 1.5 if parts feel fiddly in the headset |
| glowRadius | 0.12 m | Distance at which a point starts to glow |
| captureRadius | 0.05 m | Distance at which the guide engages |
| breakRadius | 0.08 m | Sideways distance at which the guide lets go |
| guideLength | 0.05 m plates, 0.06 m nose, 0.08 m motor, 0.04 m cap | Travel along the axis |
| orientationTolerance | 40 degrees plates, 45 degrees others | How far off the held angle may be |
| seatDuration | 0.12 s, motor 0.30 s | Time to travel to the seat on release |
| noseMagnetism | 0.5 | Fraction of hand distance shown while guiding the nose |
| capLockAngle | 180 degrees | Total clockwise turn needed to lock |
| capDetentAngle | 30 degrees | Turn between ratchet ticks |
| capUnlockedGap | 0.002 m | Gap shown by an unlocked cap |
| capPullOff | 0.04 m | Pull distance to remove an unlocked cap |
| enforceOrder | false | Section 5.5 gating (free order since 11 Oct 2026) |
| standClearance | 0.20 m | Tube base height above the desk |
| respawnDelay | 1.5 s | Time out of bounds before respawn |

Distances scale with `rocketScale`.

### 10.5 Performance

- Hold the headset's refresh rate (72 Hz minimum) in the dev scene on a Quest 3 build.
- The rocket is about 9,900 triangles and four opaque materials plus glow. Do not add per-part real-time lights.
- No allocations per frame in the guide or twist code.

### 10.6 Repository hygiene

- Before committing imported packs, check that every binary type they contain is covered by a Git LFS rule in `.gitattributes`. Add rules for any that are not.
- Never commit `Library`, `Temp`, `Logs`, `UserSettings` or `Builds`.
- Commit at the end of every milestone with a message naming it.

## 11. Milestones [PRAISE]

Work in this order. Each ends with a commit and a dated entry in `Docs/PROGRESS.md` saying what works, what was verified and how, and what is still unverified in the headset.

| # | Milestone | Done when |
|---|---|---|
| M0 | Import and verify | The six FBX files are in place, bounds match section 4.3, materials are URP Lit, attach transforms are generated from the JSON, a static assembled rocket in the scene looks like `preview.png` |
| M1 | Parts, tray, grab, respawn | All nine parts can be picked up near-only, put down, dropped and respawned in the dev scene |
| M2 | Guide mechanic on the nose cone | Approach glow, guided motion, break away, seat, remove, with all three feedback channels |
| M3 | Motor cap twist | Section 5.4 complete on a bare tube with gating off. Done early because it carries the most risk |
| M4 | Fins | Three fins in any order, roll corrected |
| M5 | Flaps | Six slots, both orientations, placements recorded, identical feedback everywhere |
| M6 | Motor and gating | Reversed motor rejected silently, self-insert animation, state machine of section 5.5 with backward moves |
| M7 | Completion and handover | Whole-rocket carry, cap twist while carried, submission stub, report, inspect-only return, new build, abort return (the stand release of the original plan was removed on 9 Oct 2026) |
| M8 | Polish and proof | Impact audio, part-side glow, tuning pass, all automated tests green, Quest build checked for frame rate |
| M9 | Integration notes | `Docs/INTEGRATION.md` for teammates: how to drop in the prefab, the interface, which stubs to delete |

## 12. Acceptance tests

### 12.1 Automated (agent must run these and report results)

- Outcome rule: all four rows of section 7.2, and `failedComponents` for mixed cases (one flap low, one flap upside down, cap unlocked).
- A flap in each Low slot reports `Base`. A flap in each Mid slot reports `Midpoint`. Up and Down are reported correctly in both.
- State machine: forward path, every backward move, and each removal exception in section 5.5.
- Gating: motor refused before the airframe is complete, cap refused before the motor, with `enforceOrder` on and off.
- Cap: progress accumulates across grabs, never decreases, locks exactly at `capLockAngle`, resets on pull-off, cannot be changed after locking.
- Attach transforms match `attach_points.json`.

### 12.2 Headset checklist (Praise runs these; the agent must not mark them passed)

1. Every part can be grabbed anywhere on its surface with either hand.
2. Each attach point glows on approach and stops glowing when the part leaves.
3. A fin slides in and out along its slot and cannot be pulled sideways while guided.
4. A flap placed low, or upside down, feels and sounds exactly like a correct one.
5. The motor presented nozzle-up gets no response at all.
6. The cap can be felt and heard ratcheting, the lock is unmistakable, and letting go early gives no warning.
7. The 2 mm gap of an unlocked cap is visible when you look for it and easy to miss when you do not.
8. A complete rocket can be picked up from the bench as one piece and carried to the other desk without parts coming off or drifting.
9. The cap can be twisted with one hand while the other holds the rocket.
10. Dropping any part, and the whole rocket, brings it back correctly.
11. Each of the four build combinations gives the right report on the readout stub.
12. A failed prototype comes back and can be examined but not changed, with a fresh kit on the tray.
13. The whole build takes a first-time user a few minutes, not seconds and not ten minutes.
14. Frame rate stays smooth throughout.

The rocket and interactions are "100% done" when 12.1 is green, 12.2 is fully ticked by a person in the headset, and M9 is written.

## 13. Decisions that differ from or go beyond the treatment

| # | Topic | Treatment says | This spec does | Why |
|---|---|---|---|---|
| D1 | Lower flap slots | Three slots immediately above the fin slots, same 120 degree spacing | Same height, but turned 60 degrees so they sit between the fins | A 78 mm flap directly above the fins reaches 148 mm, and one centred on the midpoint starts at 111 mm. In line, the two positions collide over 37 mm. Turning the lower set keeps every stated dimension and leaves the correct build exactly as drawn |
| D2 | Motor base | Slightly wider base stops reverse insertion | Plain cylinder. Reverse insertion is refused by the attach point | The kit cap's bore is 24.4 mm against a 24 mm motor, so a wider base cannot fit under the cap |
| D3 | Cap rotation | "Quarter turn" in 2.2, "a set number of turns to be determined" in 2.3 | 180 degrees with a tick every 30 degrees, both tunable, ratchet behaviour | Long enough that stopping early is a believable mistake, short enough for two wrist motions |
| D4 | Body tube | "Standing upright in a cradle", "the only component that is not grabbed" | A loose part lying on the bench, grabbable at any time; the user holds it in one hand while fitting parts (the clamp stand of the first implementation was removed on 9 Oct 2026 after the headset test showed it got in the way) | Two-handed assembly feels natural in VR and the finished rocket has to be carried to the bin |
| D5 | Removing parts | Not covered | Parts can be pulled back out before submission, with the exceptions in 5.5 | Error recovery. It falls out of the slide mechanic for free |
| D6 | Order of assembly | Figure 2.4 puts the motor after the airframe and the cap after the motor | Enforced, behind a switch | Follows the figure, easy to relax if it feels arbitrary in testing |
| D7 | Plate thickness | Slots 2.5 mm wide | Fins and flaps are 2.0 mm thick (the kit fin was 1.55 mm) | Thin plates are hard to see and grab in a headset |
| D8 | Twist sound | Ratchet in 2.3, metal scraping metal in 4.2 | One tick per detent | Matches the per-step vibration. Final clip is the audio owner's choice |
| D9 | Flap shape | Dimensions only | Top edge swept 30 mm, bottom edge 8 mm | An upside-down flap has to look different, but only slightly |
| D10 | Unlocked cap look | "Looks almost identical to a locked cap" | 2 mm gap that closes on locking | Gives the returned prototype something to diagnose |
| D11 | Overall scale | Real BT-55 size | Real size, with a single scale setting | Lets the kit be enlarged if 34 mm parts prove fiddly |
| D12 | Team systems in the team project | Section 9 systems are the teammates' | The working versions built for testing (menu, flow, submission bin, launch screen, countdown, launch site, blueprint and help displays) were ported onto the team's room as replaceable defaults, in their own scene and modules. *Pending team agreement.* | The experience can be played end to end on the real room now; each module can be deleted or replaced without touching the rocket (see Docs/INTEGRATION.md) |

## 14. Open questions for the team

1. **Flap physics.** The treatment says flaps placed too low move the centre of pressure forward and destabilise the rocket. In real rocketry, fin area added lower down moves the centre of pressure back, which is more stable. The game works either way, but the explanation shown to users (blueprint, help wall, failure screen) should either avoid the mechanism or be reworded.
2. **Abort behaviour.** This spec returns an editable rocket after Abort and a frozen one after a failed launch. Confirm.
3. **History content.** The educational aim on space mission history rests entirely on the tutorial video.
4. **Sound materials.** The asset list specifies metal-on-metal impacts for a cardboard and plastic model.
5. **Room description.** Treatment section 1.1 does not mention the puzzle panel or a separate Submit button, which section 3 relies on.
6. **Motor label.** The treatment describes a printed designation on the motor. The mesh has no label yet. It needs a small texture.
