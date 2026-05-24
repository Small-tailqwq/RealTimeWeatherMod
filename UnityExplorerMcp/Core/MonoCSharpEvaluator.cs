using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace UnityExplorerMcp.Core
{
    internal static class MonoCSharpEvaluator
    {
        private static object _eval;
        private static MethodInfo _evalSimple;
        private static MethodInfo _runMethod;
        private static StringWriter _output;
        private static bool _ready;
        private static bool _attempted;

        public static string Execute(string code)
        {
            if (!_ready && !TryInit())
                return "Mono.CSharp not available (evaluator init failed)";

            try
            {
                var result = _evalSimple.Invoke(_eval, new object[] { code });
                if (result != null)
                {
                    var s = result.ToString();
                    if (s.Length > 8000) s = s.Substring(0, 8000) + "...";
                    return s;
                }

                var output = _output?.ToString();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var lines = output.Split('\n');
                    var last = lines.Length >= 2 ? lines[lines.Length - 2].Trim() : output.Trim();
                    if (!string.IsNullOrEmpty(last)) return "Error: " + last;
                }

                return "null";
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException;
                return "Error: " + (inner?.Message ?? tie.Message);
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private static bool TryInit()
        {
            if (_attempted) return false;
            _attempted = true;

            try
            {
                Assembly targetAsm = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetType("Mono.CSharp.Evaluator") != null)
                    {
                        targetAsm = asm;
                        break;
                    }
                }
                if (targetAsm == null) return false;

                var csType = targetAsm.GetType("Mono.CSharp.CompilerSettings");
                var ccType = targetAsm.GetType("Mono.CSharp.CompilerContext");
                var spType = targetAsm.GetType("Mono.CSharp.StreamReportPrinter");
                var evalType = targetAsm.GetType("Mono.CSharp.Evaluator");
                var ibType = targetAsm.GetType("Mono.CSharp.InteractiveBase");

                // Use Reflection.Emit to create a public class extending InteractiveBase
                var dynAsmName = new AssemblyName("MCPDynamicBase");
                var dynAsm = AssemblyBuilder.DefineDynamicAssembly(dynAsmName, AssemblyBuilderAccess.Run);
                var dynMod = dynAsm.DefineDynamicModule("MainModule");
                var dynType = dynMod.DefineType("MCP.PublicInteractiveBase",
                    TypeAttributes.Public | TypeAttributes.Class, ibType);
                var publicBaseType = dynType.CreateType();

                // Create CompilerSettings
                var settings = Activator.CreateInstance(csType);
                var refsField = csType.GetField("AssemblyReferences");
                if (refsField != null)
                {
                    var refs = (System.Collections.Generic.List<string>)refsField.GetValue(settings);
                    refs.Add("System.dll");
                    refs.Add("System.Core.dll");
                    refs.Add("UnityEngine.dll");
                    refs.Add("UnityEngine.CoreModule.dll");
                    refs.Add("UnityEngine.UI.dll");
                    refs.Add("Assembly-CSharp.dll");
                }
                csType.GetField("Unsafe")?.SetValue(settings, true);

                _output = new StringWriter();
                var printer = Activator.CreateInstance(spType, new object[] { _output });
                var context = Activator.CreateInstance(ccType, new[] { settings, printer });
                _eval = Activator.CreateInstance(evalType, new[] { context });

                // Set InteractiveBaseClass to our public derived type
                var ibProp = evalType.GetProperty("InteractiveBaseClass");
                ibProp?.SetValue(_eval, publicBaseType);

                _evalSimple = evalType.GetMethod("Evaluate", new[] { typeof(string) });
                _runMethod = evalType.GetMethod("Run", new[] { typeof(string) });

                if (_runMethod != null)
                {
                    _runMethod.Invoke(_eval, new object[] { "using System;" });
                    _runMethod.Invoke(_eval, new object[] { "using System.Collections.Generic;" });
                    _runMethod.Invoke(_eval, new object[] { "using System.Linq;" });
                    _runMethod.Invoke(_eval, new object[] { "using UnityEngine;" });
                    _runMethod.Invoke(_eval, new object[] { "using UnityEngine.UI;" });
                    _runMethod.Invoke(_eval, new object[] { "using UnityEngine.EventSystems;" });
                    _runMethod.Invoke(_eval, new object[] { "using TMPro;" });
                }

                _ready = true;
                return _evalSimple != null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[UnityExplorerMcp] Init failed: " + ex);
                return false;
            }
        }
    }
}
