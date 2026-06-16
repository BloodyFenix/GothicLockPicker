using System.ComponentModel;

namespace LockPicker;

/// <summary>
/// Визуальная пластина замка в стиле веб-версии «Сундуки Готики».
/// Рисует горизонтальную металлическую пластину с 7 слотами-отверстиями,
/// золотым штырьком в выровненном слоте и стрелками ◀ ▶ для сдвига.
///
/// Над номером пластины размещены 3 взаимоисключающие кнопки связи
/// (·/+/−): они задают, как ВЫБРАННАЯ (золотая) пластина влияет на эту.
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
    private const int LabelWidth = 64;
    private const int ArrowWidth = 28;
    private const int TrackHeight = 46;
    private const int PlateHeight = 40;

    // ===== Кнопки связи (над номером пластины) =====
    private const int ButtonSize = 18;
    private const int ButtonGap = 4;
    private const int ButtonsTotalWidth = ButtonSize * 3 + ButtonGap * 2; // 62px
    private const int ButtonStripHeight = 24;

    // Подписи кнопок: нет связи (пусто) / синхронно / инверсно.
    private static readonly string[] RelationGlyphs = { " ", "+", "−" };

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

    // Цвета индикаторов связей: синхронно (зелёный) / инверсно (красный).
    private static readonly Color RelationPlus = Color.FromArgb(98, 178, 92);
    private static readonly Color RelationMinus = Color.FromArgb(196, 64, 56);

    private int _position = BreachSolver.Center;
    private bool _isActive;

    // Исходящие связи пластины: пары (номер целевой пластины, знак ±1).
    private (int Target, int Sign)[] _outgoing = Array.Empty<(int, int)>();

    // ===== Состояние связи с выбранной пластиной =====
    private int _relation;          // -1 / 0 / +1
    private bool _relationLocked;   // true для самой выбранной пластины (диагональ)

    // ===== Состояние перетаскивания (drag-and-drop) =====
    private bool _dragging;
    private int _dragStartMouseX;     // X курсора в начале перетаскивания
    private int _dragStartPosition;   // позиция пластины в начале перетаскивания
    private int _dragVisualOffsetPx;  // визуальное смещение пластины во время drag

    /// <summary>Возникает при изменении позиции пластины пользователем.</summary>
    public event EventHandler? PositionChanged;

    /// <summary>Возникает при щелчке по пластине (для выбора активной).</summary>
    public event EventHandler? PlateSelected;

    /// <summary>
    /// Возникает при нажатии кнопки связи (·/+/−). Аргумент — новое значение
    /// связи (0 / +1 / −1) от выбранной пластины к этой.
    /// </summary>
    public event EventHandler<int>? RelationChanged;

    public PlateRowControl()
    {
        DoubleBuffered = true;
        Height = ButtonStripHeight + TrackHeight + 8;
        Width = LabelWidth + ArrowWidth * 2 + PlateWidth + MaxTravel * 2 + 12;
        Margin = new Padding(0, 3, 0, 3);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    /// <summary>Порядковый номер пластины (1-based) для подписи.</summary>
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

    /// <summary>
    /// Значение связи от выбранной пластины к этой: 0 (нет), +1 (синхронно), −1 (инверсно).
    /// Отражается на состоянии кнопок над номером.
    /// </summary>
    [Browsable(false)]
    public int RelationValue
    {
        get => _relation;
        set
        {
            int v = Math.Sign(value);
            if (v == _relation)
            {
                return;
            }

            _relation = v;
            Invalidate();
        }
    }

    /// <summary>
    /// Заблокированы ли кнопки связи. Истина для самой выбранной пластины
    /// (диагональ матрицы всегда «+»): кнопки отображаются, но не редактируются.
    /// </summary>
    [Browsable(false)]
    public bool RelationLocked
    {
        get => _relationLocked;
        set
        {
            if (_relationLocked == value)
            {
                return;
            }

            _relationLocked = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Исходящие связи этой пластины (как она «тянет» другие): список пар
    /// (номер целевой пластины 1-based, знак ±1). Рисуются над пластиной
    /// справа от номера: «+N» зелёным, «−N» красным.
    /// </summary>
    public void SetOutgoing((int Target, int Sign)[] relations)
    {
        _outgoing = relations ?? Array.Empty<(int, int)>();
        Invalidate();
    }

    // ===== Прямоугольники зон (вычисляются от текущей ширины) =====

    private Rectangle TrackRect()
    {
        int trackWidth = PlateWidth + MaxTravel * 2;
        int x = LabelWidth + ArrowWidth;
        int y = ButtonStripHeight + ((Height - ButtonStripHeight) - TrackHeight) / 2;
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

    /// <summary>Прямоугольник кнопки связи по индексу 0..2 (·/+/−).</summary>
    private Rectangle ButtonRect(int index)
    {
        int startX = (LabelWidth - ButtonsTotalWidth) / 2;
        int x = startX + index * (ButtonSize + ButtonGap);
        int y = (ButtonStripHeight - ButtonSize) / 2;
        return new Rectangle(x, y, ButtonSize, ButtonSize);
    }

    /// <summary>Индекс активной кнопки связи: 0 (нет), 1 (+), 2 (−).</summary>
    private int ActiveButtonIndex => _relation == 0 ? 0 : (_relation > 0 ? 1 : 2);

    /// <summary>Текущий прямоугольник пластины с учётом позиции и drag-смещения.</summary>
    private Rectangle PlateRect()
    {
        Rectangle track = TrackRect();
        int offset = (_position - BreachSolver.Center) * TravelPerStep + _dragVisualOffsetPx;
        int plateX = track.X + (track.Width - PlateWidth) / 2 + offset;
        int plateY = track.Y + (track.Height - PlateHeight) / 2;
        return new Rectangle(plateX, plateY, PlateWidth, PlateHeight);
    }

    // ===== Взаимодействие мышью (кнопки связи + стрелки + перетаскивание) =====

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Клик по кнопкам связи над номером — задаём связь, не меняя выбор.
        for (int k = 0; k < RelationGlyphs.Length; k++)
        {
            if (!ButtonRect(k).Contains(e.Location))
            {
                continue;
            }

            if (!_relationLocked)
            {
                int value = k == 0 ? 0 : (k == 1 ? 1 : -1);
                RelationChanged?.Invoke(this, value);
            }
            return;
        }

        // Выбор активной пластины при любом нажатии вне кнопок связи.
        PlateSelected?.Invoke(this, EventArgs.Empty);

        // Клик по стрелкам — пошаговый сдвиг.
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

        DrawRelationButtons(g);
        DrawOutgoingRelations(g);
        DrawLabel(g);
        DrawArrows(g);
        DrawTrackAndPlate(g);
    }

    /// <summary>
    /// Рисует исходящие связи пластины над треком: «+N» зелёным (синхронно),
    /// «−N» красным (инверсно), где N — номер целевой пластины.
    /// </summary>
    private void DrawOutgoingRelations(Graphics g)
    {
        if (_outgoing.Length == 0)
        {
            return;
        }

        using var font = new Font("Consolas", 9F, FontStyle.Bold);
        int x = LabelWidth + ArrowWidth + 4;
        var stripRect = new Rectangle(0, 0, 0, ButtonStripHeight);

        foreach ((int target, int sign) in _outgoing)
        {
            string text = (sign > 0 ? "+" : "−") + target;
            Color color = sign > 0 ? RelationPlus : RelationMinus;

            Size sz = TextRenderer.MeasureText(g, text, font);
            var rect = new Rectangle(x, stripRect.Y, sz.Width + 4, ButtonStripHeight);
            TextRenderer.DrawText(g, text, font, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            x += sz.Width + 10;
        }
    }

    private void DrawRelationButtons(Graphics g)
    {
        using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
        int active = ActiveButtonIndex;

        for (int k = 0; k < RelationGlyphs.Length; k++)
        {
            Rectangle r = ButtonRect(k);
            bool isActive = k == active;

            Color back;
            Color fore;
            if (_relationLocked)
            {
                // Выбранная пластина: связь зафиксирована (+), кнопки приглушены.
                back = isActive ? SteelMid : SteelDark;
                fore = isActive ? GothGold : Color.FromArgb(80, 76, 70);
            }
            else
            {
                back = isActive ? GothGold : SteelDark;
                fore = isActive ? Color.FromArgb(20, 18, 22) : GothTextDim;
            }

            using (var b = new SolidBrush(back))
            {
                g.FillRectangle(b, r);
            }
            using (var p = new Pen(Color.FromArgb(90, 0, 0, 0)))
            {
                g.DrawRectangle(p, r);
            }

            TextRenderer.DrawText(g, RelationGlyphs[k], font, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawLabel(Graphics g)
    {
        string label = PlateNumber.ToString();

        using var font = new Font("Constantia", 11F, FontStyle.Bold);
        Color color = _isActive ? GothGoldBright : GothTextDim;
        var rect = new Rectangle(0, ButtonStripHeight, LabelWidth, Height - ButtonStripHeight);
        TextRenderer.DrawText(g, label, font, rect, color,
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
