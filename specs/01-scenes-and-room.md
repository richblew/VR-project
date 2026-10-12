# 01 Scenes and the room

How our scene (`VRRocket_Main`) and the room team's scene (`room.unity`) run together, how our systems find the room's objects, and what to do when the room changes. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Main` → `RoomLink` (also `WindowWall`, `Sun`, `RoomFit`) |
| **Scripts** | `Assets/Scripts/Runtime/Room/`: `RoomSceneLoader.cs`, `RoomBindings.cs`, `XRRigAdapter.cs`, `TransmissionBridge.cs`, `WindowOpening.cs`. Editor tool: `Assets/Scripts/Editor/WindowWallBuilder.cs` |
| **Assets** | `Assets/Models/Environment/RightWall_Window.asset` (window wall mesh), `Assets/Materials/Environment/`, `Assets/Scenes/VRRocketMain.lighting` (lighting settings) |
| **Tests** | `RoomIntegrationTests` (8 play-mode tests), see [spec 10](10-testing-and-debugging.md) |

## 1. Two scenes, one game

| Scene | Owner | What is in it |
|---|---|---|
| `Assets/Scenes/VRRocket_Main.unity` | VR Rocket (Praise) | Every VR Rocket system: game flow, menu, rocket kit, console add-on, screens, launch site, window wall |
| `Assets/Scenes/room.unity` | Room team | The room: walls, floor, TVs, console `hq`, assembly desk, the `Player` XR rig, the intro (`TransmissionController`) |

**Why two scenes?** The room changes almost every day. If our systems lived inside `room.unity`, every change on either side would be a scene merge conflict. Instead, our scene loads theirs at runtime and connects to it by name. Nothing we do ever saves into `room.unity`.

**How to play it in the Editor:** open `VRRocket_Main` and press Play. The room appears a moment later.

**How to see everything in place while editing:**

1. Open `VRRocket_Main`.
2. In the Project window, right-click `Assets/Scenes/room.unity` > **Open Scene Additive** (or drag it into the Hierarchy).
3. Keep `VRRocket_Main` as the **active** scene (its name is bold in the Hierarchy; right-click > Set Active Scene if not).
4. When you are done, close `room.unity` **without saving** it (unless you are on the room team and meant to change it).

Pressing Play with both scenes open works too: the loader sees the room is already loaded and does not load it twice.

**In builds:** the build profiles list `VRRocket_Main` first and `room.unity` second (see [spec 11](11-builds-and-deploy.md)). The global scene list under Build Settings is not used.

## 2. What happens when you press Play

1. `RoomSceneLoader.Awake` registers itself (`RoomSceneLoader.instance`). It has `[DefaultExecutionOrder(-1000)]`, so it runs before our other scripts.
2. `RoomSceneLoader.Start` checks whether `room.unity` is already loaded. If not, it loads it additively:
   - in the Editor with `EditorSceneManager.LoadSceneAsyncInPlayMode`, straight from the asset path, so the room does not need to be in any build list;
   - in a build with `SceneManager.LoadSceneAsync`, which needs the room in the build profile's scene list.
3. When the room has loaded, the loader makes `VRRocket_Main` the active scene again (so **our** lighting settings apply, see section 6), sets `isRoomLoaded`, and fires `RoomLoaded` once.
4. Everything that waited for the room now runs. Each one registered with `RoomSceneLoader.WhenLoaded(...)`:
   - `RoomBindings.Apply`: swaps, follows, checks (section 4);
   - `XRRigAdapter.Apply`: adapts the room's `Player` rig ([spec 08](08-xr-rig-and-input.md));
   - `TransmissionBridge.FindIn`: finds the room's intro controller (section 5).

The game flow starts in the menu straight away; it does not wait for the room.

## 3. `RoomSceneLoader`

On `RoomLink`.

| Field | Value | Meaning |
|---|---|---|
| Room Scene Path | `Assets/Scenes/room.unity` | The room scene. If it ever moves, change this (and the build profiles). |
| Make This Scene Active | on | Makes `VRRocket_Main` the active scene after loading, so its lighting and sky are used. |

Useful from code:

- `RoomSceneLoader.instance`: the loader of the running game.
- `isRoomLoaded`, `roomScene`, `RoomLoaded` (event, fires once).
- `WhenLoaded(callback)`: runs the callback now if the room is loaded, or as soon as it is. Use this for anything new that needs a room object.
- `RoomSceneLoader.FindByPath(scene, "Environment/TV/TV top left")`: finds a GameObject by its Hierarchy path, including inactive ones. A path segment ending in `*` matches the first child whose name **starts with** the rest. This is used for the big TV, whose real name is `TV LED 30` followed by a special character that does not survive copy and paste.

If the room cannot be loaded, the Console shows `RoomSceneLoader: could not load '...'. Add it to the build profile's scene list.`

