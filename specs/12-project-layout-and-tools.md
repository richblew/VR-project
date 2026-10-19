# 12 Project layout, editor tools and conventions

Where every file lives, the assemblies, the custom Editor menus, and the coding and Git conventions the VR Rocket code follows. Back to the [main spec](README.md).

## 1. The repository

The project is organised **by type**: scenes in `Assets/Scenes`, scripts in `Assets/Scripts`, prefabs in `Assets/Prefabs`, and so on. Inside the type folders the room team shared with us (`Materials`, `Models`, `Texture`, `Settings`), VR Rocket's files sit in named subfolders (`Rocket/`, `Launch/`, `Screens/`, `Environment/`...), and the room team's files stay at the top of each folder.

| Folder | What it is | Owner |
|---|---|---|
| `Assets/Scenes/` | `room.unity` (the room), `VRRocket_Main.unity` (the game: press Play here), `Dev_Interactions.unity` (test room) | room team (`room.unity`), VR Rocket (the other two) |
| `Assets/Scripts/` | all VR Rocket code, in assemblies (section 3) | VR Rocket |
| `Assets/Prefabs/` | VR Rocket's prefabs: `Rocket/`, `Launch/`, `Console/`, `Stubs/` | VR Rocket |
| `Assets/Models/`, `Assets/Materials/`, `Assets/Texture/` | the room's art at the top level; VR Rocket's in subfolders (section 2) | shared by folder |
| `Assets/Audio/`, `Assets/UI/`, `Assets/ThirdParty/` | VR Rocket's sounds, fonts and sprites, and the XRI example code we use | VR Rocket |
| `Assets/Transmission/` | the room's intro (`TransmissionController` and its videos) | room team |
| `Assets/TV LED 30¨/`, `Assets/TV images/` | the room's TV model and images | room team |
| `Assets/Samples/XR Interaction Toolkit/3.6.1/` | XRI's Starter Assets (the rig) and XR Device Simulator samples | Unity, imported by the room team |
| `Assets/Settings/` | URP assets and renderers ([spec 11](11-builds-and-deploy.md)); VR Rocket's `Rocket/` and `BuildProfiles/` | shared |
| `Assets/XR/`, `Assets/XRI/` | XR Plug-in Management, OpenXR and XRI settings | shared |
| `Assets/TextMesh Pro/` | TextMesh Pro's default resources | Unity |
| `Packages/`, `ProjectSettings/` | packages and project settings | shared |
| `Docs/` | the original design spec, port notes, progress log, credits, screenshots | VR Rocket |
| `specs/` | these specs | everyone |

Never committed (ignored by `.gitignore`): `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/`, `*.apk`. Also keep out: `.utmp/` (left by Android builds) and `Assets/InitTestScene*.unity` (left by the Test Runner when a run is interrupted).

The repository does not use Git LFS: binary files are committed directly, so keep large files (videos, builds, raw audio) out unless they are needed.

**Moving files:** always move or rename assets inside Unity's Project window (or with `AssetDatabase.MoveAsset`), never in Explorer, so each file keeps its `.meta` and GUID and nothing loses its references. Paths written in code must then be updated: they are listed in section 2.

## 2. Where VR Rocket's files are

