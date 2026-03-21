using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace Winui3_XamlNexus.Common.Utils {
    public static class DebugUtil {
        public static string OutputPath => Path.Combine(Environment.CurrentDirectory, "debug");

        public static void RestoreOutputDir() {
            if (Directory.Exists(OutputPath)) {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);
        }

        [Conditional("DEBUG")]
        public static void Output(string msg) {
            try {
                var tag = GetCallerTag();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {msg}");
            }
            catch { }
        }

        public static string GetCallerTag() {
            /*
             * SomeMethod()
                 → DebugUtil.Output()
                   → GetCallerTag()
             */
            // skip Output self
            var frame = new StackFrame(2, false);
            var method = frame.GetMethod();

            if (method == null)
                return "Unknown";

            return _cache.GetOrAdd(method, BuildTag);
        }

        private static string BuildTag(MethodBase method) {
            var type = method.DeclaringType;
            if (type == null)
                return method.Name;

            return $"{type.Namespace}.{type.Name}";
        }

        private static readonly ConcurrentDictionary<MethodBase, string> _cache = new();
    }
}
