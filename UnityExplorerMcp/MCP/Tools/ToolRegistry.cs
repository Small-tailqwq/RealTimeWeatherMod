using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class ToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> _tools = new Dictionary<string, IMcpTool>();

        public void Register(IMcpTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public IMcpTool GetTool(string name)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }

        public List<Dictionary<string, object>> GetToolDescriptions()
        {
            return _tools.Values.Select(t => new Dictionary<string, object>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema ?? new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            }).ToList();
        }
    }
}
