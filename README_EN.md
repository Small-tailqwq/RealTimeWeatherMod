# Chill Env Sync (Real-time Weather Mod)
> ⚠️ **Yes, this readme was also written by AI.** 🤖

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Framework 4.7.2](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net472)
[![BepInEx](https://img.shields.io/badge/BepInEx-Plugin-green.svg)](https://github.com/BepInEx/BepInEx)

A BepInEx plugin for the game *Chill with You - Lo-Fi Story*. It automatically synchronizes the in-game environment with real-world weather or simulates a day/night cycle based on your local time.

---

### 📢 Important Note for International Users

> **OpenMeteo support is now built in.**
> This mod can use either **Seniverse** or **OpenMeteo** as the weather provider.
> If you specifically need **OpenWeatherMap** support or a fork focused on English localization, you can still check out this community fork:
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

- 🌤️ **Real-time weather sync**: Fetches weather data from Seniverse or OpenMeteo and updates in-game environment automatically.
- 🌍 **City/coordinate support**: Seniverse supports city names or `ip` auto-location; OpenMeteo supports latitude/longitude.
- 🌓 **Day/night cycle**: Auto switches between Day/Sunset/Night based on configured sunrise and sunset time.
- 🧭 **Decoupled sync toggles**: Weather sync and time sync are now independently switchable.
- 🔓 **Unlock all environments**: Unlocks all environments and decorations in current session.
  - This does not modify save data.
  - Optional config items:
    - `UnlockAllEnvironments`: Unlock all environment scenes.
    - `UnlockAllDecorations`: Unlock all decorations.
    - `UnlockPurchasableItems`: Unlock all in-game currency purchases.
- ⌨️ **Hotkeys**:
  - `F7` - Force refresh weather.
    - Ignores cache and fetches latest weather from the currently selected provider.
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

`Automation.EnableAmbientSounds` (off by default) automates cooking, air conditioner and cicada sounds according to their conditions. Disabling it retains the existing ownership and cleanup rules; it is not a global mute.

`Automation.EnablePrecipitationSounds` separately controls rain audio. It defaults to `true`, preserving rain audio for existing configs without this key. Set it to `false` to keep synchronized light, heavy and thunderstorm rain visuals while muting their sounds, without affecting character voices, action sounds, other ambient audio or snow. With weather sync enabled, the settings-panel toggle updates current rain audio immediately without refreshing weather; cutscene and whale overrides still take priority. After editing the config file, press F7 to reload and sync. Both switches are independent. Manual rain controls retain the game's behavior.

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
## Enable automatic seasonal scenery easter eggs
EnableSeasonalEasterEggs = true
## Automate cooking, air conditioner and cicada sounds (does not control rain audio)
EnableAmbientSounds = false
## Play precipitation sounds during weather sync; false keeps rain visuals and mutes rain audio; panel changes update current rain audio immediately
EnablePrecipitationSounds = true
## Follow active in-game rain; block opening and request closing after 30–90s in light rain, 3–8s in rain, or 1–4s in thunderstorms, allowing existing actions to finish
CloseWindowWhenRaining = true


[Debug]
## Enable debug mode | Leave this off unless debugging
EnableDebugMode = false
## Simulated weather code
SimulatedCode = 1
## Simulated temperature
SimulatedTemp = 25
## Simulated weather text
SimulatedText = DebugWeather
## Simulated current time; accepts HH:mm[:ss] or a full date and time
SimulatedDateTime = 12:00
## Simulated sunrise/sunset; provider refresh cannot overwrite these in debug mode
SimulatedSunrise = 06:30
SimulatedSunset = 18:30

[Internal]

## Last sync date | Leave this unchanged unless debugging
LastSunSyncDate =
## Automatically managed environments; maintained by the plugin
AutoManagedEnvironments =

[TimeConfig]
## Sunrise (HH:mm) | v5.1.2+ usually does not require manual edits
Sunrise = 06:30
## Sunset (HH:mm) | v5.1.2+ usually does not require manual edits
Sunset = 18:30

[TimeSync]
## Enable time sync (Day/Sunset/Night auto control)
EnableTimeSync = true

[UI]
## Show real-time weather and temperature on date bar
ShowWeatherOnDate = true
## Show detailed time segments in 12-hour mode
DetailedTimeSegments = true


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
## Weather provider: Seniverse or OpenMeteo
WeatherProvider = OpenMeteo
## Seniverse API key | v5.1.2+ can use built-in key when left empty
SeniverseKey =
## Seniverse city name (Pinyin or Chinese, e.g. beijing, 上海). Use ip for auto locate; empty means ip
Location = beijing
## OpenMeteo latitude, WGS84 coordinate system, valid range [-90, 90]
Latitude = 39.9075
## OpenMeteo longitude, WGS84 coordinate system, valid range [-180, 180]
Longitude = 116.39723


[WeatherSync]
## API refresh interval in minutes | Leave as is unless debugging
RefreshMinutes = 30
## Weather cache lifetime in minutes | Leave as is unless debugging
CacheExpiryMinutes = 60
```

### Weather Provider Selection

- `WeatherProvider = Seniverse`: use Seniverse. `Location` is used; `SeniverseKey` may be left empty to use the built-in key. An empty `Location` falls back to `ip` auto-location rather than stopping sync.
- `WeatherProvider = OpenMeteo`: use OpenMeteo. `Latitude` / `Longitude` are used; no API key is required.
  - These two fields use the **WGS84** coordinate system, which is the only system the Open-Meteo API accepts. The defaults 39.9075 / 116.39723 come from the [Open-Meteo geocoding result for Beijing](https://geocoding-api.open-meteo.com/v1/search?name=Beijing&count=1&format=json). Updated defaults do not overwrite existing configuration.
  - Amap and Tencent maps typically use GCJ-02, while Baidu Maps typically uses BD-09; entering these coordinates directly can introduce a position offset. Convert them to WGS84 first, or take coordinates from the Open-Meteo geocoding API: `https://geocoding-api.open-meteo.com/v1/search?name=City&count=1&format=json`.
- If a recent [iGPUSaviorMod](https://github.com/Small-tailqwq/iGPUSaviorMod) is installed, the in-game settings page shows provider-specific fields automatically: city input for Seniverse, latitude/longitude inputs for OpenMeteo.

### Deterministic Debug Scenarios

- Debug mode replaces the weather, current date/time, sunrise, and sunset observed by the mod. Startup and periodic sync use the same simulated inputs, and background sun-schedule requests are paused.
- `SimulatedDateTime` accepts `17:45` or a full value such as `2026-12-24 23:45` for seasonal-rule testing.
- Press `F7` after editing the config to reload and apply it immediately. With the repository's UnityExplorer MCP Bridge installed, use `envsync_debug` to set inputs, force application, and compare expected versus actual game state in one call. See [UnityExplorerMcp/README.md](./UnityExplorerMcp/README.md).

### Get Seniverse API Key (Optional)

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

- From sunrise to 30 minutes before sunset: Day.
- From 30 minutes before sunset to 30 minutes after sunset: Sunset.
- All other times: Night.

### Weather Sync Mode (Optional)

1. Select a weather provider as above, then configure either city or coordinates.
2. Plugin checks weather periodically (default every 30 minutes), reusing valid cache and fetching only after cache expiry (default 60 minutes).

3. Environment mapping:

| Environment | Normal Weather | Severe Weather |
| ------ | --------- | --------- |
| ☀️ Day (Day) | Sunrise -> 30 min before sunset | — |
| 🌅 Sunset (Sunset) | 30 min before sunset -> 30 min after sunset | — |
| ☁️ Cloudy (Cloudy) | — | Sunrise -> 30 min after sunset |
| 🌙 Night (Night) | 30 min after sunset -> sunrise | 30 min after sunset -> sunrise |

> 💡 The table describes discrete behavior when smooth transitions are disabled or unavailable. The Cloudy override requires both time and weather sync. With smooth transitions enabled, graded clouds and fog overlay each time period while preserving sunset colors; sunlight and rain intensity vary continuously. Dawn, evening fade, full sunset and nightfall last 120/60/40/75 minutes respectively; manual transitions take 2 seconds and rain intensity fades over 6 seconds. Weather sync alone keeps the base time period unchanged and can still use continuous rain. See [smooth time transitions](docs/smooth-time-transitions.md) (Chinese).

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
    - Ignore cache and fetch latest weather from the currently selected provider.
    - Reset API slow timer.
  - Trigger behavior:
    1. Reset timer for next auto API call.
    2. Ignore `CacheExpiryMinutes`, send HTTP request to Seniverse or OpenMeteo directly.
    3. If success, replace in-memory `_cachedWeather`.
    4. Apply scene changes according to the current time and weather sync switches; when both are off, only weather data and enabled date-bar text are refreshed.
  - Recommended when:
    - Real weather changed but game still shows old weather.
    - You want to test API connectivity.
    - You changed `Location`, `Latitude`, or `Longitude` and want immediate effect.

- **F8: Print current environment status**
  - Writes one status log line.

- **F9: Manual weather sync**
  - Core purpose:
    - Prefer a valid weather cache and the real-time local clock, then recalculate and apply the environment.
    - If the cache is missing or expired and the current policy needs weather data, the selected provider is queried normally.
  - Trigger behavior:
    1. Read a valid cache, or fetch weather when the current sync policy requires it.
    2. Recompute day/sunset/night by current `DateTime.Now`.
    3. Reapply the base environment through `GameEnvironmentRuntime` and reconcile precipitation visuals and audio.
  - Recommended when:
    - UI/effects desync occurs.
    - Sunset/sunrise transition does not trigger on time.
    - Offline mode while a valid cached weather result is still available.

## 🔧 Technical Details

- **Framework**: BepInEx 5.x
- **Target Framework**: .NET Framework 4.7.2
- **Architecture**: See [docs/architecture.md](./docs/architecture.md) for module boundaries and dependency direction.
- **Tech / Tools**:
  - Harmony patches (feature injection)
  - Unity coroutines (async web requests)
  - Reflection (access internal game systems)
  - dnSPY
  - Multiple large language models

## 📝 Version History

See [CHANGELOG.md](./CHANGELOG.md) and the [Git commit history](https://github.com/Small-tailqwq/RealTimeWeatherMod/commits/master) for released changes.

## 🐛 Known Issues

- Initial sync checks readiness every 0.25 seconds for up to 30 seconds; timing depends on game loading.
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
- OpenMeteo API service
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
