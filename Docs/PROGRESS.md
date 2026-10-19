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

## 2026-10-12: Richard's room changes merged, two-rocket rule, mini game gate prepared (done)

- **Merged `origin/main`:** framed posters, the light dim feature, the monitor fix, and a shorter room (front wall now at z -8.15). Our overlay and clock follow the moved TVs by themselves. The window wall was rebuilt for their shortened right wall, the floor check re-recorded, and the teleport floor trimmed to the new front wall (`TeleportArea_1` removed). `room.unity` was not edited.
- **Two rockets at most:** submitting a rocket now removes the previous failed (inspection) rocket. Covered by `Build_Carry_Submit_FailedLaunch_NewBuild`.
- **Submission mini game (Yi-Xuan):** state and `MiniGameSolved()` written but commented out, with `TODO(Yi-Xuan)` in `GameFlowController.cs` and `SubmissionBin.cs`.
- **Action items** from the build-guidance discussion: `Docs/ACTION_ITEMS.md`.
- **Verified:** edit mode 36/36, play mode 26/26.

## 2026-10-12: Build-step monitor beside the blueprint panel (done)

As planned with Richard: the blueprint panel gives the step in text, and a new monitor beside it (`BuildStepMonitor`) shows the rocket as it should look after that step. Five pictures of `AssembledRocket_Static` at each stage are rendered by VR Rocket > Capture Build Step Images into `Assets/Texture/BuildSteps`; VR Rocket > Set Up Build Step Monitor builds and wires the monitor. `BlueprintDisplay` swaps the picture with the build state.

**Verified:** edit mode 36/36, play mode 26/26 (`GameFlowTests` now checks the picture follows the build). **Not verified in the headset:** legibility of the pictures from the desk.

## 2026-10-12: Player height fixed; build-step monitor tested (done)

- **Height:** in the headset the player stood far too tall and had to bend to the desks. The room's rig asks for tracking origin "Not Specified", which on Quest can become Device mode, where the rig's 1.36 m camera offset is added on top of the real head height. `XRRigAdapter` now sets Floor tracking and a zero camera offset at runtime (`room.unity` untouched). `Rig_Is_Adapted` checks it.
- **Build-step monitor:** new edit-mode `BlueprintDisplayTests` walk every build stage, including taking a part off, and check the text and the picture really on the screen.
- **Verified:** edit mode 38/38 and play mode 26/26, twice in a row. **Headset check:** the player's height on Quest 3.

## 2026-10-12: Headset feedback: posters, player height, locomotion (done)

From Praise's first headset session on the merged room:
- **Posters:** Richard's three portraits on the back wall sat behind the blueprint panel and the picture monitor. Both are now small monitors standing on the back of the desk, between the portraits' name plaques (VR Rocket > Place Build Screens On Desk).
- **Locomotion was off:** the room's `Player` was saved with its `Locomotion` object inactive. `XRRigAdapter` switches it back on (move, turn, teleport, gravity).
- **Player too high:** the room set the rig's character controller to 3 m around a 0.76 m centre, so the capsule reached 0.74 m under the feet. `XRRigAdapter` resets such a capsule to 1.36 m on the feet, snaps the rig to the floor again once tracking has started, keeps floor tracking, and logs the rig's height and tracking mode to the device log 3 s after start.
- `room.unity` was not edited; Richard may want to undo the locomotion and capsule changes in his scene too.
- **Verified:** edit mode 38/38, play mode 26/26 (`Rig_Is_Adapted` checks locomotion on, the capsule, and the rig still on the floor after 90 frames of gravity). **Headset:** height and locomotion to confirm.

## 2026-10-13: VRRocket_Final, the combined scene (done)

