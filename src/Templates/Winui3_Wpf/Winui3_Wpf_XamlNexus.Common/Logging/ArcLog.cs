using System.Collections.Concurrent;
using System.Diagnostics;
using NLog;
using Windows.ApplicationModel.Core;

namespace Winui3_Wpf_XamlNexus.Common.Logging {
    /// <summary>
    /// 统一日志入口：支持程序集自动识别、调试输出、缓存
    /// </summary>
    public static class ArcLog {
        /// <summary>
        /// 获取指定类型的日志记录器（自动识别程序集）
        /// </summary>
        public static ArcLoggerProxy GetLogger<T>() {
            var type = typeof(T);
            var loggerName = type.FullName ?? "UnknownTypeFullName";
            return _cache.GetOrAdd(loggerName, _ =>
                new ArcLoggerProxy(LogManager.GetLogger(loggerName)));
        }

        private static readonly ConcurrentDictionary<string, ArcLoggerProxy> _cache = new();
    }

    /// <summary>
    /// 日志代理：统一交由 NLog 输出，避免控制台和调试日志重复
    /// </summary>
    public sealed class ArcLoggerProxy {
        private readonly Logger _inner;

        internal ArcLoggerProxy(Logger inner) {
            _inner = inner;
        }

        public void Info(string message) {
            _inner.Info(message);
        }

        [Conditional("DEBUG")]
        public void Debug(string message) {
            _inner.Debug(message);
        }

        public void Warn(string message) {
            _inner.Warn(message);
        }

        public void Error(string message, Exception? ex = null) {
            _inner.Error(ex, message);            
        }

        public void Error(Exception ex) {
            _inner.Error(ex);
        }
        
        public void Error(UnhandledError ex) {
            _inner.Error(ex);
        }

        public void Fatal(string message) {
            _inner.Fatal(message);
        }

        public void Trace(string message) {
            _inner.Trace(message);
        }
    }
}
