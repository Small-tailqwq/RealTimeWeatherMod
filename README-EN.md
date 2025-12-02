# Chill Env Sync (Real-time Weather Mod)
> ⚠️ **Yes, this readme was also written by AI.** 🤖

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Framework 4.7.2](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net472)
[![BepInEx](https://img.shields.io/badge/BepInEx-Plugin-green.svg)](https://github.com/BepInEx/BepInEx)

A BepInEx plugin for the game *Chill with You - Lo-Fi Story*. It automatically synchronizes the in-game environment with real-world weather or simulates a day/night cycle based on your local time.

---

### 📢 Important Note for International Users (OpenWeather Support)

> **Looking for OpenWeatherMap support?**
> This version is optimized for users in China (using the **Seniverse** API).
> If you need **OpenWeather** support or better localization for non-Chinese regions, please check out this excellent fork by the community:
>
> 👉 **[danlok10/RealTimeWeatherMod-EN](https://github.com/danlok10/RealTimeWeatherMod-EN)**
> *(They grew out of this codebase like a digital plant!)*

---

[![Chill with You](./header_schinese.jpg)](https://store.steampowered.com/app/3548580/)

> 「放松时光：与你共享Lo-Fi故事」是一个与喜欢写故事的女孩聪音一起工作的有声小说游戏。您可以自定义艺术家的原创乐曲、环境音和风景，以营造一个专注于工作的环境。在与聪音的关系加深的过程中，您可能会发现与她之间的特别联系。

---
[中文版](./README.md)

> **Disclaimer:**
> All code in this project was written by **AI**. I only did the decompiling and debugging.
> This is my first time making a Unity Mod using AI. If you find bugs, please report them, but I'll probably just ask the AI to fix them again. 🫥
> This is an unofficial fan work and is not affiliated with the game developers or publishers.
> All game assets, trademarks, and copyrights belong to their respective owners.

---

**Related Works:**
- My second "Potato Mod": [iGPUSaviorMod](https://github.com/Small-tailqwq/iGPUSaviorMod)


## ✨ Key Features

- 🌤️ **Real-Time Weather Sync**: Fetches real weather data via the Seniverse API and adjusts the game environment.
- 🌍 **Multi-City Support**: Supports weather queries for any city (Pinyin or Chinese).
- 🌓 **Day/Night Cycle**: Automatically switches between Day/Sunset/Night based on configured sunrise/sunset times.
- 🔓 **Unlock Everything**: Automatically unlocks all environments and decorations.
  - *Note: This is session-based and does not modify your save file.*
  - Configurable via `UnlockAllEnvironments` and `UnlockAllDecorations`.
- ⌨️ **Hotkeys**:
  - `F7` - **Force Refresh**: Ignores cache, forces a fresh API call to Seniverse, and resets the timer.
  - `F8` - **Show Status**: Prints current status to the console log.
  - `F9` - **Manual Sync**: Re-calculates and applies the environment based on *existing* cache and local time (No network request).

## ⚠️ Known Hazards

- 💽 **Cheating Suspicion**: Unlocks environments by default (can be disabled in config). It doesn't write to your save file, but it feels like cheating.
- 💥 **Conflicts**: Might conflict with future updates or other mods. The code structure is... questionable (thanks AI).
- 💸 **Educational Use Only**: MIT License allows you to do whatever, but please **do not sell this DLL**.
- 🧷 **External Links**: Uses a third-party Weather API. Please be aware of privacy and data usage.
- 😵‍💫 **Rough Quality**: Again, 100% AI-generated. I don't know C#, so please have mercy.
- 🧱 **Region Lock**: This version is best used with the Chinese language setting or within China due to the API provider.
- 🤖 **Skynet Crisis**: Using this plugin may accelerate the AI domination of the world. Use at your own risk. (<--- I didn't write this part, the AI did).(<---这也是AI写的了🥶)

## 🎮 Supported Environments

### Basic Environments (Mutually Exclusive)
- ☀️ Day
- 🌅 Sunset
- 🌙 Night
- ☁️ Cloudy

### Precipitation & Effects
- 🌧️ Light Rain / Heavy Rain / Thunderstorm
- ❄️ Snow

### Scenery & Sound Effects
- If the "Easter Egg" mode is enabled, the system may randomly trigger specific scenery (visuals) or sound effects based on conditions.

### TODO List
- [x] Optimization of basic functions.
- [x] Scenery B display based on seasons/weather.
- [x] Scenery C display based on seasons/time.
- [x] Auto-trigger BGM based on time/season.
- [x] Code Refactoring (Splitting into sub-modules so the AI can understand it better).
- [ ] Fixing infinite bugs.
- [ ] Dealing with future game updates.

## 📦 Installation

### Prerequisites
- Game Client
- [BepInEx 5.4.23.4](https://github.com/BepInEx/BepInEx/releases) (Developed on this version)

### Steps
1. Install BepInEx framework correctly.
2. Launch the game once to generate folders, then exit.
3. Place `RealTimeWeatherMod.dll` into the `BepInEx/plugins/` folder.
4. Launch the game. The plugin will load automatically.
5. Edit the configuration file. Press `F7` in-game to reload config after editing.

## ⚙️ Configuration

Config file location: `BepInEx/config/chillwithyou.envsync.cfg`

### API Key (Seniverse)
> 💡 **v5.1.2+**: Comes with a built-in shared key. You can leave `SeniverseKey` empty to use it.
> If you prefer your own key:
1. Register at [Seniverse Console](https://www.seniverse.com/).
2. Get a **Private Key** (Free version).
3. Paste it into `SeniverseKey`.
4. Set `EnableWeatherSync = true`.

### Important Config Options
```ini
[Automation]
## Enable seasonal easter eggs and automatic environmental sound management
EnableSeasonalEasterEggs = true

[Debug]
## Enable debug mode | Do not modify unless for debugging purposes
EnableDebugMode = false
## Simulated Weather Code (for debug)
SimulatedCode = 13
## Simulated Temperature (for debug)
SimulatedTemp = 13
## Simulated Weather Description (for debug)
SimulatedText = DebugWeather

[Internal]
## Last Synchronization Date | Do not modify unless for debugging
LastSunSyncDate = 2025-12-02

[TimeConfig]
## Sunrise Time (Format: HH:mm) | No need to modify manually in v5.1.2+
Sunrise = 06:30
## Sunset Time (Format: HH:mm) | No need to modify manually in v5.1.2+
Sunset = 18:30

[UI]
## Whether to display real-time weather and temperature after the date in the top-left UI
ShowWeatherOnDate = true

[Unlock]
## Automatically unlock all environment scenes
## Warning: May include unimplemented scenes, use with caution.
UnlockAllEnvironments = true

## Automatically unlock all decorations
## Warning: May include unimplemented items, use with caution.
UnlockAllDecorations = true

[WeatherAPI]
## Enable Weather API Synchronization
## Most features will not work if disabled.
EnableWeatherSync = false

## Seniverse API Key
## For v5.1.2+, leave empty to use the built-in shared key.
## If you have your own key, paste it here.
SeniverseKey = 

## Location
## Input: City Name (Pinyin or Chinese, e.g., 'beijing', 'shanghai')
## Input: 'ip' (Auto-detect location based on IP address)
Location = beijing

[WeatherSync]
## Weather API Refresh Interval (in Minutes) | Do not modify unless necessary
RefreshMinutes = 30
```
## 🚀 Usage Guide

### Basic Mode (Default)

Syncs environment based on local time:

  - **Day**: Sunrise -\> 1 hour before Sunset.
  - **Sunset**: 30 mins before Sunset -\> 30 mins after Sunset.
  - **Night**: Everything else.

### Weather Sync Mode (Requires API)

Maps API weather codes to game environments:

  - **Snow**: All snow types -\> Game Snow effect.
  - **Thunder**: Storms/Thunder -\> Game ThunderRain.
  - **Rain**: Moderate/Heavy Rain -\> Game HeavyRain.
  - **Drizzle**: Light rain -\> Game LightRain.
  - **Cloudy/Overcast**: Turns off rain/snow, forces Cloudy environment (unless it's Night).

### Hotkey Details

  - **F7 (Force Refresh)**:
      - **Use when:** The weather changed outside but not in-game, or you changed the Location in config.
      - **Action:** Forces a network request to Seniverse.
  - **F9 (Manual Trigger)**:
      - **Use when:** Game UI says "Rain" but visual is clear, or time stuck.
      - **Action:** "Kicks" the logic engine to re-apply the current state without using network data.

## 🔧 Technical Details

  - **Framework**: BepInEx 5.x (.NET Framework 4.7.2)
  - **Tech Stack**: Harmony (Patching), Unity Coroutines, Reflection, dnSPY.
  - **Collaborators**: A bunch of Large Language Models.

## 📝 Version History (Highlights)

*Note: Version numbers are hallucinated by AI. I have no control over this.*

### v5.1.2 - The "Old Pickled Cabbage" Edition (?)

  - Added a built-in fallback Key.
  - Fixed UI interaction bugs and BGM issues.

### b5.1.1

  - Refined time definitions (Early morning, Noon, Evening, etc.).

### b5.0.1 - It's Cooking Time

  - Moved scenery enums to Gemini. It bumped the version number for no reason.
  - Likely the final version before the next game update.

### v5.0.0

  - **Major Refactor**: Separated `SceneryAutomationSystem` so it doesn't fight with Weather logic.
  - **Dirty Flag System**: If you manually click a toggle, the mod stops managing that specific toggle for the session.

### v4.x - v3.x

  - Fighting with "Cloudy" logic.
  - Fighting with Gemini to fix bugs it created.
  - Fighting with my sleep schedule.

## 🤝 Contribution

Issues and Pull Requests are welcome\!
Please enable `Logging.Console = true` in `BepInEx.cfg` before reporting bugs.

## 📄 License

**MIT License**.
You can use, modify, distribute, and learn from this.
**Warning**:

  - ✅ Free to use/mod.
  - ❌ Do not sell this DLL.
  - Use at your own risk.

## 👨‍💻 Author

  - GitHub: [@Small-tailqwq](https://github.com/Small-tailqwq)

## 🙏 Acknowledgements

  - BepInEx Team
  - Harmony Library
  - Seniverse API
  - Google Gemini 3 Pro / OpenAI ChatGPT / Claude Sonnet
  - **My liver and eyes** (I really need a new chair and some eye drops).

-----

> *There is always a lava lamp on my desk.*