using System.Text.Json;
using System.Text.Json.Serialization;

namespace LockPicker;

/// <summary>
/// Настройки приложения, сохраняемые между запусками: задержка, заголовок окна игры,
/// последняя введённая последовательность, а также позиция и размер главного окна.
/// </summary>
public sealed class AppSettings
{
    // Задержка между нажатиями клавиш, мс.
    public int DelayMs { get; set; } = 1000;

    // Часть заголовка окна игры для поиска.
    public string WindowTitle { get; set; } = "Gothic 1 Remake";

    // Последняя введённая последовательность (чтобы не вводить заново).
    public string LastSequence { get; set; } = string.Empty;

    // Геометрия главного окна.
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = -1;
    public int WindowHeight { get; set; } = -1;
    public bool WindowMaximized { get; set; }

    [JsonIgnore]
    public bool HasWindowBounds =>
        WindowX >= 0 && WindowY >= 0 && WindowWidth > 0 && WindowHeight > 0;

    // ===== Загрузка / сохранение =====

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Путь к файлу настроек рядом с исполняемым файлом (settings.json в папке с exe).
    /// </summary>
    public static string SettingsPath
    {
        get
        {
            // AppContext.BaseDirectory указывает на папку с exe (а не на рабочий каталог).
            string dir = AppContext.BaseDirectory;
            return Path.Combine(dir, "settings.json");
        }
    }

    /// <summary>
    /// Загружает настройки с диска. При любой ошибке возвращает настройки по умолчанию.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // Повреждённый или недоступный файл — откатываемся к значениям по умолчанию.
            return new AppSettings();
        }
    }

    /// <summary>
    /// Сохраняет настройки на диск. Ошибки записи игнорируются (не критично).
    /// </summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Игнорируем — отсутствие сохранения не должно ронять приложение.
        }
    }
}
