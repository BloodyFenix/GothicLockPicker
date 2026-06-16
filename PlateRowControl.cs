using System.ComponentModel;

namespace LockPicker;

/// <summary>
/// Визуальная пластина замка в стиле веб-версии «Сундуки Готики».
/// Рисует горизонтальную металлическую пластину с 7 слотами-отверстиями,
/// золотым штырьком в выровненном слоте и стрелками ◀ ▶ для сдвига.
///
/// Позиция <see cref="Position"/> — это индекс 0..6. Цель — центр (3).
/// Слот, выровненный с пазом, определяется формулой «6 − позиция» (как в оригинале).
/// </summary>
public sealed class PlateRowControl : Control
{
    // ===== Геометрия (подобрана под ширину панели конструктора) =====
    private const int SlotCount = 7;
    private const int SlotDiameter = 18;
    private const int SlotGap = 14;
    private const int SlotSpacing = SlotDiameter + SlotGap; // шаг между слотами = 32px
    private const int SlotsWidth = SlotCount * SlotDiameter + (SlotCount - 1) * SlotGap; // 210px
    private const int PlatePadding = 16; // отступ слотов внутри пластины
    private const int PlateWidth = SlotsWidth + PlatePadding * 2;
    private const int TravelPerStep = SlotSpacing;
    private const int MaxTravel = 3 * TravelPerStep; // максимальный сдвиг от центра (±96px)
    private const int LabelWidth = 40;
    private const int ArrowWidth = 28;
    private const int TrackHeight = 46;
    private const int PlateHeight = 40;

    // ===== Готическая палитра (синхронизирована с MainForm) =====
    private static readonly Color GothText = Color.FromArgb(200, 184, 150);
    private static readonly Color GothTextDim = Color.FromArgb(140, 128, 108);
    private static readonly Color GothGold = Color.FromArgb(168, 138, 78);
    private static readonly Color GothGoldBright = Color.FromArgb(229, 193, 88);
    private static readonly Color TrackBack = Color.FromArgb(12, 11, 14);
    private static readonly Color SteelLight = Color.FromArgb(96, 92, 100);
    private static readonly Color SteelMid = Color.FromArgb(66, 62, 70);
    private static readonly Color SteelDark = Color.FromArgb(40, 37, 44);
    private static readonly Color SlotHole = Color.FromArgb(13, 13, 15);

    private int _position = BreachSolver.Center;
    private bool _isActive;

    // ===== Состояние перетаскивания (drag-and-drop) =====
    private bool _dragging;
    private int _dragStartMouseX;     // X курсора в начале перетаскивания
    private int _dragStartPosition;   // позиция пластины в начале перетаскивания
    private int _dragVisualOffsetPx;  // визуальное смещение пластины во время drag

