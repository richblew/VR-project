# Action items (12 October 2026)

From Praise's message to Richard and the follow-up chat. Owner in brackets; tick when done.

## Build guidance at the assembly desk

- [ ] **(Richard)** Review the blueprint screen above the workbench (`BlueprintPanel`, text per build stage) in the headset and say what to keep. Playtest feedback: the step text tells a first-time player what is expected of them.
- [x] **(Praise)** Monitor added beside the blueprint panel (`BuildStepMonitor`, spec 05). Richard: restyle or move it as you like. ~~Add a monitor to the work desk for build slides: a proper screen, copied from one of the smaller screens, replacing the current canvas-style panel.~~
- [x] Layout chosen for now: two screens side by side, the step text on the right, the picture on the left. ~~Decide the layout: two screens side by side (left: step text such as "Step 1: attach the flaps", right: an image of the rocket at that step), or one screen with a slideshow.~~
- [x] **(Praise)** Images captured in `Assets/Texture/BuildSteps` (re-render with VR Rocket > Capture Build Step Images). Send them to Richard for the poster. ~~Capture images of the rocket model at each build stage, in a separate empty scene (not headset screenshots), and send them to Richard. Stages: bare tube, fins on, flaps on, nose on, motor in, cap on and locked.~~
- [ ] **(Richard)** Turn those images into an AI-made wall poster in the style of a Lego building guide, and hang it on the wall.
- [x] **(Praise)** The picture follows the build step. ~~Wire the step images into the screens so the image follows the current build state (the steps are in `BlueprintDisplay.ShowState`).~~
- [ ] **(Team, to try)** A pre-built reference rocket, for example a hologram the player can pick up and turn, next to the steps screen. Test it against the images, or combine both.
- Keep in mind: parts attach in any order (only the motor must be in before the cap can be twisted on). The steps are a guide, not a rule.

## Gameplay

- [x] **(Praise)** Only two rockets at a time: the one being built and the last failed one. Submitting a new rocket now removes the previous failed rocket (`RocketAssembly.DiscardInspectPrototype`).
- [ ] **(Yi-Xuan)** Submission mini game. The gate is prepared and commented out: search `TODO(Yi-Xuan)` in `Assets/Scripts/Runtime/GameFlowController.cs` and `SubmissionBin.cs`. Rules: solvable at any time in a play (even before building); SUBMIT refuses the rocket until it is solved; it stays solved after a failed launch and a new kit; a new play after a success needs it again. Uncomment the marked lines and call `GameFlowController.MiniGameSolved()` when it is solved.

## Meeting

- [ ] Environment review together tomorrow at 10:00.

## From the headset test (12 October 2026, evening)

- [x] Posters behind the build screens: screens moved onto the desk between the plaques.
- [x] Locomotion off: switched back on at runtime.
- [x] Player too high: rig capsule and floor fixed at runtime; confirm in the headset.
- [ ] **(Richard)** In `room.unity`, the `Player` rig has `Locomotion` saved off and its CharacterController height at 3 m: both are corrected by `XRRigAdapter` at runtime, but worth undoing in the scene too.
