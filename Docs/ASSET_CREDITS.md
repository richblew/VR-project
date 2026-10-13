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
| UI Pack - Sci-Fi (space expansion) | Kenney | https://kenney.nl/assets/ui-pack-sci-fi | CC0 | 2026-10-09 | START button graphic (`Assets/UI/Sprites/button_green.png`) |
| Sci-Fi Sounds | Kenney | https://kenney.nl/assets/sci-fi-sounds | CC0 | 2026-10-09 | engine loop, ignition, explosions, bin doors (`Assets/Audio/Kenney`) |
| Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 | 2026-10-09 | part impacts on the desk and on each other (`Assets/Audio/Kenney`) |
| Orbitron (variable) | Matt McInerney | https://github.com/google/fonts/tree/main/ofl/orbitron | SIL Open Font License 1.1 | 2026-10-09 | menu and screen titles |
| Share Tech Mono | Carrois Apostrophe | https://github.com/google/fonts/tree/main/ofl/sharetechmono | SIL Open Font License 1.1 | 2026-10-09 | telemetry, status and body text |

| Rough Linen | colormass (Poly Haven) | https://polyhaven.com/a/rough_linen | CC0 | 2026-10-13 | the window curtain in `VRRocket_Final` (`Assets/Texture/Curtain`, diffuse and normal maps at 1k) |

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