Our game and Richard's updated room in one scene, `Assets/Scenes/VRRocket_Final.unity`, now the only scene in both build profiles. `VRRocket_Main` and `room.unity` are kept as they were. Details in [spec 13](../specs/13-final-scene.md).
- **Room:** Richard's new layout; the help board and the old front window are gone; one large window on the alcove's left wall with the pad straight out from it; the rocket poster moved beside the moon poster; teleport areas fitted to the new room.
- **Menu:** START WITH INTRO plays Richard's intro on demand, then building starts; SKIP INTRO goes straight to building. A curtain closes over the window while the intro dims the room and opens again with the lights.
- **Desk:** BACK and NEXT buttons browse the five build steps on the blueprint panel and the picture monitor; any change to the build returns them to the live step.
- **Console:** a wide monitor over the three centre screens carries the console mirror (pad view, title, clock, full status text).
- **Abort:** ABORT during pre-launch or the countdown stops the engine, clears the pad and returns the same rocket editable; red ABORT panels flash over the four side TVs with a siren for 4 s and the big TV says LAUNCH ABORTED.
- **Countdown clock:** now covers its whole TV.
- **Fixed on the way:** the intro bridge looked for the intro while the scene was still loading and found nothing (so the intro, curtain and abort screens did not work in play); the test runner had silently not been running (the Editor had stopped answering after a restart).
- **Tests retired with `VRRocket_Main`:** `RoomIntegrationTests` is gone (its rig and display checks moved to `FinalSceneTests`); `GameFlowTests` now plays its games in `VRRocket_Final`. All tests and work are based on `VRRocket_Final` from here on.
- **Verified:** edit mode 39/39 and play mode 31/31, full suites (13 October 2026).
- **Built and installed** on the Quest 3 (`VRRocket_Quest` profile, final scene only).

## 2026-10-13: Desk buttons in reach, console nameplate (done)

From Praise's headset session on `VRRocket_Final`:
- **BACK and NEXT** moved to the player's edge of the desk (24 cm from the front edge, right of the kit); before, they sat a metre in and were hard to reach.
- **Console nameplate:** the small black plate on top of the console is now a LAUNCH STATION label screen.
- **Verified:** edit mode 39/39 and play mode 31/31 (`FinalSceneTests` now also checks the buttons are within 30 cm of the desk edge and the nameplate text). Built and installed on the Quest 3.

## 2026-10-13: Console side monitors, posters rearranged (done)

- **Side monitors:** two monitors in the console monitor's style over the console's angled side screens. Left, GO / NO-GO: a readiness poll of fitted parts and the pad with a verdict line (never revealing flap orientation or the cap lock). Right, FLIGHT DATA: range status and mission log, the countdown, live altitude and speed, then the flight result (`ConsoleStatusScreens`). The console's own five screens are switched off, all covered by our monitors.
- **Posters:** the rocket-launch poster moved to the front room's right wall between the corner TVs and the angled wall; the moon-landing poster is centred on the alcove wall beside the console.
- **Verified:** edit mode 39/39 and play mode 32/32 (new `Side_Monitors_Poll_The_Build_And_Show_Range_Status`; the full games check FLIGHT DATA in flight and the result after a failure).

## 2026-10-13: Yi-Xuan's cable puzzle merged and built into the console (done)

