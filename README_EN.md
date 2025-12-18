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

> "Chill with You: Lo-Fi Story" is a visual novel game where you work alongside Satone, a girl who loves writing stories. You can customize original music by artists, ambient sounds, and scenery to create an environment focused on work. As your relationship with Satone deepens, you may discover a special connection with her.

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
- Related Bilibili Video: [Time, Weather, and Potatoes](https://www.bilibili.com/video/BV1JXSiB4EP1) (<-- Pure clickbait, no real substance)


## ✨ Key Features

- 🌤️ **Real-Time Weather Sync**: Fetches real weather data via the Seniverse API and adjusts the game environment.
- 🌍 **Multi-City Support**: Supports weather queries for any city (Pinyin or Chinese).
- 🌓 **Day/Night Cycle**: Automatically switches between Day/Sunset/Night based on configured sunrise/sunset times.
- 🔓 **Unlock Everything**: Automatically unlocks all environments and decorations.
  - *Note: This is session-based and does not modify your save file.*
  - Configurable via:
    - `UnlockAllEnvironments`: Unlock all environment scenes
    - `UnlockAllDecorations`: Unlock all decoration items
    - `UnlockPurchasableItems`: Skip in-game currency purchases (colors/variants) - **May significantly reduce gameplay time**
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
> Note: The game logic only allows one to be active at a time, but you can force multiple on. Not sure when that would be useful other than smooth scene transitions.
- ☀️ Day
- 🌅 Sunset
- 🌙 Night
- ☁️ Cloudy

### Precipitation & Effects
> Note: The following are in-game effects. For real-world weather mapping logic, see the "Usage Guide" section below.
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

> ⚠️ **Important: Follow the order strictly!**

1. Install BepInEx framework correctly.
2. **Launch the game FIRST to let BepInEx initialize**
   - Start the game and wait until it loads to the main menu
   - Look for the BepInEx console window
   - Confirm that the `BepInEx/plugins/` folder has been created
   - Exit the game
3. Place `RealTimeWeatherMod.dll` into the `BepInEx/plugins/` folder.
4. Launch the game again. The plugin will load automatically.
5. Edit the configuration file (located at `BepInEx/config/chillwithyou.envsync.cfg`).
6. Press `F7` in-game to reload config after editing.

**Troubleshooting**: If the mod doesn't work, make sure you let BepInEx initialize completely before installing the mod!

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
UnlockAllDecorations = false

## Skip in-game currency purchases (color/type variants)
## WARNING: Enabling this will significantly shorten gameplay lifespan.
## Requires game restart to take effect.
UnlockPurchasableItems = false

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

  - **Day**: Sunrise → 30 mins before Sunset.
  - **Sunset**: 30 mins before Sunset → 30 mins after Sunset.
  - **Night**: Everything else.

### Weather Sync Mode (Requires API)

1. Configure your API Key as described above.
2. The plugin will automatically fetch weather at the specified interval (default: 30 minutes).

3. Environment Mapping Rules:

| Environment | Normal Weather | Severe Weather |
|-------------|----------------|----------------|
| ☀️ Day | Sunrise → 30 mins before Sunset | — |
| 🌅 Sunset | 30 mins before Sunset → 30 mins after Sunset | — |
| ☁️ Cloudy | — | Sunrise → 30 mins after Sunset |
| 🌙 Night | 30 mins after Sunset → Sunrise | 30 mins after Sunset → Sunrise |

> 💡 During severe weather, daytime and sunset periods are unified as Cloudy, and the Sunset effect is skipped. Night environment is not affected by weather and always kicks in at 30 minutes after sunset.

4. Severe Weather List:

```
4, 7, 8, 9 (Cloudy/Overcast)
11, 12, 14-20 (Moderate rain and above, Thunderstorms, Freezing rain)
23-25 (Moderate snow and above)
26-31 (Dust/Fog)
34-36 (Storms)
```

5. Weather Effect Mapping:

| Game Effect | Corresponding API Weather IDs | Logic |
|-------------|-------------------------------|-------|
| Snow | 20-25 (Snow/Sleet/Blizzard) | All snow types → Game Snow effect |
| ThunderRain | 11, 12, 16, 17, 18 (Thunder/Torrential rain) | Thunderstorms, torrential rain, extreme rain → Heavy intensity |
| HeavyRain | 10, 14, 15 (Showers/Moderate/Heavy rain) | Regular rainfall, larger volume |
| LightRain | 13, 19 (Drizzle/Freezing rain) | Light, pitter-patter rain |
| OFF | 0-9, 26-39 (Clear/Cloudy/Overcast/Fog/Wind) | Key point: If overcast or cloudy, must force disable all rain/snow effects! |

### Hotkey Details

  - **F7 (Force Refresh)**:
      - **Core Purpose:**
          - Ignores cache, forces a fresh API request to Seniverse for the latest real-time weather data.
          - Resets the API slow-clock timer.
      - **Triggered Actions:**
          1. Clear timer: Resets the countdown for the next automatic API request.
          2. Force network request: Ignores CacheExpiry (60-minute cache period), directly sends HTTP request to api.seniverse.com.
          3. Data update: If successful, overwrites `_cachedWeather` in memory with new data.
          4. Auto-apply: After getting new data, automatically calls ApplyEnvironment to update the game scene.
      - **Use when:**
          - You think the weather changed (e.g., it just started raining outside) but the game still shows sunny (because it's still within cache period).
          - Debugging API connection.
          - After changing `Location` (city) in config and wanting it to take effect immediately.

  - **F8 (Show Status)**:
      - Prints a log entry. Not very useful, honestly.

  - **F9 (Manual Trigger)**:
      - **Core Purpose:**
          - No network request. Uses existing weather cache and real-time local time to force recalculation and application of game environment.
          - Key point: It bypasses the "debounce" check (i.e., `if (Current == Target) return`), forcing a complete environment switch flow (lights off → lights on → service sync).
      - **Triggered Actions:**
          1. Read cache: Directly reads `_cachedWeather` from memory (if no cache, only calculates time).
          2. Recalculate time: Based on current time (`DateTime.Now`), recalculates whether it's day, sunset, or night.
          3. Brute force execution:
             - Calls ForceActivateEnvironment.
             - Force simulates clicking MainIcon (even if it appears to be lit).
             - Force calls WindowViewService.ChangeWeatherAndTime.
             - Force writes to SaveDataManager.
      - **Use when:**
          - Fixing desync: e.g., game UI shows "Rainy" but the rain stopped visually; or sound is still playing but icon is off. Press F9 to "kick" the game back into sync.
          - Sunset/Sunrise testing: When you think it's time (e.g., 17:30) but game hasn't switched to sunset (maybe fast-clock hasn't ticked), F9 immediately triggers time judgment.
          - Offline mode: When disconnected, F7 will fail, but F9 can still refresh local time logic (day/night switching).

## 🔧 Technical Details

  - **Framework**: BepInEx 5.x (.NET Framework 4.7.2)
  - **Tech Stack**: Harmony (Patching), Unity Coroutines, Reflection, dnSPY.
  - **Collaborators**: A bunch of Large Language Models.

## 📝 Version History (Highlights)

*Note: Version numbers are hallucinated by AI. I have no control over this.*

### v5.1.3 - The Last Two Months

  - Fixed UI layer display content unable to hook correctly due to game word corrections.
  - Fixed time switching not working due to game update changing time adjustment methods.
  - Fixed hotkey not working due to the above errors.
  - Added unlock support for items in "Change Mood" that require in-game currency to purchase.
    - Note: This config option is **disabled by default**. Enabling it may significantly shorten gameplay lifespan.
    - Enabling this config does not support hot-reload. Ensure the game is closed when modifying or restart after modification.
    - This config does not affect save files. You can still manually purchase items after disabling unlock features and restarting.
  - This version took approximately 4 hours to develop. Some scenarios may not have been thoroughly tested. If you encounter bugs, please report via GitHub.

### v5.1.2 - The "LaoTanSuanCai" Edition (?)

  - Added a built-in fallback Key. If abnormal key usage is detected, forced reset behavior may occur.
  - Fixed UI interaction bugs that could make controls unresponsive.
  - Fixed BGM issues where background music couldn't be turned off after easter egg mode activated.
  - Fixed weather toggles being incorrectly tracked in the dirty directory.

### b5.1.1

  - Refined time definitions (Late Night, Dawn, Morning, Noon, Afternoon, Dusk, Evening).
  - Added a feature to rewrite AM/PM display.

### b5.0.1 - It's Cooking Time

  - Moved scenery enums to Gemini. It bumped the version number for no reason.
  - Likely the final version before the next game update. Unless there are bugs to fix, I can't think of new features. Feel free to suggest ideas.

### v5.0.0

  - Wait, the version jumped straight to v5? What the hell did you do?!
  - **Independent Component**: Created a new `SceneryAutomationSystem` class (inherits `MonoBehaviour`), mounted on `Runner`, so it has its own `Update` loop without interfering with the original weather sync logic.
  - **Rule-Driven**: Gemini abstracted "cooking sounds", "AC sounds", "cherry blossoms", "fireworks", etc. into `SceneryRule`. This way, adding new easter eggs only requires adding a config line without touching core code.
  - **User Anti-Conflict Mechanism (Dirty Flag)**: This is the core. By `Hooking` the game's click events, once a user manually clicks a toggle, the system blacklists that toggle and stops auto-managing it for the session. (<--- Isn't this just being lazy?)
  - Note: The easter egg trigger mechanism is complicated to explain. Those with ability can feed the code to AI or analyze it themselves. Currently only partial parameters are used for judgment; optimization may occur after future game updates.

### v4.5.0

  - Added a feature to display weather and temperature after the date in the top-left corner of the main screen.
  - Added optional config `ShowWeatherOnDate` to control whether to append weather and temperature after the date.
  - This feature requires referencing `Unity.TextMeshPro.dll`.

### v4.4.1 - Better Skies

  - Protected some people's daytime or sunset.
  - Removed light rain, light snow, showers, and flurries from the severe weather list (these are not severe weather).

### v4.4.0

  - Protected the night sky.
  - Refactored environment/scenery derivation logic. Now night will always be night. During severe weather in daytime, it might be cloudy, or might be day/sunset. Who knows?

### v4.3.1

  - Said to have optimized the cause of issues in v4.3.0.
  - Couldn't you have optimized while fixing?! (ノಠ益ಠ)ノ

### v4.3.0

  - Fixed an issue where pressing `F9` in cloudy state would incorrectly turn off all environments.

### v4.2.3-v4.2.4

  - Still not fixed. Gemini, get out.

### v4.2.2

  - Discovered an issue where pressing `F9` in cloudy state would incorrectly turn off all environments.
  - Then Gemini failed to fix it.

### v4.2.1

  - Added a debug mode. Started testing weather ID adjustments to see corresponding weather switch effects. (Wait, you're only starting to test NOW?!)
  - Told Gemini to stop bumping version numbers.

### v4.2.0

  - Fixed an issue where `F7` wasn't responding. Added some output, supposedly.

### v3.7.0-v4.1.0 - Outside AI Came to Bump Version Numbers

  - Optimized `F9` key logic, improved log prompts.
  - Fixed occasional log freeze states (didn't affect usage anyway).
  - Fixed previous miscommunication with AI: It always thought sunset should switch 1 hour early, but it's actually 30 minutes.
  - Optimized switching logic (is it really optimized though?).

> Cache logic works like this:
> - Data storage: There's a static variable `_cachedWeather` stored in memory.
> - Validity period: 60 minutes (`TimeSpan.FromMinutes(60)`).
> - Fast clock (every 30 seconds):
>   - Only reads cache. If there's data in cache, uses it directly without network request (saves data).
>   - It uses the cached "sunny/rainy" state and combines it with "current time every second" to judge if sunset should occur.
>   - Note: I originally wanted AI to write a timer, but AI said this was better. I don't understand so I listened.
> - Slow clock (every 30 minutes):
>   - Force update. Even if cache hasn't expired (60 minutes), as long as the user-set refresh interval is reached (default 30 minutes), it will attempt a new API request to refresh data.

### v3.6.0 - Don't Want to Cheat? Granted.

  - Added optional config items for unlocking all environments and decorations.

### v3.5.0 - Probably Runs Edition

  - Optimized button logic, using simulated `MainIcon` click method.
  - Fixed environment switching not taking effect issue.
  - Improved code structure and log output.

### v3.4.x

  - Fixed some weather effects not turning off.
  - Optimized environment mutual exclusion logic.

### v3.3.0 and Earlier

  - Analyzed source code, environment toggle logic, scenery toggle logic.
  - Initial version development.
  - Who would've thought it took 3 different AIs from different vendors and a whole day to write this. I'm dead tired.

For detailed changelog, see [Git Commit History](https://github.com/Small-tailqwq/RealTimeWeatherMod/commits/master).

## 🐛 Known Issues

- First load may take about 15 seconds before the first environment sync occurs (this shouldn't count as an issue, right? ~~A gentleman never rushes, so I added some delay?~~)

## 🤝 Contribution

Issues and Pull Requests are welcome!

### About Reporting Issues

If you need to report a problem, please first ensure the issue is "reproducible", and enable debug logging (set `Logging.Console` to `true` in `BepInEx/config/BepInEx.cfg`).
This way, detailed logs will be output to the console when the game starts, helping to locate the problem.

## 📄 License

**MIT License**.
You can use, modify, distribute, and learn from this.

**⚠️ Important Declaration:**

  - ✅ Free to use, modify, and distribute.
  - ✅ Can be used for personal learning and research.
  - ❌ Do not sell this DLL.
  - Any consequences from using this software are borne by the user.

See [LICENSE](LICENSE) file for details.

## 👨‍💻 Author

  - GitHub: [@Small-tailqwq](https://github.com/Small-tailqwq)

## 🙏 Acknowledgements

  - BepInEx Team
  - Harmony Library
  - Seniverse API
  - Google Gemini 3 Pro
  - OpenAI ChatGPT 5.1
  - Claude Sonnet and Opus 4.5
  - **My liver, eyes, and butt** (I really need a new chair and some eye drops).

-----

**Disclaimer:**
This plugin is for learning and communication purposes only. Please do not use it for commercial purposes. Any issues arising from the use of this plugin are not the author's responsibility.
This is a fan-made project and is not affiliated with the game developers or publishers.
The game "Chill with You - Lo-Fi Story" and its related assets and trademarks are copyrighted by their respective owners.

> *There is always a lava lamp on my desk.*
