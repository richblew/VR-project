# 04 Launch simulator

The launch outside the window: the full-size rocket on the pad, the small physics model that flies it, the three outcomes, the effects, the sounds and the pad camera. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Main` → `LaunchSite` (at (32, 0.30, −20), turned 180°) and `Outside` (ground, pad, gantry) |
| **Scripts** | `Assets/Scripts/Runtime/LaunchSequence.cs` (the show) and `RocketFlightModel.cs` (the physics, plain C#) |
| **Prefabs** | `Assets/Prefabs/Launch/LaunchVehicle.prefab` (the 36× rocket), `Explosion.prefab` |
| **Assets** | `Assets/Materials/Launch/Mat_Particle_*.mat`, `Assets/Texture/Particles/`, `Assets/Audio/Kenney/`, `Assets/Texture/Screens/PadCameraFeed.renderTexture`, `Assets/Materials/Screens/Mat_PadFeed.mat` |
| **Tests** | `RocketFlightModelTests` (4 edit-mode tests, one per outcome plus the mass), `GameFlowTests` (a failed and a successful flight in the real scene) |

## 1. Two halves

- **`RocketFlightModel` is the physics.** Plain C# with no Unity objects: you give it an outcome, call `Step(dt)` every frame, and read the altitude, speed, tilt and events (lifted off, tumbled, exploded, motor ejected). Because it has no scene dependencies, it is unit tested.
- **`LaunchSequence` is the show.** A component on `LaunchSite`. It owns a `RocketFlightModel`, moves the full-size vehicle by the model's numbers, and plays the flame, smoke, sound, explosion and pad camera.

The game engine ([spec 02](02-game-engine.md)) tells `LaunchSequence` when to do what. The outcome comes from the build report ([spec 03](03-rocket-assembly.md)).

## 2. In the Hierarchy

```
LaunchSite            (32, 0.30, -20), yaw 180       LaunchSequence
├─ PadOrigin          the spot the rocket stands on; the flight is measured from here
├─ LaunchVehicle      LaunchVehicle.prefab: the 36x rocket (hidden until a rocket is submitted)
│  ├─ Model           the assembled rocket, scaled 36x (about 13 m tall)
│  ├─ EngineFlame     particles
│  └─ EngineSparks    particles
├─ PadCamera          camera that films the launch for the screens (off until needed)
├─ PadSmoke           particles at the base
├─ PadSteam           particles from the side of the pad
├─ EngineAudio        AudioSource: engine loop
└─ PadOneShot         AudioSource: ignition and explosion sounds

