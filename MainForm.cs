using System.ComponentModel;

namespace LockPicker;

/// <summary>
/// Главное окно приложения. UI строится программно (без дизайнера).
/// Оформлено в мрачном готическом стиле.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppSettings _settings;

    // ===== Готическая палитра =====

    /// <summary>Глубокий, почти чёрный фон — холодный камень склепа.</summary>
    private static readonly Color GothBackground = Color.FromArgb(20, 18, 22);

    /// <summary>Фон панелей и полей ввода — тёмный обсидиан.</summary>
    private static readonly Color GothPanel = Color.FromArgb(30, 27, 33);

    /// <summary>Чуть более светлый оттенок для элементов в фокусе.</summary>
    private static readonly Color GothPanelLight = Color.FromArgb(42, 38, 46);

    /// <summary>Основной текст — выцветший пергамент / старое золото.</summary>
    private static readonly Color GothText = Color.FromArgb(200, 184, 150);

    /// <summary>Приглушённый текст подсказок.</summary>
    private static readonly Color GothTextDim = Color.FromArgb(140, 128, 108);

    /// <summary>Кроваво-красный акцент.</summary>
    private static readonly Color GothBlood = Color.FromArgb(120, 22, 22);

    /// <summary>Тусклое золото для рамок и заголовков.</summary>
    private static readonly Color GothGold = Color.FromArgb(168, 138, 78);

    /// <summary>Болотно-зелёный для действия «Старт».</summary>
    private static readonly Color GothGreen = Color.FromArgb(58, 92, 56);

    /// <summary>Тёмный фон-подложка под пластинами замка.</summary>
    private static readonly Color TrackBackdrop = Color.FromArgb(14, 12, 16);

    // ===== Готические шрифты =====

    private static readonly Font GothTitleFont = new("Constantia", 22F, FontStyle.Bold);
    private static readonly Font GothHeadingFont = new("Constantia", 9.75F, FontStyle.Italic);
    private static readonly Font GothBodyFont = new("Cambria", 10F);
    private static readonly Font GothButtonFont = new("Constantia", 10F, FontStyle.Bold);
    private static readonly Font GothMonoFont = new("Consolas", 9.5F);

    // Элементы управления.
    private readonly TextBox _windowTitleBox = new();
    private readonly NumericUpDown _delayBox = new();
    private readonly TextBox _sequenceBox = new();
    private readonly NumericUpDown _startPlateBox = new();
    private readonly NumericUpDown _countdownBox = new();
    private readonly Button _findWindowButton = new();
    private readonly Label _windowStatusLabel = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly ListBox _logBox = new();
    private readonly ProgressBar _progressBar = new();

    // ===== Элементы конструктора замка (правая панель) =====
    private readonly NumericUpDown _constPlateCountBox = new();
    private readonly TableLayoutPanel _matrixPanel = new();
    private readonly FlowLayoutPanel _startPanel = new();
    private readonly Label _solveStatusLabel = new();

    // Текущие комбобоксы матрицы связей и визуальные пластины стартовых позиций.
    private ComboBox[,] _matrixCombos = new ComboBox[0, 0];
    private PlateRowControl[] _plates = Array.Empty<PlateRowControl>();

    private IntPtr _gameWindow = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public MainForm()
    {
        _settings = AppSettings.Load();
        BuildUi();
        ApplySettingsToUi();
        RestoreWindowGeometry();
    }

    // ===== Построение интерфейса =====

    private void BuildUi()
    {
        Text = "Gothic LockPicker — взлом замков";
        MinimumSize = new Size(1040, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = GothBodyFont;
        BackColor = GothBackground;
        ForeColor = GothText;
        LoadWindowIcon();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 1,
            AutoScroll = true,
            BackColor = GothBackground
        };
        // Левая колонка — управление автоматом, правая — конструктор замка.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = GothBackground
        };

        // --- Декоративный заголовок ---
        var titleLabel = new Label
        {
            Text = "⚜ Gothic LockPicker ⚜",
            Font = GothTitleFont,
            ForeColor = GothGold,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var subtitleLabel = new Label
        {
            Text = "— автоматический взломщик замков —",
            Font = GothHeadingFont,
            ForeColor = GothBlood,
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 12)
        };
        layout.Controls.Add(titleLabel);
        layout.Controls.Add(subtitleLabel);

        // --- Заголовок окна игры ---
        layout.Controls.Add(MakeLabel("Заголовок окна игры (поиск по части названия):"));
        _windowTitleBox.Width = 500;
        StyleTextBox(_windowTitleBox);
        layout.Controls.Add(_windowTitleBox);

        var findRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 8),
            BackColor = GothBackground
        };
        _findWindowButton.Text = "Найти окно";
        _findWindowButton.AutoSize = true;
        StyleButton(_findWindowButton, GothPanelLight, GothGold);
        _findWindowButton.Click += OnFindWindowClick;
        _windowStatusLabel.Text = "Окно не найдено";
        _windowStatusLabel.ForeColor = GothBlood;
        _windowStatusLabel.Font = GothHeadingFont;
        _windowStatusLabel.AutoSize = true;
        _windowStatusLabel.Margin = new Padding(12, 8, 0, 0);
        findRow.Controls.Add(_findWindowButton);
        findRow.Controls.Add(_windowStatusLabel);
        layout.Controls.Add(findRow);

        // --- Задержка ---
        layout.Controls.Add(MakeLabel("Задержка между нажатиями (мс):"));
        _delayBox.Minimum = 0;
        _delayBox.Maximum = 60000;
        _delayBox.Increment = 50;
        _delayBox.Width = 120;
        StyleNumeric(_delayBox);
        layout.Controls.Add(_delayBox);

        // --- Стартовая пластина ---
        layout.Controls.Add(MakeLabel("Текущая (стартовая) пластина — где сейчас фокус:"));
        _startPlateBox.Minimum = 1;
        _startPlateBox.Maximum = 20;
        _startPlateBox.Value = 1;
        _startPlateBox.Width = 120;
        StyleNumeric(_startPlateBox);
        layout.Controls.Add(_startPlateBox);

        // --- Обратный отсчёт ---
        layout.Controls.Add(MakeLabel("Обратный отсчёт перед стартом (сек):"));
        _countdownBox.Minimum = 0;
        _countdownBox.Maximum = 30;
        _countdownBox.Value = 3;
        _countdownBox.Width = 120;
        StyleNumeric(_countdownBox);
        layout.Controls.Add(_countdownBox);

        // --- Последовательность ---
        layout.Controls.Add(MakeLabel("Последовательность взлома:"));
        _sequenceBox.Multiline = true;
        _sequenceBox.ScrollBars = ScrollBars.Vertical;
        _sequenceBox.Width = 500;
        _sequenceBox.Height = 160;
        StyleTextBox(_sequenceBox);
        _sequenceBox.Font = GothMonoFont;
        layout.Controls.Add(_sequenceBox);

        // --- Кнопки старт/стоп ---
        var actionRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8),
            BackColor = GothBackground
        };
        _startButton.Text = "▶ Старт";
        _startButton.AutoSize = true;
        StyleButton(_startButton, GothGreen, GothText);
        _startButton.Click += OnStartClick;

        _stopButton.Text = "■ Стоп";
        _stopButton.AutoSize = true;
        StyleButton(_stopButton, GothBlood, GothText);
        _stopButton.Enabled = false;
        _stopButton.Margin = new Padding(12, 3, 0, 3);
        _stopButton.Click += OnStopClick;

        actionRow.Controls.Add(_startButton);
        actionRow.Controls.Add(_stopButton);
        layout.Controls.Add(actionRow);

        // --- Прогресс ---
        _progressBar.Width = 500;
        _progressBar.Height = 18;
        _progressBar.ForeColor = GothBlood;
        _progressBar.BackColor = GothPanel;
        layout.Controls.Add(_progressBar);

        // --- Лог ---
        layout.Controls.Add(MakeLabel("Журнал:"));
        _logBox.Width = 500;
        _logBox.Height = 140;
        _logBox.Font = GothMonoFont;
        _logBox.BackColor = GothPanel;
        _logBox.ForeColor = GothText;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_logBox);

        root.Controls.Add(layout, 0, 0);
        root.Controls.Add(BuildConstructorPanel(), 1, 0);
        Controls.Add(root);

        FormClosing += OnFormClosing;
    }

    /// <summary>
    /// Строит правую панель «Конструктор замка»: выбор числа пластин,
    /// матрицу связей, стартовые позиции и кнопку поиска решения.
    /// </summary>
    private Control BuildConstructorPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = GothBackground,
            Margin = new Padding(16, 0, 0, 0)
        };

        // --- Заголовок ---
        var title = new Label
        {
            Text = "⚙ Конструктор замка ⚙",
            Font = GothTitleFont,
            ForeColor = GothGold,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var subtitle = new Label
        {
            Text = "— схема связей и поиск решения —",
            Font = GothHeadingFont,
            ForeColor = GothBlood,
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 12)
        };
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);

        // --- Число пластин ---
        panel.Controls.Add(MakeLabel("Количество пластин:"));
        _constPlateCountBox.Minimum = 2;
        _constPlateCountBox.Maximum = 10;
        _constPlateCountBox.Value = 5;
        _constPlateCountBox.Width = 120;
        StyleNumeric(_constPlateCountBox);
        _constPlateCountBox.ValueChanged += (_, _) => RebuildMatrix();
        panel.Controls.Add(_constPlateCountBox);

        // --- Матрица связей ---
        panel.Controls.Add(MakeLabel(
            "Связи: строка «тянет» столбец (+ синхронно, − инверсно, 0 нет):"));
        _matrixPanel.AutoSize = true;
        _matrixPanel.BackColor = GothPanel;
        _matrixPanel.Padding = new Padding(6);
        _matrixPanel.Margin = new Padding(0, 2, 0, 8);
        panel.Controls.Add(_matrixPanel);

        // --- Стартовые позиции (визуальные пластины, как в веб-версии) ---
        panel.Controls.Add(MakeLabel(
            "Стартовые позиции пластин (стрелками ◀ ▶, центр — цель):"));
        _startPanel.FlowDirection = FlowDirection.TopDown;
        _startPanel.AutoSize = true;
        _startPanel.WrapContents = false;
        _startPanel.BackColor = TrackBackdrop;
        _startPanel.Padding = new Padding(8);
        _startPanel.Margin = new Padding(0, 2, 0, 8);
        panel.Controls.Add(_startPanel);

        // --- Статус поиска решения ---
        // Решение ищется автоматически при любом изменении и сразу
        // отправляется в поле последовательности взлома (левая панель).
        _solveStatusLabel.Text = "";
        _solveStatusLabel.ForeColor = GothGold;
        _solveStatusLabel.Font = GothHeadingFont;
        _solveStatusLabel.AutoSize = true;
        _solveStatusLabel.Margin = new Padding(0, 4, 0, 8);
        panel.Controls.Add(_solveStatusLabel);

        RebuildMatrix();
        return panel;
    }

    /// <summary>
    /// Пересоздаёт сетку матрицы связей и поля стартовых позиций
    /// под текущее количество пластин.
    /// </summary>
    private void RebuildMatrix()
    {
        int n = (int)_constPlateCountBox.Value;

        // Подавляем пересчёт, пока матрица и пластины пересоздаются.
        _rebuilding = true;

        // --- Матрица ---
        _matrixPanel.SuspendLayout();
        _matrixPanel.Controls.Clear();
        _matrixPanel.ColumnStyles.Clear();
        _matrixPanel.RowStyles.Clear();
        _matrixPanel.ColumnCount = n + 1;
        _matrixPanel.RowCount = n + 1;

        _matrixCombos = new ComboBox[n, n];

        // Угловая ячейка.
        _matrixPanel.Controls.Add(MakeMatrixHeader("№"), 0, 0);

        // Заголовки столбцов.
        for (int j = 0; j < n; j++)
        {
            _matrixPanel.Controls.Add(MakeMatrixHeader(ToRoman(j + 1)), j + 1, 0);
        }

        for (int i = 0; i < n; i++)
        {
            // Заголовок строки.
            _matrixPanel.Controls.Add(MakeMatrixHeader(ToRoman(i + 1)), 0, i + 1);

            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    // Диагональ зафиксирована: пластина всегда двигает сама себя (+).
                    var fixedLabel = new Label
                    {
                        Text = "+",
                        AutoSize = false,
                        Width = 42,
                        Height = 24,
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = GothGold,
                        BackColor = GothPanelLight,
                        Margin = new Padding(1)
                    };
                    _matrixPanel.Controls.Add(fixedLabel, j + 1, i + 1);
                    continue;
                }

                var combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 42,
                    Margin = new Padding(1),
                    BackColor = GothPanel,
                    ForeColor = GothText,
                    FlatStyle = FlatStyle.Flat
                };
                combo.Items.AddRange(new object[] { "0", "+", "−" });
                combo.SelectedIndex = 0;
                combo.SelectedIndexChanged += (_, _) => Solve();
                _matrixCombos[i, j] = combo;
                _matrixPanel.Controls.Add(combo, j + 1, i + 1);
            }
        }

        _matrixPanel.ResumeLayout();

        // --- Стартовые позиции (визуальные пластины замка) ---
        _startPanel.SuspendLayout();

        // Освобождаем ресурсы старых пластин перед пересозданием.
        foreach (PlateRowControl old in _plates)
        {
            old.Dispose();
        }
        _startPanel.Controls.Clear();
        _plates = new PlateRowControl[n];

        // Рисуем пластины сверху вниз: I сверху, как в веб-версии.
        for (int i = 0; i < n; i++)
        {
            var plate = new PlateRowControl
            {
                PlateNumber = i + 1,
                Position = BreachSolver.Center,
                IsActive = i == 0
            };
            plate.PlateSelected += OnPlateSelected;
            plate.PositionChanged += (_, _) => Solve();

            _plates[i] = plate;
            _startPanel.Controls.Add(plate);
        }

        _startPanel.ResumeLayout();

        // Перестроение завершено — теперь пересчитываем решение.
        _rebuilding = false;
        Solve();
    }

    /// <summary>
    /// Делает выбранную пластину активной (золотая подсветка), снимая выделение с остальных.
    /// </summary>
    private void OnPlateSelected(object? sender, EventArgs e)
    {
        foreach (PlateRowControl plate in _plates)
        {
            plate.IsActive = ReferenceEquals(plate, sender);
        }
    }

    /// <summary>Создаёт стилизованную ячейку-заголовок матрицы.</summary>
    private static Label MakeMatrixHeader(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 42,
        Height = 24,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = GothGold,
        Font = GothHeadingFont,
        Margin = new Padding(1)
    };

    /// <summary>Преобразует число 1..10 в римскую запись для подписей.</summary>
    private static string ToRoman(int n)
    {
        string[] romans = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return n >= 1 && n <= romans.Length ? romans[n - 1] : n.ToString();
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 2),
        Font = GothHeadingFont,
        ForeColor = GothTextDim
    };

    /// <summary>
    /// Загружает иконку окна из встроенного ресурса. Имя ресурса формируется
    /// как "{RootNamespace}.{имя файла}", то есть "LockPicker.icon.ico".
    /// </summary>
    private void LoadWindowIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream("LockPicker.icon.ico");
        if (stream is null)
        {
            return;
        }

        Icon = new Icon(stream);
    }

    /// <summary>Применяет тёмное готическое оформление к текстовому полю.</summary>
    private static void StyleTextBox(TextBox box)
    {
        box.BackColor = GothPanel;
        box.ForeColor = GothText;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    /// <summary>Применяет тёмное готическое оформление к числовому полю.</summary>
    private static void StyleNumeric(NumericUpDown box)
    {
        box.BackColor = GothPanel;
        box.ForeColor = GothText;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    /// <summary>Делает кнопку плоской, с золотой рамкой и заданными цветами.</summary>
    private static void StyleButton(Button button, Color back, Color fore)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = back;
        button.ForeColor = fore;
        button.Font = GothButtonFont;
        button.Padding = new Padding(8, 4, 8, 4);
        button.FlatAppearance.BorderColor = GothGold;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = GothPanelLight;
        button.FlatAppearance.MouseDownBackColor = GothBackground;
    }

    // ===== Применение / сохранение настроек =====

    private void ApplySettingsToUi()
    {
        _windowTitleBox.Text = _settings.WindowTitle;
        _delayBox.Value = Math.Clamp(_settings.DelayMs, (int)_delayBox.Minimum, (int)_delayBox.Maximum);
        _sequenceBox.Text = string.IsNullOrEmpty(_settings.LastSequence)
            ? DefaultSequenceHint()
            : _settings.LastSequence;
    }

    private void CollectSettingsFromUi()
    {
        _settings.WindowTitle = _windowTitleBox.Text.Trim();
        _settings.DelayMs = (int)_delayBox.Value;
        _settings.LastSequence = _sequenceBox.Text;
    }

    private void RestoreWindowGeometry()
    {
        if (_settings.WindowMaximized)
        {
            // Сначала задаём нормальные границы (для корректного восстановления), затем максимизируем.
            if (_settings.HasWindowBounds)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = new Rectangle(
                    _settings.WindowX, _settings.WindowY,
                    _settings.WindowWidth, _settings.WindowHeight);
            }
            WindowState = FormWindowState.Maximized;
            return;
        }

        if (_settings.HasWindowBounds && IsBoundsOnScreen(
                new Rectangle(_settings.WindowX, _settings.WindowY,
                    _settings.WindowWidth, _settings.WindowHeight)))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(
                _settings.WindowX, _settings.WindowY,
                _settings.WindowWidth, _settings.WindowHeight);
        }
    }

    private void SaveWindowGeometry()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            _settings.WindowMaximized = true;
            // RestoreBounds хранит размеры в нормальном состоянии.
            _settings.WindowX = RestoreBounds.X;
            _settings.WindowY = RestoreBounds.Y;
            _settings.WindowWidth = RestoreBounds.Width;
            _settings.WindowHeight = RestoreBounds.Height;
            return;
        }

        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowMaximized = false;
            _settings.WindowX = Bounds.X;
            _settings.WindowY = Bounds.Y;
            _settings.WindowWidth = Bounds.Width;
            _settings.WindowHeight = Bounds.Height;
        }
    }

    private static bool IsBoundsOnScreen(Rectangle bounds)
    {
        // Проверяем, что окно хотя бы частично попадает на один из экранов.
        foreach (Screen screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(bounds))
            {
                return true;
            }
        }
        return false;
    }

    private static string DefaultSequenceHint() =>
        "Шаг 1: Пластина I ➔ Вправо ▶" + Environment.NewLine +
        "Шаг 2: Пластина II ➔ Вправо ▶" + Environment.NewLine +
        "Шаг 3: Пластина III ➔ Влево ◀";

    // ===== Обработчики =====

    // Подавляет автоматический пересчёт во время перестроения интерфейса конструктора.
    private bool _rebuilding;

    /// <summary>
    /// Собирает матрицу связей и стартовые позиции из конструктора, запускает
    /// BFS-решатель и сразу отправляет найденную последовательность шагов
    /// в поле последовательности взлома. Вызывается при любом изменении конструктора.
    /// </summary>
    private void Solve()
    {
        // Во время перестроения UI поля ещё не согласованы — пропускаем.
        if (_rebuilding || _isRunning)
        {
            return;
        }

        int n = (int)_constPlateCountBox.Value;
        if (_plates.Length != n)
        {
            return; // пластины ещё не пересозданы
        }

        // Собираем матрицу связей из комбобоксов.
        var matrix = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    matrix[i, j] = 1; // диагональ всегда +1
                    continue;
                }

                matrix[i, j] = _matrixCombos[i, j].SelectedItem switch
                {
                    "+" => 1,
                    "−" => -1,
                    _ => 0
                };
            }
        }

        // Собираем стартовые позиции из визуальных пластин.
        var start = new int[n];
        for (int i = 0; i < n; i++)
        {
            start[i] = _plates[i].Position;
        }

        var solver = new BreachSolver(n, matrix);
        List<LockStep> steps = solver.GetSolutionSteps(start);

        if (steps.Count == 0)
        {
            // Различаем «уже открыт» и «неразрешим».
            bool alreadySolved = Array.TrueForAll(start, p => p == BreachSolver.Center);
            if (alreadySolved)
            {
                _solveStatusLabel.Text = "✔ Замок уже открыт (все по центру)";
                _solveStatusLabel.ForeColor = GothGold;
                _sequenceBox.Text = string.Empty;
            }
            else
            {
                _solveStatusLabel.Text = "✖ Решение не найдено (замок неразрешим)";
                _solveStatusLabel.ForeColor = GothBlood;
            }

            return;
        }

        // Формируем читаемую последовательность шагов.
        var lines = new List<string>();
        int totalMoves = 0;
        for (int idx = 0; idx < steps.Count; idx++)
        {
            LockStep s = steps[idx];
            totalMoves += s.RepeatCount;
            lines.Add($"Шаг {idx + 1}: {s}");
        }

        // Сразу отправляем решение в последовательность взлома (левая панель).
        _sequenceBox.Text = string.Join(Environment.NewLine, lines);
        _startPlateBox.Value = 1; // решение всегда начинается с фокуса на первой пластине

        _solveStatusLabel.Text = $"✔ Найдено шагов: {steps.Count} (ходов: {totalMoves})";
        _solveStatusLabel.ForeColor = GothGold;
    }

    private void OnFindWindowClick(object? sender, EventArgs e)
    {
        string title = _windowTitleBox.Text.Trim();
        if (title.Length == 0)
        {
            Log("Укажите заголовок окна игры.");
            return;
        }

        _gameWindow = NativeMethods.FindWindowByTitleContains(title);
        if (_gameWindow == IntPtr.Zero)
        {
            _windowStatusLabel.Text = "Окно не найдено";
            _windowStatusLabel.ForeColor = GothBlood;
            Log($"Окно с заголовком, содержащим \"{title}\", не найдено.");
            return;
        }

        _windowStatusLabel.Text = $"Окно найдено (handle: {_gameWindow})";
        _windowStatusLabel.ForeColor = GothGold;
        Log($"Окно игры найдено: {_gameWindow}");
    }

    private async void OnStartClick(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        // Парсим последовательность.
        List<LockStep> steps = SequenceParser.Parse(_sequenceBox.Text);
        if (steps.Count == 0)
        {
            Log("Не удалось распознать ни одного шага. Проверьте формат последовательности.");
            return;
        }

        // Ищем окно, если ещё не найдено.
        if (_gameWindow == IntPtr.Zero)
        {
            _gameWindow = NativeMethods.FindWindowByTitleContains(_windowTitleBox.Text.Trim());
        }

        if (_gameWindow == IntPtr.Zero)
        {
            Log("Окно игры не найдено. Нажмите «Найти окно» и убедитесь, что игра запущена.");
            return;
        }

        CollectSettingsFromUi();
        _settings.Save();

        _logBox.Items.Clear();
        Log($"Распознано шагов: {steps.Count}");
        foreach (LockStep s in steps)
        {
            Log("  • " + s);
        }

        SetRunningState(true);
        _cts = new CancellationTokenSource();

        try
        {
            // Обратный отсчёт, чтобы успеть переключиться в игру.
            int countdown = (int)_countdownBox.Value;
            for (int sec = countdown; sec > 0; sec--)
            {
                _cts.Token.ThrowIfCancellationRequested();
                Log($"Старт через {sec}...");
                await Task.Delay(1000, _cts.Token);
            }

            var engine = new LockPickEngine(
                _gameWindow, (int)_delayBox.Value, (int)_startPlateBox.Value);
            engine.Progress += OnEngineProgress;

            _progressBar.Value = 0;
            _progressBar.Maximum = steps.Count;

            await engine.RunAsync(steps, _cts.Token);

            Log("Готово.");
        }
        catch (OperationCanceledException)
        {
            Log("Остановлено пользователем.");
        }
        catch (Exception ex)
        {
            Log("Ошибка: " + ex.Message);
        }
        finally
        {
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnStopClick(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        Log("Запрошена остановка...");
    }

    private void OnEngineProgress(object? sender, ProgressEventArgs e)
    {
        // Событие может прийти из фонового потока — маршалим в UI-поток.
        if (InvokeRequired)
        {
            BeginInvoke(() => OnEngineProgress(sender, e));
            return;
        }

        Log(e.Message);
        if (e.TotalSteps > 0)
        {
            _progressBar.Maximum = e.TotalSteps;
            _progressBar.Value = Math.Clamp(e.StepNumber, 0, e.TotalSteps);
        }
    }

    private void SetRunningState(bool running)
    {
        _isRunning = running;
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _windowTitleBox.Enabled = !running;
        _delayBox.Enabled = !running;
        _sequenceBox.Enabled = !running;
        _startPlateBox.Enabled = !running;
        _countdownBox.Enabled = !running;
        _findWindowButton.Enabled = !running;
    }

    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logBox.Items.Add(line);
        _logBox.TopIndex = _logBox.Items.Count - 1;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _cts?.Cancel();
        CollectSettingsFromUi();
        SaveWindowGeometry();
        _settings.Save();
    }
}