    private static readonly string[] Romans =
        { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    /// <summary>Возникает при изменении позиции пластины пользователем.</summary>
    public event EventHandler? PositionChanged;

    /// <summary>Возникает при щелчке по пластине (для выбора активной).</summary>
    public event EventHandler? PlateSelected;

    public PlateRowControl()
    {
        DoubleBuffered = true;
        Height = TrackHeight + 8;
        Width = LabelWidth + ArrowWidth * 2 + PlateWidth + MaxTravel * 2 + 12;
        Margin = new Padding(0, 3, 0, 3);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    /// <summary>Порядковый номер пластины (1-based) для подписи римской цифрой.</summary>
    [Browsable(false)]
    public int PlateNumber { get; set; } = 1;

    /// <summary>Текущая позиция пластины 0..6 (центр = 3).</summary>
    [Browsable(false)]
    public int Position
    {
        get => _position;
        set
        {
            int clamped = Math.Clamp(value, BreachSolver.MinPos, BreachSolver.MaxPos);
            if (clamped == _position)
            {
                return;
            }

            _position = clamped;
            Invalidate();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Подсвечена ли пластина как активная (золотая рамка).</summary>
    [Browsable(false)]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Invalidate();
        }
    }

    // ===== Прямоугольники зон (вычисляются от текущей ширины) =====

    private Rectangle TrackRect()
    {
        int trackWidth = PlateWidth + MaxTravel * 2;
        int x = LabelWidth + ArrowWidth;
        int y = (Height - TrackHeight) / 2;
        return new Rectangle(x, y, trackWidth, TrackHeight);
    }

    private Rectangle LeftArrowRect()
    {
        Rectangle track = TrackRect();
        return new Rectangle(LabelWidth, track.Y, ArrowWidth, track.Height);
    }

    private Rectangle RightArrowRect()
    {
        Rectangle track = TrackRect();
        return new Rectangle(track.Right, track.Y, ArrowWidth, track.Height);
    }

    /// <summary>Текущий прямоугольник пластины с учётом позиции и drag-смещения.</summary>
    private Rectangle PlateRect()
    {
        Rectangle track = TrackRect();
        int offset = (_position - BreachSolver.Center) * TravelPerStep + _dragVisualOffsetPx;
        int plateX = track.X + (track.Width - PlateWidth) / 2 + offset;
        int plateY = track.Y + (track.Height - PlateHeight) / 2;
        return new Rectangle(plateX, plateY, PlateWidth, PlateHeight);
    }

    // ===== Взаимодействие мышью (стрелки + перетаскивание) =====

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Выбор активной пластины при любом нажатии.
        PlateSelected?.Invoke(this, EventArgs.Empty);

        // Клик по стрелкам — пошаговый сдвиг (как раньше).
        if (LeftArrowRect().Contains(e.Location))
        {
            Position--;
            return;
        }

        if (RightArrowRect().Contains(e.Location))
        {
            Position++;
            return;
        }

        // Нажатие на пластину — начинаем перетаскивание.
        if (PlateRect().Contains(e.Location))
        {
            _dragging = true;
            _dragStartMouseX = e.X;
            _dragStartPosition = _position;
            _dragVisualOffsetPx = 0;
            Capture = true;
            Cursor = Cursors.SizeWE;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
        {
            // Над пластиной показываем курсор «можно тащить».
            Cursor = PlateRect().Contains(e.Location) ? Cursors.SizeWE : Cursors.Default;
            return;
        }

        // Визуальное смещение ограничиваем допустимым ходом пластины.
        int deltaX = e.X - _dragStartMouseX;
        int minOffset = (BreachSolver.MinPos - _dragStartPosition) * TravelPerStep;
        int maxOffset = (BreachSolver.MaxPos - _dragStartPosition) * TravelPerStep;
        _dragVisualOffsetPx = Math.Clamp(deltaX, minOffset, maxOffset);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Capture = false;
        Cursor = PlateRect().Contains(e.Location) ? Cursors.SizeWE : Cursors.Default;

        // Привязка к ближайшему слоту: округляем смещение до шага.
        int steps = (int)Math.Round((double)_dragVisualOffsetPx / TravelPerStep);
        _dragVisualOffsetPx = 0;
        Position = _dragStartPosition + steps;
        Invalidate();
    }

    // ===== Отрисовка =====

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawLabel(g);
        DrawArrows(g);
        DrawTrackAndPlate(g);
    }

    private void DrawLabel(Graphics g)
    {
        string roman = PlateNumber >= 1 && PlateNumber <= Romans.Length
            ? Romans[PlateNumber - 1]
            : PlateNumber.ToString();

        using var font = new Font("Constantia", 11F, FontStyle.Bold);
        Color color = _isActive ? GothGoldBright : GothTextDim;
        var rect = new Rectangle(0, 0, LabelWidth, Height);
        TextRenderer.DrawText(g, roman, font, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void DrawArrows(Graphics g)
    {
        using var font = new Font("Segoe UI", 11F, FontStyle.Bold);
        TextRenderer.DrawText(g, "◀", font, LeftArrowRect(), GothGold,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "▶", font, RightArrowRect(), GothGold,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void DrawTrackAndPlate(Graphics g)
    {
        Rectangle track = TrackRect();

        // Углублённый трек (фон-направляющая).
        using (var trackBrush = new SolidBrush(TrackBack))
        {
            g.FillRectangle(trackBrush, track);
        }
        using (var trackPen = new Pen(Color.FromArgb(60, 0, 0, 0)))
        {
            g.DrawRectangle(trackPen, track);
        }

        // Положение пластины (с учётом текущего drag-смещения).
        Rectangle plateRect = PlateRect();

        // Металлическая пластина с вертикальным градиентом.
        using (var plateBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            plateRect, SteelLight, SteelDark, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
        {
            var blend = new System.Drawing.Drawing2D.ColorBlend
            {
                Colors = new[] { SteelLight, SteelMid, SteelDark },
                Positions = new[] { 0f, 0.4f, 1f }
            };
            plateBrush.InterpolationColors = blend;
            g.FillRectangle(plateBrush, plateRect);
        }

        // Рамка пластины: золотая, если активна.
        Color borderColor = _isActive ? GothGoldBright : Color.FromArgb(80, 255, 255, 255);
        using (var border = new Pen(borderColor, _isActive ? 2f : 1f))
        {
            g.DrawRectangle(border, plateRect);
        }

        DrawSlots(g, plateRect);
    }

    private void DrawSlots(Graphics g, Rectangle plateRect)
    {
        int slotsStartX = plateRect.X + (plateRect.Width - SlotsWidth) / 2;
        int slotY = plateRect.Y + (plateRect.Height - SlotDiameter) / 2;

        // Эффективная позиция: во время перетаскивания — ближайший слот
        // к текущему смещению, иначе — зафиксированная позиция.
        int effectivePosition = _position;
        if (_dragging)
        {
            int steps = (int)Math.Round((double)_dragVisualOffsetPx / TravelPerStep);
            effectivePosition = Math.Clamp(
                _dragStartPosition + steps, BreachSolver.MinPos, BreachSolver.MaxPos);
        }

        // Слот, выровненный с пазом: индекс «6 − позиция» (как в оригинале).
        int alignedIndex = (SlotCount - 1) - effectivePosition;

        for (int k = 0; k < SlotCount; k++)
        {
            int slotX = slotsStartX + k * SlotSpacing;
            var slotRect = new Rectangle(slotX, slotY, SlotDiameter, SlotDiameter);

            // Углублённое отверстие.
            using (var holeBrush = new SolidBrush(SlotHole))
            {
                g.FillEllipse(holeBrush, slotRect);
            }

            // Центральный слот (цель) — пунктирная золотая рамка.
            if (k == BreachSolver.Center)
            {
                using var targetPen = new Pen(Color.FromArgb(140, 138, 107, 60))
                {
                    DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
                };
                var targetRect = slotRect;
                targetRect.Inflate(2, 2);
                g.DrawEllipse(targetPen, targetRect);
            }

            // Золотой штырёк в выровненном слоте.
            if (k == alignedIndex)
            {
                int pinSize = 10;
                var pinRect = new Rectangle(
                    slotRect.X + (SlotDiameter - pinSize) / 2,
                    slotRect.Y + (SlotDiameter - pinSize) / 2,
                    pinSize, pinSize);
                using var pinBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    pinRect, GothGoldBright, GothGold,
                    System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
                g.FillEllipse(pinBrush, pinRect);
            }
        }
    }
}