Outside
├─ Ground             300 x 300 m, just under the room's floor level
├─ LaunchPad          9 m concrete disc at (32, 0.15, -20)
├─ GantryTower        16 m tower beside the pad
└─ GantryArm
```

## 3. `LaunchSequence` in the Inspector

| Section | Field | Value |
|---|---|---|
| Pad | Pad Origin | `LaunchSite/PadOrigin` |
| | Vehicle | `LaunchSite/LaunchVehicle` |
| | Vehicle Motor, Vehicle Cap | the motor and cap meshes inside the vehicle (they fly out on a retention-loss failure) |
| Effects | Flame, Sparks | `EngineFlame`, `EngineSparks` in the vehicle |
| | Smoke, Pad Steam | `PadSmoke`, `PadSteam` |
| | Explosion Prefab | `Assets/Prefabs/Launch/Explosion.prefab` |
| Audio | Engine Loop | `EngineAudio`: `spaceEngineLow_000.ogg`, looping, 3D (full volume within 15 m, fades out to 400 m) |
| | One Shot | `PadOneShot`: 3D, full volume within 20 m |
| | Ignition Clip | `thrusterFire_000.ogg` |
| | Explosion Clip, Explosion Low Clip | `explosionCrunch_000.ogg`, `lowFrequency_explosion_000.ogg` |
| Pad camera | Pad Camera | `LaunchSite/PadCamera` |
| | Pad Camera Offset | (55, 6, 35): where the camera stands, relative to the pad |
| Flight tuning | Success Altitude | 1200 m: a good flight ends when it gets this high |
| | Visual Scale | 1: multiplies the model's metres when moving the vehicle |
| | Arrival Scale | 0.0278 (1/36): the size the vehicle appears at before it grows |

## 4. The sequence, step by step

The game engine calls these methods in this order:

| Method | When (phase) | What it does |
|---|---|---|
| `ResetPad()` | entering Menu or Assembly | Stops any flight or growth, stops and clears every effect, removes an explosion, puts the motor, cap and flame back inside the vehicle, puts the vehicle back on `PadOrigin` at full size and **hides** it, switches the pad camera off, resets the model. |
| `PlaceVehicleOnPad(2.5)` | PreLaunch (rocket submitted) | Shows the vehicle at 1/36 of its size (the size of the model the player built) and grows it to full size over 2.5 s with a smooth ease. Aims and switches on the pad camera. |
| `Ignite()` | Countdown, at T-3 | Flame, sparks and pad steam start; the ignition sound plays; the engine loop starts at 60% volume. |
| `AbortCountdown()` | ABORT | Stops every effect and the engine sound. (The engine then calls `ResetPad` when it enters Assembly.) |
| `Liftoff(outcome, onFinished)` | T-0 | Starts the flight (below). Calls `onFinished` when it is over. |

**During the flight, every frame:**

1. `model.Step(dt)`, with the frame time capped at 0.1 s so a hitch cannot throw the rocket forward.
2. The vehicle is placed at `PadOrigin + (drift x, altitude, drift z)` and tilted by the model's tilt, around the model's tilt direction.
3. On a retention-loss failure, the motor (with the cap) is animated out of the base (section 6).
4. At burnout (7 s) the flame, sparks and engine sound stop (except on a retention-loss failure, where the flame follows the ejected motor).
5. If the model exploded: the `Explosion` prefab is spawned (and deleted after 12 s), both explosion sounds play, and the vehicle is hidden.
6. The pad camera is re-aimed (section 7).

The flight ends when `model.IsFinished(1200)` is true. Then the smoke and steam stop and `onFinished` is called.

## 5. The physics model

`RocketFlightModel` works in metres, seconds, kilograms and newtons, for the full-size vehicle (the model rocket times 36, about 13 m tall).

**Its numbers** are public fields with these defaults in `RocketFlightModel.cs`. They are not shown in the Inspector: change them in the code.

| Field | Default | Meaning |
|---|---|---|
| `dryMass` | 2000 kg | the rocket without fuel |
| `propellantMass` | 3000 kg | fuel at liftoff (burns at a constant rate) |
| `burnTime` | 7 s | how long the engine burns |
| `thrust` | 110 000 N | engine force. Thrust to weight at liftoff: 110 000 / (5000 × 9.81) ≈ 2.2 |
| `gravity` | 9.81 m/s² | |
| `dragK` | 0.35 | air drag factor: drag force = 0.35 × speed² newtons |
| `instabilityOnset` | 1.5 s | unstable flight: when the wobble starts |
| `instabilityGrowth` | 0.9 per second | how fast the wobble grows (exponential) |
| `instabilityAmplitude` | 3° | wobble size when it starts |
| `wobbleFrequency` | 2.5 rad/s | how fast the tilt pulses |
| `corkscrewRate` | 140°/s | how fast the tilt direction turns (the corkscrew) |
| `tumbleAngle` | 45° | tilt that counts as tumbling |
| `explodeTime` | 7.5 s | unstable flight: when it explodes |
| `motorEjectTime` | 0.6 s | retention loss: when the motor leaves |
| `motorEjectSpeed` | 25 m/s | retention loss: how fast the motor shoots out |

**Each step** (`Step(dt)`), for Success and UnstableFlight:

1. `time += dt`. The engine is **burning** while `time < burnTime`.
2. **Mass** falls as fuel burns: `mass = dryMass + propellantMass × max(0, 1 − time / burnTime)`.
3. **Thrust** is split by the tilt: up = `thrust × cos(tilt)`, sideways = `thrust × sin(tilt)`. No thrust after burnout.
4. **Drag** opposes the vertical speed: `drag = dragK × v × |v|`.
5. **Acceleration:** `a = (thrust up − drag) / mass − gravity`.
6. **Integrate** (semi-implicit Euler): `v += a × dt`, then `altitude += v × dt`. The ground stops it: altitude never goes below 0.
7. Once above 5 cm it has **lifted off**; from then on the sideways thrust pushes it horizontally, in the tilt direction.

**When the flight is finished** (`IsFinished(successAltitude)`):

- it exploded; or
- retention loss: 4 s after the motor left (time > 4.6 s); or
- it reached the success altitude (1200 m); or
- after burnout, it is no longer rising and at least 1 s past burnout (a safety net).

## 6. The three outcomes

The numbers below come from running the model at 90 frames per second.

### Success

Flies straight up. It burns out at 7 s at about 450 m and 160 m/s, coasts upward, and passes 1200 m after about **13 s**, which ends the flight. The pad camera follows it and zooms out. The result screen says MISSION SUCCESS.

### UnstableFlight (a flap in the wrong place or upside down)

Lifts off normally. From 1.5 s the rocket starts to wobble:

`tilt = min(90°, 3° × e^(0.9 × (t − 1.5)) × (0.55 + 0.45 × sin(2.5 × (t − 1.5))))`

and the tilt direction turns 140° per second, so the rocket corkscrews. The tilt passes 45° (tumbling) at about 4.5 s. At **7.5 s**, at about 450 m, it explodes; the pad camera stays on the fireball.

### MotorRetentionLoss (the cap was not locked)

The airframe **never leaves the pad**. At 0.6 s the motor is driven out of the base at 25 m/s, slowing to a stop over 2.5 s (about 31 m). On screen, the motor (taking the cap with it) skids out low across the pad, tumbling, with the flame following it for 2.5 s; a low explosion sound plays and the engine sound fades over 2.5 s. The flight ends at 4.6 s.

## 7. The pad camera

The big TV and the console monitor show what `PadCamera` films, from PreLaunch until the result.

- **Output:** it renders into `Assets/Texture/Screens/PadCameraFeed.renderTexture` (640 × 360, no anti-aliasing). `Mat_PadFeed.mat` displays that texture, and `LaunchScreenController` puts the material on the screens ([spec 05](05-screens-and-displays.md)).
- **On only when needed:** the camera is saved switched off and only renders between `PlaceVehicleOnPad` and `ResetPad`, so it costs nothing during building. Far clip 3000 m.
- **Where it stands:** `PadOrigin + PadOrigin.rotation × Pad Camera Offset`. With the pad turned 180°, (55, 6, 35) puts it at world (−23, 6.3, −55): in front of and to the side of the room, so the room is never in frame. (The position saved in the scene is replaced as soon as the pad is set up.)
- **Where it looks:** 6 m above the vehicle; after an explosion, at the fireball.
- **Zoom:** field of view = 28° + 0.02° per metre of altitude, between 28° and 70°.

## 8. The vehicle

`LaunchVehicle.prefab` is the assembled rocket (the same layout as `Assets/Prefabs/Rocket/AssembledRocket_Static.prefab`) scaled 36 times, plus the engine flame and sparks at its base.

- It always looks **correctly built**. The player's mistakes do not change how it looks; they change how it flies.
- It arrives at model size and grows, so the player sees "their" rocket scaled up for launch, through the window and on the screens.
- Vehicle Motor and Vehicle Cap are separate transforms inside it, so the retention-loss failure can pull them out. `ResetPad` puts them back.

## 9. Recipes

**Make the success flight shorter.** Lower Success Altitude on `LaunchSequence` (for example 800 m). The flight ends sooner.

**Make the rocket faster or slower.** Change `thrust` (or `propellantMass`, `burnTime`) in `RocketFlightModel.cs`. Keep thrust above `(dryMass + propellantMass) × 9.81` (49 050 N), or it will never lift off. Then run `RocketFlightModelTests`.

**Make the unstable flight explode sooner or later.** Change `explodeTime` and `instabilityGrowth`. The test `UnstableFlight_LiftsOff_TiltGrows_TumblesThenExplodes` checks it still lifts off and tumbles first.

**Watch a launch in slow motion.** In Play mode, set Edit > Project Settings > Time > Time Scale to 0.2 (or `Time.timeScale` from a script). Every timer in the game slows with it.

**Move the camera.** Change Pad Camera Offset (relative to the pad, then turned with it). Check in Play mode that the room is not in frame.

**Replace the launch with your own.** Keep the method names of section 4, or call your own launch from the game engine. See `Docs/INTEGRATION.md` section 4.

## 10. Troubleshooting

| Symptom | Check |
|---|---|
| No rocket on the pad after SUBMIT | `GameFlowController.Launch` must point at `LaunchSite`. The vehicle is hidden until PreLaunch: that is normal. |
| The big screen is grey or black during the launch | `PadCamera` must have `PadCameraFeed` as its target texture, and `LaunchScreenController` must have `Mat_PadFeed` as its Pad Camera Material. |
| The rocket never lifts off | Thrust is below the weight (see recipes). |
| The game is stuck after liftoff | The flight never finished: for example `LaunchSite` was switched off mid-flight, or Success Altitude is unreachable. |
| The explosion lingers | It is deleted after 12 s, or by `ResetPad` when the next phase starts. |
| Frame rate drops during the launch | The particle systems and the pad camera are the cost. Lower Max Particles or the emission rate on the flame, smoke, steam and explosion particle systems, or the render texture size. |