## 4. `RoomBindings`: connecting to the room by name

On `RoomLink`. It never changes `room.unity` on disk. At runtime it does four kinds of things, all by Hierarchy path:

- **Swap:** switch one of their objects off and our replacement on, but only if their object still has the size and position our replacement was built for.
- **Follow:** place one of our objects relative to one of theirs, using an offset recorded in the Editor. If they move their TV, our overlay moves with it.
- **Check:** compare one of their objects with the bounds it had when our layout was made, and warn if it moved. Used for things we sit on but cannot follow (the desk under the rocket kit).
- **Teleport floors:** add a `TeleportationArea` to listed room floors. (The list is empty today. We use our own teleport boxes under `RoomFit/TeleportFloor` instead; see [spec 08](08-xr-rig-and-input.md).)

Every problem becomes a yellow Console warning starting with `RoomBindings:` and adds one to `warningCount`. Zero warnings means the room matches our layout. A play-mode test (`Bindings_Match_The_Room`) fails if there is any warning.

### What is configured today

**Swaps**

| Room path | Our replacement | Expected bounds of their object |
|---|---|---|
| `Environment/Floorplan/Right wall` | `WindowWall` | x 3.9 to 4.1, y 0 to 4, z −11.91 to 3.91 (since the room was shortened on 12 October 2026) |

**Follows**

| Room path | Our follower | Notes |
|---|---|---|
| `Environment/TV/TV LED 30*` (the big TV) | `LaunchScreen` | our overlay sits just in front of their screen |
| `Environment/TV/TV top left` | `CountdownClock` | the small TV top right of the big TV, as you look at it |
| `Environment/Mission Control Desk/hq` (the console) | `ConsoleAddOn` | **Activate After Placing** is on (see below) |

**Checks**

| Room path | Expected bounds | Warning text if it moved |
|---|---|---|
| `Environment/AssmblyArea/DeskTop` | centre (0.089, 1.05, 3.16), extents (1.8, 0.075, 0.75): desk top at y 1.125 | "the rocket kit no longer lies on the desk." |
| `Environment/Floorplan/Floor` | centre (0, 0, −4), extents (4, 0.05, 8) | "the teleport floor no longer matches the room floor." |

**Tolerance:** 0.02 m. Each corner of the bounds box may drift that much before a swap is refused or a check warns.

### What happens when something does not match

| Situation | Result |
|---|---|
| A swap's room object is missing (renamed, moved in the Hierarchy, deleted) | Warning. Our replacement stays **off** and their object is left alone, so there are never two walls. |
| A swap's room object moved or changed size | Warning. Theirs is kept, ours stays off. |
| A follow's room object is missing | Warning. Our follower keeps the pose saved in `VRRocket_Main`. It is still switched on if it has Activate After Placing. |
| A checked room object moved | Warning only. Nothing is moved. |

### Why `ConsoleAddOn` and `WindowWall` are saved switched off

- **`WindowWall`** must only appear when their wall is exactly where ours expects it. `RoomBindings` switches it on after the check passes.
- **`ConsoleAddOn`** holds the XRI push buttons, which remember their world position in `Start`. If the add-on were active while being moved onto the console, the buttons would remember the wrong place and refuse to press. So it is saved off, placed first, then switched on (**Activate After Placing**).

If you edit either one, switch it off again before you save the scene.

### Editor tools on `RoomBindings`

Open `room.unity` additively first (section 1). Then click the **⋮** menu (or right-click the header) of the `RoomBindings` component on `RoomLink`:

