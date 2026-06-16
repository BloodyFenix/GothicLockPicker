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
        MinimumSize = new Size(560, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = GothBodyFont;
        BackColor = GothBackground;
        ForeColor = GothText;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 1,
            AutoScroll = true,
            BackColor = GothBackground
        };
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

        root.Controls.Add(layout);
        Controls.Add(root);

        FormClosing += OnFormClosing;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 2),
        Font = GothHeadingFont,
        ForeColor = GothTextDim
    };

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
