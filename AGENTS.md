# RealTimeWeatherMod — Agent 分析报告

## 一、项目总览

| 字段 | 值 |
|------|-----|
| 项目名 | Chill Env Sync (RealTimeWeatherMod) |
| 类型 | BepInEx 插件 (.NET Framework 4.7.2) |
| 版本 | 5.2.0 |
| BepInEx GUID | `chillwithyou.envsync` |
| 目标游戏 | 《Chill with You: Lo-Fi Story》(Steam) |
| 游戏路径 | `D:\SteamLibrary\steamapps\common\Chill with You Lo-Fi Story` |
| 开发工具 | dnSpy (反编译) + AI 生成 C# |

## 二、Git 提交规范

- **提交信息必须使用中文**（这是硬性约束）
- 格式：`<类型>: <中文描述>`
- 类型：`feat`(功能)、`fix`(修复)、`refactor`(重构)、`docs`(文档)、`test`(测试)、`chore`(杂项)
- 用简短的一句话描述变更目的，而非做了什么

## 三、大架构（三层模型）

```
┌─────────────────────────────────────────────────────────┐
│                    ChillEnvPlugin                        │
│               (BepInEx BaseUnityPlugin)                  │
│  GUID: chillwithyou.envsync                             │
│  入口: Awake() → Harmony + InitConfig + Runner GO       │
├─────────────────────────────────────────────────────────┤
│                     Harmony Patches                      │
│  ┌────────────────┐  ┌─────────────────────────┐        │
│  │ UnlockService  │  │ EnvController           │        │
│  │ Patch          │  │ Patch → EnvRegistry     │        │
│  │ → 截获 Setup   │  │ → 注册所有 Environment  │        │
│  └────────────────┘  └─────────────────────────┘        │
│  ┌────────────────┐  ┌─────────────────────────┐        │
│  │ FacilityEnv    │  │ DateUIPatch             │        │
│  │ Patch → 捕获   │  │ → 日期栏追加天气+时段   │        │
│  │ WindowViewSvc  │  └─────────────────────────┘        │
│  └────────────────┘                                      │
│  ┌────────────────┐  ┌─────────────────────────┐        │
│  │ UserInteraction│  │ UnlockConditionGodMode  │        │
│  │ Patch → 监控   │  │ → 拦截 IsUnlocked /     │        │
│  │ 用户手动操作   │  │   IsPurchasableItem     │        │
│  └────────────────┘  └─────────────────────────┘        │
├─────────────────────────────────────────────────────────┤
│                    Core 运行时组件                        │
│  ┌─────────────────────────────────────────────┐        │
│  │         AutoEnvRunner (MonoBehaviour)        │        │
│  │  - 天气API定时轮询 (FetchWeather)            │        │
│  │  - 日出日落同步 (FetchSunSchedule)           │        │
│  │  - 环境切换决策 (ApplyEnvironment)           │        │
│  │  - 快捷键: F7刷新 / F8状态 / F9强制同步      │        │
│  └─────────────────────────────────────────────┘        │
│  ┌─────────────────────────────────────────────┐        │
│  │    SceneryAutomationSystem (MonoBehaviour)   │        │
│  │  - 13条彩蛋规则 (烟花/樱花/鲸鱼/宇宙...)     │        │
│  │  - 点击冷却 + 延迟验证机制                    │        │
│  │  - 用户交互检测 (UserInteractedMods)          │        │
│  └─────────────────────────────────────────────┘        │
│  ┌─────────────────────────────────────────────┐        │
│  │    ModSettingsIntegration (MonoBehaviour)    │        │
│  │  - iGPU Savior 土豆模式设置界面集成           │        │
│  │  - 反射软依赖 (不报错)                       │        │
│  │  - 多语言翻译注册                            │        │
│  └─────────────────────────────────────────────┘        │
├─────────────────────────────────────────────────────────┤
│                  Services / Utils / Models               │
│  WeatherService  KeySecurity  EnvRegistry                │
│  WindowViewStateAccessor  WeatherData  SunResponse       │
└─────────────────────────────────────────────────────────┘
```

## 四、源码文件全景

