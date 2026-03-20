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

[![Chill with You](https://raw.githubusercontent.com/Small-tailqwq/RealTimeWeatherMod/refs/heads/master/header_schinese.jpg)](https://store.steampowered.com/app/3548580/)

> "Chill with You: Lo-Fi Story" is a visual novel game where you work alongside Satone, a girl who loves writing stories. You can customize original music by artists, ambient sounds, and scenery to create an environment focused on work. As your relationship with Satone deepens, you may discover a special connection with her.

---

[中文版](./README.md)

> **Disclaimer:**
> All code in this project was written by **AI**. I only did the decompiling and debugging.
> This is my first time making a Unity Mod using AI. If you find bugs, please report them, but I'll probably just ask the AI to fix them again. 🫥
> This is an unofficial fan work and is not affiliated with the game developers or publishers.
> All game assets, trademarks, and copyrights belong to their respective owners.

---

Related project: [iGPUSaviorMod](https://github.com/Small-tailqwq/iGPUSaviorMod)
Related Bilibili video: [Time, Weather, and Potatoes](https://www.bilibili.com/video/BV1JXSiB4EP1) (pure traffic bait, almost no technical content)

## ✨ Main Features

- 🌤️ **Real-time weather sync**: Fetches weather data from Seniverse API and updates in-game environment automatically.
- 🌍 **Multi-city support**: Supports weather lookup for any city (Pinyin or Chinese).
- 🌓 **Day/night cycle**: Auto switches between Day/Sunset/Night based on configured sunrise and sunset time.
- 🔓 **Unlock all environments**: Unlocks all environments and decorations in current session.
  - This does not modify save data.
  - Optional config items:
    - `UnlockAllEnvironments`: Unlock all environment scenes.
    - `UnlockAllDecorations`: Unlock all decorations.
    - `UnlockPurchasableItems`: Unlock all in-game currency purchases.
- ⌨️ **Hotkeys**:
  - `F7` - Force refresh weather.
    - Ignores cache and fetches latest weather from Seniverse API.
    - Resets API slow timer.
  - `F8` - Show current status.
    - Prints a status log line.
  - `F9` - Manual sync.
    - Recalculates and reapplies environment using current cache and local time.

## ⚠️ This Project May Have

- 💽 **Cheat-like behavior**: This plugin modifies environment and decoration unlock states by default, but does not write to save files.
- 💥 **Unexpected conflicts**: Future game updates or mod conflicts may break behavior.
- 💸 **Learning use only**: Licensed under MIT. Legally flexible, but please do not sell the DLL directly.
- 🧷 **External service dependency**: Uses third-party weather API; please mind privacy/data concerns.
- 😵‍💫 **Rough quality**: Fully AI-written and may contain bugs.
- 🧱 **Region limitations**: Weather features and weather UI display may only work in some regions/languages.
- 🤖 **AI uprising risk**: Using this plugin may accelerate AI world domination. Use at your own risk.

## 🎮 Supported Environment Types

### Base Environments (Mutually Exclusive)

> Note: Game logic allows only one active base environment at a time, though multiple can be force-enabled.

- ☀️ Day
- 🌅 Sunset
- 🌙 Night
- ☁️ Cloudy

### Precipitation Effects

> Note: The list below is in-game effects. For real weather mapping, see the usage section.

- 🌧️ LightRain
- 🌧️ HeavyRain
- ⛈️ ThunderRain
- ❄️ Snow

### Scenery Effects

When seasonal easter eggs are enabled, some scenery effects may be activated randomly or under specific conditions.

### Sound Effects

When seasonal easter eggs are enabled, some ambient sound effects may be activated under specific conditions.

### TODO

- [x] Core feature optimization
- [x] Show B-type scenery by season/weather
- [x] Show C-type scenery by season/time
- [x] Auto trigger background music by time/season
- [x] Refactor into submodules for easier AI maintenance
- [x] Survive future game updates (now compatible with ver1.1+)
- [ ] Endless bug fixing
- [ ] Improve todo/note related features in potato mode when time allows

## 📦 Installation

### Prerequisites

- Game client
- [BepInEx 5.4.23.4](https://github.com/BepInEx/BepInEx/releases) (plugin developed on this version)

### Steps

> ⚠️ **Important: Installation order matters.**
> ⚠️ **Important: Download DLL from [Releases](https://github.com/Small-tailqwq/RealTimeWeatherMod/releases), not source zip.**

1. Ensure BepInEx is installed correctly.
2. **Launch game once first, to let BepInEx initialize.**
   - Wait until main menu appears.
   - Confirm BepInEx console window appears.
   - Confirm `BepInEx/plugins/` folder exists.
   - Exit the game.
3. Put `RealTimeWeatherMod.dll` into `BepInEx/plugins/`.
4. Launch game again; plugin will auto-load.
5. Edit config at `BepInEx/config/chillwithyou.envsync.cfg`.
6. Press `F7` in game to refresh config/weather immediately.

Common issues:

- If mod does not work, ensure BepInEx finished first initialization before installing plugin.
- If you cannot find DLL, you likely downloaded source code archive instead of release artifact.

## ⚙️ Configuration

After first run, config is generated at `BepInEx/config/chillwithyou.envsync.cfg`.

### Config Reference

```ini
[Automation]
## Enable seasonal easter eggs and automatic ambient sound management
EnableSeasonalEasterEggs = true


[Debug]
## Enable debug mode | Leave this off unless debugging
EnableDebugMode = false
## Simulated weather code
SimulatedCode = 13
## Simulated temperature
SimulatedTemp = 13
## Simulated weather text
SimulatedText = DebugWeather

[Internal]

## Last sync date | Leave this unchanged unless debugging
LastSunSyncDate = 2025-12-02

[TimeConfig]
## Sunrise (HH:mm) | v5.1.2+ usually does not require manual edits
Sunrise = 06:30
## Sunset (HH:mm) | v5.1.2+ usually does not require manual edits
Sunset = 18:30

[UI]
## Show real-time weather and temperature on date bar
ShowWeatherOnDate = true


[Unlock]
## Unlock all environments | May include unfinished scenes, use carefully
UnlockAllEnvironments = true
## Unlock all decorations | May include unfinished items, use carefully
UnlockAllDecorations = true
## Unlock in-game currency purchases | May greatly shorten game progression
UnlockPurchasableItems = false


[WeatherAPI]
## Enable weather API sync | Most features need this enabled
EnableWeatherSync = false
## Seniverse API key | v5.1.2+ can use built-in key when left empty
SeniverseKey =
## City name (Pinyin or Chinese, e.g. beijing, 上海). Use ip for auto locate
Location = beijing


[WeatherSync]
## API refresh interval in minutes | Leave as is unless debugging
RefreshMinutes = 30
```

### Get Seniverse API Key

> 💡 New versions include a built-in key, so leaving it empty still works. Applying your own key is optional.

1. Visit [Seniverse developer platform](https://www.seniverse.com/).
2. Register and log in, then go to Console -> Product Management -> Free Edition (or your actual edition).
3. Find and copy your private key.
4. Put it into `SeniverseKey` in config. No quotes needed.
5. Set `EnableWeatherSync = true`.
6. Adjust refresh interval as needed to avoid excessive API calls.
7. If needed, check issues for extra help.

## 🚀 Usage

### Basic Mode (Default Enabled)

Plugin auto switches environment by configured sunrise/sunset:

- From sunrise to 1 hour before sunset: Day.
- From 30 minutes before sunset to 30 minutes after sunset: Sunset.
- All other times: Night.

### Weather Sync Mode (Optional)

1. Configure API key as above.
2. Plugin fetches weather periodically (default every 30 minutes).

3. Environment mapping:

| Environment | Normal Weather | Severe Weather |
| ------ | --------- | --------- |
| ☀️ Day (Day) | Sunrise -> 30 min before sunset | — |
| 🌅 Sunset (Sunset) | 30 min before sunset -> 30 min after sunset | — |
| ☁️ Cloudy (Cloudy) | — | Sunrise -> 30 min after sunset |
| 🌙 Night (Night) | 30 min after sunset -> sunrise | 30 min after sunset -> sunrise |

> 💡 In severe weather, daytime and sunset periods are unified as Cloudy, so sunset effect is skipped. Night is unaffected by weather and always starts exactly 30 minutes after sunset.

4. Severe weather codes:

```
4, 7, 8, 9 (cloudy/overcast)
11, 12, 14-20 (moderate rain+, thunderstorm, freezing rain)
23-25 (moderate snow+)
26-31 (sandstorm/fog)
34-36 (storm)
```

5. Weather effect mapping:

| In-game Effect | API Weather IDs (reference) | Logic |
| ------------ | --------------------------- | -------------------------------------------------- |
| Snow | 20-25 (snow/sleet/blizzard) | All snow-like conditions map to snow effect. |
| ThunderRain | 11, 12, 16, 17, 18 (thunder/heavy storm rain) | Thunder showers and very heavy rain map to thunder rain. |
| HeavyRain | 10, 14, 15 (showers/moderate/heavy rain) | Regular strong rainfall. |
| LightRain | 13, 19 (light rain/freezing rain) | Light rainfall effect. |
| OFF | 0-9, 26-39 (clear/cloudy/overcast/fog/wind) | Critical rule: overcast/cloudy must force disable all rain/snow effects. |

### Hotkey Details

- **F7: Force weather refresh (skip cache)**
  - Core purpose:
    - Ignore cache and fetch latest weather from Seniverse API.
    - Reset API slow timer.
  - Trigger behavior:
    1. Reset timer for next auto API call.
    2. Ignore `CacheExpiry` (60-min cache), send HTTP request to `api.seniverse.com` directly.
    3. If success, replace in-memory `_cachedWeather`.
    4. Auto call `ApplyEnvironment` to update scene.
  - Recommended when:
    - Real weather changed but game still shows old weather.
    - You want to test API connectivity.
    - You changed `Location` and want immediate effect.

- **F8: Print current environment status**
  - Writes one status log line.

- **F9: Manual weather sync**
  - Core purpose:
    - No network request. Uses current cache and real-time local clock to force a full recalc and apply.
    - Bypasses debounce check (`if (Current == Target) return`) and executes full switch flow.
  - Trigger behavior:
    1. Read `_cachedWeather` from memory (if absent, only time logic applies).
    2. Recompute day/sunset/night by current `DateTime.Now`.
    3. Force execution:
       - Call `ForceActivateEnvironment`.
       - Simulate click on `MainIcon` even if already lit.
       - Force call `WindowViewService.ChangeWeatherAndTime`.
       - Force write via `SaveDataManager`.
  - Recommended when:
    - UI/effects desync occurs.
    - Sunset/sunrise transition does not trigger on time.
    - Offline mode where F7 cannot fetch online data.

## 🔧 Technical Details

- **Framework**: BepInEx 5.x
- **Target Framework**: .NET Framework 4.7.2
- **Tech / Tools**:
  - Harmony patches (feature injection)
  - Unity coroutines (async web requests)
  - Reflection (access internal game systems)
  - dnSPY
  - Multiple large language models

## 📝 Version History

> Note: Version numbers are generated by AI and may look random.

### b5.1.4 - Awakened for This Lifetime

- Fixed mod breakage caused by game update.
  - Currently supports latest game version v1.3.4.
- No additional changes for now.
- Future updates will be tracked, but not immediately.

### v5.1.3 - Last Two Months

- Fixed UI hook failures caused by game text changes.
- Fixed time transition failure caused by game update changes.
- Fixed hotkey failures caused by cascading errors above.
- Added unlock support for currency-gated items in "Change Mood".
  - Disabled by default; enabling may significantly shorten progression.
  - No hot reload support; change while game is closed or restart afterward.
  - Does not affect save file. You can still manually redeem after disabling unlock option.
- This version was developed in about 4 hours, with limited scenario testing.

### v5.1.2 - Old Pickle Cabbage Edition (?)

- Added built-in key; if abnormal key usage is detected, forced reset may occur.
- Fixed UI interaction issues where controls could stop responding.
- Fixed issue where BGM could not be disabled after easter egg mode activation.
- Fixed weather toggle being incorrectly tracked as dirty state.

### b5.1.1

- Added rewritten AM/PM display into richer time-of-day labels.

### b5.0.1 - Time to Cook

- Mostly enum updates delegated to Gemini.
- Intended as a pre-update stable version unless bugs appear.

### v5.0.0

- Added `SceneryAutomationSystem` as independent component with its own update loop.
- Refactored to rule-driven scenery logic via `SceneryRule`.
- Added user dirty-flag mechanism by hooking manual toggle clicks.

### v4.5.0

- Added weather and temperature display after top-left date text.
- Added optional config `ShowWeatherOnDate`.
- Requires `Unity.TextMeshPro.dll`.

### v4.4.1 - Better Sky

- Removed light rain/snow/shower/flurry from severe weather list.

### v4.4.0

- Refactored environment and scenery derivation logic.

### v4.3.1

- Follow-up optimization for issues in v4.3.0.

### v4.3.0

- Fixed issue where pressing `F9` in cloudy mode could disable all environments.

### v4.2.3 - v4.2.4

- Attempted fixes but issue remained.

### v4.2.2

- Identified cloudy-state `F9` bug (all environments incorrectly off).

### v4.2.1

- Added debug mode for testing weather ID mapping behavior.

### v4.2.0

- Fixed issue where `F7` did not respond.

### v3.7.0 - v4.1.0

- Optimized `F9` logic and log messages.
- Fixed occasional log freeze.
- Corrected sunset offset from 1 hour to 30 minutes.

> Cache logic summary:
> - `_cachedWeather` is stored in memory.
> - Cache valid for 60 minutes (`TimeSpan.FromMinutes(60)`).
> - Fast clock (every 30s): reads cache and recomputes with current time, no network call.
> - Slow clock (every 30m by default): attempts fresh API call when refresh interval is reached.

### v3.6.0 - Don't Want Cheats? Here You Go

- Added optional unlock configs for all environments/decorations.

### v3.5.0 - Probably Runs Edition

- Switched button logic to simulated `MainIcon` click.
- Fixed environment switching reliability.
- Improved structure and logging.

### v3.4.x

- Fixed cases where weather effects could not be disabled.
- Improved mutually exclusive environment logic.

### v3.3.0 and earlier

- Source analysis and initial implementation.

Detailed changelog: [Git commit history](https://github.com/Small-tailqwq/RealTimeWeatherMod/commits/master)

## 🐛 Known Issues

- First sync may occur after about 15 seconds on initial load.
- F9 may occasionally cause partial button UI anomalies, but gameplay remains unaffected.

## 🤝 Contribution

Issues and pull requests are welcome.

### About Bug Reports

Please ensure issue is reproducible, and enable debug logs by setting `Logging.Console = true` in `BepInEx/config/BepInEx.cfg`.
The startup console log will then provide more details for troubleshooting.

## 📄 License

This project is licensed under **MIT**.

**Important statement:**

- You can use, modify, and redistribute freely.
- You can use it for personal learning and research.
- Any consequence caused by using this software is the user's own responsibility.

See [LICENSE](LICENSE) for details.

## 👨‍💻 Author

- GitHub: [@Small-tailqwq](https://github.com/Small-tailqwq)

## 🙏 Acknowledgements

- BepInEx team
- Harmony patch library
- Duvet
- Seniverse API service
- Google Gemini 3 Pro
- GitHub Copilot
- Claude Sonnet and Opus 4.5
- OpenAI ChatGPT 5.1
- GitHub Copilot

---

**Disclaimer:**
This plugin is for learning and communication only. Please do not use it for commercial purposes.
The author is not responsible for any issues caused by using this plugin.
This is a fan-made project and is not affiliated with the game developers or publishers.
All rights for game assets, trademarks, and related materials belong to their respective owners.

> Even if life is a mess, keep hope.
