using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using UnityExplorerMcp.MCP.Tools;
using UnityExplorerMcp.Core;

namespace UnityExplorerMcp.MCP
{
    internal class McpServer
    {
        private readonly int _port;
        private readonly ManualLogSource _logger;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly ToolRegistry _toolRegistry;

        // SSE state
        private TcpClient _sseClient;
        private NetworkStream _sseStream;
        private StreamWriter _sseWriter;
        private readonly object _sseLock = new object();

        public McpServer(int port, ManualLogSource logger)
        {
            _port = port;
            _logger = logger;
            _toolRegistry = new ToolRegistry();
        }

        public void RegisterTool(IMcpTool tool)
        {
            _toolRegistry.Register(tool);
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();

            var thread = new Thread(AcceptClients)
            {
                IsBackground = true,
                Name = "MCP-HTTP"
            };
            thread.Start();

            _logger.LogInfo($"[UnityExplorerMcp] MCP server listening on http://localhost:{_port}/");
            _logger.LogInfo($"[UnityExplorerMcp] Tools: {_toolRegistry.GetToolDescriptions().Count} registered");
        }

        public void Stop()
        {
            _cts?.Cancel();
            CloseSse();
            _listener?.Stop();
        }

        private void CloseSse()
        {
            lock (_sseLock)
            {
                if (_sseWriter != null) { _sseWriter.Flush(); _sseWriter.Dispose(); _sseWriter = null; }
                if (_sseStream != null) { _sseStream.Dispose(); _sseStream = null; }
                if (_sseClient != null) { _sseClient.Close(); _sseClient = null; }
            }
        }