```
RealTimeWeatherMod/
├── ChillEnvPlugin.cs          — 插件主入口 (442行)
├── Core/
│   ├── AutoEnvRunner.cs       — 天气轮询+环境切换引擎 (350行)
│   └── SceneryAutomationSystem.cs — 彩蛋规则引擎 (741行)
├── Patches/
│   ├── HarmonyPatches.cs      — Harmony Patch 集合 (204行)
│   ├── ModSettingsIntegration.cs — 土豆设置集成 (316行)
│   └── UnlockConditionGodMode.cs — 上帝模式解锁 (104行)
├── Services/
│   ├── WeatherService.cs      — 心知天气 API 封装 (205行)
│   └── KeySecurity.cs         — API Key AES 加解密 (81行)
├── Models/
│   ├── WeatherData.cs         — 天气数据模型 (44行)
│   └── SunResponse.cs         — 日出日落响应模型 (25行)
├── Utils/
│   ├── EnvRegistry.cs         — EnvironmentController 注册表 (13行)
│   └── WindowViewStateAccessor.cs — 窗景状态反射读取 (63行)
└── Properties/
    └── AssemblyInfo.cs        — 程序集信息 (33行)
```

## 五、游戏关键类型映射（来自反编译 Assembly-CSharp.dll）

### 5.1 命名空间架构

游戏程序集 `Assembly-CSharp.dll` 包含 **2195 个类型**，核心在 `Bulbul` 命名空间。

### 5.2 EnvironmentType 枚举（41 种环境）

```
Day(白天), Sunset(黄昏), Night(夜晚), Cloudy(多云),       ← 基础 4 时段
Fireworks(烟花), DeepSea(深海), Books(魔法书),             ← 窗景彩蛋
WindBell(风铃), Sakura(樱花), Snow(雪), LightRain(小雨),  ← 降水+彩蛋
HeavyRain(大雨), ThunderRain(雷雨), Jet(喷气), Balloon(热气球),
Whale(鲸鱼), HotSpring(温泉), Space(宇宙), Locomotive(火车),
RadioNoise, PinkNoise, RecordNoise, Wind, CookSimmer,
Crickets(蟋蟀), Frog1, Frog2, Chicada(蝉), Higurashi(蜩),
TurtleDove(斑鸠), BirdChorus(鸟鸣), Sea(海), RoomNoise(空调),
BlueButterfly(蓝蝶), CookTypeB, City(城市), Jellyfish(水母),
Fireplace(壁炉), ValentineSweets(情人节糖果), Aurora(极光), Brook(小溪)
```

### 5.3 反射桥接的关键类

| 游戏类型 | Mod 访问方式 | 用途 |
|---------|------------|------|
| `UnlockItemService` | Harmony Postfix 拦截 Setup | 获取解锁服务实例 |
| `Bulbul.EnvironmentController` | EnvRegistry 注册 | 模拟点击切换环境 |
| `Bulbul.EnvironmentUI` | AccessTools 反射 | 调用 ChangeTime 切换时段 |
| `Bulbul.WindowViewService` | 反射 GetMethod | ChangeWeatherAndTime |
| `Bulbul.UnlockConditionService` | Harmony Prefix 拦截 | 上帝模式解锁装饰品 |
| `Bulbul.CurrentDateAndTimeUI` | Harmony Postfix 拦截 | 日期栏追加天气文本 |
| `Bulbul.SaveDataManager` | 直接引用 | 读取窗景状态 |
| `Bulbul.WindowViewType` | Enum.Parse 映射 | Day/Sunset/Night/Cloudy |
| `Bulbul.EnvironmentType` | 枚举值映射 | 41 种环境类型 |

### 5.4 关键枚举映射

```
Mod 环境类型         → 游戏 EnvironmentType    → WindowViewType
Day                 → EnvironmentType.Day     → WindowViewType.Day
Sunset              → EnvironmentType.Sunset  → WindowViewType.Sunset
Night               → EnvironmentType.Night  → WindowViewType.Night
Cloudy              → EnvironmentType.Cloudy → WindowViewType.Cloudy
LightRain           → EnvironmentType.LightRain (仅窗景)
HeavyRain           → EnvironmentType.HeavyRain (仅窗景)
ThunderRain         → EnvironmentType.ThunderRain (仅窗景)
Snow                → EnvironmentType.Snow (仅窗景)
```

