# VR Rocket on the team's room

What the `interactions` branch adds to this project, how it hangs together, and what to keep stable in `room.unity` so it keeps working. `main` is untouched: everything arrives by pull request from `interactions`.

## 1. Play it

- **In the Editor:** open `Assets/VRRocket/Scenes/VRRocket_Main.unity` and press Play. The room loads by itself a moment later. Having `room.unity` open next to it is fine too: it is used as it is, not loaded twice.
- **Builds:** File > Build Profiles.
  - `Assets/VRRocket/Settings/BuildProfiles/VRRocket_Windows`: PC VR. Set SteamVR as the OpenXR runtime for a Vive or Index; a Quest works through Link or Virtual Desktop.
  - `VRRocket_Quest`: Quest 3 standalone (Android).

  Both list `VRRocket_Main` and then `room.unity`. The global scene list in Build Settings is not changed.

**The experience, in order:**

1. **Intro.** You start where the room's `Player` stands, facing the big TV. The room's intro plays: NASA logo, standby, announcement with red alert lights, dimmed room, transmission video. The start menu floats in front of you, below the screen.
2. **Start.** Pressing START (ray and trigger) ends the intro. The lights come back, the mission screens switch on and the kit unlocks.
3. **Build.** Turn around to the assembly desk. The blueprint panel on the wall above it shows the next step. Grab with the grip; the help board is on the left wall as you face the desk.
4. **Submit.** Carry the finished rocket to the console and drop it into the bin at the right end of the shelf. Press SUBMIT next to it.
5. **Pad.** The rocket appears on the pad outside the window (left side of the room) at its own size and grows to full size. The big TV and the centre console monitor show the pad camera.
6. **Launch.** Press LAUNCH (left end of the console) for the countdown and flight. ABORT returns the rocket to the desk, still editable.
7. **Result.**
   - A good build reaches orbit and the menu offers another build.
   - A bad build fails the way it was built. An unlocked cap ejects the motor; wrong flaps make the flight unstable. The failure screen marks the failed parts in red, the prototype comes back on the console for inspection, and a fresh kit is on the desk.

## 2. How it is put together

**Our scene and theirs.** `VRRocket_Main` holds every system of ours, placed at the room's coordinates. `RoomSceneLoader` loads `room.unity` additively and keeps our scene active. **`room.unity` is never edited** by this work. Their daily edits stay conflict-free, and both scenes can be worked on at the same time by different people.

**Bindings.** Everything that depends on their objects happens at runtime, in one place.

