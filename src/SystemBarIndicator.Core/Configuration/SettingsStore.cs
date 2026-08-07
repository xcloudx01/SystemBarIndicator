using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Контекст сериализации настроек. Source-generated вариант выбран сознательно:
    /// он не зависит от рефлексии, поэтому останется корректным, если проект когда-нибудь
    /// начнут публиковать с trimming/AOT (сейчас предупреждений не даёт ни тот, ни другой).
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
    /// Чтение и запись файла настроек. Загрузка никогда не бросает: любой сбой
    /// (нет файла, битый JSON, нет прав) даёт настройки по умолчанию, а причина
    /// возвращается через out-параметр — логирует её вызывающий код.
    /// </summary>
    public static class SettingsStore
    {
        public const string FileName = "settings.json";

        /// <summary>%LOCALAPPDATA%\SystemBarIndicator\settings.json — рядом с app.log.</summary>
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemBarIndicator", FileName);

        /// <summary>Загружает нормализованные настройки; при любой ошибке — значения по умолчанию.</summary>
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

                // Содержимое "null" — валидный JSON, дающий null: это не ошибка, но и не настройки.
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
        /// Атомарно сохраняет настройки: запись во временный файл рядом и подмена.
        /// Оборванная запись не оставит обрезанный JSON вместо рабочего файла.
        /// Сохраняются всегда нормализованные значения — NaN/бесконечность в JSON недопустимы.
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
