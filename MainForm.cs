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
    // Количество пластин выбирается рядом взаимоисключающих кнопок 4..7.
    private const int MinPlateCount = 4;
    private const int MaxPlateCount = 7;
    private int _plateCount = 5;
    private Button[] _plateCountButtons = Array.Empty<Button>();
    private readonly FlowLayoutPanel _startPanel = new();
    private readonly Label _solveStatusLabel = new();

    // Матрица связей (в памяти) и визуальные пластины стартовых позиций.
    // _matrix[i, j]: пластина i «тянет» пластину j (+1 синхронно, −1 инверсно, 0 нет).
    private int[,] _matrix = new int[0, 0];

    // Индекс выбранной (золотой) пластины — её строку матрицы редактируют кнопки связи.
    private int _selectedPlate;

    private PlateRowControl[] _plates = Array.Empty<PlateRowControl>();

    private IntPtr _gameWindow = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // Отмена предыдущего (возможно ещё идущего) асинхронного поиска решения.
    private CancellationTokenSource? _solveCts;

    // Таймер анимации статуса «Идёт поиск решения…» во время фонового расчёта.
    private readonly System.Windows.Forms.Timer _solveAnimTimer = new() { Interval = 250 };
    private int _solveAnimFrame;

    public MainForm()
    {
        _settings = AppSettings.Load();
        BuildUi();
        ApplySettingsToUi();
        RestoreWindowGeometry();
        _solveAnimTimer.Tick += OnSolveAnimTick;
        // При показе окна снимаем фокус с поля заголовка (иначе в нём мигает каретка).
        Shown += (_, _) => ActiveControl = null;
    }

    // ===== Построение интерфейса =====

    private void BuildUi()
    {
        Text = "Gothic LockPicker — взлом замков";
        MinimumSize = new Size(932, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = GothBodyFont;
        // Перехватываем клавиатуру на уровне формы для горячих клавиш конструктора.
        KeyPreview = true;
        KeyDown += OnConstructorKeyDown;
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 432));
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
        _windowTitleBox.Width = 400;
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

        // --- Числовые настройки в один ряд: задержка, стартовая пластина, отсчёт ---
        _delayBox.Minimum = 0;
        _delayBox.Maximum = 60000;
        _delayBox.Increment = 50;
        _delayBox.Width = 80;
        StyleNumeric(_delayBox);

        _startPlateBox.Minimum = 1;
        _startPlateBox.Maximum = 20;
        _startPlateBox.Value = 1;
        _startPlateBox.Width = 80;
        StyleNumeric(_startPlateBox);

        _countdownBox.Minimum = 0;
        _countdownBox.Maximum = 30;
        _countdownBox.Value = 3;
        _countdownBox.Width = 80;
        StyleNumeric(_countdownBox);

        var numericRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 4),
            BackColor = GothBackground
        };
        numericRow.Controls.Add(MakeNumericColumn("Задержка (мс):", _delayBox));
        numericRow.Controls.Add(MakeNumericColumn("Стартовая пластина:", _startPlateBox));
        numericRow.Controls.Add(MakeNumericColumn("Отсчёт (сек):", _countdownBox));
        layout.Controls.Add(numericRow);

        // --- Последовательность ---
        // Заголовок поля одновременно служит статусом поиска решения
        // («Идёт поиск…», «Найдено шагов…», «Замок уже открыт» и т. п.).
        _solveStatusLabel.Text = "Последовательность взлома:";
        _solveStatusLabel.ForeColor = GothTextDim;
        _solveStatusLabel.Font = GothHeadingFont;
        _solveStatusLabel.AutoSize = true;
        _solveStatusLabel.Margin = new Padding(0, 8, 0, 2);
        layout.Controls.Add(_solveStatusLabel);
        _sequenceBox.Multiline = true;
        _sequenceBox.ScrollBars = ScrollBars.Vertical;
        _sequenceBox.Width = 400;
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
        _progressBar.Width = 400;
        _progressBar.Height = 18;
        _progressBar.ForeColor = GothBlood;
        _progressBar.BackColor = GothPanel;
        layout.Controls.Add(_progressBar);

        // --- Лог ---
        layout.Controls.Add(MakeLabel("Журнал:"));
        _logBox.Width = 400;
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

        // --- Число пластин (ряд взаимоисключающих кнопок 4..7) ---
        panel.Controls.Add(MakeLabel("Количество пластин:"));
        var countRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 8),
            BackColor = GothBackground
        };

        _plateCountButtons = new Button[MaxPlateCount - MinPlateCount + 1];
        for (int count = MinPlateCount; count <= MaxPlateCount; count++)
        {
            int value = count; // фиксируем для замыкания
            var button = new Button
            {
                Text = value.ToString(),
                AutoSize = false,
                Width = 44,
                Height = 32,
                Margin = new Padding(0, 0, 6, 0)
            };
            StyleButton(button, GothPanelLight, GothGold);
            // Шрифт Constantia даёт неровные метрики цифр — берём обычный
            // моноширинный для одинакового центрирования всех чисел.
            button.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            button.Padding = new Padding(0);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Click += (_, _) => SetPlateCount(value);
            _plateCountButtons[value - MinPlateCount] = button;
            countRow.Controls.Add(button);
        }
        panel.Controls.Add(countRow);
        UpdatePlateCountButtons();

        // --- Стартовые позиции (визуальные пластины, как в веб-версии) ---
        // Разметка связей теперь прямо на пластинах: над номером — кнопки ·/+/−,
        // которые задают, как ВЫБРАННАЯ (золотая) пластина влияет на каждую.
        panel.Controls.Add(MakeLabel(
            "Пластины: позиция — стрелками ◀ ▶; кнопки ·/+/− над номером —"));
        panel.Controls.Add(MakeLabel(
            "Клавиши: W/S — выбор пластины, A/D — сдвиг, 1–7 — связь (·/+/−)."));
        _startPanel.FlowDirection = FlowDirection.TopDown;
        _startPanel.AutoSize = true;
        _startPanel.WrapContents = false;
        _startPanel.BackColor = TrackBackdrop;
        _startPanel.Padding = new Padding(8);
        _startPanel.Margin = new Padding(0, 2, 0, 8);
        panel.Controls.Add(_startPanel);

        // Статус поиска решения отображается в заголовке поля
        // «Последовательность взлома» (левая панель), отдельной метки нет.

        RebuildMatrix();
        return panel;
    }

    /// <summary>
    /// Устанавливает количество пластин и перестраивает конструктор.
    /// Вызывается из ряда кнопок выбора количества.
    /// </summary>
    private void SetPlateCount(int count)
    {
        int clamped = Math.Clamp(count, MinPlateCount, MaxPlateCount);
        if (clamped == _plateCount && _plates.Length == clamped)
        {
            return;
        }

        _plateCount = clamped;
        UpdatePlateCountButtons();
        RebuildMatrix();
    }

    /// <summary>
    /// Подсвечивает кнопку текущего количества пластин (золотой фон),
    /// остальные оставляет в обычном стиле.
    /// </summary>
    private void UpdatePlateCountButtons()
    {
        for (int i = 0; i < _plateCountButtons.Length; i++)
        {
            bool selected = (MinPlateCount + i) == _plateCount;
            Button b = _plateCountButtons[i];
            b.BackColor = selected ? GothGold : GothPanelLight;
            b.ForeColor = selected ? GothBackground : GothGold;
        }
    }

    /// <summary>
    /// Пересоздаёт сетку матрицы связей и поля стартовых позиций
    /// под текущее количество пластин.
    /// </summary>
    private void RebuildMatrix()
    {
        int n = _plateCount;

        // Подавляем пересчёт, пока матрица и пластины пересоздаются.
        _rebuilding = true;

        // --- Матрица связей (в памяти) ---
        // Диагональ всегда +1 (пластина двигает сама себя), остальное — 0.
        _matrix = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            _matrix[i, i] = 1;
        }
        _selectedPlate = 0;

        // --- Стартовые позиции (визуальные пластины замка) ---
        _startPanel.SuspendLayout();

        // Освобождаем ресурсы старых пластин перед пересозданием.
        foreach (PlateRowControl old in _plates)
        {
            old.Dispose();
        }
        _startPanel.Controls.Clear();
        _plates = new PlateRowControl[n];

        // Создаём пластины по логическому индексу (0 — первая пластина),
        // чтобы Solve() корректно читал _plates[i].Position.
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
            plate.RelationChanged += OnRelationChanged;

            _plates[i] = plate;
        }

        // Добавляем в панель в обратном порядке: пластина 1 снизу,
        // максимальная — сверху (панель имеет FlowDirection.TopDown).
        // Между соседними пластинами вставляем тонкий разделитель.
        for (int i = n - 1; i >= 0; i--)
        {
            _startPanel.Controls.Add(_plates[i]);
            if (i > 0)
            {
                _startPanel.Controls.Add(MakePlateSeparator());
            }
        }

        _startPanel.ResumeLayout();

        // Отображаем строку матрицы выбранной пластины на кнопках связи
        // и индикаторы исходящих связей над каждой пластиной.
        RefreshRelationButtons();
        RefreshOutgoingRelations();

        // Перестроение завершено — теперь пересчитываем решение.
        _rebuilding = false;
        Solve();
    }

    /// <summary>
    /// Делает выбранную пластину активной (золотая подсветка), снимая выделение с остальных,
    /// и переносит кнопки связи на её строку матрицы.
    /// </summary>
    /// <summary>
    /// Горячие клавиши конструктора замка:
    /// W/S — выбрать предыдущую/следующую пластину;
    /// A/D — сдвинуть выбранную пластину влево/вправо;
    /// 1..7 — переключить связь соответствующей пластины с выбранной (нет → + → −).
    /// </summary>
    private void OnConstructorKeyDown(object? sender, KeyEventArgs e)
    {
        // Не перехватываем клавиши, когда пользователь печатает в полях ввода.
        if (ActiveControl is TextBoxBase or NumericUpDown)
        {
            return;
        }

        // Конструктор ещё не готов или идёт прогон автомата — игнорируем.
        if (_rebuilding || _isRunning || _plates.Length == 0)
        {
            return;
        }

        int n = _plates.Length;

        switch (e.KeyCode)
        {
            // W/↑ — к пластине с большим номером (визуально вверх).
            case Keys.W:
            case Keys.Up:
                SelectPlate(Math.Min(_selectedPlate + 1, n - 1));
                break;

            // S/↓ — к пластине с меньшим номером (визуально вниз).
            case Keys.S:
            case Keys.Down:
                SelectPlate(Math.Max(_selectedPlate - 1, 0));
                break;

            // A/← — сдвиг выбранной пластины влево.
            case Keys.A:
            case Keys.Left:
                ShiftSelectedPlate(-1);
                break;

            // D/→ — сдвиг выбранной пластины вправо.
            case Keys.D:
            case Keys.Right:
                ShiftSelectedPlate(+1);
                break;

            default:
                int target = KeyToPlateIndex(e.KeyCode);
                if (target < 0 || target >= n)
                {
                    return; // не наша клавиша — не подавляем её
                }

                ToggleRelation(target);
                break;
        }

        // Подавляем дальнейшую обработку (звук «динь», навигацию фокуса и т. п.).
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>Преобразует клавишу цифры (1..7, основная или NumPad) в индекс пластины 0-based.</summary>
    private static int KeyToPlateIndex(Keys key) => key switch
    {
        >= Keys.D1 and <= Keys.D7 => key - Keys.D1,
        >= Keys.NumPad1 and <= Keys.NumPad7 => key - Keys.NumPad1,
        _ => -1
    };

    /// <summary>Делает выбранной пластину с индексом index, обновляя подсветку и кнопки связи.</summary>
    private void SelectPlate(int index)
    {
        if (index == _selectedPlate)
        {
            return;
        }

        _selectedPlate = index;
        for (int i = 0; i < _plates.Length; i++)
        {
            _plates[i].IsActive = i == index;
        }

        RefreshRelationButtons();
    }

    /// <summary>Сдвигает выбранную пластину на delta позиций (срабатывает PositionChanged → Solve()).</summary>
    private void ShiftSelectedPlate(int delta)
    {
        PlateRowControl plate = _plates[_selectedPlate];
        plate.Position = Math.Clamp(plate.Position + delta, BreachSolver.MinPos, BreachSolver.MaxPos);
    }

    /// <summary>
    /// Циклически переключает связь пластины target с выбранной: нет (0) → + (1) → − (−1) → нет.
    /// Связь пластины самой с собой (диагональ) не редактируется.
    /// </summary>
    private void ToggleRelation(int target)
    {
        if (target == _selectedPlate)
        {
            return; // диагональ заблокирована
        }

        int current = _matrix[_selectedPlate, target];
        int next = current switch
        {
            0 => 1,
            1 => -1,
            _ => 0
        };

        _matrix[_selectedPlate, target] = next;
        _plates[target].RelationValue = next;
        RefreshOutgoingRelations();
        Solve();
    }

    private void OnPlateSelected(object? sender, EventArgs e)
    {
        for (int i = 0; i < _plates.Length; i++)
        {
            bool selected = ReferenceEquals(_plates[i], sender);
            _plates[i].IsActive = selected;
            if (selected)
            {
                _selectedPlate = i;
            }
        }

        RefreshRelationButtons();
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки связи (·/+/−) на пластине: записывает значение
    /// в строку матрицы выбранной пластины и запускает пересчёт решения.
    /// </summary>
    private void OnRelationChanged(object? sender, int value)
    {
        if (sender is not PlateRowControl plate)
        {
            return;
        }

        int target = Array.IndexOf(_plates, plate);
        if (target < 0 || target == _selectedPlate)
        {
            return; // диагональ не редактируется
        }

        _matrix[_selectedPlate, target] = value;
        plate.RelationValue = value;
        RefreshOutgoingRelations();
        Solve();
    }

    /// <summary>
    /// Обновляет индикаторы исходящих связей над каждой пластиной из матрицы:
    /// для пластины i собираются все её ненулевые связи (i → j, кроме диагонали).
    /// </summary>
    private void RefreshOutgoingRelations()
    {
        int n = _plates.Length;
        for (int i = 0; i < n; i++)
        {
            var list = new List<(int Target, int Sign)>();
            for (int j = 0; j < n; j++)
            {
                if (i == j || _matrix[i, j] == 0)
                {
                    continue;
                }

                list.Add((j + 1, _matrix[i, j]));
            }

            _plates[i].SetOutgoing(list.ToArray());
        }
    }

    /// <summary>
    /// Обновляет кнопки связи на всех пластинах под строку матрицы выбранной
    /// пластины: для самой выбранной — заблокированный «+», для остальных — значение связи.
    /// </summary>
    private void RefreshRelationButtons()
    {
        for (int j = 0; j < _plates.Length; j++)
        {
            if (j == _selectedPlate)
            {
                _plates[j].RelationLocked = true;
                _plates[j].RelationValue = 1; // диагональ всегда +
                continue;
            }

            _plates[j].RelationLocked = false;
            _plates[j].RelationValue = _matrix[_selectedPlate, j];
        }
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
    /// Создаёт вертикальную колонку «подпись над числовым полем» для размещения
    /// нескольких числовых настроек в одном горизонтальном ряду.
    /// </summary>
    private static Control MakeNumericColumn(string caption, NumericUpDown box)
    {
        var column = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = GothBackground
        };
        column.Controls.Add(MakeLabel(caption));
        column.Controls.Add(box);
        return column;
    }

    /// <summary>
    /// Создаёт тонкий горизонтальный разделитель между визуальными пластинами.
    /// Ширину берём от самой пластины, так как панель имеет AutoSize.
    /// </summary>
    private Control MakePlateSeparator() => new Panel
    {
        Height = 1,
        Width = _plates.Length > 0 ? _plates[0].Width : 200,
        BackColor = GothGold,
        Margin = new Padding(0, 4, 0, 4)
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
    private async void Solve()
    {
        // Во время перестроения UI поля ещё не согласованы — пропускаем.
        if (_rebuilding || _isRunning)
        {
            return;
        }

        int n = _plateCount;
        if (_plates.Length != n)
        {
            return; // пластины ещё не пересозданы
        }

        // Матрица связей хранится в памяти (_matrix) и редактируется кнопками
        // ·/+/− на пластинах. Проверяем согласованность размеров.
        if (_matrix.GetLength(0) != n)
        {
            return;
        }
        int[,] matrix = (int[,])_matrix.Clone();

        // Собираем стартовые позиции из визуальных пластин.
        var start = new int[n];
        for (int i = 0; i < n; i++)
        {
            start[i] = _plates[i].Position;
        }

        // Замок уже открыт — решать нечего, показываем сразу (без фонового расчёта).
        if (Array.TrueForAll(start, p => p == BreachSolver.Center))
        {
            StopSolveAnimation();
            CancelPendingSolve();
            _solveStatusLabel.Text = "Последовательность взлома: ✔ замок уже открыт";
            _solveStatusLabel.ForeColor = GothGold;
            _sequenceBox.Text = string.Empty;
            return;
        }

        // Отменяем предыдущий ещё идущий расчёт и запускаем новый.
        CancelPendingSolve();
        var cts = new CancellationTokenSource();
        _solveCts = cts;
        CancellationToken token = cts.Token;

        // Запускаем анимацию статуса «Идёт поиск решения…».
        StartSolveAnimation();

        // Взвешенный решатель учитывает перемещение фокуса между пластинами,
        // поэтому расчёт может быть долгим — выполняем его в фоне, не блокируя UI.
        // Фокус на старте — первая пластина (как выставляется в UI: _startPlateBox.Value = 1).
        List<LockStep> steps;
        try
        {
            steps = await Task.Run(() =>
            {
                var solver = new BreachSolver(n, matrix);
                return solver.GetSolutionStepsWeighted(start, 0, token);
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Пришёл новый расчёт — этот результат больше не актуален, молча выходим.
            return;
        }
        catch (Exception ex)
        {
            // Если этот расчёт уже устарел — игнорируем его ошибку.
            if (!ReferenceEquals(_solveCts, cts))
            {
                return;
            }

            StopSolveAnimation();
            _solveStatusLabel.Text = "Последовательность взлома: ✖ ошибка — " + ex.Message;
            _solveStatusLabel.ForeColor = GothBlood;
            return;
        }

        // Пока считали, мог стартовать более новый расчёт — тогда наш результат устарел.
        if (!ReferenceEquals(_solveCts, cts))
        {
            return;
        }

        _solveCts = null;
        StopSolveAnimation();

        if (steps.Count == 0)
        {
            _solveStatusLabel.Text = "Последовательность взлома: ✖ решение не найдено";
            _solveStatusLabel.ForeColor = GothBlood;
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

        _solveStatusLabel.Text =
            $"Последовательность взлома: ✔ шагов {steps.Count}, ходов {totalMoves}";
        _solveStatusLabel.ForeColor = GothGold;
    }

    /// <summary>Отменяет предыдущий незавершённый фоновый расчёт решения, если он есть.</summary>
    private void CancelPendingSolve()
    {
        if (_solveCts is null)
        {
            return;
        }

        _solveCts.Cancel();
        _solveCts.Dispose();
        _solveCts = null;
    }

    /// <summary>Запускает анимацию статуса на время фонового поиска решения.</summary>
    private void StartSolveAnimation()
    {
        _solveAnimFrame = 0;
        _solveStatusLabel.ForeColor = GothTextDim;
        _solveStatusLabel.Text = "Последовательность взлома: ⏳ идёт поиск решения";
        _solveAnimTimer.Start();
    }

    /// <summary>Останавливает анимацию статуса поиска решения.</summary>
    private void StopSolveAnimation()
    {
        _solveAnimTimer.Stop();
    }

    /// <summary>Кадр анимации: дописывает к статусу бегущие точки «.», «..», «...».</summary>
    private void OnSolveAnimTick(object? sender, EventArgs e)
    {
        _solveAnimFrame = (_solveAnimFrame + 1) % 4;
        _solveStatusLabel.Text =
            "Последовательность взлома: ⏳ идёт поиск решения" + new string('.', _solveAnimFrame);
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
        _solveCts?.Cancel();
        _solveAnimTimer.Stop();
        CollectSettingsFromUi();
        SaveWindowGeometry();
        _settings.Save();
    }
}
