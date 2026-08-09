using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Serialization context for the settings. The source-generated variant is deliberate:
    /// it does not rely on reflection, so it stays correct if the project is ever published
    /// with trimming/AOT (neither of which produces warnings today).
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(AppSettings))]
    internal sealed partial class SettingsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Reads and writes the settings file. Loading never throws: any failure (missing file,
    /// corrupt JSON, no permissions) yields the default settings, and the reason is returned
    /// through an out parameter for the caller to log.
    /// </summary>
    public static class SettingsStore
    {
        public const string FileName = "settings.json";

        /// <summary>%LOCALAPPDATA%\SystemBarIndicator\settings.json — next to app.log.</summary>
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemBarIndicator", FileName);

        /// <summary>Loads normalized settings; on any error, the defaults.</summary>
        public static AppSettings Load(string path, out Exception? error)
        {
            error = null;
            try
            {
                if (!File.Exists(path))
                    return new AppSettings();

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new AppSettings();

                // A file containing "null" is valid JSON that deserializes to null: not an
                // error, but not settings either.
                var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
                return (loaded ?? new AppSettings()).Normalized();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
            {
                error = ex;
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves the settings atomically: write to a temp file alongside, then swap it in.
        /// An interrupted write cannot leave truncated JSON in place of a working file.
        /// Normalized values are always persisted — NaN/infinity are not valid JSON.
        /// </summary>
        public static void Save(string path, AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings.Normalized(), SettingsJsonContext.Default.AppSettings);

            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
    }
}
