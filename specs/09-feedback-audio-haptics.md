# 09 Feedback: glow, sound and haptics

Every assembly moment gives the player three kinds of feedback: something to see (glow), something to hear, and a vibration in the controller. This is a marking requirement (`Docs/SPEC.md` section 6). This spec lists every moment and how to change its feedback. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Main` → `RocketWorkstation` (`AssemblyFeedback` on the prefab root) |
| **Settings** | `Assets/Settings/Rocket/FeedbackLibrary.asset`: every clip and every vibration value |
| **Scripts** | `AssemblyFeedback.cs`, `FeedbackLibrary.cs`, `AssemblyEvents.cs`, `ButtonPressHaptics.cs`, `ImpactAudio.cs`; the glow in `AttachPoint.cs` and `RocketPart.cs` |
| **Tests** | `FeedbackDeviceTuningTests` (edit mode); the play-mode assembly tests check that feedback fires (for example `Flaps_SixSlots_EitherWayUp_PlacementsRecorded_FeedbackIdentical`) |

## 1. How it fits together

1. A part does something: it engages a guide, seats, ticks the cap's ratchet. It raises a static event in `AssemblyEvents` ([spec 03](03-rocket-assembly.md)).
2. `AssemblyFeedback` (on the `RocketWorkstation` root) listens to every one of those events.
3. It looks up the sound and the vibration for that moment in `FeedbackLibrary.asset`.
4. It plays the sound as a 3D sound at the attach point or the part, and sends the vibration to the right controller.

The glow is separate: attach points and parts drive their own glow meshes every frame (section 4).

Because all values live in one asset, the team's final sounds can be dropped in without touching code.

## 2. Every moment

"Holding hand" means the hand that holds the part, or held it last.

| Moment | You see | You hear (field → clip) | You feel (field: strength, seconds, Hz; second pulse) | Which hand |
|---|---|---|---|---|
| A part comes near an attach point | the point glows, brighter as it gets closer; the part glows too | | | |
| Guide engaged (the part locks onto the rail) | full glow | `guideEngage` → HoverSound.wav | `hapticGuideEngage`: 0.15, 0.02 s, 220 Hz | holding |
| Sliding along the rail (every 4 mm) | | | `hapticSlideTick`: 0.07, 0.01 s, 220 Hz | holding |
| Break away from the rail | glow drops | nothing, on purpose | nothing, on purpose | |
| Fin, flap or motor seated | the part settles | `seat` → Button_22_Click.wav | `hapticSeat`: 0.5, 0.08 s, 120 Hz; then 0.15, 0.05 s after 0.03 s | holding |
| Nose cone seated | | `noseSeat` → Button_14_Hover.wav | `hapticNoseSeat`: 0.4, 0.08 s, 100 Hz | holding |
| Cap seated (still unlocked) | the cap sits 2 mm proud | `capSeatUnlocked` → Button_14_Hover.wav | `hapticCapSeatUnlocked`: 0.2, 0.04 s, 100 Hz | holding |
| Cap ratchet tick (every 30°) | the cap turns | `capDetent` → HoverSound.wav | `hapticCapDetent`: 0.2, 0.02 s, 250 Hz | holding |
| Cap thread texture (every 6° between ticks) | | | `hapticTwistTexture`: 0.08, 0.012 s, 200 Hz | holding |
| Cap locked (180°) | the gap closes | `capLock` → Button_22_Click.wav | `hapticCapLock`: 1.0, 0.20 s, 80 Hz; then 0.3, 0.10 s after 0.05 s | holding |
| A part taken off | | `unseat` → Button_14_Hover.wav | `hapticPartRemoved`: 0.2, 0.04 s, 150 Hz | holding |
| Rocket complete | | `standRelease` → Button_22_Click.wav, at the rocket | `hapticPrototypeComplete`: 0.3, 0.10 s, 90 Hz; then 0.2, 0.06 s after 0.08 s | both |
| A dropped part respawns | it reappears on the pad | `respawnPop` → Button Pop.wav | | |
| A part hits something | | `impactDesk` → impactGeneric_light_000.ogg (`impactPart` → impactMetal_002.ogg if it hit another part) | | |
| A hand comes within reach of a loose part | | | `hapticHoverPart`: 0.08, 0.012 s, 250 Hz (at most every 0.25 s per part) | that hand |
| Picking up a loose part | | | `hapticGrabPart`: 0.3, 0.03 s, 160 Hz | that hand |
| Picking up the rocket | | | `hapticGrabRocket`: 0.55, 0.05 s, 90 Hz; then 0.2, 0.08 s after 0.04 s | that hand |
| Letting go of a loose part | | | `hapticReleasePart`: 0.12, 0.015 s, 180 Hz | that hand |
| Letting go of the rocket | | | `hapticReleaseRocket`: 0.25, 0.03 s, 110 Hz | that hand |
| Rocket accepted by the bin | it sinks into the bin | the bin's door-close sound ([spec 07](07-console-and-bin.md)) | `hapticSubmitAccepted`: 0.5, 0.12 s, 100 Hz; then 0.3, 0.15 s after 0.10 s | both |
| A fresh kit appears | new parts on the desk | | `hapticKitSpawned`: 0.15, 0.04 s, 200 Hz | both |
| Console button pressed / released | the button moves and tints (XRI's affordance system) | XRI's button sound | `hapticButtonPress`: 0.6, 0.03 s, 200 Hz / `hapticButtonRelease`: 0.25, 0.02 s, 180 Hz | pressing |

Notes:

- The HoverSound, Button_22_Click and Button_14_Hover clips are from the XRI examples (`Assets/ThirdParty/XRI_Examples/Global/Audio/`). Impacts are Kenney sounds (`Assets/Audio/Kenney/`). `Button Pop.wav` comes from XRI's Starter Assets sample in `Assets/Samples`.
- Rocket parts never collide with each other ([spec 03](03-rocket-assembly.md)), so in practice the impact sound is the desk one.
- Impact volume follows the speed: silent below 0.25 m/s, full at 2.5 m/s, at most one sound per part every 0.12 s (`ImpactAudio` on each part).
- Gripping a part that is already attached gives no "grab" pulse, and letting go of a part that is seating or staying attached gives no "release" pulse: the mechanic's own feedback follows instead.
- `hapticStandCapture` and `hapticStandLiftOut` belong to the removed assembly stand and never play.

## 3. Sound

- `AssemblyFeedback` creates four 3D audio sources at startup (children `AudioSource_0` to `_3` of `RocketWorkstation`) and takes turns using them. Each sound plays at the attach point or the part.
- Range: full volume within 0.2 m, fading linearly to silence at 6 m.
- **Volume** on `FeedbackLibrary` (1) scales every assembly sound.
- The game engine's beeps and the launch sounds are separate ([spec 02](02-game-engine.md), [spec 04](04-launch-simulator.md)).

## 4. Glow

- **Attach points:** each point on the body tube has a `Glow` child (an emissive mesh with `Assets/Materials/Rocket/Glow.mat`, URP Unlit, HDR orange). While a part that the point would accept is within the glow radius (0.12 m), the point glows, brighter as the part gets closer, and at full brightness while the part is on its rail.
- **Parts:** each part has a `PartGlow` child that lights up the same way.
- Every frame each point and part shows the brightest request it got that frame, then resets, so the glow goes off as soon as the part leaves. Several points can glow at once.
- A point that is refused by the build rules (for example the cap seat before the motor) never glows. A motor held nozzle up makes nothing glow.
- Glow colour: **Glow Color** on `AttachPoint` and `RocketPart` (HDR (2, 1.1, 0.2)). Brightness goes from 25% to 100% of that colour.

## 5. Haptics

**How a vibration is sent:**

1. `AssemblyFeedback` finds the hand: the interactor holding the part (`RocketPart.lastHoldingInteractor`), the hovering or pressing interactor, or both `NearFarInteractor`s for "both hands".
2. It finds that controller's `HapticImpulsePlayer` (XRI creates one on demand).
3. It adjusts the values for the controller model (below).
4. It sends the pulse: **strength** (0 to 1), **duration** (seconds) and **frequency** (Hz, 0 for the runtime's default). If the moment has a second pulse, it is sent after the first one plus the gap.

**Designed for the Quest 3.** The Touch Plus controllers have voice-coil motors that respond to frequency, so the table uses it: low frequencies for heavy moments (locks, clunks), high ones for fine textures (ticks), and short durations everywhere.

**Other controllers.** `FeedbackLibrary.deviceTunings` corrects the table for controllers that cannot play it as designed. The controller is identified by its Input System layout and product name; the first entry whose **Match** text appears in that name wins:

| Match | Strength at least | Duration at least | Use Frequency | Why |
|---|---|---|---|---|
| `vive` | 0.35 | 0.04 s | off (runtime default) | the Vive wand's motor cannot spin up for very short, soft pulses |
| `index` | 0.25 | 0.02 s | on | |

Any other controller (Touch Plus, Touch) plays the table unchanged. Silent entries (strength 0 or duration 0) stay silent. `FeedbackDeviceTuningTests` checks these rules.

These two entries are not yet saved in the asset, so the defaults from the code apply. Editing them in the Inspector saves them into the asset.

**The rig's own buzz is off.** The room's rig has a generic hover and select vibration (`SimpleHapticFeedback`). `XRRigAdapter` turns it off, so only these designed vibrations play ([spec 08](08-xr-rig-and-input.md)).

## 6. Recipes

**Swap in a final sound.** Import the clip (`.wav` or `.ogg`) under `Assets/Audio/`, select `FeedbackLibrary.asset`, and drag the clip into the field (for example **Cap Lock**). No code. Add the source to `Docs/ASSET_CREDITS.md`.

**Make a vibration stronger or longer.** Select `FeedbackLibrary.asset`, open the entry (for example **Haptic Cap Lock**) and change Amplitude or Duration. Feel it in the headset: haptics cannot be checked on a computer.

**Silence one moment.** Set its Amplitude (or Duration) to 0, or clear its clip.

**Add feedback for a new moment.**

1. Add an event to `AssemblyEvents.cs` (and clear it in its `ResetStatics`).
2. Raise it where the moment happens.
3. Add a clip field and a `Haptic` field to `FeedbackLibrary.cs`.
4. In `AssemblyFeedback.cs`, subscribe in `OnEnable`, unsubscribe in `OnDisable`, and play them with `PlayAt` and `SendHaptic` (holding hand), `Play` (a given hand) or `SendHapticBothHands`.

**Add haptics to a new push button.** `PushButton.prefab` already has `ButtonPressHaptics`. For a different interactable, add `ButtonPressHaptics` and wire its `OnPress` and `OnRelease` from the interactable's events.

## 7. Troubleshooting

| Symptom | Check |
|---|---|
| No sound at all | `AssemblyFeedback` must be on `RocketWorkstation` with **Library** set to `FeedbackLibrary.asset`; Volume above 0; the clip field filled. |
| No vibration | The entry's Amplitude and Duration must be above 0. `AssemblyFeedback.instance` must exist (only one `AssemblyFeedback` in the scene). On a Vive wand, very soft pulses are lifted by device tuning; if still nothing, raise the floor. |
| The vibration goes to the wrong hand | Holding-hand moments go to the hand that held the part last. |
| Every hover buzzes | The rig's generic haptics are back on: `XRRigAdapter` Silence Generic Haptics. |
| Nothing glows | The part may be refused (build rules, wrong way round). Check the `Glow` child still has its renderer and `Glow.mat`. |
| The respawn pop is silent | `Button Pop.wav` lives in the room team's `Assets/Samples` folder; if that sample was removed, assign another clip. |
