# 11 Builds and deploying

How to build the game for PC VR and for a standalone Meta Quest 3, how to install it on the headset, and the project settings that matter for VR. Back to the [main spec](README.md).

| | |
|---|---|
| **Build profiles** | `Assets/Settings/BuildProfiles/VRRocket_Windows.asset` (PC VR), `VRRocket_Quest.asset` (Quest 3, Android) |
| **Where in the Editor** | File > Build Profiles |
| **Output** | `Builds/` in the project folder (ignored by git): `Builds/VRRocket_Win64/VRRocket.exe`, `Builds/VRRocket_Quest.apk` |
| **Unity** | 6000.6.2f1, with the Android Build Support module (Android SDK, NDK and OpenJDK) for Quest |

## 1. The build profiles

Unity 6 uses **build profiles**: each one is an asset with its own platform, settings and scene list. Ours are in `Assets/Settings/BuildProfiles/`.

| Profile | Platform | Scenes, in order |
|---|---|---|
| `VRRocket_Windows` | Windows (Standalone) | `Assets/Scenes/VRRocket_Final.unity` |
| `VRRocket_Quest` | Android | the same |

Both override the global scene list, which is left as the room team had it. Since 13 October 2026 they build only `VRRocket_Final`, which holds the room itself ([spec 13](13-final-scene.md)). To build the older `VRRocket_Main` instead, list it first and `room.unity` second: that scene loads the room at runtime, and a build can only load scenes it contains ([spec 01](01-scenes-and-room.md)).

## 2. Building for PC VR (Windows)

1. File > **Build Profiles**.
2. Select **VRRocket_Windows** and click **Switch Profile** (if it is not already active).
3. Click **Build**, choose a folder (we use `Builds/VRRocket_Win64/`).
4. Start the headset's PC runtime, then run the `.exe`:
   - **HTC Vive or Valve Index:** start SteamVR, and make it the OpenXR runtime (SteamVR > Settings > OpenXR > Set SteamVR as OpenXR Runtime).
   - **Meta Quest over Link or Air Link:** start the Meta Quest Link app and set it as the active OpenXR runtime (in its Settings > General). Virtual Desktop also works.

**Play in the Editor with a PC headset:** the same runtime choice applies. With the Windows profile active and the headset connected, press Play in `VRRocket_Final`.

## 3. Building for Quest 3 (Android)

**First time only:**

- Put the headset in **developer mode** (in the Meta Horizon phone app: Devices > your headset > Headset settings > Developer mode).
- Connect it with a USB-C cable and accept **Allow USB debugging** inside the headset.

**Build:**

1. File > **Build Profiles**, select **VRRocket_Quest**, **Switch Profile**. The first switch re-imports assets for Android and takes a while.
2. Either:
   - **Build And Run** with the headset connected: Unity builds, installs and starts it; or
   - **Build** to `Builds/VRRocket_Quest.apk` and install it yourself (below).

The APK is about 61 MB, built with IL2CPP for ARM64, minimum Android API level 26.

**Install and start from the command line.** `adb` comes with Unity's Android module:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" devices
"...\adb.exe" install -r Builds\VRRocket_Quest.apk
"...\adb.exe" shell am start -n com.UnityTechnologies.com.unity.template.urpblank/com.unity3d.player.UnityPlayerGameActivity
```

In the headset, the app is in the Library under **Unknown Sources**, named **VRProject**.

If the game does not start and the headset shows a "controllers required" message, wake the controllers (press a button) and start it again.

**Logs from the headset:** `adb logcat -s Unity` shows Unity's log while the game runs.

## 4. App identity (still the template defaults)

| Setting (Edit > Project Settings > Player) | Value today |
|---|---|
| Product Name | VRProject |
| Company Name | DefaultCompany |
| Version | 0.1.0 |
| Package name (Android) | `com.UnityTechnologies.com.unity.template.urpblank` |

These came with the project template and are the team's to decide. Change the package name before installing next to other builds made from the same template, or they replace each other.

## 5. Settings that matter for VR

**XR (Edit > Project Settings > XR Plug-in Management):**

- OpenXR on both Windows and Android, with the controller profiles listed in [spec 08](08-xr-rig-and-input.md). Render mode: single pass instanced on both.
- Android also has **Meta Quest Support** and the latency setting the project validator asked for.
- **Project Validation** (same page) lists problems for the selected platform. Fix red errors before building.

**Rendering (URP).** Each platform uses its own quality level and URP asset:

| Platform | Quality level | URP asset | Renderer | MSAA | Render scale | SSAO |
|---|---|---|---|---|---|---|
| Windows | PC | `Assets/Settings/PC_RPAsset.asset` | `PC_Renderer` | 4× | 1.0 | on (intensity 0.4, radius 0.3, full resolution) |
| Android | Mobile | `Assets/Settings/Mobile_RPAsset.asset` | `Mobile_Renderer` | 4× | 0.8 | none |

- **MSAA 4×** is needed: without it the thin fins, screen bezels and screen text shimmer badly in a headset.
- **SSAO** (screen-space ambient occlusion) is only in `PC_Renderer`. It came with Unity's URP project template; it was not added for any headset. On PC VR it costs GPU time for both eyes at full resolution: if the frame rate in a Vive is too low, turn it off or set it to downsample (select `PC_Renderer` and look at the Screen Space Ambient Occlusion renderer feature). The OpenXR validator's SSAO warning on Android refers to this PC renderer and can be ignored for Quest.

**Play mode settings:** the project enters Play mode without reloading the domain ([spec 10](10-testing-and-debugging.md)). This does not affect builds.

## 6. Things to know

- Switching profiles only changes **your** active platform (Unity keeps that in the `Library` folder, which is not committed). The profile assets themselves are shared: change their settings only on purpose.
- An Android build leaves a `.utmp/` folder in the project root. It is temporary: do not commit it.
- The `Builds/` folder is ignored by git; share builds another way (for example a release on GitHub or a drive link).
- After switching platform, the first Play in the Editor recompiles shaders and can take a minute.

## 7. Troubleshooting

| Symptom | Check |
|---|---|
| The build shows only the sky or is empty | `room.unity` is missing from the profile's scene list (the Console in a development build shows `RoomSceneLoader: could not load`). |
| PC build: black screen or no tracking | The headset's runtime is not the active OpenXR runtime (section 2). |
| PC build: controllers do nothing | Their interaction profile must be enabled for Windows ([spec 08](08-xr-rig-and-input.md)). |
| `adb devices` lists nothing or "unauthorized" | Developer mode on, cable connected, and accept Allow USB debugging in the headset. |
| The APK will not install over an older one | Same package name signed by another computer's key: uninstall the old app first (`adb uninstall <package>`). |
| Low frame rate on Quest | Check the particle effects during launch, the pad camera, and the render scale (0.8). Use the OVR Metrics Tool or `adb logcat` to measure. |
