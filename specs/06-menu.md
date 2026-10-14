# 06 The start menu (styling guide)

Written for Richard, who styles the menu. It shows where every word of the menu is edited, how the menu is built, the exact values it uses, and recipes for restyling it without breaking the buttons. Back to the [main spec](README.md).

| | |
|---|---|
| **Where in the Editor** | `VRRocket_Final` → `GameFlow` → `MenuCanvas` |
| **Script** | `MenuPanel` on `MenuCanvas` (`Assets/Scripts/Runtime/MenuPanel.cs`): holds the menu's words for each state, writes them onto the canvas, shows and hides the menu |
| **Called by** | `GameFlowController` on `GameFlow` ([spec 02](02-game-engine.md)) |
| **Assets** | Fonts: `Assets/UI/Fonts/` (`Orbitron SDF.asset`, `ShareTechMono SDF.asset`). Sprites: `Assets/UI/Sprites/MenuButtonFrame.png` (made from Richard's `nasa_button.png`), `MenuRule.png`, `MenuGrid.png` |
| **Tests** | `FinalSceneTests.Menu_Offers_Start_With_Intro_And_Skip_Intro` (buttons wired, the first-play words on the canvas), `FinalSceneTests.Menu_Words_Are_Set_Per_State_On_The_Menu_Panel` (each state's words, and edits showing), `GameFlowTests` (the menu shows, hides and comes back with its after-success words) |

## 1. Editing the words (the important part)

**Every word on the menu is edited in one place: select `MenuCanvas` and look at its Menu Panel component.** Do not type into the texts on the canvas (`Title`, `Body`, the button labels...): the game writes them from the Menu Panel each time the menu opens, so anything typed there is replaced.

The menu has two states, each with its own words:

| State | When | Default words |
|---|---|---|
| **First Play** | the game starts | VR ROCKET · ASSEMBLE · LAUNCH · BE MISSION CONTROL · "Everyone wants to be an astronaut, But only cool people build rockets. Wanna be cool?" · BRIEF ME · BUILD ROCKET |
| **After Success** | the menu comes back after a successful flight | MISSION SUCCESS · FLIGHT COMPLETE · ALL STATIONS NOMINAL · "Your rocket flew clean. Think you can do it again?" · BRIEF ME · BUILD AGAIN |

Each state has these fields:

| Field | Where it shows |
|---|---|
| Title | the big red line |
| Kicker | the small line under the title |
| Body | the copy in the middle (several lines: press Enter for a new line) |
| Brief Button | the left button: plays the briefing (the room's intro), then building starts |
| Build Button | the right button: straight to building |
| Footer | the small line at the bottom |

Plus **No Briefing Button** ("START"): the left button's words in a room that has no intro to play. Then it starts building and the right button is hidden. (Not the case in `VRRocket_Final`.)

**Preview:** the two buttons at the top of the component (First Play, After Success) pick which state the canvas shows while you edit. Every change you type shows on the canvas straight away, so what you see in the Scene view is what plays. Leave it on First Play when you save.

Every text **auto-sizes**: longer words shrink to fit their box (down to a minimum size), so you can change words without re-laying-out the menu. If words get too small, make the box wider (section 5).

If a big screen shows during the menu (not in `VRRocket_Final`, where the room's intro TV is up), it repeats the state's Title and Kicker.

## 2. How the menu is built

```
GameFlow
└─ MenuCanvas        Canvas (World Space), CanvasScaler, TrackedDeviceGraphicRaycaster, CanvasGroup, MenuPanel
   ├─ Border         Image: the glowing blue edge (fills the whole canvas)
   ├─ Grid           Image (tiled): the faint HUD grid
   ├─ Panel          Image: the deep navy glass (4 units inside the border)
   ├─ AccentBar      Image (sliced): the blue rule with red-and-white stripes at its ends
   ├─ Title          TextMeshPro - Text (UI)    words from Menu Panel
   ├─ Kicker         TextMeshPro - Text (UI)    words from Menu Panel
   ├─ Body           TextMeshPro - Text (UI)    words from Menu Panel
   ├─ StartButton    Image (sliced NASA frame) + Button: On Click → GameFlowController.StartGame (the briefing)
   │  └─ Label       TextMeshPro - Text (UI)    words from Menu Panel (Brief Button)
   ├─ Footer         TextMeshPro - Text (UI)    words from Menu Panel
   └─ SkipIntroButton Image (sliced NASA frame) + Button: On Click → GameFlowController.StartWithoutIntro (build)
      └─ Label       TextMeshPro - Text (UI)    words from Menu Panel (Build Button)
```

UI draws in Hierarchy order: lower children draw on top of higher ones.

### Size and place

- `MenuCanvas` is **1000 × 650** units with scale **0.001**: **1.0 m × 0.65 m**. **One canvas unit is one millimetre**: a font size of 30 means letters about 30 mm tall.
- Pos (0.24, 1.36, 1.23), Rotation (20, 180, 0): over the console, facing the player (who starts at about (0, floor, 1.8) facing −Z), tilted back toward the eyes. Its lower edge hangs just above the console, which is fine because the console's buttons are only used once the menu has gone.
- It is fixed in the room; it does not follow the head.

## 3. Every element's style today

| Element | Rect (centre-anchored unless stated) | Style |
|---|---|---|
| `Border` | stretched to the canvas edges | colour (0.22, 0.56, 1), alpha 0.95 `#388FFF` |
| `Grid` | stretched, 12 units inside | `MenuGrid.png` tiled (Pixels Per Unit Multiplier 1.5), white at alpha 0.07 |
| `Panel` | stretched, 4 units inside | colour (0.035, 0.045, 0.11), alpha 0.97 `#090B1C` |
| `Title` | Pos Y 192, 900 × 110 | Orbitron SDF, auto size 44–86, **NASA red** (0.988, 0.239, 0.129) `#FC3D21`, character spacing 12 |
| `Kicker` | Pos Y 128, 900 × 40 | Share Tech Mono SDF, auto size 14–24, (0.64, 0.73, 0.87) `#A3BADE`, spacing 6 |
| `AccentBar` | Pos Y 92, 880 × 22 | `MenuRule.png`, Sliced (the stripes keep their size), white |
| `Body` | Pos Y 8, 840 × 132 | Share Tech Mono SDF, auto size 18–31, (0.86, 0.91, 0.98) `#DBE8FA`, line spacing 8 |
| `StartButton`, `SkipIntroButton` | Pos (−222, −150) and (222, −150), 404 × 112 | `MenuButtonFrame.png`, Sliced, Pixels Per Unit Multiplier 1.25. Button Color Tint: Normal (0.72, 0.8, 0.95) (dimmed), Highlighted white (full glow under the ray), Pressed (1, 0.62, 0.5) (a warm flash), Fade 0.08 s |
| `…/Label` | stretched over the button, 35 units in left and right, 17 top and bottom | Orbitron SDF Bold, auto size 16–30, (0.93, 0.97, 1), spacing 5 |
| `Footer` | Pos Y −272, 900 × 30 | Share Tech Mono SDF, auto size 12–18, (0.45, 0.56, 0.7) `#8FA6C9`, spacing 6 |

Only the two buttons' Images have **Raycast Target** on; every other image and every text has it off, so nothing blocks the ray. Keep it that way for anything you add.

**The sprites.**
- `MenuButtonFrame.png` (1075 × 151) is Richard's `nasa_button.png` (a glowing frame on black, 2172 × 724) with the black turned to transparency, cropped to the frame, edges feathered and halved in size. 9-slice borders 113 left and right (the stripes, rivets and chevrons keep their size), 45 top and bottom.
- `MenuRule.png` (512 × 24): a glowing blue line with red-and-white diagonal stripes at the ends; borders 48 left and right.
- `MenuGrid.png` (64 × 64): one line on two edges, tiled.

**The fonts** are dynamic TextMeshPro font assets: Unity may mark them changed after Play. Do not commit that unless you meant to edit the font.

## 4. What the code does at runtime

- `GameFlowController` calls `MenuPanel.Show(state, briefingAvailable)` when the menu opens: First Play at the start, After Success after a good flight. `Show` writes that state's words onto the canvas, then fades the `CanvasGroup` to 1 over 0.35 s.
- `Hide` (when a button is pressed) fades it to 0, turns off Interactable and Blocks Raycasts so the hidden buttons cannot be clicked, then switches `MenuCanvas` off.
- Nothing else in code sets menu text.

## 5. Recipes

### Change the words
Select `MenuCanvas` → Menu Panel → open First Play or After Success → type. Use Preview to see the other state.

### Change a colour
Select the element. Image: change **Color**. Text: change **Vertex Color** in the TextMeshPro component. A button's Normal colour comes from its Button component (Color Tint), not the Image.

### Make room for longer words
Select the text and widen its Rect Transform (Width). Auto size then keeps a bigger font. To stop shrinking, lower **Font Size Min** in the TextMeshPro component's Auto Size settings.

### Change a font
1. Copy the `.ttf` into `Assets/UI/Fonts/` with its licence.
2. Right-click it > **Create > TextMeshPro > Font Asset** (dynamic).
3. Drag the font asset into the text's **Font Asset**. Check the middle dot `·` renders.

### Change the button graphic
Import a PNG with transparency into `Assets/UI/Sprites/`, Texture Type **Sprite (2D and UI)**, open the **Sprite Editor** and drag the green border lines in to where the corners end, Apply. Drag it into both buttons' **Source Image**, Image Type **Sliced**. Adjust **Pixels Per Unit Multiplier** on the Image to make the corners bigger or smaller.

### Add a picture
Right-click `MenuCanvas` > **UI > Image**, set its Source Image, drag it below `Panel` in the Hierarchy, and **untick Raycast Target**.

### Add another button
Duplicate a button, move it, and set its **On Click ()**. Its label is not managed by Menu Panel, so type its words straight into its label.

### Move the menu
Change Pos X/Y/Z in `MenuCanvas`'s Rect Transform. Keep it in reach of the ray from the start position and tilted toward the eyes.

## 6. Check your work

1. **Both states:** Preview First Play and After Success and check the words fit.
2. **Press Play:** the menu appears with the First Play words; BRIEF ME plays the intro, BUILD ROCKET starts building.
3. **Run the tests:** Window > General > Test Runner > PlayMode, run `FinalSceneTests` and `GameFlowTests`.
4. **In the headset:** readable at arm's length, the buttons easy to hit, the highlight visible.
5. **Commit** the scene and any new assets (with `.meta` files), not the font assets' Play-mode changes.

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| My words are replaced in Play mode | Type them in the Menu Panel component (section 1), not on the canvas texts. |
| The ray does not highlight or click a button | The button's Image needs Raycast Target on, images over it need it off; `MenuCanvas` needs `TrackedDeviceGraphicRaycaster`, `GameFlow/EventSystem` needs `XRUIInputModule`. |
| A button clicks but nothing happens | Its **On Click ()** must call `GameFlowController.StartGame` (left) or `StartWithoutIntro` (right) on `GameFlow`. |
| A text is missing in Play mode | Its field in Menu Panel's Canvas section must point at it (Title, Kicker, Body, Button Label, Build Button, Build Button Label, Footer). |
| Squares instead of letters | The font has no such character. |
| The button frame looks stretched at the corners | Image Type must be Sliced and the sprite needs its borders. |
