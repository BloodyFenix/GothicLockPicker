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

        return new LockStep
        {
            PlateIndex = plateIndex,
            Direction = direction.Value
        };
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
        // Распознаём как по словам, так и по символам-стрелкам.
        bool hasLeft = line.Contains("Влево", StringComparison.OrdinalIgnoreCase)
                       || line.Contains('◀')
                       || line.Contains('◄')
                       || line.Contains('←');

        bool hasRight = line.Contains("Вправо", StringComparison.OrdinalIgnoreCase)
                        || line.Contains('▶')
                        || line.Contains('►')
                        || line.Contains('→');

        if (hasLeft && !hasRight)
        {
            return TurnDirection.Left;
        }

        if (hasRight && !hasLeft)
        {
            return TurnDirection.Right;
        }

        return null;
    }
}
