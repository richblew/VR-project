# VR Rocket progress log

One dated entry per milestone (SPEC.md section 11): what works, what was verified and how, and what is still unverified in the headset. Headset checklist items (SPEC.md 12.2) are only ever ticked by a person wearing the headset.

## 2026-10-08: Housekeeping

- SPEC.md v1.0 adopted as source of truth (`Docs/SPEC.md`). Old conventions note reduced to a pointer.
- Kit v2 extracted to `SourceArt/VR_Rocket_Kit_Parts_v2/`; v1 folder removed. Six FBX files in `Assets/VRRocket/Models/` (BodyTube_StandIn deleted; TailFin, NoseCone, MotorCap overwritten keeping their .meta files; BodyTube, WingFlap, Motor added).
- LFS rules added for `.unitypackage` and `.cubemap` ahead of committing the Creepy Cat kit.
- Verified: file layout only. Nothing opened in Unity yet.

## 2026-10-08: M0 Import and verify (done)

**What works**
- Six FBX files in `Assets/VRRocket/Models`, import scale 1, no axis conversion needed. Shared URP Lit materials extracted to `Assets/VRRocket/Materials` (Rocket_White, Rocket_Orange, Rocket_Black, Rocket_Cardboard) and remapped on every importer.
- `VRRocket.Runtime` / `VRRocket.Editor` / `VRRocket.Tests.Editor` assemblies. Runtime: PartType, RocketPart, AttachPoint, BuildReport (section 7), AssemblyTuning (section 10.4). Editor: AttachPointGenerator reads `SourceArt/VR_Rocket_Kit_Parts_v2/attach_points.json` (menu VR Rocket > Generate Attach Points On Selected BodyTube).
- Prefabs: BodyTube (with 12 generated attach points), TailFin, WingFlap, NoseCone, Motor, MotorCap. FBX nested as a child named Mesh, RocketPart on the root.
- `Assets/VRRocket/Scenes/Dev_Interactions.unity` with a static assembled rocket (M0_StaticAssembledRocket), added to the build list.
- Interaction layer `RocketPart` added at index 1 of `Assets/XRI/Settings/Resources/InteractionLayerSettings.asset`.

**Verified, and how**
- Mesh bounds read in the Editor match SPEC.md 4.3 to 0.1 mm for all six parts; triangle counts match 4.2 (2100 / 425 / 28 / 2702 / 768 / 3000).
- Mirror and rotation check from vertex data: flap outer edge Y range -31..+9 mm (swept edge up); tube slot vertex clusters at yaw 0/120/240 (fins, mid flaps) and 60/180/300 (low flaps); thrust ring at Y=59; motor black submesh at Y 0..10; nose orange submesh at Y 37.4..49.4; cap lugs within r=15.2 mm.
- Edit-mode tests (6/6 green): generated transforms match the JSON, +Z points outward, generator is idempotent; all four outcome rows of 7.2 plus mixed failedComponents.
- Game-view render of the assembled rocket matches `preview.png`. NoseSeat at world y+0.300, CapSeat at +0.000.

**Not verified in the headset**: nothing to verify yet (no interaction).

**Notes**
- XRI 3.4 API confirmed from package source: `XRBaseGrabTransformer.Process(XRGrabInteractable, UpdatePhase, ref Pose, ref Vector3)`; haptics via `XRBaseInputInteractor.SendHapticImpulse(float amplitude, float duration)` (wraps HapticImpulsePlayer). The rig's hands are `NearFarInteractor` (sphere caster near, curve caster far) so a single interaction layer cannot block far grabs without blocking near ones; near-only will be a select/hover filter on the part.
- Creepy Cat kit: all 39 materials use the built-in Standard shader and need the URP converter before the dev room is built. Its Example_01 scene has 257 lights.

## 2026-10-08: M1 Parts, tray, grab, respawn (done)

**What works**
- Part prefabs now carry physics and grab: box colliders padded to 8 mm on fins and flaps, capsules on nose cone, motor and tube, convex mesh on the cap; dynamic rigidbodies with gravity, continuous collision, interpolation. `XRGrabInteractable` (Kinematic movement, dynamic attach, snap to collider volume, throw on detach), interaction layers Default|RocketPart, and `NearOnlyGrabFilter`.
- `Assets/VRRocket/Prefabs/RocketWorkstation.prefab`: root on the desk surface, `ScaledRoot` (scaled by `AssemblyTuning.rocketScale`), greybox `AssemblyStand` holding the tube kinematic with its bottom rim 0.20 m up, `PartsTray` with 3 fins, 3 flaps, nose cone, motor, cap laid out in groups, `RespawnPad`, `WorkstationBounds` trigger, `PartRespawner`.
- `Assets/VRRocket/Settings/AssemblyTuning.asset` with the section 10.4 starting values.
- Dev scene: greybox room (floor, workstation desk, mission control desk), `Complete XR Origin Set Up Variant` with continuous move, teleportation provider and both teleport interactors disabled, workstation on the back desk. The M0 static rocket is kept as `Prefabs/AssembledRocket_Static.prefab` for the launch-animation owner.
- XR Interaction Simulator set to auto-instantiate in the Editor only (`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset`), so Play mode works on the laptop and the Quest build is unaffected.

**Verified, and how (Play mode, driven through the Editor link)**
- All nine parts start at rest on the desk, bodies dynamic, one collider each, one select filter and one hover filter registered. Tube clamped kinematic at world y 0.95 with no grab interactable.
- Respawn: a fin moved below the floor and a flap and nose cone parked out of bounds all reappeared on the pad within respawnDelay, upright in their tray orientation, zero velocity, collider resting on the desk, bodies dynamic.
- Near-only: `XRInteractionManager.IsSelectPossible/IsHoverPossible` for the right NearFarInteractor against a fin: false at 0.58 m and 0.20 m, true at 0.10 m and 0.03 m. A forced SelectEnter then SelectExit toggles the body kinematic and back. An existing grab is not cancelled when the hand moves far.
- Teleport ray inactive and cannot select parts.

