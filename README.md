# Auto Wool And Milk

A small BepInEx plugin for **Raft** that automatically collects wool from llamas and milk from goats as soon as the animal's resource is ready.

No menu, no hotkey, no framework dependencies — just one DLL and a config file.

## Features

- **Auto shear llamas** — wool goes straight into your inventory the moment it grows back
- **Auto milk goats** — collects the milk bucket as soon as the goat is ready
- **Faithful mode (default on)** — requires shears / an empty bucket in your inventory, and spends one empty bucket per milk, exactly like the vanilla loop
- **Host authoritative** — only the host harvests; state changes are synced to other players by the game's own RPC, so multiplayer works without extra handling
- **Distance limit** — ignore animals further than N metres from you

## Requirements

- Raft (The Final Chapter, 1.0+)
- [BepInEx 5 (x64)](https://github.com/BepInEx/BepInEx/releases) 5.4.23.x — installed into the Raft folder

## Installation

1. Install BepInEx 5 into your Raft folder (`steamapps/common/Raft`) and run the game once so it generates its folders.
2. Drop `AutoWool.dll` into `Raft/BepInEx/plugins/`.
3. Start the game. A config file appears at `BepInEx/config/jayliao.AutoWool.cfg`.

### Raft-specific gotchas

Read this before reporting "it doesn't work" — these four cover almost every failed install.

1. **Set `HideManagerGameObject = true`** in `BepInEx/config/BepInEx.cfg`, under `[Chainloader]`. Raft's startup interferes with BepInEx's manager object otherwise, and plugins may silently fail to load. Restart the game after changing it.
2. **Launch the game once before adding plugins**, so BepInEx creates its folders and `LogOutput.log`. If that log does not exist after launching, BepInEx itself is not loading — check that `winhttp.dll` and `doorstop_config.ini` sit next to `Raft.exe`, and that your antivirus did not quarantine `winhttp.dll`.
3. **Verify it loaded**: `BepInEx/LogOutput.log` should contain `Loading [Auto Wool And Milk ...]` and `Chainloader startup complete`.
4. **Installed but nothing happens?** With `FaithfulMode` on you need shears (wool) or an empty bucket (milk) somewhere in your backpack **or** hotbar. In multiplayer only the host collects.

## Configuration

| Section | Key | Default | Description |
|---|---|---|---|
| General | `ModEnabled` | `true` | Master switch |
| General | `IsDebug` | `true` | Write collection events to `LogOutput.log` |
| Options | `Interval` | `2` | Seconds between scans |
| Options | `Range` | `30` | Max distance from the player to the animal, `-1` = unlimited |
| Options | `ShearLlamas` | `true` | Enable wool collection |
| Options | `MilkGoats` | `true` | Enable milk collection |
| Options | `FaithfulMode` | `true` | Require and spend the vanilla tool (see below) |

### FaithfulMode

| FaithfulMode | Tool in inventory | Result |
|---|---|---|
| `true` | yes | Collected, one empty bucket spent per milk |
| `true` | no | Skipped (logged once every 30s) |
| `false` | irrelevant | Collected with no tool and no bucket cost |

"Tool in inventory" means anywhere in the backpack **or** the hotbar, including the slot in your hand. Shears do not lose durability in the current version.

## How it works

Raft keeps a `ResourceRegenerative` component on every tamed llama and goat. The plugin scans the scene for `AI_NetworkBehaviour_Domestic_Resource` (the only subclasses are `AI_NetworkBehaviour_Llama` and `AI_NetworkBehaviour_Goat`), reads the `Resource` property, and when `IsReady` is true it calls `HarvestResource()` and adds the returned item to the player inventory — the same two steps the vanilla `ResourceCollector.OnHarvest()` performs.

The component itself lives on a child object that the game keeps deactivated until the resource is ready, so it cannot be found with `FindObjectsOfType<ResourceRegenerative>()`; the network behaviour property is the reliable way in.

## Build

```bash
# .NET SDK 8 via mise (any recent SDK works)
mise use -g dotnet@8

# GamePath defaults to the standard Steam location, override if needed:
dotnet build -c Release -p:GamePath="D:\Steam\steamapps\common\Raft"
```

The build copies `AutoWool.dll` into `BepInEx/plugins` automatically.

`NuGet.config` points at a mirror because `api.nuget.org` was unreliable on the machine this was written on — delete it if the official feed works for you.

## Notes

- `tools/inspect_assembly.py` dumps type/field/method metadata from `Assembly-CSharp.dll` without loading it. Handy for re-checking class names after a game update.
- Only llamas and goats are handled; chickens and eggs are untouched.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) by the BepInEx team — the plugin framework this mod runs on. It is not bundled; users install it separately.
- [RaftMods](https://github.com/aedenthorn/RaftMods) by aedenthorn — a useful reference for Raft plugin structure.
- [BepInEx Unity Installer](https://www.nexusmods.com/site/mods/287) by aedenthorn — the installer recommended in the installation steps.

## License

MIT — see [LICENSE](LICENSE). You are free to fork, modify and reuse this code. If you publish a modified version, please link back to this repository so people can find the original.

