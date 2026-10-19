# VR Rocket

**Assemble. Launch. Be Mission Control.**

*Everyone wants to be an astronaut. Only the cool ones build the rocket.*

VR Rocket is a virtual reality experience set inside a NASA-style mission control room. You can choose to be briefed by an incoming transmission or assemble a hand-built model rocket. To launch the rocket from the console, a puzzle must be done and the rocket can then be submitted and launched/aborted.

Built for **UCT VR 2026**.

---
# Setup
This game requires the user to use a VR that tracks movement. An example would be the HTC Vive gear and along side bases. Additionally, we recommend that the room be cleared of any obsticels. We also limit the user to a walking area of 2m x 2m. There is no teleportation in this game.

Must do
- OpenXR needs to have its render mode set to Multi-pass
- Run it in the unity IDE
- Use scene VRRocket_Final

# Tutorial

The user loads into the game. They are presented with a menu that presents them with two options. Either they can 1. watch the intro video or 2. build the rocket. 

Option 1: the room plays a siren and the lights flash red. The nasa screen changes to a signal incomming. After this a video plays, curtains draw closed and lights dim. After the video has played the user can start building their rocket.

Option 2: lets the user build the rocket. They can either start with the puzzle first or the rocket. If they choose to assemble the rocket, they can follow the promts on the monitor that indicate what should be done and if parts fall to the ground they are respawned back onto the table. If they choose to do the puzzle, the next step has been completed.

After the rocket has been assembled. The user is required to submit it into the bin next to the main desk. If they have not done the puzzle yet, the submission button does not work and the desk remains offline. Once the puzzle is complete the button works and the rocket can be submitted. 

Once the rocket has been submitted correctly. Outside the window a full scale version is shown. The user can then proceed to launch or abort the launch. 

If launch is pressed. The countdown begins and the user can see if it takes off successfully. If not the rocket respawns back on the table and the user has to rebuild a new rocket using thier failed one as a reference.

If abort is pressed, an abort alarm goes off and the rocket is spawned back on the assembly table.

# Interations
The controlers - The trigger button allows the user to pick up items 
Buttons - can be pressed 
Parts - can be picket up

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
Praise

Toolkits used
- OpenXR
- XR interactions toolkit
- youtube for videos and audio
- TV imports from free assets
- Puzzle and sockets are from sketchfab
- Particle simulator (PRAISE)