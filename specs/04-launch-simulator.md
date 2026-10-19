# 04 Launch simulator

The launch outside the window: the full-size rocket on its launch mount, the small physics model that flies it, the outcomes, the fire, smoke and explosions, the sounds and the pad camera. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Final` → `LaunchSite` (at (48, 0.30, 2), turned 180°, straight out from the alcove window) and `Outside` (ground, pad, launch mount, gantry) |
| **Scripts** | `Assets/Scripts/Runtime/LaunchSequence.cs` (the show) and `RocketFlightModel.cs` (the physics, plain C#) |
| **Prefabs** | `Assets/Prefabs/Launch/LaunchVehicle.prefab` (the 36× rocket with its engine effects), `Explosion.prefab` |
| **Assets** | `Assets/Materials/Launch/` (`Mat_Particle_*`, `Mat_Concrete`), `Assets/Texture/Particles/` (Kenney, CC0), `Assets/Audio/Kenney/`, `Assets/Texture/Screens/PadCameraFeed.renderTexture`, `Assets/Materials/Screens/Mat_PadFeed.mat` |
| **Tests** | `RocketFlightModelTests` (edit mode: each outcome, the mass, the tilt direction), `BuildReportTests` (the wrong-flap count), `LaunchEffectsTests` (play mode: fire, smoke, crash, break-up, abort), `GameFlowTests` (a failed and a successful flight in the real game) |

## 1. Two halves

- **`RocketFlightModel` is the physics.** Plain C# with no Unity objects: you give it an outcome and the number of wrong flaps, call `Step(dt)` every frame, and read the position, speed, tilt and events (lifted off, tumbled, exploded, crashed, motor ejected). Because it has no scene dependencies, it is unit tested.
- **`LaunchSequence` is the show.** A component on `LaunchSite`. It owns a `RocketFlightModel`, moves the full-size vehicle by the model's numbers, and plays the fire, smoke, sound, explosion, debris and pad camera.

The game engine ([spec 02](02-game-engine.md)) tells `LaunchSequence` when to do what. The outcome and the wrong-flap count come from the build report ([spec 03](03-rocket-assembly.md)).

## 2. In the Hierarchy

```
LaunchSite            (48, 0.30, 2), yaw 180          LaunchSequence
├─ PadOrigin          (48, 2.43, 2): the spot on the mount's deck the rocket stands on; the flight is measured from here
├─ LaunchVehicle      LaunchVehicle.prefab: the 36x rocket (hidden until a rocket is submitted)
│  ├─ Model           the assembled rocket, scaled 36x (about 13 m tall)
│  ├─ EngineFlame     flame core at the nozzle
│  │  └─ NozzleGlow   bright flickering glow where the flame starts
│  ├─ EngineSparks    sparks falling from the nozzle
│  └─ ExhaustTrail    smoke left in the air behind the climb
├─ PadCamera          camera that films the launch for the screens (off until needed)
├─ PadSmoke           smoke rolling out along the ground from under the mount
├─ PadFire            fire spraying out sideways under the deck
├─ PadSteam           steam venting from the side of the deck
├─ DebrisTrail        (switched off) smoke-trail template copied onto each piece of debris
├─ EngineAudio        AudioSource: engine loop
└─ PadOneShot         AudioSource: ignition and explosion sounds (moves to an explosion away from the pad)