| Menu item | When to use it |
|---|---|
| **Snap followers to the open room** | The room team moved their TV, corner TV or console, and you want to see our add-ons in the right place in the Editor. (At runtime they follow by themselves.) Save `VRRocket_Main` afterwards. |
| **Record check bounds from the open room** | The desk or floor moved on purpose. First make sure the rocket kit still lies on the desk and the teleport boxes still sit on the floor, then run this and save `VRRocket_Main`. |
| **Re-record follow offsets from the current placement** | Only when you deliberately moved one of **our** followers relative to their object (for example, nudged the overlay on the TV). |

There is no menu item for swap bounds: **VR Rocket > Rebuild Window Wall** records them (section 7).

### How to add a new binding

1. Put your object in `VRRocket_Main`, placed where it should be relative to the room object (open the room additively to place it).
2. On `RoomBindings`, add an element to **Follows**: type the room path (copy names exactly from the Hierarchy, `/` between levels), drag your object into **Follower**.
3. Run **Re-record follow offsets from the current placement**. This records the offset and sets **Recorded**.
4. Tick **Activate After Placing** if your object (or a child) caches its world position in `Start`, and save it switched off.
5. Save `VRRocket_Main` and run `RoomIntegrationTests`.

## 5. `TransmissionBridge`: the room's intro under our menu

On `RoomLink`. The room's intro is `TransmissionController` in `room.unity`: NASA logo, standby video, announcement audio, red alert lights, dimmed room, transmission video. It plays while our start menu is up.

When the game leaves the Menu phase (the player pressed START), the bridge stops the intro cleanly:

1. stops every coroutine on the controller;
2. stops its `standbyPlayer` and `videoPlayer` video players;
3. stops its `announcementAudio` and switches that AudioSource off;
4. switches the controller off. Its own `OnDisable` restores the alert light colours and the room's light, ambient and reflection levels;
5. puts `nasaLogoMaterial` back on `mainScreen` (at `screenMaterialIndex`), so the big TV shows the logo again.

**Why find it by name?** The room's scripts live in Unity's default assembly (`Assembly-CSharp`), which our assembly definition cannot reference. So the bridge looks for a component whose type name is `TransmissionController` and reads the public fields listed above by name.

**Room team, please note:** if you rename `TransmissionController` or any of those six public fields, the bridge cannot find them. It then silently skips that step. Tell whoever works on `VRRocket_Main`, or update **Controller Type Name** on the bridge. If the room has no intro at all, the menu simply has no intro.

| Field | Value |
|---|---|
| Flow | `GameFlow` (`GameFlowController`) |
| Loader | `RoomLink` (`RoomSceneLoader`) |
| Controller Type Name | `TransmissionController` |
| Stop When Game Starts | on |

## 6. Lighting and the sky

Unity uses the lighting settings of the **active** scene for everything that is loaded. At runtime that is `VRRocket_Main`. So:

- `VRRocket_Main` copies the room's look: the same procedural **Default-Skybox**, ambient from the skybox (intensity 1), and Unity's built-in default lighting data. Without this the room's interior goes dark.
- Its lighting settings asset is `Assets/Scenes/VRRocketMain.lighting`, with baked and realtime global illumination off. Pressing Generate Lighting by accident only refreshes the environment lighting.
- **If the room team changes the sky or ambient light in `room.unity`, it will not show in the game.** Make the same change in `VRRocket_Main` (Window > Rendering > Lighting, Environment tab, with `VRRocket_Main` active).
- `Sun` is the only light we add: a realtime directional light, rotation (40, 100, 0), colour (1, 0.96, 0.9), intensity 1.3, soft shadows. It lights the outside and the pad camera's view. The room's roof and walls keep it out of the room.

## 7. The window wall

So the player can watch the launch, our scene replaces the room's right wall (x = 4, the player's left at the start) with a copy that has a window in it.

**In the Hierarchy:** `WindowWall` (saved off), with the `WindowOpening` component and these children:

| Child | What it is |
|---|---|
| `Wall` | A mesh of their wall with a hole (`Assets/Models/Environment/RightWall_Window.asset`), in their wall material. Its UVs continue their cube's mapping, so the wood grain lines up. Four box colliders for the solid parts. |
| `Glass` | `Mat_Glass` quad with a box collider, so nothing falls out |
| `Frame_Head`, `Frame_Sill`, `Frame_JambFront`, `Frame_JambBack` | dark metal frame (`Mat_DarkMetal`) |
| `Mullion` (×2) | vertical bars at z −4.6 and −3.2, kept off the sightline from the console to the pad |
| `InnerSill` | a ledge inside, with a collider |

