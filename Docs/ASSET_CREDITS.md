# Asset credits

Every third-party asset that the VR Rocket port added to this project, and a placeholder for the room's own assets. Licence texts:

- CC0: https://creativecommons.org/publicdomain/zero/1.0/
- BSD 2-Clause: as noted.
- SIL OFL 1.1: `Assets/UI/Fonts/OFL-*.txt`.
- Unity Companion License: `Assets/ThirdParty/XRI_Examples/LICENSE.md`.

## Rocket

| Asset | Author | Source | Licence | Downloaded | Used for |
|---|---|---|---|---|---|
| Model Rocket Kit (STL) | ellipticaloptician | https://www.printables.com/model/157400-model-rocket-kit | BSD 2-Clause (credit required) | 2026-10-08 | TailFin, NoseCone and MotorCap meshes, cleaned up in Blender (the build script lives in the VR Rocket source repo, `SourceArt/VR_Rocket_Kit_Parts_v2/build_kit.py`) |

## Menu, screens, launch and sound

| Asset | Author | Source | Licence | Downloaded | Used for |
|---|---|---|---|---|---|
| Particle Pack 1.1 | Kenney (kenney.nl) | https://kenney.nl/assets/particle-pack | CC0 | 2026-10-09 | engine flame, smoke, sparks and explosion sprites (`Assets/Texture/Particles`, 7 sprites) |
| UI Pack - Sci-Fi (space expansion) | Kenney | https://kenney.nl/assets/ui-pack-sci-fi | CC0 | 2026-10-09 | the old START button graphic (`Assets/UI/Sprites/button_green.png`, no longer used by the menu) |
| NASA-style button frame | added by Richard, 2026-10-14 | source not recorded (Richard to fill in) | to be confirmed | 2026-10-14 | `Assets/UI/Sprites/nasa_button.png`; the menu buttons use `MenuButtonFrame.png`, made from it (black made transparent, cropped, halved). `MenuRule.png` and `MenuGrid.png` are drawn by our editor tooling (no third-party content) |
| Sci-Fi Sounds | Kenney | https://kenney.nl/assets/sci-fi-sounds | CC0 | 2026-10-09 | engine loop, ignition, explosions, bin doors (`Assets/Audio/Kenney`) |
| Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 | 2026-10-09 | part impacts on the desk and on each other (`Assets/Audio/Kenney`) |
| Orbitron (variable) | Matt McInerney | https://github.com/google/fonts/tree/main/ofl/orbitron | SIL Open Font License 1.1 | 2026-10-09 | menu and screen titles |
| Share Tech Mono | Carrois Apostrophe | https://github.com/google/fonts/tree/main/ofl/sharetechmono | SIL Open Font License 1.1 | 2026-10-09 | telemetry, status and body text |
| Rough Linen | colormass (Poly Haven) | https://polyhaven.com/a/rough_linen | CC0 | 2026-10-13 | the window curtain in `VRRocket_Final` (`Assets/Texture/Curtain`, diffuse and normal maps at 1k) |
| Plug Socket | Artik (@artikdev, Sketchfab) | https://sketchfab.com/3d-models/plug-socket-7141f3defd2f4796b956136e8ae710bf | CC BY 4.0 | 2026-10-13 | the wiring puzzle's sockets (`Assets/Models/Console/Console_Socket.fbx`, turned face up and normalised in Blender) |
| Power cable (high poly) | icvetlan (Sketchfab) | https://sketchfab.com/3d-models/power-cable-hight-poly-9b7fe4759a694c978a11c1ccbd64356f | CC BY 4.0 | 2026-10-13 | the wiring puzzle's plugs: the Schuko plug head only, reduced from 8.3k to 1.5k triangles (`Assets/Models/Console/Console_Plug.fbx`) |
| Handles | RuslanOz (@KarinaOz, Sketchfab) | https://sketchfab.com/3d-models/handles-ddd1c24620814c2eb022b97c90c7df2f | CC BY 4.0 | 2026-10-13 | the wiring hatch's grip: the bar pull only, reduced to 1.2k triangles (`Assets/Models/Console/Console_Grip.fbx`) |
| DiamondPlate008D | ambientCG | https://ambientcg.com/view?id=DiamondPlate008D | CC0 | 2026-10-13 | the wiring hatch's tread plate (`Assets/Texture/Console`) |
| Rubber004 | ambientCG | https://ambientcg.com/view?id=Rubber004 | CC0 | 2026-10-13 | the puzzle cables' rubber, tinted per pair (`Assets/Texture/Console`) |
| Simple AA Battery | MasterYogurt (Sketchfab) | https://sketchfab.com/3d-models/simple-aa-battery-5dcd884e27b642c8a7932a227d7c1b7a | CC BY 4.0 | 2026-10-13 | the rocket's battery (the kit part that was the motor), scaled to 24 x 70 mm, + end up (`Assets/Models/Rocket/Battery.fbx`, `Assets/Texture/Rocket/Battery_*.png`) |

Beeps, chimes, buzzes and the abort siren are generated in code (`ProceduralAudio`).

## Unity

| Asset | Author | Source | Licence | Used for |
|---|---|---|---|---|
| XR Interaction Toolkit Examples 3.4.0 (subset) | Unity Technologies | https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples | Unity Companion License | the console push buttons: `XRPushButton.cs`, `PushButton.fbx`, four materials, two affordance themes, three button sounds (`Assets/ThirdParty/XRI_Examples`) |
| TextMesh Pro (LiberationSans SDF) | Unity Technologies / Red Hat (font) | Unity package | Unity Companion License / SIL OFL (font) | console and help-board labels |

## Not carried over from the VR Rocket test project

The decorative Poly Haven props, surface textures and the Qwantani sky of the test project's greybox room were left out on purpose: the room's own look is kept. See the source repo's credits for those.

## The room's assets

Added by the room's owners. Their source and licence are theirs to record here:

- `Assets/Models/hq.fbx` (mission control console)
- the `Assets/TV LED 30*` model
- the NASA logo and TV images
- the "no signal" and transmission videos
- the announcement audio
- the wall and floor textures
