# Simulant · 仿生石

An ACT plugin for local FFXIV raid simulation using native in-game rendering.

## Features

- Local duty simulation

- Spawn local party members and enemies

- Native animation and visual effect control

- Timeline scripting for mechanics

- Player behavior validation

## Status

Work in progress. Collaboration is welcome.

<img width="2222" height="1170" alt="image" src="https://github.com/user-attachments/assets/87dc9104-2b5c-417f-8415-e783bc945b87" />


## Instructions

> [!TIP]
> **When downloading, get the latest released `dll` from the Release section on the right. Do not download the source code from the top of the page.**

- The plugin currently depends on the following ACT plugins:

  - FFXIV Parsing Plugin
  - [OverlayPlugin](https://github.com/OverlayPlugin/OverlayPlugin)
  - [Triggernometry](https://github.com/MnFeN/Triggernometry) (v2.0 or above)
  - [PostNamazu](https://github.com/Natsukage/PostNamazu)

  Installation instructions for the latter two can be found at [this link](https://www.bilibili.com/opus/998425290402168834).

  If you have already installed my Triggernometry bundle, you can skip this step.

> [!WARNING]
> This plugin is still under active development. It is currently only a demo for testing whether this idea is feasible.
> 
> It has no i18n support for now, and it relies on several tools that I personally maintain or that are mainly used in the Chinese community.
- Load this plugin's `dll` into ACT's plugin list to complete the setup. It can be placed fairly far down the list.

<img width="800" alt="image" src="https://github.com/user-attachments/assets/b2fde36d-dde1-47d1-9f7c-857ea61c9b9f" />

- After the plugin is loaded and while the game is running, click "Initialize Plugin".

> [!WARNING]
> This plugin has compatibility issues with some Dalamud plugins.
>
> If you encounter a game crash or an initialization error (such as a certain Ptr not being found during scanning), try a clean launch, or run only the Dalamud framework.

- Once initialization is complete, enter any inn map and check "Enable Firewall". This feature is similar to the Dalamud plugin [Hyperborea](https://github.com/kawaii/Hyperborea).

  This intercepts all incoming and outgoing packets except heartbeat packets, putting you into local mode.

  For now, do not interact with scene entities (such as the inn door) while in this mode, otherwise you will get stuck.

- Select a map on the left, or enter a map ID. If the map has phases/simulations available, you can pick one from the dropdown list.

  To filter for the currently implemented demos, check "Show only maps with preset areas" in the map selection window.

<img width="800" alt="image" src="https://github.com/user-attachments/assets/cc9d2ebe-1685-4b51-8d08-a4c237a5d865" />

- Click "Load Area". After the map finishes loading, click "Start Simulation" in the preset panel on the right. You can click "Stop Simulation" during a simulation; it cancels pending timeline nodes, waits for the current action to return, and cleans up simulation entities.

- When you are done, uncheck "Enable Firewall" to return to your original location in the inn.

- If any step during this process causes the game to crash, please record any errors and restart ACT.

- Optional: If you experience problems with text or UI layout, you can install a font:

  [Sarasa Gothic](https://github.com/be5invis/Sarasa-Gothic/releases)

  Download the Mono archive from the Single Family TTF Package and install the font.

## Development

- [Mechanic timeline development (Chinese)](docs/Timeline.md): node definitions, timeline composition, cancellation, and entity lifetime.

## License

Simulant's original code is licensed under GPLv3 (`GPL-3.0-only`). You may freely use and modify this project.

When redistributing the project, you must also comply with GPLv3 and provide the complete corresponding source code for that version. If you have made changes, you must indicate them.

See [COPYING.md](COPYING.md) for third-party content information and [LICENSE](LICENSE) for the full terms.
