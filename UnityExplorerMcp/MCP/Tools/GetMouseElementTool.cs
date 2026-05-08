using System.Collections.Generic;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class GetMouseElementTool : MainThreadTool
    {
        public override string Name => "get_mouse_element";
        public override string Description => "Get the UI element currently under the mouse cursor. Returns the topmost UI element hit by GraphicRaycaster at the current mouse position.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>()
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            var result = Core.UiHierarchyTraverser.GetMouseElement();
            if (result == null)
                return new McpContentItem { type = "text", text = "{\"found\":false}" };

            var parts = result.Split('|');
            if (parts.Length >= 2)
            {
                var components = Core.UiHierarchyTraverser.GetComponentDetails(parts[1]);
                var json = MCP.JsonHelper.Serialize(new
                {
                    found = true,
                    name = parts[0],
                    path = parts[1],
                    components
                });
                return new McpContentItem { type = "text", text = json };
            }

            return new McpContentItem { type = "text", text = "{\"found\":false}" };
        }
    }
}
