# VR Rocket modules on the room

Every object `Assets/Scenes/VRRocket_Main.unity` adds on top of `Assets/Scenes/room.unity`. All positions are world space in the room's frame: +Y up, the big TV is at −Z, the assembly desk at +Z, and the window is in the room's "Right wall" at +X. Each module is one root object. Delete a root and that piece is gone, and nothing in the rocket breaks. (The game flow needs the console add-on and the launch site; see `Docs/INTEGRATION.md` for replacing them.)

The room's look is kept:

- The sky is the room's own procedural default sky.
- The lighting data is Unity's built-in default, the same as `room.unity`, so the interior has the ambient light it was designed under.
- The only light added is a sun for the outside. Their roof and walls keep it out of the room.
- No decorative props were added.

| Root | Where | What |
|---|---|---|
| `RoomLink` | origin | `RoomSceneLoader`, `RoomBindings`, `XRRigAdapter`, `TransmissionBridge` (see `Docs/PORTING.md`). |
| `XR Interaction Manager` | origin | The single interaction manager. |
| `GameFlow` | origin | Flow, big-screen driver, blueprint text, `PhaseDisplays`, EventSystem (XR UI input), and `MenuCanvas`: the start menu at (−0.40, 1.22, 0.10), 0.8 m in front of the spawn, tilted toward the eye and kept below the big TV's sightline. |
| `RocketWorkstation` | (−0.30, 1.125, 2.76) on `AssmblyArea/DeskTop` | The rocket kit, respawn pad and workstation bounds. The kit lies within x −0.85 to −0.15, z 2.61 to 3.06. Dropped parts come back to the pad (−0.05, 1.127, 2.61). |
| `BlueprintPanel` | (0.85, 1.75, 3.885) on the back wall, facing −Z | 0.9 × 0.6 m screen with the current build step. |
| `HelpWall` | (−3.88, 1.65, 0.80) on the left wall, facing +X | Build steps and controls on a white board. |
| `LaunchScreen` | (−0.010, 2.150, −11.343), yaw 180 | Overlay 6 mm in front of the big TV's screen, which is 4.364 × 3.149 m.<br>Contents: title band, big text with backdrop (countdown digits, result), status band (the first sentence of a hint), the failure diagram on the left, and the pad-camera feed as the surface during pre-launch, countdown and flight.<br>Off during the menu so the room's intro shows. Follows the TV. |
| `CountdownClock` | (−3.088, 2.963, −10.370), yaw 222 | Covers the screen of `TV top left` (1.587 × 1.353 m). Off during the menu; follows that TV. |
| `ConsoleAddOn` | on `Mission Control Desk/hq` | Saved inactive; `RoomBindings` places it on the console and switches it on. See the console add-on breakdown below. |
| `WindowWall` | x 3.9 to 4.1 | Replaces their right wall while it is still there with its original bounds (x 3.9 to 4.1, y 0 to 4, z −12 to 4). If it moved, changed size or is missing, theirs stays and ours stays off.<br>The wall is a mesh with a 5.5 × 2.4 m opening (z −6.0 to −0.5, y 0.9 to 3.3), in their own wall material with the UV mapping of their cube continued, so the grain lines up.<br>Also: dark metal frame, two mullions kept off the sightline to the pad, glass with a collider, and an inner sill. |
| `Outside` | | Ground 300 × 300 m (top at y −0.01, under their floor), the pad (a 9 m disc at (32, 0.15, −20)), and the gantry tower and arm. |
| `LaunchSite` | (32, 0.30, −20), yaw 180 | `LaunchSequence` with the 36× vehicle, flame, smoke, steam, audio and the pad camera.<br>The pad camera sits at (−23, 6.3, −55), so the room is out of frame. It renders to `PadCameraFeed` only from pre-launch on. |
| `Sun` | rotation (40, 100, 0) | Realtime directional light, soft shadows. It shines away from the window, lighting the pad side you see from the room and the pad camera. |
| `RoomFit` / `TeleportFloor` | floor top | Seven thin boxes with `TeleportationArea` (Teleport layer, directional), covering the floor but not the console or the desk footprint. Thumbstick or trackpad direction chooses the facing. |

**Console add-on breakdown**

| Part | Where | Notes |
|---|---|---|
| Control panel | root at (0.00, 0.72, −0.23), tilted 20° toward the player | The surface rises toward the console's monitors, so it faces the player; its back edge rests on the shelf. SUBMIT at x −0.55, LAUNCH at x 1.05, ABORT at x 1.40, buttons about 0.90 m high, each with its label on the panel in front of it. |
| Bin opening | (−1.05, 0.779, −0.30), 0.46 m square | A black quad on the shelf with a green rim and a shaft down to y 0.39 in the knee space.<br>The trigger is (−1.05, 0.67, −0.30), size 0.44 × 0.60 × 0.44; it counts as inside the workstation bounds.<br>The return spot for a failed prototype is (−1.62, 0.80, −0.30). |
| Console safe zone | centre (0.03, 0.97, −0.30), 4.0 × 0.4 × 0.6 | Parts left on the shelf stay there. |
| Invisible colliders | console body, the shelf around the bin opening, the keyboard | hq has no colliders of its own; these keep the player, the teleport ray and parts out of it. |
| Console mirror | on hq's centre monitor | Repeats the big screen: surface, title, clock and the full status text, at reading distance. Off during the menu. |

**New assets** used by the modules:

- `Meshes/RightWall_Window.asset`: the wall mesh. To change the opening, edit `WindowOpening` on `WindowWall`, open `room.unity` additively, run **VR Rocket > Rebuild Window Wall**, and save `VRRocket_Main`. It rebuilds the mesh, colliders, glass and frame and records the new expected bounds.
- `Assets/Materials/Screens/Mat_ScreenBand.mat` (translucent band behind screen text).
- `Assets/Materials/Environment/Mat_Whiteboard.mat`.
- `VRRocketMain.lighting`: baked and realtime GI both off, so an accidental Generate Lighting only refreshes the environment.