### 5.5 游戏依赖的主要 DLL

```
0Harmony.dll           — Harmony 补丁库
BepInEx.dll            — BepInEx 插件加载器
Assembly-CSharp.dll    — 游戏主程序集 (2195 类型)
R3.dll                 — 响应式编程库 (类似 UniRx)
VContainer.dll         — DI 容器 (依赖注入)
MagicLightmapSwitcher.dll — 光照贴图切换
DOTween.dll            — 动画库
UniTask.dll            — 异步任务库
Unity.TextMeshPro.dll  — 文本渲染
```

### 5.6 EnvironmentType ↔ 游戏 UI 名称对照

**基底时段（4 个，影响光照/时间/WindowViewType）**

| EnvironmentType | WindowViewType | LocalizeKey | 游戏 UI 名称（推测） | 说明 |
|----------------|---------------|-------------|-------------------|------|
| `Day` | `WindowViewType.Day` | `ui_guide_enviroment_day` | 白天/日中 | 基础日间时段 |
| `Sunset` | `WindowViewType.Sunset` | `ui_guide_enviroment_sunset` | 黄昏/日没 | 日落时段，30 分钟窗口 |
| `Night` | `WindowViewType.Night` | `ui_guide_enviroment_night` | 夜晚/夜 | 夜间时段 |
| `Cloudy` | `WindowViewType.Cloudy` | `ui_guide_enviroment_cloudy` | 阴天/曇り | 坏天气时的基底 |

**降水窗景（4 个，依附于基底时段之上）**

| EnvironmentType | LocalizeKey | 游戏 UI 名称（运行时读取） | 心知天气 Code | 说明 |
|----------------|-------------|------------------------|---------------|------|
| `LightRain` | `ui_enviroment_lightrain_01` | 小雨 | 13, 19 | 小雨/细雨，需解锁 |
| `HeavyRain` | `ui_enviroment_heavyrain_01` | 雨 | 10, 14, 15 | 大雨，需解锁 |
| `ThunderRain` | `ui_enviroment_rainthunder_01` | 雷雨 | 11, 12, 16~18 | 雷雨，需解锁 |
| `Snow` | `ui_enviroment_snow_01` | 雪 | 20~25 | 下雪，需解锁 |

**窗景类彩蛋（GridLayout，分类标题 1，视觉为主）**

| EnvironmentType | LocalizeKey | 游戏 UI 名称 | 说明 |
|----------------|-------------|-------------|------|
| `Fireworks` | `ui_enviroment_fireworks_01` | 烟花 | 烟花绽放 |
| `DeepSea` | `ui_enviroment_deepsea_01` | 深海 | 深海潜水 |
| `Jellyfish` | `ui_enviroment_jellyfish_01` | 水母 | 水母游动 |
| `Whale` | `ui_enviroment_whales_01` | 鲸鱼 | 鲸鱼遨游 |
| `HotSpring` | `ui_enviroment_hotspring_01` | 温泉 | 温泉泡澡 |
| `Locomotive` | `ui_enviroment_locomotive_01` | 蒸汽机车 | 火车 |
| `City` | `ui_enviroment_city_01` | 城市 | 城市夜景 |
| `WindBell` | `ui_enviroment_windbell_01` | 风铃 | 风铃摇曳 |

**窗景类彩蛋（GridLayout (1)，分类标题 2，视觉为主）**

| EnvironmentType | LocalizeKey | 游戏 UI 名称 | 说明 |
|----------------|-------------|-------------|------|
| `Sakura` | `ui_enviroment_sakura_01` | 樱花 | 樱花飘落 |
| `Snow` | `ui_enviroment_snow_01` | 雪 | 雪花纷飞 |
| `Aurora` | `ui_enviroment_aurora_01` | 极光 | 极光舞动 |
| `Books` | `ui_enviroment_book_01` | 书 | 书房/魔法书 |
| `Jet` | `ui_enviroment_jet_01` | 飞机 | 喷气机 |
| `Balloon` | `ui_enviroment_balloon_01` | 热气球 | 热气球飞行 |
| `Space` | `ui_enviroment_universe_01` | 宇宙 | 星空宇宙 |
| `BlueButterfly` | `ui_enviroment_alterego_01` | 蓝色蝴蝶 | 蓝蝶 |
| `ValentineSweets` | `ui_enviroment_valentineSweets_01` | 零食 | 情人节糖果 |