Outside
├─ Ground             300 x 300 m, just under the room's floor level
├─ LaunchPad          9 m concrete disc, top at 0.45 m
├─ LaunchMount        Leg_1..4 (concrete), Deck_* (a 5 m steel deck, top at 2.45 m, round a 1.1 m flame hole),
│                     Deflector_PlusZ / _MinusZ (a steel ridge under the hole that splits the flame out to both sides)
├─ GantryTower        18.5 m tower, standing back clear of the deck
└─ GantryArm          stops short of the rocket's body
```

**Why a mount:** the rocket used to stand on the pad with its nozzle 8 cm above it, so the flame had nowhere to go but into the ground and only the part of each flame streak that pointed up the rocket was visible: the rocket looked on fire. On the deck the flame has 2 m of open space below the nozzle, and the smoke and fire roll out from under the deck.

## 3. `LaunchSequence` in the Inspector

| Section | Field | Value |
|---|---|---|
| Pad | Pad Origin | `LaunchSite/PadOrigin` |
| | Vehicle | `LaunchSite/LaunchVehicle` |
| | Vehicle Motor, Vehicle Cap | the battery and cap meshes inside the vehicle (they fly out on a retention-loss failure) |
| | Ground Level | 0: world height of the ground round the mount, where an off-course rocket crashes |
| Effects | Flame, Nozzle Glow, Sparks, Exhaust Trail | `EngineFlame`, `EngineFlame/NozzleGlow`, `EngineSparks`, `ExhaustTrail` in the vehicle |
| | Smoke, Pad Fire, Pad Steam | `PadSmoke`, `PadFire`, `PadSteam` |
| | Explosion Prefab | `Assets/Prefabs/Launch/Explosion.prefab` |
| | Debris Trail | `LaunchSite/DebrisTrail` |
| | Debris Parts | the vehicle's 3 fin, 3 flap, nose cone and cap meshes (copied and thrown out when it explodes) |
| Staging | Ignition Level, Pre Liftoff Level | 0.3, 0.75: flame strength when it lights at T-3, and what it builds to by T-0 (1 is full thrust) |
| | Ignition Ramp Seconds | 2.6 |
| | Pad Smoke Ignition Rate, Pad Smoke Liftoff Rate | 35, 90 particles per second |
| | Pad Smoke Fade Altitude | 60 m: the pad smoke tapers to nothing as the rocket climbs to this height |
| | Pad Fire Rate, Pad Fire Fade Altitude | 200 per second at full thrust; dies away by 14 m |
| | After Explosion Seconds | 2.5: the flight lingers this long after an explosion before the result screen |
| | Crash Lift, Crash Scale | 4.5 m, 1.5: a crash's fireball sits 4.5 m above the impact (its ground effects stay on the ground) and is drawn 1.5 times bigger, because it is further from the window |
| Audio | Engine Loop | `EngineAudio`: `spaceEngineLow_000.ogg`, looping, 3D (full volume within 15 m, fades out to 400 m) |
| | One Shot | `PadOneShot`: 3D, full volume within 20 m |
| | Ignition Clip | `thrusterFire_000.ogg` |
| | Explosion Clip, Explosion Low Clip | `explosionCrunch_000.ogg`, `lowFrequency_explosion_000.ogg` |
| Pad camera | Pad Camera | `LaunchSite/PadCamera` |
| | Pad Camera Offset | (55, 6, −35): where the camera stands, relative to the pad |
| Flight tuning | Success Altitude | 1200 m: a good flight ends when it gets this high |
| | Visual Scale | 1: multiplies the model's metres when moving the vehicle |
| | Arrival Scale | 0.0278 (1/36): the size the vehicle appears at before it grows |
| | Off Course Azimuth | 70°: the world direction an off-course rocket veers to (0 = +Z, 90 = +X, away from the window). 70 lands the crash in the window's view, a little left of centre. |

## 4. The sequence, step by step

The game engine calls these methods in this order:

| Method | When (phase) | What it does |
|---|---|---|
| `ResetPad()` | entering Menu or Assembly | Stops any flight or growth, stops and clears every effect, removes an explosion and its debris, puts the battery, cap and flame back inside the vehicle, puts the vehicle back on `PadOrigin` at full size and **hides** it, switches the pad camera off, resets the model. |
| `PlaceVehicleOnPad(2.5)` | PreLaunch (rocket submitted) | Shows the vehicle at 1/36 of its size (the size of the model the player built) and grows it to full size over 2.5 s with a smooth ease. Aims and switches on the pad camera. |
| `Ignite()` | Countdown, at T-3 | The flame lights small at the nozzle (Ignition Level) and builds to Pre Liftoff Level over 2.6 s; the pad smoke starts and builds; the pad fire sprays out under the deck; sparks and steam start; the ignition sound plays and the engine loop starts at 60% volume, rising to 85%. The rocket stays on the deck. |
| `AbortCountdown()` | ABORT | Stops every effect, the build-up and the engine sound. (The engine then calls `ResetPad` when it enters Assembly.) |
| `Liftoff(outcome, onFinished, wrongFlaps)` | T-0 | Full flame, a surge of pad smoke, the exhaust trail starts, engine at full volume; then the flight (below). Calls `onFinished` when it is over. The two-argument `Liftoff(outcome, onFinished)` still works and treats an unstable flight as a break-up. |

**During the flight, every frame:**

1. `model.Step(dt)`, with the frame time capped at 0.1 s so a hitch cannot throw the rocket forward.
2. The vehicle is placed at `PadOrigin + (x, altitude, z)` and leaned over by the model's tilt, towards the model's tilt direction (the same direction the thrust pushes it).
3. The pad smoke's rate falls as the rocket climbs (none by 60 m), and the pad fire's (none by 14 m, or at burnout).
4. On a retention-loss failure, the battery (with the cap) is animated out of the base, off the deck and down to the ground (section 6).
5. At burnout (7 s) the flame, glow, sparks, trail and engine sound stop (except on a retention-loss failure, where the flame follows the ejected battery).
6. If the model exploded: the `Explosion` prefab is spawned where it happened (with its ground effects on a crash) and deleted after 12 s; the fins, flaps, nose cone and cap are copied as **debris** with physics, thrown out from the explosion and left to fall trailing smoke (deleted after 14 s); both explosion sounds play from where it happened; the vehicle is hidden. The flight then lingers 2.5 s before it ends.
7. The pad camera is re-aimed (section 8).

The flight ends when `model.IsFinished(1200)` is true (and, after an explosion, the 2.5 s have passed). Then the smoke and steam stop and `onFinished` is called.

## 5. The physics model

`RocketFlightModel` works in metres, seconds, kilograms and newtons, for the full-size vehicle (the model rocket times 36, about 13 m tall). Horizontal positions are world X and Z.

**Its numbers** are public fields with these defaults in `RocketFlightModel.cs`. They are not shown in the Inspector: change them in the code.

| Field | Default | Meaning |
|---|---|---|
| `dryMass` | 2000 kg | the rocket without fuel |
| `propellantMass` | 3000 kg | fuel at liftoff (burns at a constant rate) |
| `burnTime` | 7 s | how long the engine burns |
| `thrust` | 110 000 N | engine force, along the rocket's body. Thrust to weight at liftoff: 110 000 / (5000 × 9.81) ≈ 2.2 |
| `gravity` | 9.81 m/s² | |
| `dragK` | 0.35 | air drag flying nose first: drag force = 0.35 × speed² newtons |
| `broadsideDrag` | 10 | extra drag factor when flying sideways (body across the flight path) |
| `length` | 13 m | nose to base, for touching the ground nose first |
| `groundBelowPad` | set by `LaunchSequence` | how far the ground is below the pad origin (2.43 m: the mount) |
| `wrongFlaps` | from the build report | flaps in the wrong place or upside down |
| `offCourseOnset`, `pitchRate`, `pitchRamp`, `maxPitch` | 0.9 s, 110°/s, 0.8 s, 150° | off course: when it starts to pitch over, how fast (reached after the ramp), and how far (past 90° the nose points at the ground) |
| `offCourseAzimuth` | set from Off Course Azimuth | off course: which way |
| `instabilityOnset` | 0.2 s | break-up: when the wobble starts |
| `instabilityGrowth` | 2.4 per second | how fast the wobble grows (exponential) |
| `instabilityAmplitude` | 6° | wobble size when it starts |
| `wobbleFrequency` | 6 rad/s | how fast the tilt pulses |
| `corkscrewRate` | 220°/s | how fast the tilt direction turns (the corkscrew) |
| `tumbleAngle` | 45° | tilt that counts as tumbling |
| `breakUpDelay` | 0.35 s | from the tumble to the break-up |
| `explodeTime` | 6 s | a break-up never lasts longer than this |
| `motorEjectTime` | 0.6 s | retention loss: when the battery leaves |
| `motorEjectSpeed` | 25 m/s | retention loss: how fast the battery shoots out |

**Each step** (`Step(dt)`), for Success and UnstableFlight:

1. `time += dt`. The engine is **burning** while `time < burnTime`.
2. **Mass** falls as fuel burns: `mass = dryMass + propellantMass × max(0, 1 − time / burnTime)`.
3. **Tilt** for the outcome (section 6). The body points up, leaned over by the tilt towards the tilt direction.
4. **Thrust** pushes along the body. No thrust after burnout.
5. **Drag** opposes the velocity: `(dragK + broadsideDrag × s) × speed × velocity`, where `s` is 0 flying nose first and 1 flying fully sideways.
6. **Integrate** (semi-implicit Euler) the velocity, then the position. Until it has lifted off (above 5 cm) it cannot move sideways or sink below the pad.
7. **Ground:** once lifted off, if the lowest point of the rocket (the base, or the nose when it points below the horizon) reaches the ground, it has **crashed** and exploded there.

**When the flight is finished** (`IsFinished(successAltitude)`):

- it exploded (in the air or on the ground); or
- retention loss: 4 s after the battery left (time > 4.6 s); or
- it reached the success altitude (1200 m); or
- after burnout, it is no longer rising and at least 1 s past burnout (a safety net).

## 6. The outcomes

The numbers below come from running the model at 90 frames per second. The cap rule comes first: an unlocked cap is a retention loss whatever the flaps.

### Success

Flies straight up. It burns out at 7 s at about 450 m and 160 m/s, coasts upward, and passes 1200 m after about **13 s**, which ends the flight. The pad camera follows it and zooms out. The result screen says MISSION SUCCESS.

### UnstableFlight, one wrong flap: veers off course and crashes

The one bad flap fights the other two and the rocket weathercocks over to one side. It climbs straight for 0.9 s, then pitches over towards Off Course Azimuth (70°) at up to 110° a second: 45° at 1.7 s (19 m), past the horizontal at 2.1 s (29 m), peaking at about 38 m, then flying nose down under power. At about **4.2 s** it hits the ground nose first, about 80 m from the pad: about 125 m from the console and 13° left of the window's centre, so the whole arc and the crash are seen through the window. The explosion has its ground effects (section 7). The result screens say UNSTABLE FLIGHT, VEERED OFF COURSE AND CRASHED.

### UnstableFlight, two or three wrong flaps: breaks up

Lifts off and starts to wobble at once (from 0.2 s):

`tilt = min(90°, 6° × e^(2.4 × (t − 0.2)) × (0.55 + 0.45 × sin(6 × (t − 0.2))))`

and the tilt direction turns 220° per second, so the rocket corkscrews. The tilt passes 45° (tumbling) at about 1.3 s, and 0.35 s later, at about **1.6 s and 16 m**, the airframe breaks up in the air, low enough to see the fireball whole from the window. The fins, flaps, nose cone and cap fall out of it trailing smoke. The result screens say UNSTABLE FLIGHT, BROKE UP IN FLIGHT.

### MotorRetentionLoss (the cap was not locked)

The airframe **never leaves the pad**. At 0.6 s the battery is driven out of the base at 25 m/s with a puff of smoke, slowing to a stop over 2.5 s (about 31 m). On screen, the battery (taking the cap with it) shoots out low off the deck and skids along the ground, tumbling, with the flame following it for 2.5 s; a low explosion sound plays and the engine sound fades over 2.5 s. The flight ends at 4.6 s. The result screens say BATTERY RETENTION LOSS, BATTERY BLEW OUT ON THE PAD.

The second result line comes from `BuildReport.FlightDetail` (the big TV's status line and the result on the monitor beside the bin).

## 7. The effects

All are Shuriken particle systems on Kenney's particle sprites (`Assets/Texture/Particles`, CC0) with URP Particles/Unlit materials. The values are on each system in the Inspector; these are the ones that matter.

| System | What it does | Key settings |
|---|---|---|
| `EngineFlame` | the flame core | Local space (stays on the nozzle as it climbs). Stretched billboards: length = size × 2 + speed × 0.05, about 4.6 m. **Each streak is centred on its particle, so the cone emitter sits 2.3 m below the nozzle** (Shape > Position z = 2.3): every streak starts at the nozzle and reaches down. Speed 32–42, life 0.16–0.26 s, size 1.2–1.6, 240 per second, `Mat_Particle_Flame`. `LaunchSequence` scales rate, size and speed with the flame level. |
| `NozzleGlow` | the bright ball where the streaks start | Local space, size 2–2.6, life 0.07–0.13 s, 60 per second, `Mat_Particle_Flash` |
| `EngineSparks` | sparks spraying down and falling | 45 per second, gravity 0.8 |
| `ExhaustTrail` | the smoke trail in the air | World space, **1.2 particles per metre moved** (none while standing still), life 5–7 s, grows to 3.2 times, light noise; at most 600 |
| `PadSmoke` | the cloud rolling out from under the deck | World space, a horizontal ring emitting outwards at 9–17 m/s, slowed by Limit Velocity, lifted 1.2–2.6 m/s², life 4.5–8 s, grows to 2.8 times; at most 420 |
| `PadFire` | fire spraying out sideways under the deck | a horizontal ring at 8–13 m/s, life 0.25–0.45 s, size 2–3.2, `Mat_Particle_Flame` |
| `DebrisTrail` | dark smoke off each piece of debris | 8 per second and 1 per metre, life 1.8–3 s |

**`Explosion.prefab`** (every system scales with the root, so a crash is drawn 1.5 times bigger):

| Child | |
|---|---|
| `Flash` | one 45 m white flash for 0.3 s, with a point light (range 90 m) through its Lights module |
| `Fireball` | 22 solid (alpha-blended) puffs, pale yellow to orange to dark red to smoke: it reads against a bright sky, where additive fire washes out |
| `Flames`, `Embers` | additive flame licks and falling sparks |
| `Smoke` | a dark cloud that rises and lingers 6–9 s |
| `Ground` (on a crash only) | `Shockwave` (a ring on the ground), `Dirt` (thrown up and falling), `DustRing` (rolling out along the ground), `Scorch` (a dark patch on the ground for 14 s), `GroundFire` (6 s), `SmokeColumn` (8 s) |

**Quest cost:** at the worst moment (a break-up over the pad smoke) about 2000 particles are alive, most of them large smoke puffs; frame rate must be checked in the headset. To lower it, reduce Max Particles and the rates on `PadSmoke` and `ExhaustTrail` first.

## 8. The pad camera

The big TV and the console monitor show what `PadCamera` films, from PreLaunch until the result.

- **Output:** it renders into `Assets/Texture/Screens/PadCameraFeed.renderTexture` (640 × 360, no anti-aliasing). `Mat_PadFeed.mat` displays that texture, and `LaunchScreenController` puts the material on the screens ([spec 05](05-screens-and-displays.md)).
- **On only when needed:** the camera is saved switched off and only renders between `PlaceVehicleOnPad` and `ResetPad`, so it costs nothing during building. Far clip 3000 m.
- **Where it stands:** `PadOrigin + PadOrigin.rotation × Pad Camera Offset`. (The position saved in the scene is replaced as soon as the pad is set up.)
- **Where it looks:** 6 m up the vehicle's body; after an explosion, at the fireball.
- **Zoom:** field of view = 28° + 0.02° per metre of altitude and distance flown sideways, between 28° and 70°.

## 9. The vehicle

`LaunchVehicle.prefab` is the assembled rocket (the same layout as `Assets/Prefabs/Rocket/AssembledRocket_Static.prefab`) scaled 36 times, plus the engine effects at its base.

- It is **built the way the player built the rocket** (`MatchBuild(report)`, applied in `PlaceVehicleOnPad`): each fin, flap, nose, battery and cap sits on the point the player put it on, a flap upside down where it went on upside down, parts the rocket did not have are left off, and an unlocked cap sits out of the base (Unlocked Cap Gap, 2 mm in model metres). So a rocket with a flap in a Low slot grows and launches with it there. Explosion debris only uses parts that are on the vehicle. `TryGetVehiclePart` reports where a vehicle part is (for tests). Without a report (the tests' own launches) every part is on its own point.
- It arrives at model size and grows, so the player sees "their" rocket scaled up for launch, through the window and on the screens.
- Vehicle Motor and Vehicle Cap are separate transforms inside it, so the retention-loss failure can pull them out. `ResetPad` puts them back.

## 10. Recipes

**Make the success flight shorter.** Lower Success Altitude on `LaunchSequence` (for example 800 m). The flight ends sooner.

**Make the rocket faster or slower.** Change `thrust` (or `propellantMass`, `burnTime`) in `RocketFlightModel.cs`. Keep thrust above `(dryMass + propellantMass) × 9.81` (49 050 N), or it will never lift off. Then run `RocketFlightModelTests`: the failure tests check the crash and break-up still happen in the window's view.

**Move the off-course crash.** Change Off Course Azimuth on `LaunchSequence` (keep it between about 60° and 80° so the crash is in the window), or `pitchRate` in the model (faster: closer and lower).

**Make the break-up higher or lower.** `instabilityOnset`, `instabilityGrowth` and `breakUpDelay` in the model. Above about 25 m the fireball leaves the top of the window.

**Make the fire longer.** On `EngineFlame` raise Start Speed or the renderer's Length Scale, and move Shape > Position z down by half the extra streak length, or the flame will climb the rocket again (`LaunchEffectsTests` checks it).

**Watch a launch in slow motion.** In Play mode, set Edit > Project Settings > Time > Time Scale to 0.2 (or `Time.timeScale` from a script). Every timer in the game slows with it.

**Move the camera.** Change Pad Camera Offset (relative to the pad, then turned with it). Check in Play mode that the room is not in frame.

**Replace the launch with your own.** Keep the method names of section 4, or call your own launch from the game engine. See `Docs/INTEGRATION.md` section 4.

## 11. Troubleshooting

| Symptom | Check |
|---|---|
| No rocket on the pad after SUBMIT | `GameFlowController.Launch` must point at `LaunchSite`. The vehicle is hidden until PreLaunch: that is normal. |
| The big screen is grey or black during the launch | `PadCamera` must have `PadCameraFeed` as its target texture, and `LaunchScreenController` must have `Mat_PadFeed` as its Pad Camera Material. |
| The rocket never lifts off | Thrust is below the weight (see recipes). |
| The rocket looks on fire / flame climbs its side | `EngineFlame`'s Shape > Position z must stay half a streak below the nozzle (2.3 with the current sizes). |
| The trail is a row of separate puffs or missing | `ExhaustTrail` emits per metre moved: its Emitter Velocity Mode must be Transform. |
| The game is stuck after liftoff | The flight never finished: for example `LaunchSite` was switched off mid-flight, or Success Altitude is unreachable. |
| The explosion or debris lingers | They are deleted after 12 s and 14 s, or by `ResetPad` when the next phase starts. |
| Frame rate drops during the launch | See section 7, Quest cost. |
