# VR Rocket on the team's room

Instructions for Claude Code in this repository (the team's Unity project, github.com/richblew/VR-project). If a CLAUDE.md from a parent folder is also loaded, this file wins for anything in this repository.

UCT VR course project, team of three. The player hand-assembles a model rocket (body tube, 3 tail fins, 3 wing flaps, motor, motor cap, nose cone) on the assembly desk, submits it at the mission control console, and watches it launch outside the window. Wrong flap placement or an unlocked motor cap makes the launch fail. Praise owns the rocket and every assembly interaction; Richard and Yi-Xuan own the room, the puzzle, the launch screen and the final audio (`Docs/SPEC.md` section 0). Richard is restyling the start menu.

## Read first

- **`specs/README.md`**: how everything is set up, with links to 12 sub-specs (scenes and room, game engine, rocket assembly, launch simulator, screens, menu, console and bin, XR rig, feedback, testing, builds, project layout). Read the relevant sub-spec before changing a system.
- `Docs/SPEC.md`: the original design specification (requirements, the feedback table in section 6, acceptance tests in section 12).
- `Docs/INTEGRATION.md` (the rocket's public interface), `Docs/PORTING.md`, `Docs/ENVIRONMENT.md` (exact positions), `Docs/PROGRESS.md` (dated log), `Docs/ASSET_CREDITS.md`.
- When code and a spec disagree, the code wins: fix the spec in the same commit. Specs are written for teammates: plain English, where to find things in the Editor, exact values.

## Project facts

- Unity **6000.6.2f1** exactly, URP, XR Interaction Toolkit 3.6.1, OpenXR. Targets: PC VR (Windows: Vive and Index through SteamVR, Quest through Link) and Meta Quest 3 standalone (Android). Build profiles: `Assets/Settings/BuildProfiles/` (`VRRocket_Windows`, `VRRocket_Quest`).
- Entry scene `Assets/Scenes/VRRocket_Main.unity`. At runtime `RoomLink` loads `Assets/Scenes/room.unity` additively and binds to it by Hierarchy path (`RoomSceneLoader`, `RoomBindings`, `XRRigAdapter`, `TransmissionBridge`).
- The game engine is `GameFlowController` on `GameFlow`. The rocket's interface is `RocketAssembly` on the `RocketWorkstation` prefab root. The launch is `LaunchSequence` (scene) plus `RocketFlightModel` (plain C# physics) on `LaunchSite`.
- The project is organised by type (`Assets/Scenes`, `Assets/Scripts`, `Assets/Prefabs`, `Assets/Models`, `Assets/Materials`, `Assets/Texture`, `Assets/Audio`, `Assets/UI`, `Assets/Settings`); VR Rocket's files sit in named subfolders such as `Rocket/`, `Launch/`, `Screens/` (`specs/12-project-layout-and-tools.md` section 2). Move assets only inside Unity so GUIDs survive, and update the paths written in code (listed there). Code: `Assets/Scripts` (assemblies `VRRocket.Runtime`, `VRRocket.Editor`, `VRRocket.Tests.Editor`, `VRRocket.Tests.PlayMode`, `VRRocket.ThirdParty.XRIExamples`; namespace `VRRocket`). The room's scripts are in the default `Assembly-CSharp`, which our asmdefs cannot reference.
- `Dev_Interactions.unity` is a grey test room with only the rocket kit, used by most play-mode tests.

## Rules

1. Never edit or save `Assets/Scenes/room.unity` for VR Rocket work. Put things in `VRRocket_Main` or a prefab and bind to the room by path. The room paths we depend on are listed in `specs/01-scenes-and-room.md` section 9.
2. One person edits a scene at a time; scenes merge badly. Prefer prefabs.
3. `ConsoleAddOn` and `WindowWall` are saved switched off in `VRRocket_Main` on purpose (`RoomBindings` places and enables them). Keep them saved off.
4. Every assembly moment needs visual, audio and controller-vibration feedback (`Docs/SPEC.md` section 6, a marking requirement). New moments go through `AssemblyEvents` → `AssemblyFeedback` → `FeedbackLibrary.asset`.
5. Play mode runs without a domain reload. Every new static field or event must be reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method and added to `StaticResetsTests`.
6. Run both test suites (36 edit-mode, 26 play-mode) before pushing changes to VR Rocket's scripts, prefabs, scenes or settings, and report failures with their output. Haptics and feel can only be checked in a headset: say so instead of claiming them.
7. Branches: the room team works on `main`; VR Rocket work happens on `interactions` and reaches `main` by pull request. Ask before committing to, merging into or pushing `main`.
8. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/`, `.utmp/`, `Assets/InitTestScene*`, `.claude/settings.local.json` or `.claude/local/`. Do not commit incidental re-serialization noise (the two TextMesh Pro font assets in `Assets/UI/Fonts` after Play, render textures, URP assets, `ProjectSettings` you did not mean to change). This repo does not use Git LFS: keep large binaries out.
9. Credit every new third-party asset in `Docs/ASSET_CREDITS.md`. Add a dated entry to `Docs/PROGRESS.md` per milestone.

## Gotchas

- `XRPushButton` caches its world position in `Start`: that is why `ConsoleAddOn` is activated only after it is placed.
- In the Editor, `GetComponent<T>()` on a missing component returns a fake-null placeholder, so `?? AddComponent<T>()` never runs: use an explicit `!= null` check.
- A `NearFarInteractor`'s attach transform jumps to the far-cast hit point: measure hand distance from the interactor's own transform. `XRGrabInteractable` unparents an object while it is held.
- `VRRocket_Main` must stay the active scene at runtime: its lighting reuses the room's Default-Skybox and Unity's built-in lighting data, and the interior goes dark without them.
- Place a world-space canvas through its `RectTransform` (`anchoredPosition3D`), not `transform.localPosition` alone.
- Unity 6.6 turns `Object.GetInstanceID` into a compile error (CS0619): use `GetEntityId`.
- XRI auto-creates an interaction manager in `DontDestroyOnLoad` when a scene has none: every scene needs its own `XR Interaction Manager`.
- Known gap: with the free build order, a rocket with the motor and cap fitted counts as complete even with fins, flaps or the nose missing (`specs/03-rocket-assembly.md` section 6).
