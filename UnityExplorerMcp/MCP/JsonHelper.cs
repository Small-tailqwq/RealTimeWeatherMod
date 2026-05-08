using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UnityExplorerMcp.MCP
{
    internal static class JsonHelper
    {
        public static string Serialize(object obj, int maxDepth = 10)
        {
            return SerializeValue(obj, 0, maxDepth);
        }

        private static string SerializeValue(object obj, int depth, int maxDepth)
        {
            if (depth > maxDepth) return "\"<max depth>\"";
            if (obj == null) return "null";
            if (obj is string s) return EscapeString(s);
            if (obj is bool b) return b ? "true" : "false";
            if (obj is int i) return i.ToString();
            if (obj is long l) return l.ToString();
            if (obj is float f) return f.ToString("R");
            if (obj is double d) return d.ToString("R");
            if (obj is decimal m) return m.ToString("R");
            if (obj is Enum e) return EscapeString(e.ToString());
            if (obj is IDictionary dict) return SerializeDictionary(dict, depth, maxDepth);
            if (obj is IList list) return SerializeArray(list, depth, maxDepth);
            if (obj is IEnumerable enumerable && !(obj is string))
                return SerializeArray(enumerable, depth, maxDepth);
            return SerializeObject(obj, depth, maxDepth);
        }

        private static string EscapeString(string s)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string SerializeObject(object obj, int depth, int maxDepth)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            var type = obj.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var val = prop.GetValue(obj);
                    if (!first) sb.Append(',');
                    sb.Append(EscapeString(prop.Name));
                    sb.Append(':');
                    sb.Append(SerializeValue(val, depth + 1, maxDepth));
                    first = false;
                }
                catch { }
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var val = field.GetValue(obj);
                    if (!first) sb.Append(',');
                    sb.Append(EscapeString(field.Name));
                    sb.Append(':');
                    sb.Append(SerializeValue(val, depth + 1, maxDepth));
                    first = false;
                }
                catch { }
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeArray(IEnumerable enumerable, int depth, int maxDepth)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first) sb.Append(',');
                sb.Append(SerializeValue(item, depth + 1, maxDepth));
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string SerializeDictionary(IDictionary dict, int depth, int maxDepth)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(',');
                sb.Append(SerializeValue(entry.Key, depth + 1, maxDepth));
                sb.Append(':');
                sb.Append(SerializeValue(entry.Value, depth + 1, maxDepth));
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static Dictionary<string, object> DeserializeSimple(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var parser = new JsonParser(json);
            return parser.ParseObject();
        }

        private class JsonParser
        {
            private readonly string _json;
            private int _pos;

            public JsonParser(string json) { _json = json; _pos = 0; }

            private void SkipWhitespace()
            {
                while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos])) _pos++;
            }

            private char Peek()
            {
                SkipWhitespace();
                return _pos < _json.Length ? _json[_pos] : '\0';
            }

            private char Consume()
            {
                SkipWhitespace();
                return _pos < _json.Length ? _json[_pos++] : '\0';
            }

            public Dictionary<string, object> ParseObject()
            {
                if (Consume() != '{') return null;
                var dict = new Dictionary<string, object>();
                if (Peek() == '}') { _pos++; return dict; }
                while (true)
                {
                    var key = ParseString();
                    if (key == null) return null;
                    if (Consume() != ':') return null;
                    dict[key] = ParseValue();
                    var c = Consume();
                    if (c == '}') break;
                    if (c != ',') return null;
                }
                return dict;
            }

            private string ParseString()
            {
                if (Consume() != '"') return null;
                var sb = new StringBuilder();
                while (_pos < _json.Length)
                {
                    var c = _json[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_pos >= _json.Length) return null;
                        var esc = _json[_pos++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else sb.Append(c);
                }
                return null;
            }

            private object ParseValue()
            {
                var c = Peek();
                if (c == '"') return ParseString();
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == 't') { _pos += 4; return true; }
                if (c == 'f') { _pos += 5; return false; }
                if (c == 'n') { _pos += 4; return null; }
                return ParseNumber();
            }

            private List<object> ParseArray()
            {
                if (Consume() != '[') return null;
                var list = new List<object>();
                if (Peek() == ']') { _pos++; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    var c = Consume();
                    if (c == ']') break;
                    if (c != ',') return null;
                }
                return list;
            }

            private object ParseNumber()
            {
                int start = _pos;
                while (_pos < _json.Length && "-0123456789.eE+".IndexOf(_json[_pos]) >= 0) _pos++;
                var numStr = _json.Substring(start, _pos - start);
                if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                {
                    if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double d))
                        return d;
                    return numStr;
                }
                if (long.TryParse(numStr, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out long l))
                    return l;
                return numStr;
            }
        }

        public static string SerializeJsonRpcRequest(string method, object args)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":");
            sb.Append(EscapeString(method));
            sb.Append(",\"params\":");
            sb.Append(Serialize(args));
            sb.Append("}");
            return sb.ToString();
        }

        public static string SerializeJsonRpcResponseBody(long id, object result)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
            sb.Append(id);
            sb.Append(",\"result\":");
            sb.Append(Serialize(result));
            sb.Append("}");
            return sb.ToString();
        }

        public static string SerializeJsonRpcResponse(long id, object resultContent)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
            sb.Append(id);
            sb.Append(",\"result\":{\"content\":[");
            bool first = true;
            if (resultContent is IEnumerable<McpContentItem> items)
            {
                foreach (var item in items)
                {
                    if (!first) sb.Append(',');
                    sb.Append(Serialize(item));
                    first = false;
                }
            }
            else
            {
                sb.Append("{\"type\":\"text\",\"text\":");
                sb.Append(EscapeString(resultContent?.ToString() ?? "null"));
                sb.Append("}");
            }
            sb.Append("],\"isError\":false}}");
            return sb.ToString();
        }

        public static string SerializeJsonRpcError(long id, int code, string message)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
            sb.Append(id);
            sb.Append(",\"error\":{\"code\":");
            sb.Append(code);
            sb.Append(",\"message\":");
            sb.Append(EscapeString(message));
            sb.Append("}}");
            return sb.ToString();
        }
    }

    internal class McpContentItem
    {
        public string type { get; set; }
        public string text { get; set; }
        public string data { get; set; }
        public string mimeType { get; set; }
    }
}
