# UnityExplorer MCP Bridge

将 UnityExplorer 的运行时调试能力暴露为 **MCP (Model Context Protocol)**，让 AI 助手能直接「看到」游戏运行时的 UI 布局、组件属性和场景结构。

## 解决的问题

设计 Mod 前端 UI 时，AI 无法看到游戏运行时的实际 UI 布局。反编译只能看到代码逻辑，看不到运行时坐标、锚点、层级关系。本插件填补了这个缺口。

## 架构

```
AI 助手 (opencode)
  │  MCP Streamable HTTP
  ▼
UnityExplorerMcp Plugin (BepInEx)
  │
  ├── TcpListener (localhost:8972)
  ├── Unity API (Resources.FindObjectsOfTypeAll)
  └── Mono.CSharp (UnityExplorer 内置)
```

## 安装

### 前置要求

- **BepInEx 5.4.x** — Unity Mod 加载器
- **UnityExplorer 4.9.0** (BepInEx 5 Mono 版) — 运行时调试器

### 部署

1. 编译项目：`dotnet build UnityExplorerMcp/UnityExplorerMcp.csproj`
2. DLL 自动复制到 `BepInEx/plugins/UnityExplorerMcp/`
3. 启动游戏

### AI 助手配置

如需在 opencode 中使用，在 `opencode.json` 中添加：

```json
"unity-explorer": {
  "type": "remote",
  "url": "http://localhost:8972/",
  "enabled": true
}
```

## MCP 工具

| Tool | 功能 | 典型用途 |
|------|------|---------|
| `get_ui_hierarchy` | 遍历所有 Canvas → UI 树 | 查看整体布局结构、获取完整元素路径 |
| `inspect_element` | 指定路径元素的组件详情 | 查看 Button、Text(TMP)、Image 的具体属性 |
| `search_elements` | 按名称/类型搜索 UI 元素 | 快速定位特定元素，`componentType` 支持"Text"→自动匹配 TMP |
| `get_mouse_element` | 当前鼠标下方的 UI 元素 | 想知道鼠标指着什么元素 |
| `execute_code` | 在游戏内执行 C# 代码 | 运行任意 C# 查询（返回 8000 字符以内结果） |
| `get_console_logs` | 获取最近 N 条 Unity 日志 | 排查运行时错误 |
| `take_screenshot` | 游戏截图（base64 PNG） | AI 看到游戏当前画面 |
| `list_scenes` | 列出所有场景及根对象 | 了解游戏场景结构 |

### 典型用法示例

```csharp
// 查找所有 Button
execute_code("GameObject.FindObjectsOfType<Button>().Length")

// 分析一个 GameObject 的所有组件
inspect_element("Canvas/SomePanel/Button")

// 获取完整 UI 树（可用 canvasName 过滤）
get_ui_hierarchy("SettingsCanvas")
```

## 协议

- 监听端口：`localhost:8972`
- 传输：MCP HTTP+SSE (JSON-RPC 2.0)
- 自动支持 CORS（`Access-Control-Allow-Origin: *`）

## 项目结构

```
UnityExplorerMcp/
├── Plugin.cs                    — BepInEx 主入口
├── MCP/
│   ├── McpServer.cs             — HTTP + JSON-RPC 2.0 服务器
│   ├── JsonHelper.cs            — 轻量 JSON 序列化
│   └── Tools/                   — MCP 工具实现
├── Core/
│   ├── UiHierarchyTraverser.cs  — UI 树遍历引擎
│   ├── ConsoleCaptureService.cs — Unity 日志捕获
│   ├── MainThreadDispatcher.cs  — 主线程调度
│   └── MonoCSharpEvaluator.cs   — C# 代码执行引擎
└── Models/
    └── UiElementInfo.cs         — UI 元素数据模型
```

## 开发注意

1. **Mono.CSharp** — 集成在 UnityExplorer.BIE5.Mono.dll 中，运行时通过 `Reflection.Emit` 创建 public 基类解决 accessibility 问题
2. **C# 7.3** — 目标 .NET Framework 4.7.2
3. **主线程** — Unity API 需在主线程调用，通过 `SynchronizationContext.Post` 调度
4. **BepInEx OnDestroy** — 插件在 chainloader 启动后被销毁（未知根因），当前方案是 OnDestroy 不停止后台线程服务器

## 已知限制

- `execute_code` 依赖 UnityExplorer 内置的 Mono.CSharp，卸载 UnityExplorer 后不可用
- 大 UI 树的 JSON 输出会自动截断（超过指定层级）
- SSE 长连接稳定性有待长期测试

## 未来计划

- [ ] 拆分为独立开源 MCP 项目发布
- [ ] 标准 MCP 安装包
- [ ] 支持更多运行时查询（材质、动画、粒子系统）
- [ ] Mouse-Inspect 视觉反馈
- [ ] WebSocket 替代 SSE 传输