```
Assets
├─ Scenes/                  room.unity (room team), VRRocket_Main.unity (the game),
│                           Dev_Interactions.unity (test room), VRRocketMain.lighting (lighting settings of VRRocket_Main)
├─ Scripts/
│  ├─ Runtime/              game code (assembly VRRocket.Runtime)
│  │  └─ Room/              the room connection: loader, bindings, rig adapter, intro bridge, window opening
│  ├─ Editor/               Editor tools (assembly VRRocket.Editor)
│  └─ Tests/Editor, Tests/PlayMode    the tests (spec 10)
├─ Prefabs/
│  ├─ Rocket/               the six parts, RocketWorkstation, AssembledRocket_Static
│  ├─ Launch/               LaunchVehicle (36x), Explosion
│  ├─ Console/              PushButton (spec 07)
│  └─ Stubs/                MissionControlStubs, StubButton (only in Dev_Interactions)
├─ Models/                  hq.fbx (room team)
│  ├─ Rocket/               BodyTube, TailFin, WingFlap, NoseCone, Motor, MotorCap (.fbx), attach_points.json,
│  │                        Generated/ (glow meshes for the attach points)
│  └─ Environment/          RightWall_Window.asset: the window wall mesh (generated, spec 01)
├─ Materials/               the room team's materials, plus:
│  ├─ Rocket/               Glow and the part colours (Rocket_Black, _Cardboard, _Orange, _Red, _White)
│  ├─ Launch/               Mat_Particle_* (flame, smoke, steam, sparks, explosion)
│  ├─ Screens/              Mat_LaunchScreen, Mat_PadFeed, Mat_ScreenBand, Mat_Diagram, Mat_Black, Mat_SmallScreen
│  └─ Environment/          console, window, help board and outside: Mat_AccentGreen, Mat_AccentRed,
│                           Mat_DarkMetal, Mat_Glass, Mat_Ground, Mat_LEDPanel, Mat_Whiteboard, Mat_PuzzleWire0..2
├─ Texture/                 the room team's, plus:
│  ├─ Particles/            Kenney particle sprites (CC0)
│  └─ Screens/              LaunchScreen_Placeholder.png, PadCameraFeed.renderTexture
├─ Audio/Kenney/            launch, bin and impact sounds (CC0)
├─ UI/                      Fonts (Orbitron, Share Tech Mono, with OFL licences), Sprites (button_green)
├─ Settings/                URP assets (shared), plus:
│  ├─ Rocket/               AssemblyTuning, FeedbackLibrary
│  └─ BuildProfiles/        VRRocket_Windows, VRRocket_Quest
└─ ThirdParty/XRI_Examples/ the parts of Unity's XRI examples we use: XRPushButton, its model,
                            materials, affordance themes and button sounds (Unity Companion License)
```

**Paths written in code** (update them if you move these files):

| Path | Used in |
|---|---|
| `Assets/Scenes/room.unity` | `RoomSceneLoader` (Room Scene Path field on `RoomLink`) and both build profiles |
| `Assets/Scenes/VRRocket_Main.unity`, `Assets/Scenes/Dev_Interactions.unity` | the build profiles; the play-mode tests (`DevSceneTestBase`, `GameFlowTests`, `RoomIntegrationTests`) |
| `Assets/Models/Rocket/attach_points.json` | `AttachPointGenerator` |
| `Assets/Models/Environment/RightWall_Window.asset`, `Assets/Materials/Environment/Mat_Glass.mat`, `Mat_DarkMetal.mat` | `WindowWallBuilder` |

Everything else is referenced by GUID and survives moves done inside Unity.

Where each third-party asset came from, and its licence: `Docs/ASSET_CREDITS.md`.

## 3. Assemblies

Our code is split into assembly definitions (`.asmdef`), so it compiles separately from the room's scripts:

| Assembly | Folder | References | Notes |
|---|---|---|---|
| `VRRocket.Runtime` | `Scripts/Runtime` | XRI, XR Core Utils, Input System, TextMeshPro, UGUI | namespace `VRRocket`; auto-referenced, so scripts without an asmdef can use it |
| `VRRocket.Editor` | `Scripts/Editor` | `VRRocket.Runtime` | Editor only; namespace `VRRocket.Editor` |
| `VRRocket.ThirdParty.XRIExamples` | `ThirdParty/XRI_Examples` | XRI, XR Core Utils, Input System | the push button (namespace `UnityEngine.XR.Content.Interaction`) |
| `VRRocket.Tests.Editor` | `Scripts/Tests/Editor` | runtime, editor, test framework | edit-mode tests |
| `VRRocket.Tests.PlayMode` | `Scripts/Tests/PlayMode` | runtime, XRI examples, UGUI, TMP, test framework | play-mode tests |

The room's scripts (for example `TransmissionController`) have no asmdef, so they compile into Unity's default `Assembly-CSharp`. That assembly can use ours, but ours cannot reference it: that is why `TransmissionBridge` finds the intro by name ([spec 01](01-scenes-and-room.md)).

## 4. Editor tools

