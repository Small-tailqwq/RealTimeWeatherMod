# Chill Env Sync（实时天气同步插件）

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Framework 4.7.2](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net472)
[![BepInEx](https://img.shields.io/badge/BepInEx-Plugin-green.svg)](https://github.com/BepInEx/BepInEx)
<a href="https://github.com/anomalyco/opencode"><img src="https://github.com/anomalyco/opencode/raw/dev/packages/console/app/src/asset/logo-ornate-dark.svg" height="20" alt="opencode"></a>
[![opencode](https://img.shields.io/badge/opencode-go-000?style=flat)](https://opencode.ai/go?ref=X83D5WQ2NS)
> 使用 Opencode 等工具进行构建，由 Opencode GO 提供模型支持

一个用于游戏 《*放松时光：与你共享Lo-Fi故事*》 的实时天气同步 BepInEx 插件，可以根据真实世界的天气情况自动调整游戏内的环境效果，或基于现实时间模拟昼夜循环。

---

[![Chill with You](https://raw.githubusercontent.com/Small-tailqwq/RealTimeWeatherMod/refs/heads/master/header_schinese.jpg)](https://store.steampowered.com/app/3548580/)

> 「放松时光：与你共享Lo-Fi故事」是一个与喜欢写故事的女孩聪音一起工作的有声小说游戏。您可以自定义艺术家的原创乐曲、环境音和风景，以营造一个专注于工作的环境。在与聪音的关系加深的过程中，您可能会发现与她之间的特别联系。
---
[English Version](./README_EN.md)


> 所有代码均由 AI 编写，人工仅作反编译和排错处理。
> 这是我第一次用 AI 为 Unity 游戏做 MOD，有问题请反馈，虽然反馈了我也是再去找 AI 修就是了🫥

---

关联我写的第二个辅助 mod：[iGPUSaviorMod](https://github.com/Small-tailqwq/iGPUSaviorMod)  
> 依托此 MOD，可以将部分配置项目注册到游戏的设置界面，进行快捷配置。

关联 BILIBILI 视频：[时间、天气与土豆](https://www.bilibili.com/video/BV1JXSiB4EP1) （<-- 评价为：纯引流，啥干货没有）


## ✨ 主要功能

- 🌤️ **实时天气同步**：通过心知天气或 OpenMeteo 获取真实天气数据，自动调整游戏内环境
- 🌍 **多城市/坐标支持**：心知天气支持城市名或 `ip` 自动定位，OpenMeteo 支持经纬度定位
- 🌓 **昼夜循环**：根据配置的日出日落时间自动切换白天/黄昏/夜晚场景
- 🧭 **同步开关解耦**：天气同步与时间同步可独立开关，互不强依赖
- 🔓 **解锁所有环境**：自动解锁所有环境和装饰品
  - 此选项不会影响存档，仅在当前游戏会话中生效
  - 可选配置项
	  - `UnlockAllEnvironments`：是否解锁所有环境
	  - `UnlockAllDecorations`： 是否解锁所有装饰品
	  - `UnlockPurchasableItems`：是否解锁所有游戏币解锁内容
- ⌨️ **快捷键操作**：
  - `F7` - 强制刷新天气
	  - 忽略缓存，强制从当前选择的天气源获取最新实时天气数据。
	  - 重置 API 慢钟计时器。
  - `F8` - 显示当前状态
	  - 在日志中输出一条信息，没啥用
  - `F9` - 手动触发同步
	  - 使用当前已有的天气缓存和实时的本地时间，强制重新计算并应用游戏环境

## ⚠️ 本项目可能有

- 💽**外挂嫌疑**：本插件会默认修改游戏中的环境和装饰品解锁状态，但是**不会写入存档**。*如果你打算自己解锁，还请自己调整配置文件（详见下文）*
- 💥**莫名冲突**：如果未来游戏更新或者 Mod 大井喷，本插件很可能与其他插件发生冲突。*因为我也知道我和 AI 配合写得很烂，届时还请别抱太大修复希望*
- 💸**仅供学习**：本插件采用了 MIT许可证，法律上来说你可以想干啥干啥，但是请**不要直接拿 DLL 去卖**。
- 🧷**外部链接**：本插件使用了心知天气、OpenMeteo 等第三方天气 API 服务，使用时请注意隐私和数据安全。且第三方数据服务提供范围请以实际为准
- 😵‍💫**粗制滥造**：本插件完全由 AI 编写，可能存在各种问题和漏洞，请谨慎使用。*我完全不懂 C#，那你叫我怎么办嘛*
- 🧱**区域限定**：获取天气功能、UI 显示天气数据等功能可能只在**部分地区**、**部分语言**环境下可用，对此深感歉意。
- 🤖**智械危机**：使用本插件可能会加速AI统治世界的进程，后果自负。（<---这段不是我自己写的）


## 🎮 支持的环境类型

### 基础环境（互斥）

> 注：游戏逻辑上只能同时存在一个开启，但是实际上可以强开多个。感觉除了平滑切换场景能用上外感觉没啥必要提这一嘴
- ☀️ 白天 (Day)
- 🌅 黄昏 (Sunset)
- 🌙 夜晚 (Night)
- ☁️ 多云 (Cloudy)

### 降水效果
> 注:下列为游戏内效果，具体现实天气对应逻辑请看下文“使用方法”部分
- 🌧️ 小雨 (LightRain)
- 🌧️ 大雨 (HeavyRain)
- ⛈️ 雷雨 (ThunderRain)
- ❄️ 雪 (Snow)

### 风景效果
开启彩蛋功能后，系统可能会随机或者在满足特定条件后主动开启某些风景效果。

### 声音效果
开启 `Automation.EnableAmbientSounds`（默认关闭）后，系统会按条件托管做饭、空调和蝉鸣；关闭时沿用原有托管清理与用户接管规则，不是全局静音。

雨声由独立的 `Automation.EnablePrecipitationSounds` 控制（默认开启，旧配置缺少此项时保留原有雨声行为）。设为 `false` 后，天气同步仍切换小雨、大雨、雷雨画面，但会静音对应雨声，不影响角色语音、动作声、其他环境音或雪景。天气同步开启时，在设置面板切换会立即更新当前雨声，无需刷新天气；过场或鲸鱼覆盖期间仍遵循同步保护。修改配置文件后按 F7 重载并同步。两项开关可以独立组合，手动雨景操作仍由游戏控制。

### TODO

>  🗒️ 本项目的未来计划和待办清单

- [x] 基础功能优化
- [x] 根据季节、天气等因素展示B类风景
- [x] 根据季节、时间等因素展示C类风景
- [x] 根据时间、季节等因素自动触发背景音乐
- [x] 代码重构，拆分为多个子模块，方便后续 AI 维护
- [x] 应付将来的游戏更新（成功活到了 ver1.1）
- [x] 永远都修不够的 BUG
- [x] 抽空在土豆模式中对 todo、note 等功能进行优化


## 📦 安装方法

### 前置要求
- 游戏本体
- [BepInEx 5.4.23.4](https://github.com/BepInEx/BepInEx/releases) (本插件基于此版本开发)

### 安装步骤

> ⚠️ **重要：必须按顺序安装！**  
> ⚠️ **重要：请在[发布/releases](https://github.com/Small-tailqwq/RealTimeWeatherMod/releases)界面下载 dll 文件**

1. 确保已正确安装 BepInEx 框架
2. **先启动一次游戏，让 BepInEx 完成初始化**
   - 游戏启动后，等待加载到主界面
   - 看到 BepInEx 控制台窗口出现
   - 确认 `BepInEx/plugins/` 文件夹已创建
   - 退出游戏
3. 将下载好的 `RealTimeWeatherMod.dll` 放入 `BepInEx/plugins/` 目录
4. 再次启动游戏，插件将自动加载
5. 编辑配置文件（位于 `BepInEx/config/chillwithyou.envsync.cfg`）
6. 游戏中按 `F7` 可以刷新配置

**常见问题**：
- 如果 mod 无法正常工作，请确保先让 BepInEx 初始化完成后再安装 mod！
- 如果找不到 `*.dll`，说明你下载的是源码（Source code），而不是发布产物（releases）

## ⚙️ 配置说明

首次运行后，配置文件将生成在 `BepInEx/config/chillwithyou.envsync.cfg`

### 配置项说明

```ini
[Automation]
## 启用季节性景色彩蛋自动托管
EnableSeasonalEasterEggs = true
## 启用做饭、空调、蝉鸣环境音效自动托管（不控制雨声）
EnableAmbientSounds = false
## 天气同步时开启降水声音；false 保留雨景并静音雨声，设置面板切换立即更新当前雨声
EnablePrecipitationSounds = true
## 跟随游戏内实际雨景，阻止角色自动开窗；已开窗时，小雨 30–90 秒、雨 3–8 秒、雷雨 1–4 秒后请求关窗，等待现有动作完成
CloseWindowWhenRaining = true


[Debug]
## 是否开启调试模式 | 非调试请勿动
EnableDebugMode = false
## 模拟天气代码
SimulatedCode = 1
## 模拟温度
SimulatedTemp = 25
## 模拟天气描述
SimulatedText = DebugWeather
## 模拟当前时间；支持 HH:mm[:ss]，也可带日期测试季节规则
SimulatedDateTime = 12:00
## 模拟日出/日落；调试模式下不会被天气源后台同步覆盖
SimulatedSunrise = 06:30
SimulatedSunset = 18:30

[Internal]

## 上次同步日期 | 非调试请勿动
LastSunSyncDate =
## 自动托管环境记录，由插件维护
AutoManagedEnvironments =

[TimeConfig]
## 日出时间（格式：HH:mm） | 5.1.2版本不用手动修改
Sunrise = 06:30
## 日落时间（格式：HH:mm） | 5.1.2版本不用手动修改
Sunset = 18:30

[TimeSync]
## 是否启用时间同步（白天/黄昏/夜晚）
EnableTimeSync = true

[UI]
## 是否在游戏日期栏显示实时天气和温度
ShowWeatherOnDate = true
## 12 小时制下显示详细时段
DetailedTimeSegments = true


[Unlock]
## 是否自动解锁所有环境场景 | 可能包含某些未实装的场景，谨慎开启
UnlockAllEnvironments = true
## 是否自动解锁所有装饰品 | 可能包含某些未实装的饰品，谨慎开启
UnlockAllDecorations = true
## 解锁游戏币购买内容 | 可能大幅缩短游戏寿命，谨慎开启
UnlockPurchasableItems = false


[WeatherAPI]
## 是否启用天气 API 同步 | 不开启大部分功能无法使用
EnableWeatherSync = false
## 天气数据源: Seniverse（心知天气）或 OpenMeteo
WeatherProvider = OpenMeteo
## 心知天气 API Key | v5.1.2版本后，留空可直接使用内置密钥
SeniverseKey = 
## 心知天气城市名称（拼音或中文，如：beijing、上海，ip 表示自动定位；留空等同于 ip）
Location = beijing
## OpenMeteo 纬度，WGS84 坐标系，有效范围 [-90, 90]
Latitude = 39.9075
## OpenMeteo 经度，WGS84 坐标系，有效范围 [-180, 180]
Longitude = 116.39723


[WeatherSync]
## 天气API刷新间隔（分钟） | 非调试请勿动
RefreshMinutes = 30
## 天气缓存有效期（分钟） | 非调试请勿动
CacheExpiryMinutes = 60
```

### 天气数据源选择

- `WeatherProvider = Seniverse`：使用心知天气。`Location` 生效，`SeniverseKey` 留空时使用内置 Key。`Location` 留空则按 `ip` 自动定位，而不是停止同步。
- `WeatherProvider = OpenMeteo`：使用 OpenMeteo。`Latitude` / `Longitude` 生效，不需要 API Key。
  - 这两项使用 **WGS84** 坐标系（Open-Meteo 接口只接受 WGS84）。默认值 39.9075 / 116.39723 来自 [Open-Meteo 北京地理编码结果](https://geocoding-api.open-meteo.com/v1/search?name=Beijing&count=1&format=json)。更新默认值不会覆盖已有配置。
  - 高德、腾讯地图通常使用 GCJ-02，百度地图通常使用 BD-09；直接填入可能造成位置偏移。想精确到点，请先把坐标转成 WGS84 再填，或直接使用 Open-Meteo 地理编码接口返回的坐标：`https://geocoding-api.open-meteo.com/v1/search?name=城市名&count=1&format=json`。
- 如果安装了新版 [iGPUSaviorMod](https://github.com/Small-tailqwq/iGPUSaviorMod)，设置页会根据天气源自动显示对应配置项：心知天气显示城市输入框，OpenMeteo 显示经纬度输入框。

### 确定性调试场景

- 调试模式统一替换 Mod 看到的天气、当前日期时间、日出和日落；启动同步与常规定时同步使用同一组模拟输入，并暂停后台日出日落请求。
- `SimulatedDateTime` 可写 `17:45`，也可写 `2026-12-24 23:45` 来测试季节和彩蛋时间条件。
- 修改配置后按 `F7` 会立即重载并应用。安装仓库内的 UnityExplorer MCP Bridge 后，推荐使用 `envsync_debug` 一次完成参数写入、强制应用及“期望状态 vs 游戏实际状态”验证，详见 [UnityExplorerMcp/README.md](./UnityExplorerMcp/README.md)。

### 获取心知天气 API Key（可选）
> 💡 新版本内置密钥，留空也可用天气服务。自己申请也可用，看个人喜好

1. 访问 [心知天气开发者平台](https://www.seniverse.com/)
2. 注册账号并登录，转到 `控制台` - `产品管理` - `免费版`(如果你是试用版或者其他版本，请选择对应版本)
3. 在产品管理中找到 **私钥**，复制该密钥
4. 将该 **私钥** 填入配置文件的 `SeniverseKey` 字段，无需添加引号等特殊符号
5. 设置 `EnableWeatherSync = true` 启用天气同步
6. 可按需配置刷新时间，避免API调用过于频繁
7. 如有不懂之处，可以参阅 issues。

## 🚀 使用方法

### 基础使用（默认开启）

插件默认会根据配置的日出日落时间自动切换环境：
- 日出后到日落前 30 分钟：白天
- 日落前 30 分钟到日落后 30 分钟：黄昏
- 其他时间：夜晚

### 天气同步模式（需开启）

1. 按照上述方法选择天气数据源，并配置城市或经纬度
2. 插件按指定间隔（默认 30 分钟）检查天气，有效缓存优先；缓存过期（默认 60 分钟）后才重新联网获取，并按同步开关更新环境

3. 环境映射规则：

| 环境 | 正常天气 | 恶劣天气 |
|------|---------|---------|
| ☀️ 白天(Day) | 日出 → 日落前30分钟 | — |
| 🌅 黄昏(Sunset) | 日落前30分钟 → 日落后30分钟 | — |
| ☁️ 多云(Cloudy) | — | 日出 → 日落后30分钟 |
| 🌙 夜晚(Night) | 日落后30分钟 → 日出 | 日落后30分钟 → 日出 |  

> 💡 上表为关闭平滑时段切换或平滑不可用时的离散映射，多云覆盖需要同时开启时间与天气同步。启用平滑后，云雾按天气等级独立叠加到各时段，保留黄昏色调；太阳、光照和雨量支持连续变化。清晨/傍晚/完整黄昏/入夜时长为 120/60/40/75 分钟，手动切换为 2 秒，雨量渐变为 6 秒。只开天气同步不会切换基底时段，但可使用连续降雨。详见[平滑时段切换](docs/smooth-time-transitions.md)。

4. 恶劣天气名单：

```
4, 7, 8, 9 (多云/阴天)
11, 12, 14-20 (中雨及以上, 雷暴, 冻雨)
23-25 (中雪及以上)
26-31 (沙尘/雾)
34-36 (风暴)
```
5. 天气映射规则：

| 游戏效果代码 | 对应 API 天气 ID (参考)       | 逻辑说明                                             |
| ------------ | ----------------------------- | ---------------------------------------------------- |
| Snow         | 20-25 (雪/雨夹雪/暴雪)        | 所有的雪类，统统下雪。                               |
| ThunderRain  | 11, 12, 16, 17, 18 (雷/暴雨)  | 雷阵雨、暴雨、特大暴雨，上强度。                     |
| HeavyRain    | 10, 14, 15 (阵雨/中雨/大雨)   | 普通的下雨，量比较大。                               |
| LightRain    | 13, 19 (小雨/冻雨)            | 淅淅沥沥的小雨。                                     |
| OFF (关闭)   | 0-9, 26-39 (晴/多云/阴/雾/风) | 关键点：如果是阴天或多云，必须强制关闭所有雨雪效果！ |



### 快捷键功能

- **F7**：立即强制刷新天气（跳过缓存）
	- 核心目的：
		- 忽略缓存，强制从当前选择的天气源获取最新实时天气数据。
	- 重置 API 慢钟计时器。
		- 触发行为：
			1. 清空计时器：重置下次自动请求 API 的倒计时。
			2. 强制联网：无视 `CacheExpiryMinutes` 缓存期，直接向心知天气或 OpenMeteo 发起 HTTP 请求。
			3. 数据更新：如果请求成功，会用新的天气数据覆盖内存中的 _cachedWeather。
			4. 自动应用：拿到新数据后，按当前时间同步和天气同步开关更新场景；两者都关闭时仅刷新天气数据与已启用的日期栏显示。
	- 适用场景：
		- 你觉得天气变了（比如窗外刚下雨了），但游戏里还是晴天（因为还在缓存期内）。
		- 调试 API 连接是否正常。
		- 修改了配置文件中的 `Location`、`Latitude` 或 `Longitude` 后，想立即生效。
- **F8**：在日志中显示当前环境状态
	- 打印一条日志
- **F9**：手动触发一次天气同步
	- 核心目的：
	  - 优先使用有效天气缓存和实时本地时间，重新计算并应用游戏环境。
	  - 若缓存不存在或已经过期，且当前配置需要天气数据，则会正常请求所选天气源。
	- 触发行为：
	  1. 读取有效缓存；不可用时按当前同步策略获取天气。
	  2. 重算时间：根据当前秒数 (DateTime.Now)，重新计算是白天、黄昏还是夜晚。
	  3. 通过 `GameEnvironmentRuntime` 重新应用基底环境，并校准降水窗景与环境音状态。
	- 适用场景：
	  - 修复错位：比如游戏 UI 显示是“雨天”，但画面上雨停了；或者声音还在响，但图标灭了。按 F9 可以“踢”一下游戏，让它回正。
	  - 日落/日出测试：当你觉得时间到了（比如17:30），但游戏没切黄昏（可能是快钟还没跳），按 F9 可以立即触发时间判断。
	  - 无网状态：有效缓存仍存在时，F9 可以刷新本地时间逻辑和现有天气效果。

## 🔧 技术细节

- **框架**：BepInEx 5.x
- **目标框架**：.NET Framework 4.7.2
- **架构**：模块边界与依赖方向见 [docs/architecture.md](./docs/architecture.md)
- **使用技术/工具**：
  - Harmony 补丁（用于游戏功能注入）
  - Unity 协程（用于异步网络请求）
  - 反射（用于访问游戏内部系统）
  - dnSPY
  - 各种大语言模型

## 📝 版本历史

详细更新日志请查看 [CHANGELOG.md](./CHANGELOG.md) 及 [Git 提交记录](https://github.com/Small-tailqwq/RealTimeWeatherMod/commits/master)

## 🐛 已知问题

- 首次加载会每 0.25 秒检查环境入口是否就绪，最长等待 30 秒；同步时机取决于游戏加载进度。
- F9 可能导致部分按钮 UI 异常，但是不影响游戏内容，修复优先级低

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 关于反馈

如需反馈问题，请先确保问题“可复现”，并开启调试日志（BepInEx/config/BepInEx.cfg 中将 `Logging.Console` 设置为 `true`）。  
这时，游戏启动时会在控制台输出详细日志，有助于定位问题。

## 📄 许可证

本项目采用 **MIT 许可证** 开源。

**⚠️ 重要声明**：
- ✅ 可以自由使用、修改和分发
- ✅ 可以用于个人学习和研究
- 使用本软件产生的任何后果由使用者自行承担

详见 [LICENSE](LICENSE) 文件。

## 👨‍💻 作者

- GitHub: [@Small-tailqwq](https://github.com/Small-tailqwq)

## 🙏 致谢

- BepInEx 团队
- Harmony 补丁库
- Duvet
- 心知天气 API 服务
- OpenMeteo API 服务
- Github Copilot
  - 再见了，所有的 Copilot
- 谷、O、A御三家
- Opencode GO
	- 性价比最高的 coding plan


---

**免责声明**：
本插件仅供学习交流使用，请勿用于商业用途。使用本插件产生的任何问题与作者无关。  
本项目为粉丝自制 (Fan-made)，与游戏开发商及发行商无关。
游戏《Chill with You - Lo-Fi Story》及其相关素材、商标的版权均归原作者所有。


> 即使生活一团糟，但也要心存希望
