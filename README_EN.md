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

- Requires 64-bit ACT and the FFXIV Parsing Plugin.

> [!WARNING]
> This plugin is still under active development. It is currently only a demo for testing whether this idea is feasible.
> 
> It has no i18n support for now, and it relies on several tools that I personally maintain or that are mainly used in the Chinese community.
- Load this plugin's `dll` into ACT's plugin list to complete the setup. It can be placed fairly far down the list.

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/957070e3-1255-4859-8cc0-6a21c782e84b" />

- Simulant prepares the configured game files at startup and connects automatically when the FFXIV ACT Plugin selects a game process. Use “Retry Connection” if the connection fails.

> [!WARNING]
> This plugin has compatibility issues with some Dalamud plugins.
>
> If you encounter a game crash or an initialization error (such as a certain Ptr not being found during scanning), try a clean launch, or run only the Dalamud framework.

- Once initialization is complete, enter any inn map and check "Enable Firewall". This feature is similar to the Dalamud plugin [Hyperborea](https://github.com/kawaii/Hyperborea).

  This intercepts all incoming and outgoing packets except heartbeat packets, putting you into local mode.

- Select a map on the left, or enter a map ID. If the map has phases/simulations available, you can pick one from the dropdown list.

  To filter for the currently implemented demos, check "Show only maps with preset areas" in the map selection window.

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/042e777e-ca15-42cc-8b17-47abe33355c7" />

- Click "Load Area". After the map finishes loading, click "Start Simulation" in the preset panel on the right. You can click "Stop Simulation" during a simulation; it cancels pending timeline nodes, waits for the current action to return, and cleans up simulation entities.

- When you are done, uncheck "Enable Firewall" to return to your original location in the inn.

- If any step during this process causes the game to crash, please record any errors and restart ACT.

## Development

- [Mechanic timeline development (Chinese)](docs/Timeline.md): node definitions, timeline composition, cancellation, and entity lifetime.

## Acknowledgements

Simulant draws on many Dalamud repositories and builds on earlier research into the FFXIV client, game data, and simulation tools. Thank you to the authors and contributors of the following Dalamud and ACT projects.

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) provided definitions for client structures and native functions.
- [Lumina](https://github.com/NotAdam/Lumina) and [EXDSchema](https://github.com/xivdev/EXDSchema) made it possible to read game assets and parse data tables.
- [Sapphire](https://github.com/SapphireServer/Sapphire), [Machina](https://github.com/ravahn/machina), and [BossMod](https://github.com/awgil/ffxiv_bossmod) served as references for packet layouts and field meanings. BossMod also informed some of the ability area checks.
- [Kodakku Assist](https://github.com/Karlin-Z/KodakkuAssistScriptLibrary), [NyaDraw](https://github.com/NiGuangOwO/DalamudPlugins/tree/main/plugins/NyaDraw), and [Daily Routines](https://github.com/Dalamud-DailyRoutines/DailyRoutines) were key references for drawing with the game's native visual effects and using other game assets.
- [AnoMech](https://github.com/anomek/AnoMech), a Dalamud simulator, helped refine the underlying implementation of party updates and related features.
- [Hyperborea](https://github.com/kawaii/Hyperborea) served as a reference for entering duties locally in the client.
- [Brio](https://github.com/Valentine-Tools/Brio) was an early reference for spawning local entities.
- [Advanced Combat Tracker](https://advancedcombattracker.com/) and [NewMoe Technology](https://act.diemoe.net/), which maintains its Chinese distribution, provide the platform that hosts Simulant.
- [PostNamazu](https://github.com/Natsukage/PostNamazu) was a key early reference for interacting with the game process from ACT.
- Simulant's replay reader benefited from work on the replay format by [OrchestrionPlugin](https://github.com/perchbirdd/OrchestrionPlugin) and [ARealmRecorded](https://github.com/UnknownX7/ARealmRecorded).

## License

Simulant's original code is licensed under GPLv3 (`GPL-3.0-only`). You may freely use and modify this project.

When redistributing the project, you must also comply with GPLv3 and provide the complete corresponding source code for that version. If you have made changes, you must indicate them.

See [COPYING.md](COPYING.md) for third-party content information and [LICENSE](LICENSE) for the full terms.
