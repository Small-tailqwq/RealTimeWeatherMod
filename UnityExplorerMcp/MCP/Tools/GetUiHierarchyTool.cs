using System.Collections.Generic;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class GetUiHierarchyTool : MainThreadTool
    {
        public override string Name => "get_ui_hierarchy";
        public override string Description => "Get the full UI hierarchy tree with RectTransform coordinates. Lists all Canvas elements and their children with positions, sizes, anchors.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["canvasName"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Optional canvas name filter (case-insensitive)"
                }
            }
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            string canvasFilter = args != null && args.TryGetValue("canvasName", out var v) ? v?.ToString() : null;
            var hierarchy = Core.UiHierarchyTraverser.GetHierarchy(canvasFilter);
            var json = MCP.JsonHelper.Serialize(hierarchy);
            return new McpContentItem { type = "text", text = json };
        }
    }
}