**音效类彩蛋（GridLayout (2)，分类标题 3，纯音频）**

| EnvironmentType | LocalizeKey | 游戏 UI 名称 | 说明 |
|----------------|-------------|-------------|------|
| `RadioNoise` | `ui_enviroment_radionoise_01` | 无线电噪声 | 收音机噪音 |
| `PinkNoise` | `ui_enviroment_pinknoise_01` | 粉红噪声 | 粉红噪音 |
| `RecordNoise` | `ui_enviroment_recordnoise_01` | 录音噪声 | 唱片噪音 |
| `RoomNoise` | `ui_enviroment_roomnoise_01` | 空调 | 房间空调声 |
| `Sea` | `ui_enviroment_seawave_01` | 波浪 | 海浪声 |
| `Brook` | `ui_enviroment_brook_01` | 溪流 | 小溪流水 |
| `Wind` | `ui_enviroment_wind_01` | 风 | 风声 |
| `CookSimmer` | `ui_enviroment_cook_01` | 烹饪-A | 煮东西 A |
| `CookTypeB` | `ui_enviroment_cook_02` | 烹饪-B | 煮东西 B |
| `Fireplace` | `ui_enviroment_fireplace_01` | 壁炉 | 柴火燃烧 |
| `Crickets` | `ui_enviroment_crickets_01` | 蟋蟀 | 蟋蟀叫 |
| `Frog1` | `ui_enviroment_frog_01` | 青蛙的叫声 | 单只青蛙 |
| `Frog2` | `ui_enviroment_frog_02` | 青蛙齐鸣 | 多只青蛙 |
| `Chicada` | `ui_enviroment_chicada_01` | 蝉 | 蝉鸣 |
| `Higurashi` | `ui_enviroment_higurashi_01` | 寒蝉 | 暮蝉 |
| `TurtleDove` | `ui_enviroment_turtledove_01` | 山斑鸠 | 斑鸠 |
| `BirdChorus` | `ui_enviroment_birdchorus_01` | 鸟儿齐鸣 | 鸟鸣合唱 |

> **注意**：
> - 实际游戏 UI 名称由 `inspect_element` 直接读取 `TMP_Text.text` 获取，确保与游戏内完全一致。
> - `Sea` 在代码中对应 `EnvironmentType.Sea`，但游戏 UI 显示"波浪"而非"海"。
> - 解锁环境请确认 `UnlockAllEnvironments = true`。
> - `LightRain`="小雨"，`HeavyRain`="雨"，`ThunderRain`="雷雨"。

## 六、核心数据流

### 6.1 天气同步流

```
[心知天气 API] ──HTTP──> WeatherService.FetchWeather()
                              │
                        解析 JSON → WeatherInfo
                              │
                    AutoEnvRunner.ApplyEnvironment()
                     ├── GetTimeBasedEnvironment() ← 日出日落配置
                     ├── IsBadWeather() → 决定 Day/Cloudy
                     ├── ApplyBaseEnvironment() → SimulateClick()
                     └── ApplyScenery() → 降水窗景
```

### 6.2 彩蛋自动化流

```
SceneryAutomationSystem.Update() [每 5 秒]
  ├── ProcessPendingActions() [延迟验证]
  └── RunAutomationLogic()
       ├── Step1: 关闭不满足条件的已托管环境
       └── Step2: 开启满足条件的未托管环境
            └── EnableMod() → SimulateClickMainIcon()
                               → PendingAction (0.5s 后验证)
```

### 6.3 解锁流

```
UnlockItemService.Setup()
  → [Harmony Postfix] ChillEnvPlugin.TryInitializeOnce()
       ├── ForceUnlockAllEnvironments() [暴力反射 _environmentDic]
       └── ForceUnlockAllDecorations() [核弹 v2: 全组件扫描 _isLocked]

UnlockConditionService.IsUnlocked<T>()
  → [Harmony Prefix] 强制返回 (true, true)

UnlockConditionService.IsPurchasableItem<T>()
  → [Harmony Prefix] 强制返回 false
```

