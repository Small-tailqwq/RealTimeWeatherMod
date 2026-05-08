using System.Collections.Generic;

namespace UnityExplorerMcp.MCP.Tools
{
    internal interface IMcpTool
    {
        string Name { get; }
        string Description { get; }
        Dictionary<string, object> InputSchema { get; }
        object Execute(Dictionary<string, object> args);
    }
}
