using System.Text.RegularExpressions;

namespace LockPicker;

/// <summary>
/// Разбирает текстовую последовательность взлома в список шагов <see cref="LockStep"/>.
/// Поддерживает формат вида: "Шаг 1: Пластина III ➔ Влево ◀".
/// Гибко относится к разделителям и регистру.
/// </summary>
public static class SequenceParser
{
    // Сопоставление римских цифр (I..X) их числовым значениям.
    private static readonly Dictionary<string, int> RomanMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["I"] = 1,
        ["II"] = 2,
        ["III"] = 3,
        ["IV"] = 4,
        ["V"] = 5,
        ["VI"] = 6,
        ["VII"] = 7,
        ["VIII"] = 8,
        ["IX"] = 9,
        ["X"] = 10
    };

    // Захватывает "Пластина <римское/арабское число>" и направление.
    private static readonly Regex PlateRegex = new(
        @"Пластина\s+([IVX]+|\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Захватывает число повторений вида "× 3", "x3", "*3", "х 3" (кириллическая х).
    private static readonly Regex RepeatRegex = new(
        @"[×xX*хХ]\s*(\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Парсит весь многострочный текст. Пустые и нераспознанные строки пропускаются.
    /// </summary>
    public static List<LockStep> Parse(string text)
    {
        var result = new List<LockStep>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            LockStep? step = ParseLine(line);
            if (step is not null)
            {
                result.Add(step);
            }
        }

        return result;
    }

    /// <summary>
    /// Парсит одну строку. Возвращает null, если строку не удалось распознать.
    /// </summary>
    public static LockStep? ParseLine(string line)
    {
        Match plateMatch = PlateRegex.Match(line);
        if (!plateMatch.Success)
        {
            return null;
        }

        int plateIndex = ParsePlateNumber(plateMatch.Groups[1].Value);
        if (plateIndex <= 0)
        {
            return null;
        }

        TurnDirection? direction = ParseDirection(line);
        if (direction is null)
        {
            return null;
        }

        int repeatCount = ParseRepeatCount(line);

        return new LockStep
        {
            PlateIndex = plateIndex,
            Direction = direction.Value,
            RepeatCount = repeatCount
        };
    }

    private static int ParseRepeatCount(string line)
    {
        Match repeatMatch = RepeatRegex.Match(line);
        if (repeatMatch.Success && int.TryParse(repeatMatch.Groups[1].Value, out int count) && count > 0)
        {
            return count;
        }

        // По умолчанию один поворот.
        return 1;
    }

    private static int ParsePlateNumber(string raw)
    {
        // Сначала пытаемся как арабское число.
        if (int.TryParse(raw, out int arabic))
        {
            return arabic;
        }

        // Затем как римское из словаря.
        if (RomanMap.TryGetValue(raw, out int roman))
        {
            return roman;
        }

        return 0;
    }

    private static TurnDirection? ParseDirection(string line)
    {
        // Сначала — однозначные ключевые слова. У них приоритет над символами-стрелками,
        // потому что стрелки → и ➔ часто используются как разделитель в строке
        // «Пластина N → направление», и иначе возникает ложный конфликт направлений.
        bool wordLeft = line.Contains("Влево", StringComparison.OrdinalIgnoreCase);
        bool wordRight = line.Contains("Вправо", StringComparison.OrdinalIgnoreCase);

        if (wordLeft && !wordRight)
        {
            return TurnDirection.Left;
        }

        if (wordRight && !wordLeft)
        {
            return TurnDirection.Right;
        }

        // Затем — направленные символы. Разделители → и ➔ намеренно НЕ считаем
        // направлением (это служебные стрелки между номером пластины и направлением).
        bool symLeft = line.Contains('◀') || line.Contains('◄') || line.Contains('←');
        bool symRight = line.Contains('▶') || line.Contains('►');

        if (symLeft && !symRight)
        {
            return TurnDirection.Left;
        }

        if (symRight && !symLeft)
        {
            return TurnDirection.Right;
        }

        return null;
    }
}