| Where | Item | What it does | Spec |
|---|---|---|---|
| Menu bar | **VR Rocket > Generate Attach Points On Selected BodyTube** | (re)creates the 12 attach points from `attach_points.json` under the selected body tube | [03](03-rocket-assembly.md) |
| Menu bar | **VR Rocket > Capture Build Step Images** | renders the five build-step pictures into `Assets/Texture/BuildSteps` | [05](05-screens-and-displays.md) |
| Menu bar | **VR Rocket > Set Up Build Step Monitor** | adds the picture monitor beside the blueprint panel and wires it | [05](05-screens-and-displays.md) |
| Menu bar | **VR Rocket > Place Build Screens On Desk** | stands the blueprint panel and the picture monitor on the desk between the portraits' plaques | [05](05-screens-and-displays.md) |
| Menu bar | **VR Rocket > Rebuild Window Wall** | rebuilds `WindowWall` from `WindowOpening` and the room's wall (open `room.unity` additively first) | [01](01-scenes-and-room.md) |
| `RoomBindings` ⋮ menu | **Snap followers to the open room** | places our add-ons on the room's objects in the Editor | [01](01-scenes-and-room.md) |
| `RoomBindings` ⋮ menu | **Record check bounds from the open room** | accepts the desk's and floor's current place | [01](01-scenes-and-room.md) |
| `RoomBindings` ⋮ menu | **Re-record follow offsets from the current placement** | after moving one of our add-ons relative to the room | [01](01-scenes-and-room.md) |
| `LaunchScreenController` ⋮ menu | **Preview > Title, Assembly, Received, Pre-launch, Countdown, Flight, Result success, Result failure** | shows a screen stage in edit mode | [05](05-screens-and-displays.md) |
| Project window | **Create > VR Rocket > Assembly Tuning**, **Create > VR Rocket > Feedback Library** | new tuning or feedback assets | [03](03-rocket-assembly.md), [09](09-feedback-audio-haptics.md) |

## 5. Code conventions

These keep the code testable and easy to rewire. Please follow them in new VR Rocket code.

- **Logic in plain C# where possible.** The rules live in classes with no Unity objects (`AssemblyStateMachine`, `BuildReport`, `CapTwistLogic`, `GuideMath`, `RocketFlightModel`), tested in edit mode. Components only connect them to the scene.
- **Inspector wiring first, `Configure(...)` second.** Every component's references are serialized fields set in the Inspector. Each component also has a `Configure(...)` method that sets the same fields, for tests and tools.
- **Missing references are skipped, not fatal.** Components check for null before using a reference, so a module can be removed without breaking the others.
- **Events in pairs.** Important moments are both a C# event (for code) and a UnityEvent (for the Inspector): `StateChanged`/`onStateChanged`, `PhaseChanged`/`onPhaseChanged`.
- **Statics reset on Play.** The project enters Play mode without a domain reload, so every static field and static event is cleared in a method marked `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`. Today: `AssemblyEvents`, `AttachPoint`'s registry, `AssemblyFeedback.instance` and `RoomSceneLoader.instance`. `StaticResetsTests` checks them. Add any new static to that test.
- **Never edit the room.** Bind to it by path at runtime ([spec 01](01-scenes-and-room.md)).
- **Comments explain why**, not what.
- **One namespace:** `VRRocket` (editor code `VRRocket.Editor`, tests `VRRocket.Tests`).

## 6. Working together in Git

- **Branches:** the room team works on `main`. VR Rocket work happens on `interactions` and reaches `main` through pull requests.
- **Scenes:** one person edits a scene at a time; tell the team before you open `VRRocket_Main` for editing. Prefer putting new things in prefabs, which merge more safely.
- **Before you push:** run both test suites ([spec 10](10-testing-and-debugging.md)), and check `ConsoleAddOn` and `WindowWall` are saved switched off.
- **Noise you should not commit:** the two TextMesh Pro font assets in `Assets/UI/Fonts` change on disk after Play (their character atlases grow); render textures and URP assets sometimes re-serialize when opened. Commit them only when you meant to change them.
- **Commit messages:** say what changed and why, in plain words.

## 7. Documentation

- Change the code, update the spec: when something in these specs stops being true, fix it in the same commit.
- `Docs/PROGRESS.md` gets one dated entry per milestone.
- New third-party assets go into `Docs/ASSET_CREDITS.md` with their source and licence.