**`WindowOpening` fields:**

| Field | Value | Meaning |
|---|---|---|
| Room Wall Path | `Environment/Floorplan/Right wall` | the wall to copy and replace |
| Z Range | −6.0 to −0.5 | where the opening runs along the wall (world z) |
| Y Range | 0.9 to 3.3 | sill to head height (world y) |
| Mullion Z | −4.6, −3.2 | where the vertical bars go |

**To change the opening, or after the room team changed their wall:**

1. Edit the fields on `WindowOpening`.
2. Open `room.unity` additively.
3. Run **VR Rocket > Rebuild Window Wall**. It deletes the children of `WindowWall`, rebuilds them from their wall and your fields, and records the new expected bounds on `RoomBindings`.
4. Switch `WindowWall` off again if it is on, and save `VRRocket_Main` (not `room.unity`).

If the room team ever cuts a real window into their wall, delete `WindowWall` and its swap entry on `RoomBindings`.

## 8. Other objects on the room

- `RoomFit/TeleportFloor`: six thin boxes with `TeleportationArea`, covering the room's floor but not the console or desk. See [spec 08](08-xr-rig-and-input.md).
- `Outside`: ground (300 × 300 m), the launch pad disc and the gantry tower. See [spec 04](04-launch-simulator.md).
- Exact positions of every module are listed in `Docs/ENVIRONMENT.md`.

## 9. The room objects we depend on

Keep these paths and roughly these positions, or tell whoever works on `VRRocket_Main`:

| Path in `room.unity` | Used for | If it changes |
|---|---|---|
| `Player` (XR Origin from the XRI Starter Assets) | the player rig | adapted wherever it is ([spec 08](08-xr-rig-and-input.md)) |
| `Environment/Floorplan/Right wall` | replaced by `WindowWall` | theirs stays, ours stays off, warning |
| `Environment/TV/TV LED 30*` | `LaunchScreen` follows it | overlay keeps its saved pose, warning |
| `Environment/TV/TV top left` | `CountdownClock` follows it | same |
| `Environment/Mission Control Desk/hq` | `ConsoleAddOn` follows it | same (and still switched on) |
| `Environment/AssmblyArea/DeskTop` | the rocket kit lies on it | warning; the kit does not move |
| `Environment/Floorplan/Floor` | the teleport boxes sit on it | warning |
| a `TransmissionController` component, anywhere | the intro | no intro, no error |
| `Incoming_Transmission`, `Transmission_Video`, `stand_by` (root objects of the intro) | only the intro test (`Start_Ends_The_Intro_And_Switches_The_Mission_Displays_On`) | that test fails; update its names |
| `Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/DemoAssets/Audio/Button Pop.wav` | the respawn "pop" sound | the pop goes silent |

## 10. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `RoomBindings: the room has no '...'` | A room object was renamed or moved in the Hierarchy. Fix the path in `RoomBindings` (or ask the room team to restore the name). |
| `RoomBindings: '...' is at ... not ...; keeping it and leaving WindowWall off` | Their right wall moved or changed size. Rebuild the window wall (section 7). |
| `RoomBindings: '...' moved to ...; the rocket kit no longer lies on the desk.` | Their desk moved. Move the `RocketWorkstation` onto the desk top, then **Record check bounds from the open room**. |
| Two walls, z-fighting, or no window | `WindowWall` was saved on, or the swap's room path is wrong. Save `WindowWall` off. |
| The room is dark or the sky is grey | `VRRocket_Main`'s lighting settings changed, or `room.unity` became the active scene. Check section 6. |
| The intro keeps playing after START | The bridge could not find the controller or one of its fields was renamed (section 5). `Start_Ends_The_Intro_And_Switches_The_Mission_Displays_On` covers this. |
| The room loads twice | Should not happen: the loader checks first. If you load the room yourself from a new script, use `RoomSceneLoader.WhenLoaded` instead. |