## 七、配置项清单

| 配置键 | 默认值 | 说明 |
|-------|--------|------|
| WeatherAPI.EnableWeatherSync | false | 启用天气API同步 |
| WeatherAPI.SeniverseKey | "" | 心知天气 API Key |
| WeatherAPI.Location | "beijing" | 城市名称 |
| WeatherSync.RefreshMinutes | 30 | API 刷新间隔(分) |
| TimeConfig.Sunrise | "06:30" | 日出时间 |
| TimeConfig.Sunset | "18:30" | 日落时间 |
| Unlock.UnlockAllEnvironments | true | 自动解锁环境 |
| Unlock.UnlockAllDecorations | true | 自动解锁装饰品 |
| Unlock.UnlockPurchasableItems | false | 解锁游戏币购买内容 |
| UI.ShowWeatherOnDate | true | 日期栏显示天气 |
| UI.DetailedTimeSegments | true | 详细时段显示 |
| Automation.EnableSeasonalEasterEggs | true | 启用彩蛋 |
| Debug.EnableDebugMode | false | 调试模式 |

## 八、外部依赖

- **心知天气 API** (seniverse.com) — 实时天气 + 日出日落数据
- **BepInEx 5.4.x** — Unity 游戏 Mod 加载器
- **Harmony 2.x** — 运行时方法补丁库
- **iGPU Savior** (可选) — 土豆模式设置界面集成
- **R3** — 游戏自带的响应式编程库
- **VContainer** — 游戏自带的 DI 容器

## 九、开发注意事项

1. **反射脆弱性**：大量使用反射（FieldInfo/MethodInfo），游戏更新可能导致字段名/路径变化
2. **无存档写入**：解锁仅在当前会话生效，不修改存档(UnlockConditionGodMode 拦截)
3. **点击模拟**：使用 `AccessTools.Method(EnvironmentUI, "ChangeTime")` 切换时段
4. **C# 7.3 限制**：目标 .NET Framework 4.7.2，不支持 C# 8+ 语法
5. **游戏路径**：csproj 中 GameDir 默认指向 `E:\SteamLibrary\...`，本地实际在 `D:\SteamLibrary\`
6. **BepInEx 目录**：`BepInEx/plugins/RealTimeWeatherMod.dll`
7. **预埋 API Key**：`KeySecurity.cs` 内置 AES 加密的默认 Key（防滥用）

## 十、游戏已安装的 MOD

```
D:\SteamLibrary\...\BepInEx\plugins\
  ├── RealTimeWeatherMod.dll       ← Chill Env Sync (本项目)
  ├── UnityExplorer.BIE5.Mono.dll  ← UnityExplorer 运行时调试器
  ├── UniverseLib.Mono.dll         ← UnityExplorer 依赖库
  └── UnityExplorerMcp.dll         ← UnityExplorer MCP Bridge
```

## 十一、UnityExplorer MCP Bridge

| 字段 | 值 |
|------|-----|
| 项目名 | UnityExplorer Mcp Bridge |
| 目录 | `UnityExplorerMcp/` |
| 类型 | BepInEx 插件 (.NET Framework 4.7.2) |
| 版本 | 1.0.0 |
| BepInEx GUID | `chillwithyou.unityexplorermcp` |
| 目标 | 将 UnityExplorer 的运行时 UI 布局查询能力暴露为 MCP 协议 |

### 11.1 大架构

```
opencode ──HTTP──▶ UnityExplorerMcp Plugin (BepInEx)
  MCP tools          │
                  HttpListener (TcpListener)
                       │
                  Unity API (Resources.FindObjectsOfTypeAll, etc.)
                       │
                  UnityExplorer (硬依赖, Mono.CSharp 容器)
