using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using XamlNexus.Gallery.Common.Logging;

namespace XamlNexus.Gallery.Common.Utils.Storage {
    public static class JsonSaver {
        static JsonSaver() {
            _optionsStore.Converters.Add(new IntPtrJsonConverter());
            _optionsLoad.Converters.Add(new IntPtrJsonConverter());
        }

        public static T Load<T>(string filePath, JsonSerializerContext context) {
            return LoadAsync<T>(filePath, context).GetAwaiter().GetResult();
        }

        public static void Save<T>(string filePath, T data, JsonSerializerContext context) {
            SaveAsync(filePath, data, context).GetAwaiter().GetResult();
        }

        public static async Task<T> LoadAsync<T>(string filePath, JsonSerializerContext context, params JsonConverter[]? converters) {
            try {
                JsonSerializerOptions combinedLoadOptions = new(_optionsLoad) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };

                if (converters != null) {
                    foreach (var converter in converters) {
                        combinedLoadOptions.Converters.Add(converter);
                    }
                }

                using FileStream stream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<T>(stream, combinedLoadOptions).ConfigureAwait(false)
                    ?? throw new JsonException("The JSON file contains null instead of the requested value.");
            }
            catch (FileNotFoundException) { throw; }
            catch (DirectoryNotFoundException) { throw; }
            catch (Exception ex) {
                ArcLog.GetLogger<JsonSerializerContext>().Error(ex);
                throw;
            }
        }

        // Recover malformed JSON only; access failures must not reset user settings.
        public static async Task<T> LoadOrCreateAsync<T>(string filePath, JsonSerializerContext context,
            Func<T> createDefault) {
            try {
                return await LoadAsync<T>(filePath, context).ConfigureAwait(false);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (JsonException) {
                string backup = filePath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")
                    + "." + Guid.NewGuid().ToString("N") + ".bak";
                // A failed backup aborts recovery, leaving the original untouched.
                File.Copy(filePath, backup, overwrite: false);
                ArcLog.GetLogger<JsonSerializerContext>().Warn($"Invalid JSON preserved at {backup}; restoring defaults.");
            }

            T defaults = createDefault();
            await SaveAsync(filePath, defaults, context).ConfigureAwait(false);
            return defaults;
        }

        public static async Task SaveAsync<T>(string filePath, T data, JsonSerializerContext context, params JsonConverter[]? converters) {
            try {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                JsonSerializerOptions combinedStoreOptions = new(_optionsStore) { TypeInfoResolver = JsonTypeInfoResolver.Combine(context) };

                if (converters != null) {
                    foreach (var converter in converters) {
                        combinedStoreOptions.Converters.Add(converter);
                    }
                }

                string destination = Path.GetFullPath(filePath);
                string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try {
                    // Do not truncate the current settings until a complete replacement is ready.
                    await using (var stream = new FileStream(temporary, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, 81920, useAsync: true)) {
                        await JsonSerializer.SerializeAsync(stream, data, combinedStoreOptions).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);
                        stream.Flush(flushToDisk: true);
                    }
                    File.Move(temporary, destination, overwrite: true);
                }
                finally {
                    try { File.Delete(temporary); }
                    catch (IOException cleanupError) { ArcLog.GetLogger<JsonSerializerContext>().Warn(cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { ArcLog.GetLogger<JsonSerializerContext>().Warn(cleanupError.Message); }
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<JsonSerializerContext>().Error(ex);
                throw;
            }
        }

        private static readonly JsonSerializerOptions _optionsLoad = new() {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip, // 允许 JSON 文件里写注释
            Converters = {
                new JsonStringEnumConverter() // 允许 Enum 读写为字符串
            }
        };

        // ref: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonignorecondition?view=net-8.0
        private static readonly JsonSerializerOptions _optionsStore = new() {
            WriteIndented = true,
            // 允许写入空值
            // Property is always serialized and deserialized, regardless of IgnoreNullValues configuration.
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip, // 允许 JSON 文件里写注释
            Converters = {
                new JsonStringEnumConverter() // 允许 Enum 读写为字符串
            }
        };
    }
}