Merged `test-hand-models` (Yi-Xuan, up to be9eb60) into `interactions`. Details in [spec 13](../specs/13-final-scene.md), section 6.
- **In the console:** her `PuzzleBox` sits in a well cut into the control panel between SUBMIT and LAUNCH, under a tread-plate hatch that slides into a slot when pulled by its grip (`SlidingPanel`; click and controller pulse at each end; the puzzle cannot be touched through the closed hatch).
- **Looks like wiring:** Schuko plugs (icvetlan) and sockets (Artik) in each pair's colour, round rubber cables (`CableTube` over her Verlet cables), dark console walls. Assets downloaded from Sketchfab (CC BY) and ambientCG (CC0), reduced in Blender; credited in ASSET_CREDITS.
- **Console offline until wired:** `GameFlowController` Require Mini Game is on in `VRRocket_Final`; SUBMIT refuses until her `CablePairManager.onAllConnected` calls `MiniGameSolved()`; the GO / NO-GO monitor shows WIRING and CONSOLE OFFLINE.
- **Her rig:** her hand-model rig and physical-room calibration stay in the scene, switched off; with them on the player floated a metre up and could not grab parts. Her be9eb60 had also deleted our `RoomLink` (intro bridge) and `Player`; both were restored and rewired.
- **Tests:** the play-mode tests now drive the hand of the rig in use (they had picked her switched-off rig's hand). New: hatch opened and closed by the hand and locking the puzzle, console offline until the wiring is connected. Edit mode 39/39, play mode 34/34.

## 2026-10-13: The wiring puzzle finished; console fault and online states; abort screens inside a bezel (done)

- **Puzzle finished** (`CablePuzzle.ConsoleWiring`, next to Yi-Xuan's scripts, hers unchanged): a click and a controller pulse for every plug in or out; a row of five status lamps on the console surface (in each wire's colour, green when connected, visible with the hatch shut); a lock click and locked wiring when all pairs connect; a reset for a new play that unplugs, puts the ends back and shuffles the sockets' rows.
- **Insulation colours** (red, blue, yellow, black, white) instead of the prototype's rainbow; the cables keep the rubber's surface relief.
- **Broken and fixed:** SUBMIT on a broken console buzzes and shows CONSOLE FAULT with what to do on the big TV and the console monitor; fixing it shows CONSOLE ONLINE and brings the three NO SIGNAL side TVs alive (NASA logo, SYSTEMS ONLINE, the rocket on its pad; `ConsoleLiveScreens`).
- **Abort screens** now show inside a gloss-black bezel on each TV instead of covering the whole TV.
- **Verified:** edit mode 39/39, play mode 35/35 (new: the wiring loop with lamps, lock and reset; the fault and online messages with the live TVs; the abort picture inside the bezel).

## 2026-10-13: Side TVs aligned; the motor is a battery (done)

- **Side TVs:** each top TV sits exactly over its bottom TV, all four the same size, both pairs at the same height; the countdown clock, ABORT and live screens refitted.
- **Battery:** the kit's motor is an AA battery (MasterYogurt, CC BY), 24 x 70 mm, + end up; everything the player reads says battery (build steps, hints, GO / NO-GO, BATTERY RETENTION LOSS, "Battery cap" in failed parts). Build-step pictures re-rendered. Code names unchanged.
- **Verified:** edit mode 40/40, play mode 37/37 (new: TVs stacked square and the same size; the kit motor shows the battery; failed parts read as the player sees them).

## 2026-10-14: Battery slot and cap twist made reliable (done)

From Praise's headset session: the battery popped out through the tube's wall when not slotted perfectly, and the cap twisted off the rocket's axis, ended up crooked, and was hard to grab.
- **Causes found:** parts never collide with each other, so a battery not yet caught (or knocked off its track by sideways hand drift mid-insert, or let go uncaught) passed through the tube; the cap's twist was computed against the rocket's orientation at the moment the cap was seated, so once the rocket was turned (and twisting needs it held) the cap turned about the wrong axis and was left crooked; the cap was moved through physics a step behind the rocket; a hand at the cap was given the whole rocket; the twist read the hand orbiting the axis rather than the wrist turning.
- **Battery:** caught from 11 cm and up to 70 degrees off axis; while any of it is in the tube sideways drift never knocks it off the track; let go within 15 cm, the right way up, it slides home; the wrong way up it is held out of the tube with a buzz.
- **Cap:** everything in the seat's own frame (like the XRI example knob), so it always turns about the tube's current axis and is left square; the wrist's twist about the axis (swing-twist decomposition) at 1.5x; placed directly while twisted; a hand at an unlocked cap takes the cap, not the rocket, and can twist it on a resting rocket; pull-off needs a deliberate 6 cm axial pull with the rocket steady; let go at 75 percent and it finishes the lock.
- **Verified:** edit mode 40/40, play mode 44/44 (new `BatteryInsertTests`; cap tests for a rocket turned after seating, auto-finish, and grab priority).

## 2026-10-14: Launch fire, smoke and failures reworked (done)

From Praise: the fire and smoke started halfway up the rocket, so it looked on fire rather than lifted by its flame; and the failures needed checking. Details in [spec 04](../specs/04-launch-simulator.md).
- **Cause:** the flame's streaks (9 to 15 m long) are drawn centred on the nozzle, and the nozzle sat 8 cm above the pad, so only the upper half of each streak, running up the rocket's side, could be seen; the pad smoke shot upward beside the rocket and only started at liftoff.
- **Launch mount:** the rocket stands on a 2 m steel deck on four legs over a flame deflector; the gantry steps back and its arm no longer reaches over the nose.
- **Fire and smoke:** the flame's streaks start at the nozzle and reach down (the emitter sits half a streak below it), with a glow at the nozzle; at T-3 the flame lights small and builds while the pad smoke rolls out along the ground and fire sprays out under the deck; at liftoff a surge of smoke and a trail left in the air behind the climb.
- **Failures by number of wrong flaps:** one veers off course, arcs over and crashes about 80 m out, in the window's view, with a ground explosion (shockwave, dirt, scorch, fire, smoke column); two or three corkscrew and break up about 16 m up. Both throw the fins, flaps, nose and cap out as debris that falls trailing smoke. The result screens add how it failed. The unlocked cap still blows the battery out on the pad, now with a puff of smoke, down off the deck.
- **Flight model:** thrust now pushes along the rocket's body with more drag flying sideways; a crash is detected when the nose meets the ground. Fixed on the way: the rocket used to lean 90 degrees away from the direction it drifted.
- **Explosion:** rebuilt with a solid fireball (additive fire washed out against the sky), a flash with a short light, flames, embers and rising smoke.
- **Verified:** edit mode 43/43, play mode 49/49 (new `LaunchEffectsTests`: no flame streak reaches above the nozzle, smoke rolls out along the ground, the trail and the pad fire, the crash on the ground in the window's view, the break-up low in the air and a reset clearing it, abort putting everything out; flight-model tests for each failure). Frame rate during the launch is still to be checked in the headset.

## 2026-10-14: main and Yi-Xuan's branch merged; menu words editable per state, menu restyled; fixed sockets (done)

- **Merged** `main` (Richard's menu update: NASA colours, new copy, BRIEF ME and BUILD ROCKET) and `test-hand-models` (Yi-Xuan: the sockets fixed to the well's floor, one cable per pair, new insulation colours, our intro link renamed `IntroController` and her calibration `RoomLink`). Her branch also switched to her hand-model rig; the merge keeps our rig on and hers off, as before.
- **Menu words editable:** every word of the menu, for each state (First Play, After Success), is edited on `MenuCanvas`'s Menu Panel in the Inspector, with a preview switch; nothing in code overrides them any more (they were hard-coded in `GameFlowController`). Texts auto-size so new words fit. Details in [spec 06](../specs/06-menu.md).
- **Menu restyled in Richard's direction:** his NASA frame (`nasa_button.png`) turned into a transparent nine-sliced button sprite, a glowing blue rule with red-and-white stripe ends, a faint HUD grid, NASA red title (#FC3D21), white button labels, dimmed buttons that glow under the ray and flash warm when pressed.
- **Sockets:** they sat 1.5 cm into the well's floor, so only their faces showed; they now stand on it.
- **Verified:** edit mode 43/43, play mode 50/50 (new: the menu's words per state and edits showing; the after-success menu; the fixed sockets; the console-button test now presses with the menu gone, as in play, since Richard's menu hangs over the console).

## 2026-10-14: The battery goes in only once it is in, and always seats straight (done)

From Praise's headset session: the battery slid in on its own as it approached the base, twisting and tilting on the way when it was not lined up, and its glow ring lit the wrong end.
- **Causes found** (checked with a throwaway test): it was caught from an 11 cm zone round its seat, from any side (8 cm below and 6 cm aside was enough), and up to 70 degrees over, then snapped straight in mid-air; letting go within 15 cm slid it in even with its + end 7 cm outside the tube; its straight angle was worked out once, when caught, so a rocket turned in the other hand left it 30 degrees crooked and through the wall, and it seated crooked; leaning past 70 degrees it was never caught and could fall through the tube; the glow ring sat on the - end, the end that goes in last.
- **Now:** caught only once its + end is in the tube's base (a little or a long way, up to 60 degrees over); it straightens on the axis without moving in or out; drifting or leaning the hand or turning the rocket never takes it off the axis; let go while in, it always slides home straight; let go outside, it drops; pulled back out, it comes free. The wrong way up it is still kept out with a buzz. The glow ring is on the + end. The straight angle is kept relative to the seat for every part (the nose cone had the same hidden fault).
- **Tuning:** Motor Capture Radius, Motor Orientation Tolerance and Motor Release Snap Radius are replaced by Motor Entry Radius (2 cm), Motor Entry Tilt (60 degrees) and Motor Exit Margin (1 cm).
- **Verified:** edit mode 43/43, play mode 55/55 (`BatteryInsertTests` rewritten: 9 tests). Feel in the headset still to check.

## 2026-10-14: Yi-Xuan's socket holes merged (done)

- **Merged** `main` (Yi-Xuan, f3d4bb9 and c1b4133): the well's floor is now pieces round a hole at each socket row, raised about 1.3 cm, so the fixed sockets sit down in the floor with their faces level with it; the puzzle's volume moved to match.
- **Kept on:** her save had the menu canvas and the wiring hatch switched off (left over from working on the puzzle); both are switched on again, as Praise asked.
- **Verified:** edit mode 43/43, play mode 55/55.

## 2026-10-14: A held fin or flap lights only the slot it would go into (done)

From Praise: bringing a flap to the tube lit almost the opposite slot.
- **Cause found** (checked with a throwaway test and pictures): every slot that fits the part within 12 cm glowed (a flap near the tube lit five or six flap slots, the nearest barely brighter: 57% against 30 to 44%), and the nearest slot's strip sits right behind the edge of the flap coming in, so what the player saw lit were the neighbouring slots round the side of the tube, the other height.
- **Now:** only the nearest slot that fits the part glows, the one it would go into, brighter as it nears and full on its track; the light moves to another slot only once that one is 1 cm closer (Glow Switch Margin), so it does not flicker between two; the slot strips are 1.6 cm wide (were 1 cm) so they show either side of the part's edge. Fins the same.
- **Verified:** edit mode 43/43, play mode 60/60 (new `SlotGlowTests`, 5 tests).

## 2026-10-19: Every refused press answered; the bin has a status screen; unfinished rockets bounce back; the launch rocket is the one built (done)

From Praise: only SUBMIT on a dead console said anything; LAUNCH and ABORT got no response, the bin gave no sign of what was in it, and the failed rocket came back beside the bin.
- **Audit** (in the session): 15 cases answered with nothing or a bare buzz (SUBMIT with an empty bin, with the rocket still in the hand, after a submission; LAUNCH and ABORT on a dead console, with nothing on the pad, during the countdown or flight; the cap before the battery; a plug at the wrong socket...). Also found: the failed rocket in the bin could be launched in place of the new build; a rocket with only the battery and cap counted as complete; the launch rocket was always a correctly built copy.
- **Refusals:** each one now gives a buzz, a double vibration on the hand that pressed (`hapticRefused`) and the reason with what to do, on the big screen and console monitor for 4 s and on the monitor beside the bin. On the menu and in the intro, the buzz and vibration only.
- **The bin's screen:** the console's right monitor shows the bin: EMPTY, LET GO, READY, PARTS MISSING with the list, FAILED ROCKET, CHECKING, SUBMITTED; then the countdown, flight data and result during a launch. The weather, wind and range lines are gone. A soft click when a rocket goes in or comes out.
- **Complete means all nine parts.** An unfinished rocket can be submitted: it sinks, PARTS MISSING lists what is missing, and it comes back up on the workbench next to the remaining parts, editable. Not a flight.
- **Failed rockets** come back on the side desk under the moon-landing painting, lying along it, examinable but not editable.
- **The launch rocket is the one built:** flaps on the slots they were put on, upside down where they went on upside down, missing parts left off, an unlocked cap sitting out of the base.
- **Verified:** edit mode 44/44, play mode 69/69 (new `ConsoleFeedbackTests`, 9 tests; checked the side desk and the screens in pictures). Feel in the headset still to check.

## 2026-10-19: Main merged (Yi-Xuan's hand rig and plug respawn); the team's playtest list worked through (done)

- **Merged main:** Yi-Xuan's hand-model rig with room calibration is now the rig in use (room-scale, no stick locomotion); her plug respawn replaces the guard colliders, so a plug that falls out of the box comes back; the puzzle box is a prefab; rocket parts carry her GrabPose and gentler throw settings; Richard's intro video without the "leave a like" end. Her changes are kept as they are, cable thickness included. Tests updated for her rig (the hand-part offset is measured once a grab settles; room-scale has no locomotion).
- **Fixed:** "Particle Velocity curves must all be in the same mode" errors from the explosion and pad smoke (one axis had a range, the others fixed numbers).
- **Cap twist cue:** an unlocked seated cap shows an orange arrow circling it the way it locks, and its glow pulses, until it locks. The bin monitor's READY screen asks "Cap twisted until it clicks?".
- **Fin and flap glow** is a band round the root edge, visible from both faces at any angle; the glow material is double-sided.
- **Verified:** edit mode 44/44, play mode 72/72 (new: twist arrow, glow band, plug comes back). Feel in the headset still to check.

## 2026-10-19: The cap twist hint shows how, then how far, then the lock (done)

From Praise: the twist arrow showed the same thing the whole time; how long should it last, and how is this best done?
- **Research** (Ultraleap and Leap Motion XR guidelines, Meta's hands design and onboarding guidance, Road to VR on Half-Life: Alyx, Roblox onboarding techniques): give a cue before the action and feedback during it; show state through light (Alyx's half-bright half-full magazine); give hints at the moment of need, after a short hesitation, resetting on input; make the cue as specific as the motion (a turning ghost for a turn).
- **Now:** before (after 1.5 s left alone) an arrow and a see-through ghost cap turning the locking way, with a pulsing glow; during the twist a ring of six detent segments lights with each click; let go early, the ring keeps the progress and the hint returns; on the lock the ring flashes green and everything goes. The ghost uses a new transparent material referenced by the cap prefab, so it is in the Quest build.
- **Verified:** edit mode 44/44, play mode 72/72 (the cap hint test now walks all three stages); each stage checked in pictures. Feel in the headset still to check.

## 2026-10-19: Tail fins get the wing flaps' notched tab (done)

From Yi-Xuan: the tail fins should have the same attaching-edge affordance as the wing flaps.
- **Before:** the wing flap's root has a tab 2 mm proud along its middle 60 mm, so both ends read as notches; the tail fin had a plain lip along its whole root. Yi-Xuan's test model (`testTail.fbx`, the fin plus a block) was not used anywhere and kept the full lip.
- **Now:** `TailFin.fbx` is rebuilt in Blender as a clean fin with the same outline and thickness and a tab 2 mm proud along the middle 45 mm of its 58.6 mm root (the flaps' proportion), notched at both ends. Same object and mesh names, so the kit fins and the launch rocket's fins all use it; the colliders, slots and glow band are unchanged. The old mesh's leftover boolean slivers are gone (177 vertices down to 18). `testTail.fbx` is deleted (agreed with Praise).
- **Verified:** edit mode 44/44, play mode 72/72; the new fin checked next to the wing flap in pictures. The build-step pictures still show the old fin (the difference is a 2 mm notch).

## 2026-10-19: The launch rocket's fins, its battery after a blow-out, and matching the build (done)

From Praise: the tail fins did not appear on the launch rocket; on the second launch the battery was huge, but only when the first launch had the cap unscrewed; the launch rocket should look like the rocket put in the bin.
- **Tail fins:** they were there but 2 cm long. The notched `TailFin.fbx` (earlier today) was exported with Blender's default scaling, which put the unit conversion (100) on the node and left the mesh 100 times too small. The kit fins use the FBX's own prefab, so they looked right; the launch rocket uses the mesh directly. Re-exported with Apply Scalings = FBX All: the mesh is its real size and the node scale is 1, like the original fin and the flaps. How to export is now in spec 03.
- **The battery:** when it blows out, it is taken off the 36 times rocket keeping its world size, so its own scale became 36. `ResetPad` put it back in the base but kept that scale, so the next launch showed it 36 × 36 times its size, swallowing the rocket. `ResetPad` now restores the battery's and cap's whole local pose and scale, and every vehicle part gets its own scale back when the vehicle is built.
- **Matching the build** was already in (flaps on the slots they went on, upside down where they went on upside down, missing parts left off, an unlocked cap out of the base); the fin size was what hid it for fins. Now checked end to end.
- **Verified:** new tests: every launch rocket part is 36 times its kit part; a blown-out battery and cap come back at their own size (on the old code the battery came back 100.8 m long instead of 2.8 m); two games back to back, an unscrewed cap then a build with the fins one slot round and a flap upside down in a Low slot, the launch rocket matching each build. Edit mode 44/44, play mode 75/75. The Quest build is redone with this fix.

## 2026-10-19: Main merged again (Richard's build-step hints, Yi-Xuan's gloves and smaller failed-rocket zone) (done)

- **From main:** Richard's new wording for the five build-step hints on the blueprint monitor (spec 05 updated to match); Yi-Xuan's rubber-glove material on the hand models; the failed-rocket desk area made smaller to fit the room (the spot moved with it and still sits on the right side desk, the rocket lying along the zone with room to spare); a font asset reset. Merged cleanly, kept as they made them.
- **Rigs:** unchanged. Yi-Xuan's rig is on and ours is off in the scene; ours is only switched on for Praise's own Quest builds.
- **Verified:** edit mode 44/44, play mode 75/75 (the failed rocket still lands and stays on the side desk).

## 2026-10-19: Red wing flaps (done)

From Praise, in line with Richard's new step hints ("orange fins" at the base, "red fins" in the upper slots): the wing flaps are now red so the two kinds of fin read apart.
- **The red** is Yi-Xuan's test fin's (`testTail.fbx` on her branch, its material imported by Unity as #F31E1E), on a new `Rocket_Red.mat` that is otherwise our orange (same smoothness, no metal).
- **Everywhere a rocket is shown:** the kit's flaps (the flap model's material now maps to `Rocket_Red`), the launch rocket's three flaps, and the five build-step pictures (re-rendered; they now also show the notched fins). The pad camera feed, the explosion debris and the failed rocket on the side desk show the same parts, so they follow. Tail fins and the nose are unchanged.
- **Verified:** new test, flaps red and fins orange on the kit and the launch rocket; pictures of the launch rocket on the pad, the kit on the tray and the new step pictures checked. Edit mode 44/44, play mode 76/76.

## 2026-10-19: Switching hands with the rocket (done)

From Praise: someone holding a nearly built rocket who wants it in the other hand grabs with that hand, but lands on a flap (the hand models are bulky and the white tube is mostly hidden), so they pull a part instead of taking the rocket. Taking parts off must not get harder.
- **Now:** grip any part of the rocket with the other hand, then let go with the hand holding the rocket. The rocket stays in the second hand and the part goes back on its seat (half pulled out, it slides home; a half-twisted cap keeps its twist). Pulling parts off is untouched: it needs the rocket held, and the hand-over only happens once it is not. A part already pulled off is just a part in the hand, and letting go of the rocket drops it as before.
- **Found on the way:** on Yi-Xuan's rig the hand's attach point carries the gripped part's GrabPose offset (a flap's is 2 cm and a quarter turn), so after the hand-over the rocket jumped 4.7 cm and turned in the hand. A small grab transformer on the tube now keeps the rocket's place relative to the hand itself after a hand-over. Her rig code is unchanged.
- **Verified:** new `SwitchHandsTests`, run in the test scene and in `VRRocket_Final` on Yi-Xuan's rig (the tests now drive a real left hand): a seated flap, a half-pulled flap, the cap mid-twist, and a flap pulled off (rocket drops). With the hand-over switched off three of them fail; without the hold the 4.7 cm jump fails the final-scene test. Edit mode 44/44, play mode 84/84. Feel in the headset still to check.
