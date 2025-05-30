![logo.png](/logo.png "INFINIFORTRESS v2")

Infinifortress is a mod based on Infiniminer that extends the gameplay loop beyond the classic Infiniminer with the addition of research, artifacts, and more abilities to the four classes.

[![Build and Release](https://github.com/zuzu-a/Infinifortress/actions/workflows/build.yml/badge.svg)](https://github.com/zuzu-a/Infinifortress/actions/workflows/build.yml)

## Releases
[The latest releases can be found here.](https://github.com/zuzu-a/Infinifortress/releases)

## Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.7.2 or later
- MonoGame

## Building

1. Clone the repository
2. Open the solution in Visual Studio
3. Restore NuGet packages
4. Build the solution

## Running

1. Navigate to the `bin` directory
2. Run the executable


## Controls

### Basic Movement
- `W/A/S/D` - Move
- `SPACE` - Jump
- `Left Click` - Use tool/Place block
- `Right Click` - Use secondary function/Remove block

### Tool Selection
- `E` - Cycle through tools
- `1-4` - Quick select tools (when available)

### Communication
- `Q` - Ping location (visible to team)
- `Y` - Global chat (all players)
- `U` - Team chat

### Team Management
- `N` - Switch teams
- `M` - Switch role
> Note: You must be at your base or above ground to change your team or role.

### Chat Commands
- `/restart` - Initiate a server restart vote
  - Requires majority of online players to pass
  - Auto-passes if you're the only player

## Configuration

### Server configuration
The server can be configured through "server.config.txt" some of the options include:

#### Server settings
- 'name': The servername that is displayed in the server browser. (default: "Infiniminer Server")
- 'maxplayers': The maxmimum amount of concurrent players allowed on the server. (default: 16)
- 'port': The port that the server listens on. (default: 5565)
- 'greeter': Custom welcome message for new players. (default: "")
- 'publiclist': Toggles whether the server is publicly listed on the server list or not. (default: false)
- 'serverListURL': Debug option. Checks which serverlist to ping prior to uploading server information to the list.

#### Game settings
- 'siege': Siege mode. Range from 0-4 (default: 0)
- 'sandbox': Toggles whether the game is in sandbox mode or not. (default: false)
- 'tnt': Enable TNT explosions. (default: true)
- 'lava': Enable lava. (default: true)
- 'water': Enable water. (default: true)
- 'artifacts': Enable artifacts. (default: true)

#### Map settings
- 'mapsize': The map size in blocks. Do not change this from the default. Custom map generation is in the pipeline. (default: 64)
- 'regionsize': The size of regions, which are effectively chunks. Do not change this from the default. (default: 16)
- 'winningcash': The amount of artifacts needed to win. (default: 6)

#### Other settings
- 'autosave': The autosave interval in minutes. (default: 4)
- 'decaytimerps': The powerstone decay time in seconds. (default: 2000)
- 'spawndecaytimer': The artifact decay time in seconds. (default: 3000)
- 'autoban': Auto-bans griefers (default: true)
- 'warnings': Amount of warnings before a ban is given. (default: 4)

### Client configuration
The client can be configured through editing "client.config.txt" or the in-game settings some of the options include:

#### Display
- 'width': Game window width. (default: 1280)
- 'height': Game window height. (default: 720)
- 'windowmode': Display mode. (windowed/fullscreen) (default: windowed)
- 'showfps': Toggle FPS counter. (default: false)
- 'pretty': Enable bloom effect. (default: false)
- 'light': Enable enhanced lighting. (default: false)

#### Controls
- 'yinvert': Invert mouse y-axis. (default: false)
- 'sensitivity': Mouse sensitivity (1-10). (default: 5)
- 'inputlagfix': Debug option.

#### Audio
- 'volume': Sound volume. (0-1.0) (default: 1)
- 'nosound': Disable all sound.

#### Player settings
- 'handle': Set your player name/handle. (default: "Player")

## License

This project is licensed under the terms found in the [LICENSE](LICENSE) file.

## Changelog

See [changelog.txt](changelog.txt) for a list of changes and updates.