# Simulant · 仿生石

An ACT plugin for local FFXIV raid simulation using native in-game rendering.

Simulant spawns simulated party members and enemies locally in the game client. Its timeline scripts drive the encounter using the game's built-in abilities, animations, and visual effects.

It works in a similar way to Dalamud plugins such as Hyperborea and Brio. Encounters run entirely on the client, with no need to set up or connect to a private server.

## Features

- Local duty simulation

- Spawn local party members and enemies

- Native animation and visual effect control

- Timeline scripting for mechanics

- Player behavior validation

## Status

Development began in February 2026 and is ongoing. Collaboration is welcome.

<img width="2222" height="1170" alt="image" src="https://github.com/user-attachments/assets/87dc9104-2b5c-417f-8415-e783bc945b87" />


## Instructions

Simulant is still in development. Errors and crashes can occur, so please follow the steps below carefully.

> [!TIP]
> **Download the plugin DLL from the latest release under Releases on the right. The download at the top of the repository contains the source code.**

- You need 64-bit ACT and the FFXIV Parsing Plugin. Simulant no longer requires Triggernometry or PostNamazu.

- Add the plugin DLL to ACT's plugin list. You can place it near the end of the list.

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/957070e3-1255-4859-8cc0-6a21c782e84b" />

- Simulant prepares the configured game client files at startup. When the FFXIV Parsing Plugin attaches to the game process, Simulant connects automatically. If the connection fails, click “Retry Connection”.

> [!WARNING]
> This plugin has compatibility issues with some Dalamud plugins.
>
> If the game crashes when you enable the firewall or enter a duty, or if initialization fails (for example, because a scan cannot find a pointer), try launching the game without Dalamud, or with only the Dalamud framework enabled.

- Once initialization is complete, enter any inn room and check "Enable Firewall". This feature works similarly to the Dalamud plugin [Hyperborea](https://github.com/kawaii/Hyperborea).

  This intercepts all incoming and outgoing packets except heartbeat packets, putting you into local mode.

- Select a map on the left, or enter a map ID. If the map has phases/simulations available, you can pick one from the dropdown list.

  To filter for the currently implemented demos, check "Show only maps with preset areas" in the map selection window.

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/042e777e-ca15-42cc-8b17-47abe33355c7" />

- Click "Load Area". After the map finishes loading, click "Start Simulation" in the preset panel on the right. To stop a simulation early, click "Stop Simulation". Simulant cancels the remaining timeline events, waits for the current action to finish, then removes the simulated entities.

- When you are done, uncheck "Enable Firewall" to return to your original location in the inn.

  If the firewall fails to turn off, check the log, address the reported issue, then uncheck the box again. If the box stays checked, cleanup is incomplete. Some network traffic may already be getting through. Cleanup must finish before you can initialize the plugin again.

  If an area fails to load partway through, Simulant keeps the map and position you started from. Retrying the load preserves that return point. Simulant attempts to return you there before turning off the firewall.

- If the game crashes at any step, make a note of what happened and restart ACT.

## Acknowledgements

Simulant draws on many Dalamud repositories and builds on earlier research into the FFXIV client, game data, and simulation tools.

Special thanks to the authors and contributors of the following Dalamud and ACT projects.

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) provided definitions for client structures and native functions.
- [Lumina](https://github.com/NotAdam/Lumina) and [EXDSchema](https://github.com/xivdev/EXDSchema) made it possible to read game assets and parse data tables.
- [Sapphire](https://github.com/SapphireServer/Sapphire), [Machina](https://github.com/ravahn/machina), and [BossMod](https://github.com/awgil/ffxiv_bossmod) served as references for packet layouts and field meanings. BossMod also informed some of the ability area checks.
- [Kodakku Assist](https://github.com/Karlin-Z/KodakkuAssistScriptLibrary), [NyaDraw](https://github.com/NiGuangOwO/DalamudPlugins/tree/main/plugins/NyaDraw), and [Daily Routines](https://github.com/Dalamud-DailyRoutines/DailyRoutines) were key references for drawing with the game's native visual effects and using other game assets.
- [AnoMech](https://github.com/anomek/AnoMech), a Dalamud simulator, helped refine the underlying implementation of party updates and related features.
- [Hyperborea](https://github.com/kawaii/Hyperborea) served as a reference for entering duties locally in the client.
- [Brio](https://github.com/Valentine-Tools/Brio) was an early reference for spawning local entities.
- [Advanced Combat Tracker](https://advancedcombattracker.com/) and [NewMoe Technology](https://act.diemoe.net/), which maintains its Chinese distribution, provide the platform that hosts Simulant.
- [PostNamazu](https://github.com/Natsukage/PostNamazu) was a key early reference for interacting with the game process from ACT.
- Simulant's replay reader draws on [Hypostasis](https://github.com/UnknownX7/Hypostasis) and the replay reader in [ARealmRecorded](https://github.com/UnknownX7/ARealmRecorded).

## License

Simulant's original code is licensed under GPLv3 (`GPL-3.0-only`). You may freely use and modify this project.

When redistributing the project, you must also comply with GPLv3 and provide the complete corresponding source code for that version. If you have made changes, you must indicate them.

See [COPYING.md](COPYING.md) for third-party content information and [LICENSE](LICENSE) for the full terms.