| Object in `VRRocket_Main` | What it does |
|---|---|
| `RoomLink` / `RoomSceneLoader` | Loads the room (Editor: straight from the asset; builds: from the build profile's list). |
| `RoomLink` / `RoomBindings` | Swaps their right wall for ours with a window, if it still has the expected bounds. Keeps our TV overlay, corner clock and console add-on on their TV, corner TV and console if those move. Warns when the desk or floor under our things has moved. Every problem is a Console warning, counted in `warningCount`. |
| `RoomLink` / `XRRigAdapter` | Adapts their `Player` rig at runtime: turns off the generic hover/select buzz (the rocket plays its own designed haptics), adds the `RocketPart` interaction layer to the hands, turns off Jump, and puts the rig on the floor (it spawns 2.4 m up). |
| `RoomLink` / `TransmissionBridge` | Finds their `TransmissionController` by type name and lets it play under the menu. On START it stops the coroutines, videos and announcement, switches the controller off (its `OnDisable` restores lights and ambient) and puts the NASA logo back on the TV. |
| `GameFlow` | The flow (`GameFlowController`), the big-screen driver (`LaunchScreenController`), the blueprint text, the start menu, the EventSystem, and `PhaseDisplays` (our screens are off during the menu, on for the game). |
| `XR Interaction Manager` | The one manager. The room has none. |

**Their objects we depend on.** Keep these names and roughly these poses, or tell us when they change:

| Path in `room.unity` | Used for | If it changes |
|---|---|---|
| `Player` (XR Origin, Starter Assets rig) | the rig | adapted wherever it is |
| `Environment/Floorplan/Right wall` | replaced by our window wall | moved, resized or renamed: their wall stays, ours stays off, and a warning says so |
| `Environment/TV/TV LED 30*` (the big TV) | our screen overlay follows it | renamed: the overlay keeps its saved pose and a warning is logged |
| `Environment/TV/TV top left` | the countdown clock follows it | same |
| `Environment/Mission Control Desk/hq` | the console add-on follows it (buttons, bin, mirror, colliders) | same |
| `Environment/AssmblyArea/DeskTop` | the rocket kit lies on it | moved: warning (the kit does not follow) |
| `Environment/Floorplan/Floor` | the teleport floor sits on it | moved: warning |
| `TransmissionController` (component, any object) | the intro bridge | missing: the menu simply has no intro |

When you move one of these on purpose:

- **TV, corner TV or console.** Nothing to do: our overlay, clock and console add-on follow them at runtime. To see them in place in the Editor, open `VRRocket_Main`, open `room.unity` additively, run **Snap followers to the open room** from the `RoomBindings` context menu, and save `VRRocket_Main` only.
- **Desk or floor.** The check warns. Make sure the kit still lies on the desk and the teleport floor still sits on the floor, then run **Record check bounds from the open room** and save `VRRocket_Main`.
- **Right wall.** Either cut the window into it yourselves (then delete `WindowWall`), or rebuild ours with **VR Rocket > Rebuild Window Wall**.
- **Re-record follow offsets from the current placement** is only for when you deliberately move one of our objects relative to yours.

**Modules and coordinates** are in `Docs/ENVIRONMENT.md`. **The rocket's interface** for your own systems is in `Docs/INTEGRATION.md`.

## 3. What changed outside `Assets/VRRocket`

Each change is its own commit on `interactions`.

| Commit subject | What | Why |
|---|---|---|
| Settings: OpenXR for PC VR and Quest | PC: HTC Vive, Valve Index, Oculus Touch and Touch Plus controller profiles. Android: XR Plug-in Management settings, the OpenXR loader, Meta Quest Support, Touch Plus and Touch profiles. | Vive wands bind nothing without their profile; Quest standalone needs the Android loader. |
| Settings: RocketPart interaction layer (index 1) | names interaction layer 1 | parts, attach points and hands filter on it (Teleport stays at 31) |
| Settings: 4x MSAA on the PC and Mobile URP assets | MSAA 1 to 4 | thin fins, bezels and screen text shimmer badly without it |
| Transmission: null-check the alert lights | one `if` in `TransmissionController.SetAlert` | each Play-mode stop logged a MissingReferenceException when the room unloaded (also in `room.unity` on its own) |

**Left as you set them, on purpose:**

- Enter Play Mode options without domain reload. Our statics reset themselves instead.
- Physics, time, quality and tag settings.
- Render scale and HDR.
- The global scene list (the build profiles carry ours).
- Product name, company and app id. These are yours to decide, and still the template defaults.
- Packages.

**Quest (Android):**

- With Android active, the OpenXR project validator asked for the Quest latency mode. It is applied, in its own commit.
- The validator also warns about SSAO (screen-space ambient occlusion). That check looks at every renderer in the project, and the only one with SSAO is `PC_Renderer`. That renderer has carried it since the project was created from Unity's URP template; it was not set up for the Vive. The Quest build uses `Mobile_Renderer`, which has no SSAO, so nothing needs doing for Quest.
- On PC VR, SSAO costs GPU time for both eyes at full resolution. Turn it off, or set it to downsample, if frame timing in the Vive is over budget.
- `VRRocket_Quest` builds an APK (61 MB, IL2CPP, ARM64) with no errors.
- The app id is still the template's `com.UnityTechnologies.com.unity.template.urpblank`; set your own before installing it next to other builds.

## 4. Tests

Window > General > Test Runner.

- **Edit mode (36):**
  - outcome rule, assembly state machine, cap ratchet, attach geometry, flight model;
  - static resets for Play mode without domain reload;
  - haptic tuning for Vive and Index;
  - big-screen text trimming.
- **Play mode (26):**
  - every assembly interaction in `Dev_Interactions`;
  - safe zones and the near-only grab rule;
  - in `VRRocket_Main` on the real room:
    - two full games (a failed launch with inspection and a new kit, then a clean flight back to the menu);
    - the console buttons pressed by the rig's own hand;
    - the button and START wiring;
    - one rig, one EventSystem, one interaction manager;
    - rig adaptation;
    - bindings with no warnings, and a missing wall left alone;
    - teleport floor coverage;
    - START ending the intro.

Both suites passed twice in a row in one Editor session on 12 October 2026, without domain reload.

## 5. Only a headset can check

- **Haptics on Vive wands:** the soft ticks are lifted to amplitude 0.35 / 40 ms there. Are they felt? Is there no generic buzz on hover?
- **Grip on wands:** a full build with the binary grip on Vive wands.
- **Trackpad locomotion:** does the trackpad teleport and turn only when intended?
- **Legibility:**
  - Is the big screen readable from the console? The status line shows only its first sentence there; the console's centre monitor shows all of it.
  - Is the corner clock readable?
- **Heights:** button height (0.90 m, on a panel tilted toward the player) and the 1.125 m assembly desk.
- **View and frame rate:**
  - Is the vehicle fully visible through the window from the console?
  - Is 90 Hz held through countdown and flight?

## 6. Requests to the room's owners

- **Cut the window into your right wall permanently** if you like it:
  - opening z −6.0 to −0.5, y 0.9 to 3.3, on the wall at x 3.9 to 4.1;
  - the wall swap then stops itself because the bounds no longer match;
  - delete `WindowWall` from `VRRocket_Main` at the same time.
- **A real hole in `hq.fbx` for the bin** (optional). It sits at x −1.28 to −0.82, z −0.53 to −0.07 on the shelf; today it is a black quad.
- **Keep the section 2 paths stable**, or tell us when they change.
- **`TransmissionController.FindObjectsByType`:** the `FindObjectsSortMode` overload is obsolete in 6000.6 (two Console warnings).
- **Ignore `.utmp/`:** an Android build leaves it in the project root.
