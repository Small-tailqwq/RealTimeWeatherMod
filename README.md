# Chill Env Sync（实时天气同步插件）

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Framework 4.7.2](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net472)
[![BepInEx](https://img.shields.io/badge/BepInEx-Plugin-green.svg)](https://github.com/BepInEx/BepInEx)

一个用于游戏的实时天气同步 BepInEx 插件，可以根据真实世界的天气情况自动调整游戏内的环境效果，或基于现实时间模拟昼夜循环。

所有代码均由AI编写，人工仅作反编译和排错处理。
这是我第一次用AI为UE游戏做MOD，有问题请反馈，虽然反馈了我也是再去找AI修就是了🫥

## ✨ 主要功能

- 🌤️ **实时天气同步**：通过心知天气 API 获取真实天气数据，自动调整游戏内环境
- 🌍 **多城市支持**：支持任意城市的天气查询（拼音或中文）
- 🌓 **昼夜循环**：根据配置的日出日落时间自动切换白天/黄昏/夜晚场景
- 🔓 **解锁所有环境**：自动解锁所有环境和装饰品
  - 解锁的环境和装饰品不影响游戏存档，mod删掉或者出bug了就会恢复（大概）（这段是人工写的）
- ⌨️ **快捷键操作**：
  - `F7` - 强制刷新天气
  - `F8` - 显示当前状态
  - `F9` - 手动触发同步

## 🎮 支持的环境类型

### 基础环境（互斥）
- ☀️ 白天 (Day)
- 🌅 黄昏 (Sunset)
- 🌙 夜晚 (Night)
- ☁️ 阴天 (Cloudy)

### 降水效果（可叠加在阴天上）
- 🌧️ 小雨 (LightRain)
- 🌧️ 大雨 (HeavyRain)
- ⛈️ 雷雨 (ThunderRain)
- ❄️ 雪 (Snow)
- 💨 风 (Wind)

### TODO（这段也是人工写的）
游戏内有很多种效果，后面可能会对主模块进行重构，拆分为多个模块，提供更多的规则
例如：  
- [ ] 根据季节、天气等因素选择展示樱花
- [ ] 根据季节、时间等因素选择背景音
- [ ] 诸如此类，反正经可能把能用的都用上
 
## 📦 安装方法

### 前置要求
- 游戏本体
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) 或更高版本

### 安装步骤

1. 确保已正确安装 BepInEx 框架
2. 将 `ChillEnvSync.dll` 放入 `BepInEx/plugins/` 目录
3. 启动游戏，插件将自动加载
4. （可选）编辑配置文件以启用天气 API 同步

## ⚙️ 配置说明

首次运行后，配置文件将生成在 `BepInEx/config/chillwithyou.envsync.cfg`

### 配置项说明

```ini
[TimeConfig]
# 日出时间（格式：HH:mm）
Sunrise = 06:30

# 日落时间（格式：HH:mm）
Sunset = 18:30

[WeatherSync]
# 天气刷新间隔（分钟）
RefreshMinutes = 30

[WeatherAPI]
# 是否启用天气 API 同步
EnableWeatherSync = false

# 心知天气 API Key（需要申请）
SeniverseKey = 

# 城市名称（拼音或中文，如：beijing、上海、ip 表示自动定位）
Location = beijing
```

### 获取心知天气 API Key

1. 访问 [心知天气开发者平台](https://www.seniverse.com/)
2. 注册账号并登录
3. 创建应用获取免费 API Key
4. 将 API Key 填入配置文件的 `SeniverseKey` 字段
5. 设置 `EnableWeatherSync = true` 启用天气同步

## 🚀 使用方法

### 基础使用（无需 API）

插件默认会根据配置的日出日落时间自动切换环境：
- 日出到日落前 1 小时：白天
- 日落前 1 小时到日落后 30 分钟：黄昏
- 其他时间：夜晚

### 天气同步模式（需要 API）

1. 按照上述方法配置好 API Key
2. 插件将每隔指定时间（默认 30 分钟）自动获取天气并更新环境
3. 天气映射规则：
   - 晴天（0-3）→ 根据时间显示白天/黄昏/夜晚
   - 阴天（4-9）→ 阴天环境
   - 小雨（10-12）→ 阴天 + 小雨
   - 大雨（13-14）→ 阴天 + 大雨
   - 雷雨（15-18）→ 阴天 + 雷雨
   - 雪（21-25）→ 阴天 + 雪

### 快捷键功能

- **F7**：立即强制刷新天气（跳过缓存）
- **F8**：在日志中显示当前环境状态
- **F9**：手动触发一次天气同步
- 没记错的话这玩意好像没用(这句是人写的)

## 🔧 技术细节

- **框架**：BepInEx 5.x
- **目标框架**：.NET Framework 4.7.2
- **使用技术**：
  - Harmony 补丁（用于游戏功能注入）
  - Unity 协程（用于异步网络请求）
  - 反射（用于访问游戏内部系统）

## 📝 版本历史

### v3.5.0（当前版本）
- ✅ 优化按钮逻辑，采用模拟点击 MainIcon 方式
- ✅ 修复环境切换可能不生效的问题
- ✅ 改进代码结构和日志输出

### v3.4.x
- 修复部分天气效果无法关闭的问题
- 优化环境互斥逻辑

### v3.3.0 及更早版本
- 基础天气同步功能
- 从 MelonLoader 迁移至 BepInEx
- 初始版本开发
- 谁能想到用了3个不同厂商的不同AI写了一天才写出来，我累死了

详细更新日志请查看 [Git 提交记录](https://github.com/Small-tailqwq/RealTimeWeatherMod/commits/master)

## 🐛 已知问题

- 首次加载可能需要等待 15 秒后才会进行第一次环境同步
- 部分环境切换可能存在延迟
- 快捷键可能不起作用
- 部分天气效果没有测试

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用 **MIT 许可证** 开源。

**⚠️ 重要声明**：
- ✅ 可以自由使用、修改和分发
- ✅ 可以用于个人学习和研究
- ❌ **禁止用于任何商业目的**
- 使用本软件产生的任何后果由使用者自行承担

详见 [LICENSE](LICENSE) 文件。

## 👨‍💻 作者

- GitHub: [@Small-tailqwq](https://github.com/Small-tailqwq)

## 🙏 致谢

- BepInEx 团队
- Harmony 补丁库
- 心知天气 API 服务
- Google Gemini3Pro
- OpenAI ChatGPT5.1
- Claude Sonnet and Ops 4.5
- 我的肝脏和眼球

---

**免责声明**：本插件仅供学习交流使用，请勿用于商业用途。使用本插件产生的任何问题与作者无关。


AI 真好用