        private void AcceptClients()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch when (_cts.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError($"[UnityExplorerMcp] Accept error: {ex.Message}");
                }
            }
        }

        private void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[8192];
                    int totalRead = 0;
                    int bodyStart = -1;
                    int contentLength = 0;
                    string firstLine = null;

                    while (true)
                    {
                        if (totalRead >= buffer.Length)
                            Array.Resize(ref buffer, buffer.Length * 2);

                        int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                        if (read <= 0) break;
                        totalRead += read;

                        if (firstLine == null)
                        {
                            var headerStr = Encoding.UTF8.GetString(buffer, 0, totalRead);
                            int idx = headerStr.IndexOf("\r\n", StringComparison.Ordinal);
                            if (idx >= 0) firstLine = headerStr.Substring(0, idx);
                        }

                        if (bodyStart < 0)
                        {
                            var headerStr = Encoding.UTF8.GetString(buffer, 0, totalRead);
                            int idx = headerStr.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                bodyStart = idx + 4;
                                foreach (var line in headerStr.Substring(0, idx).Split('\n'))
                                {
                                    var l = line.Trim();
                                    if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                        int.TryParse(l.Substring(15).Trim(), out contentLength);
                                }
                            }
                        }

                        if (bodyStart >= 0 && totalRead >= bodyStart + contentLength)
                            break;
                    }

                    bool isGet = firstLine != null && firstLine.StartsWith("GET");
                    bool isPost = firstLine != null && firstLine.StartsWith("POST");
                    bool isOptions = firstLine != null && firstLine.StartsWith("OPTIONS");

                    if (isOptions)
                    {
                        var resp = "HTTP/1.1 204 No Content\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nAccess-Control-Max-Age: 86400\r\n\r\n";
                        var respBytes = Encoding.UTF8.GetBytes(resp);
                        stream.Write(respBytes, 0, respBytes.Length);
                        return;
                    }

                    if (isGet)
                    {
                        HandleSseConnection(client, stream);
                        return; // SSE keeps the connection alive in the method above
                    }

                    if (isPost && bodyStart >= 0)
                    {
                        string body = Encoding.UTF8.GetString(buffer, bodyStart, totalRead - bodyStart);
                        HandlePostRequest(stream, body);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UnityExplorerMcp] Handle error: {ex.Message}");
            }
        }

        private void HandleSseConnection(TcpClient client, NetworkStream stream)
        {
            lock (_sseLock)
            {
                if (_sseWriter != null) { try { _sseWriter.Dispose(); } catch { } }
                if (_sseStream != null) { try { _sseStream.Dispose(); } catch { } }
                if (_sseClient != null) { try { _sseClient.Close(); } catch { } }
                _sseClient = client;
                _sseStream = stream;
                _sseWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            }

            lock (_sseLock)
            {
                try
                {
                    _sseWriter.Write("HTTP/1.1 200 OK\r\n");
                    _sseWriter.Write("Content-Type: text/event-stream\r\n");
                    _sseWriter.Write("Cache-Control: no-cache\r\n");
                    _sseWriter.Write("Connection: keep-alive\r\n");
                    _sseWriter.Write("Access-Control-Allow-Origin: *\r\n");
                    _sseWriter.Write("Access-Control-Allow-Methods: POST, OPTIONS\r\n");
                    _sseWriter.Write("\r\n");
                    _sseWriter.Flush();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[UnityExplorerMcp] SSE header error: {ex.Message}");
                    return;
                }
            }

            _logger.LogInfo("[UnityExplorerMcp] SSE connection established");

            SendSseEvent("endpoint", "/");

            try
            {
                int tick = 0;
                while (!_cts.IsCancellationRequested && client.Connected)
                {
                    Thread.Sleep(1000);
                    tick++;
                    if (tick % 5 == 0)
                    {
                        lock (_sseLock)
                        {
                            try { if (_sseWriter != null) { _sseWriter.Write(": keepalive\r\n\r\n"); _sseWriter.Flush(); } } catch { }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                lock (_sseLock)
                {
                    if (_sseWriter != null) { _sseWriter.Dispose(); _sseWriter = null; }
                    if (_sseStream != null) { _sseStream.Dispose(); _sseStream = null; }
                    if (_sseClient != null) { _sseClient.Close(); _sseClient = null; }
                }
                _logger.LogInfo("[UnityExplorerMcp] SSE connection closed");
            }
        }

        private void SendSseEvent(string eventName, string data)
        {
            lock (_sseLock)
            {
                try
                {
                    if (_sseWriter != null)
                    {
                        _sseWriter.Write("event: " + eventName + "\r\n");
                        _sseWriter.Write("data: " + data + "\r\n");
                        _sseWriter.Write("\r\n");
                        _sseWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[UnityExplorerMcp] SSE send error: {ex.Message}");
                }
            }
        }

        private void HandlePostRequest(NetworkStream stream, string body)
        {
            string response = ProcessMcpRequest(body);
            var respBytes = Encoding.UTF8.GetBytes(response);

            lock (_sseLock)
            {
                try
                {
                    if (_sseWriter != null)
                    {
                        var header = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: " + respBytes.Length + "\r\n\r\n";
                        var headerBytes = Encoding.UTF8.GetBytes(header);
                        stream.Write(headerBytes, 0, headerBytes.Length);
                        stream.Write(respBytes, 0, respBytes.Length);
                        // try to push over SSE too
                        _sseWriter.Write("event: message\r\n");
                        _sseWriter.Write("data: " + response + "\r\n");
                        _sseWriter.Write("\r\n");
                        _sseWriter.Flush();
                        return;
                    }
                }
                catch { }
            }

            // No SSE fallback: just write HTTP directly
            var header2 = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: " + respBytes.Length + "\r\n\r\n";
            var headerBytes2 = Encoding.UTF8.GetBytes(header2);
            stream.Write(headerBytes2, 0, headerBytes2.Length);
            stream.Write(respBytes, 0, respBytes.Length);
        }

        private string ProcessMcpRequest(string body)
        {
            var req = JsonHelper.DeserializeSimple(body);
            if (req == null || !req.ContainsKey("method"))
                return JsonHelper.SerializeJsonRpcError(0, -32700, "Parse error");

            var idObj = req.TryGetValue("id", out var idVal) ? idVal : null;
            long id = 0;
            if (idObj is long l) id = l;
            else if (idObj is int i) id = i;
            else if (idObj is double d) id = (long)d;

            string method = req["method"]?.ToString();

            Dictionary<string, object> paramsObj = null;
            if (req.TryGetValue("params", out var pVal) && pVal is Dictionary<string, object> pd)
                paramsObj = pd;

            // MCP Initialize handshake
            if (method == "initialize")
            {
                return JsonHelper.SerializeJsonRpcResponseBody(id, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "unity-explorer-mcp",
                            version = "1.0.0"
                        }
                    });
            }

            if (method == "notifications/initialized")
            {
                return "";
            }

            if (method == "tools/list")
            {
                var tools = _toolRegistry.GetToolDescriptions();
                return JsonHelper.SerializeJsonRpcResponseBody(id, new { tools });
            }

            if (method == "tools/call")
            {
                if (paramsObj == null || !paramsObj.TryGetValue("name", out var nameVal))
                    return JsonHelper.SerializeJsonRpcError(id, -32602, "Missing tool name");

                string toolName = nameVal?.ToString();
                var tool = _toolRegistry.GetTool(toolName);
                if (tool == null)
                    return JsonHelper.SerializeJsonRpcError(id, -32601, $"Unknown tool: {toolName}");

                Dictionary<string, object> args = null;
                if (paramsObj.TryGetValue("arguments", out var argsVal) && argsVal is Dictionary<string, object> ad)
                    args = ad;

                try
                {
                    McpContentItem contentItem;
                    if (tool is MainThreadTool mtt)
                    {
                        var resultObj = Core.MainThreadDispatcher.Execute(() => (object)mtt.ExecuteOnMain(args));
                        contentItem = (McpContentItem)resultObj;
                    }
                    else
                    {
                        var toolResult = tool.Execute(args ?? new Dictionary<string, object>());
                        var json = toolResult is string s ? s : JsonHelper.Serialize(toolResult);
                        contentItem = new McpContentItem { type = "text", text = json };
                    }
                    var list = new List<McpContentItem>();
                    list.Add(contentItem);
                    try
                    {
                        return JsonHelper.SerializeJsonRpcResponseBody(id, new { content = list, isError = false });
                    }
                    catch (Exception serEx)
                    {
                        return JsonHelper.SerializeJsonRpcResponseBody(id, new { content = new List<McpContentItem> { new McpContentItem { type = "text", text = "Serialization error: " + serEx.Message } }, isError = true });
                    }
                }
                catch (Exception ex)
                {
                    return JsonHelper.SerializeJsonRpcError(id, -32603, ex.Message);
                }
            }

            if (method == "ping")
            {
                return JsonHelper.SerializeJsonRpcResponseBody(id, new { });
            }

            return JsonHelper.SerializeJsonRpcError(id, -32601, $"Unknown method: {method}");
        }
    }

    internal abstract class MainThreadTool : IMcpTool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Dictionary<string, object> InputSchema { get; }
        object IMcpTool.Execute(Dictionary<string, object> args) => Execute(args);
        public abstract object Execute(Dictionary<string, object> args);
        public abstract McpContentItem ExecuteOnMain(Dictionary<string, object> args);
    }
}