```

### 11.2 源码文件全景

```
UnityExplorerMcp/
├── Plugin.cs                    — BepInEx 主入口
├── MCP/
│   ├── McpServer.cs             — HTTP JSON-RPC 2.0 MCP 服务器（SSE + POST）
│   ├── JsonHelper.cs            — 轻量 JSON 序列化/反序列化
│   └── Tools/
│       ├── IMcpTool.cs          — 工具接口
│       ├── ToolRegistry.cs      — 工具注册表
│       ├── GetUiHierarchyTool.cs— UI 层级树
│       ├── InspectElementTool.cs— 元素组件详情
│       ├── SearchElementsTool.cs— UI 搜索
│       ├── GetMouseElementTool.cs — 鼠标指向元素
│       ├── ExecuteCodeTool.cs   — C# 代码执行
│       ├── GetConsoleLogsTool.cs— 日志获取
│       ├── TakeScreenshotTool.cs— 游戏截图
│       └── ListScenesTool.cs    — 场景列表
├── Core/
│   ├── UiHierarchyTraverser.cs  — UI 树遍历引擎
│   ├── ConsoleCaptureService.cs — Unity 日志捕获
│   ├── MainThreadDispatcher.cs  — 主线程调度 (SynchronizationContext)
│   └── MonoCSharpEvaluator.cs   — Mono.CSharp 表达式求值器
└── Models/
    └── UiElementInfo.cs         — UI 元素数据模型
```

### 11.3 MCP Tools

| Tool | 功能 |
|------|------|
| `get_ui_hierarchy` | 遍历 Canvas → UI 树，返回 element name/path/rectTransform 全字段 |
| `inspect_element` | 指定 GameObject 的组件详情（Text、Image、Button、Canvas 等专有字段） |
| `search_elements` | 按名称搜索 UI 元素，可加 `componentType` 过滤（"Text" 自动兼容 TMP） |
| `get_mouse_element` | 当前鼠标下方的 UI 元素 |
| `execute_code` | 在游戏内执行 C# 代码（Mono.CSharp REPL），返回结果 |
| `get_console_logs` | 最近 N 条 Unity 日志 |
| `take_screenshot` | 游戏画面截图，返回 base64 PNG |
| `list_scenes` | 列出所有场景及根 GameObject |

### 11.4 传输协议

- 监听 `localhost:8972`
- MCP Streamable HTTP + SSE 双通道
- JSON-RPC 2.0 over HTTP POST
- CORS 支持（`Access-Control-Allow-Origin: *`）

### 11.5 配置项

| 键 | 默认值 | 说明 |
|----|--------|------|
| MCP.Port | 8972 | HTTP 监听端口 |

### 11.6 外部依赖

- **BepInEx 5.4.x** — Unity Mod 加载器
- **UnityExplorer 4.9.0** (硬依赖) — 提供 Mono.CSharp 运行时编译器
- **Harmony 2.x** — 运行时方法补丁（日志捕获）
- **DOTween** — HoldButtonAnimation 动画库（游戏中已有）

### 11.7 开发注意事项

1. **Mono.CSharp 集成**：Mono.CSharp 被 ILMerge 进 UnityExplorer.BIE5.Mono.dll，需要用 `Reflection.Emit` 动态创建 `InteractiveBase` 的 public 派生类以解决 CS0060 accessibility 问题
2. **BepInEx OnDestroy 问题**：插件在 Chainloader 启动后立即被销毁（原因未知），当前方案是 OnDestroy 不停止 HTTP 服务器，服务器在后台线程持续运行
3. **主线程调度**：使用 UnityEngine `SynchronizationContext.Post` 实现跨线程调度，不依赖 MonoBehaviour.Update
4. **C# 7.3 限制**：目标 .NET Framework 4.7.2
5. **游戏路径**：与主项目相同，`D:\SteamLibrary\...`

### 11.8 已知问题 / 待迭代

1. **`execute_code` 依赖 UnityExplorer 的 Mono.CSharp** — 如果 UnityExplorer 不可用则无法运行
2. **BepInEx OnDestroy 未解决根因** — 插件实例被销毁但不影响功能
3. **SSE 连接长稳性** — 需要测试长时间游戏后的连接保持
4. **大 JSON 输出截断** — UI 树超过一定大小后会截断
5. **opencode MCP 配置** — 在 `opencode.json` 中以 `type: "remote"` 注册 `http://localhost:8972/`

### 11.9 未来计划

- 拆分为独立开源 MCP 项目发布
- 制作标准 MCP 安装包
- 支持更多 Unity 运行时查询（材质、动画、粒子等）
- 添加 Mouse-Inspect 视觉反馈