**Not verified in the headset** (SPEC 12.2 items 1 and 10 remain for Praise): real controller grabs anywhere on a surface with either hand, throw and drop feel, respawn timing.

**Deviations and notes**
- SPEC 10.3 suggests a `RocketPart` interaction layer to stop far and ray interactors. The rig's hands are single `NearFarInteractor`s whose near and far casters share one layer mask, so a layer cannot separate near from far. The layer exists and parts carry it, but near-only is enforced by `NearOnlyGrabFilter` measuring from the interactor's own transform (its attach transform sits at the far-cast hit point and is useless for this). Parts also keep the Default layer so the prefab works in any scene without rig edits.
- Dev room is greybox primitives, not the Creepy Cat kit: all 39 kit materials use the built-in Standard shader and render magenta in URP until the Render Pipeline Converter is run, which edits the read-only kit folder and is Praise's call. The kit's demo scene has 257 lights.

## 2026-10-08: M2 Guide mechanic on the nose cone (done)

**What works**
- `GuideGrabTransformer` (XRBaseGrabTransformer, registered after XRI's XRGeneralGrabTransformer so it runs last): approach glow on every accepting point within glowRadius (brightness by closeness), capture inside captureRadius when within orientation tolerance, rotation locked to the seated rotation, position projected onto the attach axis and clamped to guideLength, nose magnetism (displayed depth = hand depth x noseMagnetism), break away on sideways drift past breakRadius or past the end of the guide, seat on release (animated over seatDuration, then parented under the attach point, kinematic, collisions with the rocket ignored), re-grab of an attached part re-enters the guide at depth 0, pull past guideLength detaches and returns the part to its tray group.
- `RocketGrabInteractable`: XRGrabInteractable plus a pre-grab event, needed so an attached (kinematic) part is dynamic again when XRI records its state; otherwise XRI restores kinematic on release and refuses to throw.
- `AttachPoint` now owns its glow (unlit emissive ring mesh, MaterialPropertyBlock, per-frame max request) and a static registry of active points.
- Feedback: `AssemblyEvents` static hub, `FeedbackLibrary` asset (clips per event, haptic amplitude and duration per SPEC 6 table), `AssemblyFeedback` on the workstation root (pooled 3D AudioSources, haptics through `XRBaseInputInteractor.SendHapticImpulse` on the holding hand). Placeholder clips: HoverSound (engage tick), Button_22_Click (seat click), Button_14_Hover (nose soft click, cap seat, unseat), Button Pop (impacts, respawn).
- Only the nose cone carries the transformer so far; the other parts stay plain grabbables until their milestones.

**Verified, and how**
- Edit-mode tests 13/13 green (GuideMath: depth/sideways decomposition, clamp and magnetism, break rule, orientation tolerance, seated rotation for nose/fin/flap and flap orientation readout).
- Play-mode test `NoseGuideTests.NoseCone_ApproachCaptureSlideBreakSeatAndRemove` green (6.4 s): loads Dev_Interactions, takes over the right controller transform, forces the grab through XRInteractionManager and asserts every stage of 5.2 including events, parenting, kinematic state, collision ignore, glow on/off and that the nose seat clip was queued on a 3D source. Run it with Test Runner > PlayMode or `unity command run_tests --mode playmode --filter NoseGuide --async_tests true`.
- Console clean of errors and of warnings from VRRocket code during the runs.

**Not verified in the headset** (SPEC 12.2 items 2 and 3 for the nose, and all haptics): the feel of capture and break-away radii, the vibration values, whether the glow ring reads well on Quest.

**Deviations and notes**
- `noseMagnetism` is implemented literally as the displayed fraction of the hand's distance (0.5 shows half). If "pull strength" was meant, invert it in `GuideGrabTransformer.Process`.
- XRGrabInteractable unparents a held object and restores the grab-time parent on drop; the transformer re-parents a pulled-off part to its tray group after the drop.
- Part-side glow (the "should" in SPEC 6) not done yet; planned for M8 polish.
- Teleporting a controller through the tray in tests sweeps parts off the desk via the rig's Pusher bodies; harmless (they respawn) but tests park the hand away from the tray first.

## 2026-10-08: Decisions applied after M2

- Creepy Cat kit and both `.unitypackage` archives deleted; the dev room stays greybox. SPEC.md 3.2 and 10.1 and CLAUDE.md updated; no art kit is a dependency.
- Near-only distance filter accepted; SPEC.md 10.3 rewritten to describe it. It already blocks hover as well as select, so a far-pointed part shows no highlight, ray cursor or glow (verified: IsHoverPossible false at 0.20 m and 0.58 m).
- noseMagnetism confirmed in the literal sense. New requirement in SPEC.md 5.3: the pull fades in along the guide (exact follow at the outer end, full value at the seat) and engaging or breaking away never jumps. Implemented as `fraction = Lerp(noseMagnetism, 1, depth / guideLength)` plus a `guideBlendDuration` (0.1 s, tunable) ease between free and guided poses on engage, release and detach.
- `Dev_Interactions` is now first in the build scene list.

## 2026-10-08: M3 Motor cap twist (done)

**What works**
- `IAttachedGrabHandler`: a part can take over what happens when it is grabbed while attached, and offset where it rests when seated. `GuideGrabTransformer` delegates to it; everything else (guide to the seat, seat animation, pull-off detach) is shared with the other parts.
- `MotorCapTwist` on the MotorCap prefab (SPEC 5.4): seats unlocked `capUnlockedGap` proud of the tube with the soft seat sound and 0.2/0.04 haptic; gripping the seated cap does not move it; the cap turns about the tube axis following the hand, clockwise only viewed from below, as a ratchet (turning back does nothing, progress kept between grabs); one tick and 0.2/0.02 haptic per `capDetentAngle` of new progress; at `capLockAngle` it stops, plays the firm click, fires 1.0/0.20 and `capLocked` becomes true; the gap closes over 0.1 s; a locked cap cannot be turned or removed; letting go early is silent; an unlocked cap pulled more than `capPullOff` along the axis comes off and its progress resets.
- Twist reading borrows the XRKnob technique: the hand's position orbiting the axis when it grips more than 3 cm off-axis, otherwise its wrist roll (forward or up vector, whichever lies flattest), never both, with wrap-safe per-frame deltas.
- `CapTwistLogic` is plain C# (progress, detents, lock, reset) so SPEC 12.1's cap rule is unit-tested.
- Glow rings on CapSeat and MotorSeat at the bottom rim, sharing the nose ring mesh.
- New events `CapDetent` and `CapLocked` wired into `AssemblyFeedback` with the placeholder ratchet tick (HoverSound) and firm click (Button_22_Click).

**Verified, and how**
- Edit-mode 20/20 green. CapTwistLogic: accumulates across grabs, never decreases, detent ticks exactly on each 30 degree boundary of new progress, locks exactly at 180 without overshoot even from summed float deltas, lock reported once, nothing changes after lock, reset on pull-off.
- Play-mode 4/4 green (26 s), via `unity command run_tests --mode playmode --filter VRRocket.Tests --async_tests true`:
  - `CapTwistTests.Cap_SeatsUnlockedWithGap_RatchetsClockwise_LocksAtLockAngle`: 2 mm gap after seating, gripping leaves it, 100 degrees of clockwise orbit gives progress 100 and 3 ticks with the cap turned to match, 50 back changes nothing, forward again locks at exactly 180 with 5 ticks total and one lock, gap 0 afterwards, further turning and a 8 cm pull do nothing, release leaves it seated kinematic under CapSeat.
  - `CapTwistTests.Cap_EarlyLetGoKeepsSeat_ProgressKeptBetweenGrabs_PullOffResets`: early let-go keeps the seat and the gap with no events; progress continues across grabs; a 6 cm pull detaches, resets to 0, the cap follows the hand and drops dynamic into its tray group; re-seating starts from zero with the gap.
  - `NoseGuideTests` (2): the M2 walk-through now also checks the faded magnetism at three depths and that engage and break-away ease over the blend instead of jumping.
- Console clean of VRRocket errors and warnings during the runs. One teardown bug found and fixed on the way: XRI releases a held object from OnDisable during scene unload, and re-parenting there threw.

**Not verified in the headset**: the twist direction (clockwise viewed from below is implemented as negative Unity yaw about the tube's +Y; `MotorCapTwist` has a flip toggle if it feels reversed), the 3 cm position/wrist mode radius, how the ratchet reads with real wrist motion, whether the 2 mm gap is visible on Quest, all haptic values.

**Notes**
- Gating is not implemented yet (M6), so the cap can be seated on a bare tube, as M3 requires.
- Dynamic attach snaps to the collider surface, so the hand-to-origin offset of a held cap depends on where it was grabbed.

## 2026-10-08: Environment v1, control room and workstation (done)

Separate from the rocket milestones. Rocket prefabs and scripts untouched; `Dev_Interactions.unity` untouched.

**What was built**
- `Assets/VRRocket/Environment/Prefabs/ControlRoom.prefab` and `Assets/VRRocket/Scenes/Env_ControlRoom.unity` (second in the build list). Room per SPEC 3.1: workbench with the RocketWorkstation at the back, mission control console with bin, Submit, Launch, Abort and puzzle placeholders at the front, 2.4 m emissive launch screen on the front wall with a countdown clock and mission panel beside it, window with an outside view and an exit door on the left, help board and roller shutter door on the right, LED ceiling panels and cove strips, rubber standing mat. Both work surfaces at 0.95 m, checked against the workstation's stand and tray. No chairs. Seven anchors (PlayerSpawn, WorkstationAnchor, MissionDeskAnchor, BinAnchor, LaunchScreenAnchor, HelpWallAnchor, BlueprintAnchor). Full description in `Docs/ENVIRONMENT.md`, credits in `Docs/ASSET_CREDITS.md`, screenshots in `Docs/Screenshots/`.
- Extra: a concrete apron, launch pad, gantry and a full-size (36x) static copy of the assembled rocket 30 m outside the window, so the user sees what they are building.

**Style decision**: photoreal Poly Haven props on primitive-built architecture dressed with Poly Haven PBR surfaces. The rocket parts are photoreal and the mood is "real mission control plus clean workshop", so stylised kits would clash; and primitives keep the architecture cheap and trivially editable. The stylised kits (Quaternius Modular Sci-Fi MegaKit and Essentials, Kenney Space and Furniture kits) could not be fetched without a click-through on itch.io / kenney.nl, so no side-by-side scratch scene was made; if you want that comparison, drop their zips in `specs/` and I will stage it.

**Assets considered**
- Taken: steel_frame_shelves_03, worn_metal_rack, metal_tool_chest, metal_toolbox, bench_vice_01, industrial_pipe_lamp, Television_01 (as the blueprint CRT), fire_alarm, rollershutter_door (plain variant), mounted_fluorescent_lights (one tube variant, 2.5k tris instead of 17.8k); textures painted_plaster_wall, smooth_concrete_floor (albedo dropped for a neutral grey epoxy look, normal and roughness kept), metal_plate, rubber_tiles; HDRI qwantani_puresky 2K.
- Rejected: metal_office_desk (0.79 m sitting height, raising or scaling it looks wrong, so both desks are built from primitives at 0.95 m), steel_frame_shelves_01 (redundant with 03), tool_cart (29k tris for a background prop), industrial_storage_cart (19k tris, does not fit a clean room). Poly Haven's 14k-tri toolbox and 13k-tri tool chest were kept because they sit at the workbench where the user looks closely.
- Not found as free CC0/CC BY within the bounded search: a proper console desk with screens, a window frame, a door, a countdown clock. All built from primitives plus TMP text instead; they are the obvious things for the teammates to replace.
- Unity Asset Store items were not used (they need your account); none was needed for v1.

**Pipeline**: a Poly Haven material builder (scratch editor script, kept in `SourceArt/Environment/tools/`) converts the EXR roughness/metal maps into URP metallic-smoothness PNGs, converts normal EXRs to 8-bit PNG and remaps each FBX importer. Props import with file scale off and global scale 0.01 (Poly Haven FBX vertices are in centimetres).

**Measured at commit** (room hierarchy only, Editor figures)
- Triangles: 75,418 for the room including the outside pad and full-size rocket (about 10k of that is the big rocket); the workstation adds 12,485; XR rig controllers add a few thousand.
- Materials: 38 on room renderers, of which 5 are the rocket's (outside copy), 4 the XRI button's and 3 puzzle-wire dots; 26 are the room's own.
- Textures: 52, all 1K except the 2K HDRI cubemap at 1024. Editor memory 125 MB (uncompressed in the Editor); estimated 33 MB on Quest at ASTC 6x6. One 1024 lightmap, 2.7 MB.
- Added to the repo: about 58 MB under Assets/VRRocket/Environment, 82 MB of originals under SourceArt/Environment, 5 MB of scene and lightmaps. Total about 145 MB.
- Lighting baked (Progressive CPU, 10 texels/unit). One real-time directional fill at 0.35 with no shadows; everything else baked or emissive. Light probes and a baked reflection probe cover the dynamic parts.
- Headset frame rate not measured here; that is Praise's check on the Quest.

**Known rough edges for v1.1**: the CRT's screen face orientation has not been confirmed in a close-up; the puzzle panel and side screens are flat placeholders; the roller door and rack are a little rusty for the palette; the bin has no lid or animation (M7 and the teammates own that).

## 2026-10-09: M4 to M9, all remaining interactions (done)

All milestones of SPEC 11 are now implemented. Automated results: 27/27 edit-mode, 11/11 play-mode (116 s), console clean of VRRocket errors.

### M4 Fins
- `GuideGrabTransformer` on the TailFin prefab; the slot axis is the point's local +Z, roll is corrected to the slot on seating (`GuideMath.SeatedRotation`), any slot in any order. Emissive frame meshes (`GlowFrame_Fin`) on the three fin slots.
- Play test `Fins_AnyOrder_RollCorrected_Removable`: fin 1 held rolled 150 degrees seats in slot 3 with the slot's rotation to under 1 degree, fins 2 and 3 fill slots 1 and 2, an occupied slot accepts nothing, a fin pulls back out and the count drops.

### M5 Flaps
- All six flap slots accept any flap, either way up; the held orientation is kept (`SeatedRotation` picks the flipped rotation when closer). `RocketAssembly` records slot name, Base/Midpoint and Up/Down at seating; `GetReport()` reads them.
- Play test `Flaps_SixSlots_EitherWayUp_PlacementsRecorded_FeedbackIdentical`: Low_2 up reports Base/Up, Mid_1 upside down reports Midpoint/Down, Mid_3 reports Midpoint/Up; failedComponents lists the two wrong flaps; every placement raised exactly one engage and one seat event, wrong or right; removing a flap clears its record.

### M6 Motor and gating
- Motor: the reversed (nozzle-up) motor gets no glow and never engages (orientation now gates the glow for the motor only); nozzle-down engages and travels into the mount by itself over seatDurationMotor (0.30 s).
- `AssemblyStateMachine` (plain C#) implements 5.5: forward path, backward moves on removal, removal exceptions (airframe locked once the motor is seated, motor locked while the cap is seated), gating on and off (`enforceOrder` from the tuning asset, settable at runtime). `RocketAssembly` pushes gating into every attach point; `AssemblyGrabGate` on every part refuses grabs the rules forbid, so the hand falls through to whatever else it touches.
- Edit tests `AssemblyStateMachineTests` (7) cover 12.1's state machine and gating rows. Play tests: `Motor_RefusedBeforeAirframe_RefusedNozzleUp_SeatsNozzleDown_ThenCapGating` and `Cap_RefusedBeforeMotor_AndEnforceOrderOff_MotorAcceptedEarly`.

### M7 Completion and handover
- The stand's clamp is an `AttachPoint` accepting the tube, so lifting out, carrying and re-seating reuse the guide. At PrototypeComplete the stand releases (event, stand-release sound, both-hand haptic); the tube becomes grabbable near-only; attached parts cannot come off out of the stand except the cap twist; a dropped rocket returns to the stand.
- `RocketAssembly` implements the 8.4 interface: `State`, `StateChanged`, `PartAttached`, `PartRemoved`, `Submitted` (C# events and UnityEvents), `GetReport()`, `TrySubmit()`, `ReturnPrototype(mode, at)`, `BeginNewBuild()`. `RocketWorkstation` spawns a fresh kit from the part prefabs at the original tray layout.
- Stubs (8.5) in `Prefabs/Stubs/MissionControlStubs.prefab`, placed in both scenes: `SubmissionZoneStub` (trigger box plus Submit button), `OutcomeReadoutStub` (report panel plus Failed-launch and Abort buttons). XRI push button `onPress` wired as persistent UnityEvents, no code reference to the example assembly.
- Play tests: `StandRelease_Carry_Submit_FailedLaunch_NewBuild` (tube ungrabbable until complete, release fires once, lift out, parts stay on and cannot be pulled off while carried, cap still grippable, release in the zone, submit yields MotorRetentionLoss with the cap listed, frozen, taken away, inspect-only return that can be picked up but not altered, fresh kit of nine parts and a new tube in the stand, old points dead), `Submit_Abort_ReturnsEditableToStand`, `DroppedRocket_ReturnsToStand`.
- Bug found by the suite and fixed: the stub's take-away coroutine hid the rocket after an abort had already returned it. It now checks the state first.

### M8 Polish (partial)
- `ImpactAudio` on every part: dull desk impact or sharp part-on-part impact, volume by relative speed, cooldown per part.
- Part-side glow: each part has an emissive mesh on its attach feature (tab frame, shoulder ring, nozzle ring, lug ring) that lights with the nearest accepting point.
- Not done here: the tuning pass and the Quest frame-rate check, both headset work.

### M9 Integration notes
- `Docs/INTEGRATION.md` written for the teammates: drop-in, interface, flow, stubs to delete.

### Not verified in the headset (SPEC 12.2, all of it is Praise's)
Items 1 to 14. In particular: the twist direction, whether a 40 degree slot tolerance feels right, whether the fin and flap frames read on Quest, the stand release vibration on both hands, carrying the rocket between desks, the inspect-only prototype feel, and frame rate with the glow meshes.

### Notes
- Test harness: the hand's attach controller smoothing is disabled in tests and the placement helper verifies and re-derives the hand relation, because teleporting a controller through XRI's anchor smoothing is not what a real hand does.
- XRI logs "Retain Transform Parent ... old parent is deactivated" on two test pull-offs; harmless, the transformer re-parents loose parts itself.
- In Env_ControlRoom the stub Submit, Failed-launch and Abort buttons sit on the console top next to the real-looking panel buttons; the panel buttons are not wired. Delete the stubs when the real systems arrive.

## 2026-10-09: Haptics pass for the Quest 3, and the room first in the build

**Research.** Meta's OpenXR haptics documentation states that varying vibration frequency is supported on controllers with a voice-coil motor, which includes the Quest 3's Touch Plus (and Touch Pro); Quest 2 ignores it. XRI 3.4's `OpenXRHapticImpulseChannel` passes amplitude, duration and frequency through, so frequency is a usable design dimension here. Design rules applied: low frequency (80 to 120 Hz) for heavy events, high (200 to 250 Hz) for fine ones, 10 to 100 ms durations with the lock at 200 ms, an optional second settle pulse, and continuous sensations rendered as sparse ticks by distance or angle rather than sustained buzz, which fatigues and masks the discrete clicks. The rig's generic `SimpleHapticFeedback` (0.25 for 0.1 s on every hover, 0.5 for 0.1 s on every grab, on every interactor) was silenced on the hands in both scenes so only the designed set plays.

**What changed**
- `FeedbackLibrary.Haptic` gained `frequency` and an optional second pulse; the SPEC 6 values keep their amplitude and duration and gained a frequency, with a settle pulse on seats, the cap lock and the stand release.
- New haptics (SPEC 6 now lists them): presence tick when a hand comes within reach of a loose part, pick-up contact, rocket heft, let-go, slot texture ticks every 4 mm of guided travel, thread texture ticks every 6 degrees of cap progress between detents, clamp let-go when the rocket is lifted out, clunk when it snaps back into the stand, both-hand confirmation when the bin accepts the rocket, both-hand tick when a fresh kit arrives, push button down and up on the pressing hand.
- Kept silent on purpose: rejected placement, break-away, early let-go of the cap, respawn, loose-part impacts (audio only).
- `ButtonPressHaptics` on both push button prefabs (stubs and the room's console), wired through the button's own onPress/onRelease events.
- Build list: Env_ControlRoom first, then Dev_Interactions, then the XRI example scene.

**Verified**: 27/27 edit-mode, 11/11 play-mode. The play tests now assert that the slide texture ticks during a guided slide, the thread texture ticks between detents, the pick-up and heft events fire once, the kit-spawned event fires once, and that an early let-go of the cap produces no feedback-bearing event. One real bug found on the way: the slide tick was gated on the Fixed update phase, which XRI never uses for grab transformers; it now counts in the Dynamic phase.

**Not verified in the headset**: everything about how these feel. The simulator has no haptic device. Tune in `Assets/VRRocket/Settings/FeedbackLibrary.asset`; if the frequency changes do nothing on your controllers, set them to 0 to fall back to the runtime default.

## 2026-10-09: Menu, game flow and launch sequence (done)

Praise asked for a themed start menu, a game flow (menu, assembly, submission, countdown, launch outside, result) and a full launch sequence with explosions and physics. SPEC section 9 gives the launch screen, bin and flow to the teammates; everything here is a working version they can replace, built outside the rocket prefab and driven by the room's real console buttons.

**What exists now (`Env_ControlRoom.unity`)**
- `GameFlow` root: `GameFlowController` (phases Menu, Assembly, PreLaunch, Countdown, Launch, Result), `LaunchScreenController`, `BlueprintDisplay`, a 2D AudioSource, and the children `SubmissionBin` (trigger over the console opening, door sounds, `ReturnSpot` on the console top), `ConsoleZone`, `EventSystem` (XRUIInputModule) and `MenuCanvas` (world-space UGUI 1.0 x 0.65 m, 0.9 m in front of the spawn point, TrackedDeviceGraphicRaycaster, Orbitron and Share Tech Mono, Kenney button; START calls `GameFlowController.StartGame`).
- `LaunchSite` root at the pad (-30, 0.3, 6): `LaunchSequence`, `PadOrigin`, the `LaunchVehicle` prefab (the assembled rocket at 36x stripped to meshes, with the engine flame and sparks at the nozzle), pad smoke and steam emitters, a `PadCamera` rendering to `PadCameraFeed.renderTexture` (640 x 360, enabled only from pre-launch to result), engine loop and one-shot audio. `Prefabs/Launch/Explosion.prefab` (fireball, flames, smoke, sparks, flash) is instantiated at the failure point.
- Screen: the ControlRoom prefab's launch screen gained a status line (`ScreenStatus`), a bigger centre text, the new fonts and a `Diagram` of quads named `Diag_*` that `LaunchScreenController` paints red for failed components. From pre-launch to the flight the screen surface switches to `Mat_PadFeed`, the pad camera.
- Console: SubmitButton.onPress calls `SubmissionBin.Submit`, LaunchButton.onPress `GameFlowController.Launch`, AbortButton.onPress `GameFlowController.Abort` (persistent listeners on the prefab instance). The MissionControlStubs instance is gone from this scene; it stays in `Dev_Interactions.unity`, where its tests run.
- Flow: parts are locked (`RocketAssembly.interactionEnabled`) until START. Submission needs the loose complete rocket in the bin; the rocket sinks, the vehicle appears on the pad, and Launch is accepted only once it is there. Countdown 10 s with ignition at T-3. The outcome comes from the build report: success climbs to 1200 m, then back to the menu with a fresh kit; motor retention loss ejects the motor and never leaves the pad; unstable flight corkscrews and explodes at 7.5 s. A failure returns the prototype for inspection at the console's return spot and spawns a new kit (SPEC 8.4). Abort from pre-launch or countdown puts the same rocket back in the stand, editable.
- Flight model: `RocketFlightModel` (plain C#, 4 unit tests): 110 kN thrust for 7 s, 2 t dry and 3 t propellant, gravity, quadratic drag, per-outcome behaviour. Open-source Unity rocket sims were looked at (JKamue/SpaceFlight, Cybalt-SR/SpaceSimulator, RocketNasa) and not used: mixed or unclear licences and far heavier than a 15 second show needs.
- Respawner change: `PartRespawner` has safe zones (the bin and the console top) where parts count as in bounds. Without them the rocket dropped in the bin and the prototype returned to the console were pulled back to the bench after 1.5 s.
- Assets (CC0 and OFL, see ASSET_CREDITS.md): Kenney particle sprites in `Assets/VRRocket/Textures/Particles`, sounds in `Assets/VRRocket/Audio/Kenney` (the FeedbackLibrary impact clips now use the Kenney impact sounds), UI sprites and fonts with their TMP font assets in `Assets/VRRocket/UI`, particle materials in `Assets/VRRocket/Materials` (URP Particles/Unlit; additive ones blend SrcAlpha/One because the sprites are white under their alpha).
- Lighting rebaked after removing the static 36x rocket from the ControlRoom prefab. The environment build's helper camera `ShotCam` was still active and rendering every frame; it is now inactive.

**Verified**: 31/31 edit-mode, 14/14 play-mode. `GameFlowTests` runs in Env_ControlRoom through the same hand helpers: parts locked in the menu, START, full build, carry to the bin, submit, abort, resubmit, refused early launch, countdown, retention-loss flight, result screen with the diagram, inspect-only return that stays on the console, new kit; the success path locks the cap, flies past 1000 m and returns to the menu with nine fresh parts; out-of-phase button presses do nothing. One flow bug found by the tests: an aborted submission counted as a flight attempt; attempts are now counted at liftoff. Screenshots in `Docs/Screenshots/flow_*.png`.

**Not verified in the headset**: the UI ray on the START button (XRUIInputModule plus the NearFar interactor's UI mode, the standard XRI setup), menu legibility at 0.9 m, the pad camera's cost on Quest (one extra 640 x 360 render from pre-launch to result), the explosion's fill rate seen through the window, audio levels. The wire puzzle of SPEC 9 is not built; Launch is available as soon as the prototype is accepted.

## 2026-10-09: First headset test, stand removed, floating parts fixed, teleport enabled (done)

Praise ran the first Quest 3 build (APK via Build Profiles, installed over USB). Two problems came back: attached parts floated apart from the carried rocket, and the clamp stand got in the way of fitting parts. Decision: no stand, every part including the tube starts flat on the bench, parent-child attachment stays.

- **Floating parts, root cause.** Every part's Rigidbody had interpolation on. Unity's interpolator rewrites the transform from the rigidbody's own pose each frame, so (a) kinematic child parts lagged and wobbled behind a carried rocket, and (b) any pose set through the transform (the seat animation, a freshly spawned kit) was overwritten by the stale rigidbody pose until the next physics sync. Attached parts now run with interpolation off (`RocketPart.SetAttached`), the seat routine writes to physics directly, and `SpawnKit` sets `Rigidbody.position` for every spawned part. Loose and held parts keep interpolation for smooth motion.
- **Stand removed.** `AssemblyStand` deleted; the tube is a tray slot like the parts (`RocketWorkstation.tube`, `TryGetTubeHome`), always grabbable, dynamic (the BodyTube prefab is no longer kinematic). `RocketAssembly` no longer special-cases the clamp: gating follows the state machine only, a dropped or aborted rocket is placed at rest on its bench slot, `BeginNewBuild` spawns the tube on the tray. Rocket parts never collide with each other (`ApplyPartCollisionRules`), so a held part cannot shove the resting tube and the guided mechanic alone assembles them. Part-on-part impact sounds therefore never play; part-on-desk still does.
- **Teleport.** Nothing in the room was a teleport target and the rig's TeleportationProvider was disabled in both scenes (the XRI example toggles it from its locomotion panel). `TeleportationArea` on the room's Floor and StandingMat (Teleport interaction layer), provider and snap turn enabled on the rig instances in Dev_Interactions and Env_ControlRoom. Smooth move stays off.
- **Editor.** The XR Interaction Simulator no longer auto-spawns in Play mode: it overrode a real headset's pose (SteamVR test showed the room glued to the head).
- **Tests.** Play-mode tests stand the tube upright and freeze it in `SetUpScene` (`StandTubeUpright`), standing in for the player's other hand, so the upright seat poses of the helpers hold. FullFlowTests rewritten for the bench (carry from the bench, inspect-only return with the fresh tube on its slot, abort back to the bench, dropped rocket back to the bench). 31/31 edit-mode, 14/14 play-mode.

**Not verified in the headset**: the new bench layout, two-handed assembly feel, parts staying put while carrying, teleport arc and snap turn. The spawn point is still 1.3 m from the bench; teleport or walk up to it.

## 2026-10-09: Second headset test, one-piece rocket and reachable console (done)

Praise reported parts (the cone) coming off the assembled rocket, wobble when working the cap, and console buttons out of reach from the player's side.

- **One physical unit.** Attached parts stayed separate kinematic bodies, so the tube's rigidbody knew nothing of its fins and cone: the rocket rested on its round tube (free to roll), and an accidental grip near the top grabbed the removable cone instead of the rocket. Now `RocketAssembly` mirrors every attached part's colliders onto a `Compound_<part>` child of the tube (box, capsule, sphere, convex mesh), so one rigidbody carries the assembled shape; the part keeps its own kinematic body only for hover and grab. Proxies are removed on detach and die with the tube on a new build.
- **Grab rule.** While the rocket is in a hand, attached parts cannot be grabbed; only the cap can be twisted with the other hand. Parts come off only from a resting rocket by a deliberate pull along the guide, when the state machine allows. This restores the SPEC 5.6 intent without the stand.
- **Console.** The control panel (Submit, puzzle, Launch, Abort) moved from the back edge of the console to the front edge: buttons are now 0.25 m in from the player's side at 1.14 m height. The decorative telemetry screens moved to a new back panel. Button wiring is unchanged (same objects).
- Research used: Unity's collider and rigidbody manuals on compound colliders and kinematic bodies, and XRI's grab interactable docs on movement types. The alternative of removing part rigidbodies on attach was rejected because XRI's grab interactable requires one.

**Verified**: 14/14 play-mode (compound colliders do not disturb the guided mechanic, carry, submission, returns). **Not verified in the headset**: the feel of the one-piece rocket, cap twist stability, button reach. If the cone still comes off, the next step is to require a two-handed pull (rocket held in one hand) for removal.

## 2026-10-11: Attached parts now really move with the rocket (done)

Praise's third test (SteamVR in the Editor): a seated cone stayed frozen in space when the tube was moved, so did every other part; attached parts could not be pulled off; parts on the floor were a chore to collect.

- **Root cause, reproduced in a test.** A seated part is a kinematic rigidbody child of its attach point. Unity only drags a child rigidbody along when its parent is moved through the transform; when the parent is moved by the physics engine (XRI's kinematic movement while carried, falling, settling), the physics write-back restores the child's stale pose, so the part stays where it was and even its local pose goes wrong. XRI's grab interactable requires a rigidbody on every part, so the rigidbody cannot simply go. New `AttachedPartSync` on the tube re-asserts every attached part's recorded seat pose (`RocketPart.seatLocalPosition/Rotation`, taken at seating) in FixedUpdate, LateUpdate and before rendering, in both transform and rigidbody; held parts are left to the hand. The new test `AttachedParts_FollowTheCarriedTube_AndDroppedPartsRespawn` carries and turns the tube with the hand and checks the nose and a fin keep their tube-relative positions, then checks a flap dropped below the bench reappears on it.
- **Removal while held.** The one-day-old rule that blocked grabbing attached parts while the rocket is in a hand is gone: parts come off by a deliberate pull along the guide whenever the state machine allows, so a cone can be pulled off a held tube (two-handed).
- **Floor parts.** The respawner already returns any loose part below bench level to the respawn pad at once (now covered by the test). Parts that looked stuck on the floor were attached parts left behind by the bug above; a dropped tube returns to its bench slot.

**Verified**: 31/31 edit-mode, 15/15 play-mode. **Not verified in the headset**: feel of the carried rocket with the sync, two-handed removal.

## 2026-10-11: Parts lagging behind the carried rocket, option 1 (done, awaiting headset check)

Praise's fourth test: parts hold the rocket's shape now but trail behind it while it moves. Research (XRI manual and source, Unity threads on nested kinematic rigidbodies, XRI's socket interactor) pointed at two causes: XRI runs its before-render pass at order 100 while `AttachedPartSync` ran at the default order 0, and the tube was carried with the Kinematic movement type, whose rendered pose comes from rigidbody interpolation outside the sync points. Two changes: the body tube's grab interactable now uses the Instantaneous movement type (transform-driven while held, as XRI's socket forces for attached objects), so children follow through the hierarchy at every phase; and the sync's before-render callback carries `[BeforeRenderOrder(200)]`. A new frame-by-frame test sweeps the hand for 90 frames and measures the nose against the tube after each rendered frame (under 2 mm throughout). The fallback if the headset still shows lag is the per-part swap to rigidbody-free attached parts (one rigidbody per rocket), about a day of work.

**Verified**: 16/16 play-mode. **Not verified in the headset**: lag during a carry.

## 2026-10-11: Free build order and whole-rocket grab (done)

Praise, after the fifth headset test (option 1 fixed the lag): the ordered build felt limiting, and pointing at the base while parts were attached kept picking the parts instead of the rocket.

- `enforceOrder` is now off by default in `AssemblyTuning.asset` (and in the script default): any part attaches to the tube in any order; the cap still needs the motor and the motor cannot come out while the cap is on. The state machine is unchanged and still reports progress for the screen and the blueprint text; the order-dependent play test switches the setting on for itself.
- `RocketAssembly.CanGrab`: a resting rocket is grabbed as a whole (attached parts refuse hover and select), and parts become grabbable only while the rocket is held in the other hand (or otherwise fixed), the way people build in VR. The tests' frozen upright tube counts as held.

**Verified**: 16/16 play-mode, 31/31 edit-mode. **Not verified in the headset**: the two-handed removal feel and whether the free order makes the screen hints read oddly (they still suggest fins first).

## 2026-10-12: Ported onto the team's room (branch `interactions`)

The rocket, its interactions and the working versions of the team's systems now run on the team's own room (`room.unity`, Unity 6000.6.2f1, XRI 3.6.1), in this repository, on `interactions`. `main` is untouched. How it is put together and what to keep stable is in `Docs/PORTING.md`; the modules and their coordinates are in `Docs/ENVIRONMENT.md`.

**What works**
- **Entry scene.** `Assets/VRRocket/Scenes/VRRocket_Main.unity` loads `room.unity` additively at runtime and uses its `Player` rig, adapted in code: no generic buzz, the rocket layer on the hands, no Jump, on the floor. `room.unity` is not edited.
- **Intro.** The room's intro (the teammates' `TransmissionController`: logo, standby, announcement, red alert, dimmed room, video) plays under the start menu. START ends it cleanly and switches the mission screens on. This is SPEC 1's "introductory video".
- **Screens and console.**
  - The launch screen is an overlay on their big TV, with translucent bands that keep text readable over the pad feed.
  - The countdown clock sits on their top-left corner TV, and a mirror of the big screen on their console's centre monitor.
  - Submit, Launch and Abort and the bin sit on their mission control desk, with invisible colliders since the desk has none.
- **Window and launch.** A window in their right wall shows the pad. It uses their wall material with the UVs continued, and is swapped in only while their wall is where it was. The rocket appears on the pad at its own size and grows to full size.
- **Rest of the room.**
  - The kit is on their assembly desk, with the blueprint panel above it and the help board on the left wall.
  - The teleport floor covers the room except the furniture.
  - Their sky and built-in lighting data keep the interior looking as designed.
- **Settings.**
  - Shared settings, each in its own commit: OpenXR controller profiles for Vive, Index and Touch on PC; the OpenXR loader and Meta Quest support for Android; the RocketPart interaction layer; 4x MSAA.
  - Build profiles `VRRocket_Windows` and `VRRocket_Quest` carry the two scenes.
- **Haptics per controller.** Vive wands get stronger, longer pulses, since eccentric motors cannot render 10 to 20 ms soft ticks. Touch Plus keeps the designed table.

**Verified, and how**
- Edit mode 36/36; play mode 26/26, twice in a row in one Editor session.
  - New play tests on the real room: rig, bindings, teleport floor, START ending the intro, the console buttons pressed by the rig's own hand, button and START wiring.
  - Two full games (a failed launch with inspection and a new kit, a clean flight back to the menu) run in `VRRocket_Main`.
- Captures from the spawn, the console, the desk and the pad camera for every screen stage (`Docs/Screenshots`).
- **Builds.**
  - Windows build with `VRRocket_Windows`: 0 errors, and it starts with no script errors (no headset attached).
  - Quest APK with `VRRocket_Quest`: 0 errors, 61 MB.
- **Fixed on the way:**
  - The console's button panel was tilted the wrong way: it faced the monitors, not the player, and had done so since the test project. It now rises toward the monitors.
  - The start menu could snap back to its old place on reload, because its saved anchored position was stale.
- **Not verified in the headset:**
  - haptics on Vive wands;
  - trackpad locomotion;
  - legibility of the big screen at 11.6 m;
  - button and desk heights;
  - frame rate through the flight.

## 2026-10-12: Specs for the team, Claude Code setup, and a by-type folder layout (done)

Work moves to sessions started in this repository, and the teammates need to follow and debug everything.

- **`specs/`.** A main spec (`specs/README.md`) that connects every system, and twelve linked sub-specs: scenes and the room, the game engine, rocket kit and assembly, the launch simulator, screens, the start menu (written for Richard's restyle), console and bin, XR rig and controls, feedback, testing and debugging, builds, project layout. Each says where things are in the Editor and gives the saved values, checked against the code and the scenes.
- **`.claude/CLAUDE.md`.** Instructions for Claude Code sessions in this repository.
- **Folder layout.** `Assets/VRRocket` is gone: VR Rocket's files now sit by type next to the room team's (`Assets/Scenes/VRRocket_Main.unity` and `Dev_Interactions.unity` beside `room.unity`, `Assets/Scripts`, `Assets/Prefabs/{Rocket,Launch,Console,Stubs}`, and `Rocket/`, `Launch/`, `Screens/`, `Environment/` subfolders in `Assets/Models`, `Materials`, `Texture` and `Settings`). Everything was moved inside Unity, so every GUID and reference is kept; the paths written in code (the test scenes, the attach point JSON, the window wall builder) and the build profiles' scene lists were updated. Map in `specs/12-project-layout-and-tools.md` section 2.
- **Found, not changed:** with the free build order, a rocket with the motor and cap fitted counts as complete even with fins, flaps or the nose missing, and missing fins or nose are not in the build report (`specs/03-rocket-assembly.md` section 6).
- **Fixed:** a stale path in `AttachPointGenerator`'s summary comment.
- **Verified:** edit mode 36/36 and play mode 26/26, twice in a row after the move, with no compile errors.
