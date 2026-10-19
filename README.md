# VR Rocket

**Assemble. Launch. Be Mission Control.**

*Everyone wants to be an astronaut. Only the cool ones build the rocket.*

VR Rocket is a virtual reality experience set inside a NASA-style mission control room. You can choose to be briefed by an incoming transmission or assemble a hand-built model rocket. To launch the rocket from the console, a puzzle must be done and the rocket can then be submitted and launched/aborted.

Built for **UCT VR 2026**.

---
# Setup
This game requires the user to use a VR that tracks movement. An example would be the HTC Vive gear with the base stations. Additionally, we recommend that the room be cleared of any obstacles. We also limit the user to a walking area of 2m x 2m. There is no teleportation in this game.

Must do
- OpenXR needs to have its render mode set to Multi-pass
- Run it in the unity IDE
- Use scene VRRocket_Final

# Tutorial

The user loads into the game. They are presented with a menu that presents them with two options. Either they can 1. watch the intro video ("Brief me") or 2. build the rocket. 

Option 1: the room plays a siren and the lights flash red. The nasa screen changes to a signal incoming. After this a video plays, curtains draw closed and lights dim. After the video has played the user can start building their rocket. During this time, the user will be able to complete the cable puzzle, but not build the rocket.

Option 2: lets the user build the rocket immediately. They can either start with the puzzle first or the rocket. If they choose to assemble the rocket, they can follow the prompts on the monitor that indicate what should be done and if parts fall to the ground or to the side of the desk where they cannot reach, they are respawned back onto the table. If they choose to do the puzzle, the next step has been completed.

After the rocket has been assembled. The user is required to submit it into the bin next to the console. If they have not done the puzzle yet, the submission button does not work and the console remains offline. Once the puzzle is complete the button works and the rocket can be submitted. 

Once the rocket has been submitted correctly, a full scale version of the rocket is shown outside the window. The user can then proceed to launch or abort the rocket launch. 

If launch is pressed. The countdown begins and the user can see if it takes off successfully. If not, the rocket respawns back on the side table and the user has to rebuild a new rocket using thier failed one as a reference.

If abort is pressed, an abort alarm goes off and the rocket is spawned back on the assembly table.

# Interations
The controllers - The trigger button allows the user to pick up items and to close their hand into a fist
Buttons in the scene - can be pressed by moving the controller over the button and moving their controller down
Parts and plugs - can be picked up using the trigger button

---
Contributions

Richard
- Built enviroment 
- Menu/colour
- Cubes used to build the room
- TV screens 
- Nasa HQ desk
- Video and light implementation
- posters on wall with plaques
- desks
- readme

Yi-Xuan
- Hand model 
- Hand interactions
- Button interactions
- Virtual room link to physical room
- Puzzle box
- Cables and connections for sockets
- Respawn for parts and puzzle
- Readme

Praise

Toolkits used
- OpenXR
- XR interactions toolkit
- youtube for videos and audio
- TV imports from free assets
- Puzzle and sockets are from sketchfab
- Hand model from the Unity Asset store
- Particle simulator (PRAISE)
