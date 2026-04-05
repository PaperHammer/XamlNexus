namespace Winui3_XamlNexus.Common {
    public class Errors {        
        public class FileAccessException : Exception {
            public string FilePath { get; } = string.Empty;
            public string Operation { get; } = string.Empty;

            public FileAccessException() {
            }

            public FileAccessException(string filePath, string operation)
                : base(GenerateMessage(filePath, operation)) {
                FilePath = filePath;
                Operation = operation;
            }
            
            public FileAccessException(string filePath, string operation, Exception inner)
                : base(GenerateMessage(filePath, operation), inner) {
                FilePath = filePath;
                Operation = operation;
            }

            private static string GenerateMessage(string filePath, string operation) {
                return $"对文件 \"{filePath}\" 的 {operation} 操作发生错误，请检查文件完整性或相关权限。";
            }
        }

        public class FileCreateException : Exception {
            public string FilePath { get; } = string.Empty;

            public FileCreateException() {
            }

            public FileCreateException(string msg, string filePath)
                : base(msg) {
                FilePath = filePath;
            }
            
            public FileCreateException(string msg, string filePath, Exception inner)
                : base(msg, inner) {
                FilePath = filePath;
            }
        }
    }
}
