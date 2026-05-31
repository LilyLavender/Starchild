using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class PegboardPanel : Panel
{
    public List<PegboardParser.TransformData> Pegs { get; set; } = new();
    public PegboardParser.TransformData HighlightedPeg { get; set; }
    public IReadOnlySet<PegboardParser.TransformData> SelectedPegs => _selectedPegs;
    public bool SnapToGrid { get; set; }
    public float GridSize { get; set; } = 1.0f;
    public bool ShowGrid { get; set; }

    public event Action<PegboardParser.TransformData> PegClicked;
    public event Action<PegboardParser.TransformData, Dictionary<PegboardParser.TransformData, (float posX, float posY)>> PegMoved;
    public event Action<float, float> CanvasClicked;

    private readonly HashSet<PegboardParser.TransformData> _selectedPegs = new();

    private PegboardParser.TransformData _dragPeg;
    private Point _dragStartMouse;
    private Dictionary<PegboardParser.TransformData, (float posX, float posY)> _dragStartPositions;
    private bool _isDragging;

    private bool _isRubberBanding;
    private Point _rubberBandStart;
    private Rectangle _rubberBandRect;

    private PegboardParser.TransformData _hoveredPeg;

    private const int DragThreshold = 4;

    private readonly Dictionary<string, Color> colorByPegType = new()
    {
        { "peg_regular",            Color.DimGray },
        { "peg_long",               Color.DimGray },
        { "peg_slime_only",         Color.Violet },
        { "peg_bomb",               Color.FromArgb(30, 30, 30) },
        { "indestructible_peg",     Color.Gainsboro },
        { "bouncer_peg",            Color.OliveDrab },
        { "obstacle_black_hole",    Color.FromArgb(110, 0, 160) },
        { "obstacle_bouncer",       Color.FromArgb(200, 80, 20) },
        { "obstacle_bouncer_mines", Color.FromArgb(110, 90, 30) },
        { "parent_firework_movement", Color.FromArgb(220, 80, 160) },
    };

    public PegboardPanel()
    {
        this.DoubleBuffered = true;
    }

    private void RecordDragPositions(PegboardParser.TransformData peg)
    {
        _dragStartPositions[peg] = (peg.posX, peg.posY);
        if (peg.child != null)
            foreach (var child in peg.child)
                RecordDragPositions(child);
    }

    public void ClearSelection()
    {
        _selectedPegs.Clear();
        Invalidate();
    }

    public void SetSelection(IEnumerable<PegboardParser.TransformData> pegs)
    {
        _selectedPegs.Clear();
        foreach (var p in pegs)
            _selectedPegs.Add(p);
    }

    private static (float x, float y, float sizeX, float sizeY) PegScreenBounds(PegboardParser.TransformData peg)
    {
        float sizeX = 24 * peg.scaleX;
        float sizeY = 24 * peg.scaleY;
        return (50 * peg.posX + 400 - sizeX / 2, -50 * peg.posY - sizeY / 2, sizeX, sizeY);
    }

    private PegboardParser.TransformData HitTest(Point pt)
    {
        // Return the first non-highlighted peg hit; fall back to highlighted peg so
        // overlapping pegs don't get permanently blocked by the selected one.
        PegboardParser.TransformData fallback = null;
        foreach (var peg in Pegs)
        {
            bool hit;
            var (x, y, sizeX, sizeY) = PegScreenBounds(peg);
            if (pt.X >= x && pt.X <= x + sizeX && pt.Y >= y && pt.Y <= y + sizeY)
                hit = true;
            else if (peg.prefab is PegboardParser.LongPegData lpd && lpd.spline != null)
                hit = HitTestSpline(peg, lpd, pt);
            else
                hit = false;

            if (!hit) continue;
            if (peg == HighlightedPeg) fallback = peg;
            else return peg;
        }
        return fallback;
    }

    private static bool HitTestSpline(PegboardParser.TransformData peg, PegboardParser.LongPegData lpd, Point pt)
    {
        try
        {
            int ptCount = lpd.spline.GetPointCount();
            if (ptCount < 2) return false;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            for (int si = 0; si < ptCount - 1; si++)
            {
                var p0 = lpd.spline.GetPosition(si);
                var p1 = lpd.spline.GetPosition(si + 1);
                var rt = lpd.spline.GetRightTangent(si);
                var lt = lpd.spline.GetLeftTangent(si + 1);
                path.AddBezier(
                    50 * (peg.posX + p0.x) + 400, -50 * (peg.posY + p0.y),
                    50 * (peg.posX + p0.x + rt.x) + 400, -50 * (peg.posY + p0.y + rt.y),
                    50 * (peg.posX + p1.x + lt.x) + 400, -50 * (peg.posY + p1.y + lt.y),
                    50 * (peg.posX + p1.x) + 400, -50 * (peg.posY + p1.y));
            }
            using var pen = new Pen(Color.Black, 24f * peg.scaleX);
            return path.IsOutlineVisible(pt.X, pt.Y, pen);
        }
        catch { return false; }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Right)
        {
            float posX = (e.X - 400) / 50f;
            float posY = -e.Y / 50f;
            CanvasClicked?.Invoke(posX, posY);
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        var hitPeg = HitTest(e.Location);

        if (hitPeg != null)
        {
            if ((ModifierKeys & Keys.Control) != 0)
            {
                if (!_selectedPegs.Remove(hitPeg))
                    _selectedPegs.Add(hitPeg);
                Invalidate();
                return;
            }

            if (!_selectedPegs.Contains(hitPeg))
            {
                _selectedPegs.Clear();
                _selectedPegs.Add(hitPeg);
            }

            _dragPeg = hitPeg;
            _dragStartMouse = e.Location;
            _dragStartPositions = new Dictionary<PegboardParser.TransformData, (float posX, float posY)>();
            foreach (var peg in _selectedPegs)
                RecordDragPositions(peg);
        }
        else
        {
            _selectedPegs.Clear();
            _isRubberBanding = true;
            _rubberBandStart = e.Location;
            _rubberBandRect = Rectangle.Empty;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragPeg != null)
        {
            int dx = e.X - _dragStartMouse.X;
            int dy = e.Y - _dragStartMouse.Y;

            if (!_isDragging && (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold))
                _isDragging = true;

            if (_isDragging)
            {
                float gameDx = dx / 50f;
                float gameDy = -dy / 50f;

                foreach (var (peg, (origX, origY)) in _dragStartPositions)
                {
                    float newX = origX + gameDx;
                    float newY = origY + gameDy;
                    if (SnapToGrid && GridSize > 0)
                    {
                        float snapUnit = GridSize / 2f;
                        newX = MathF.Round(newX / snapUnit) * snapUnit;
                        newY = MathF.Round(newY / snapUnit) * snapUnit;
                    }
                    peg.posX = newX;
                    peg.posY = newY;
                }

                Invalidate();
            }
        }
        else if (_isRubberBanding)
        {
            _rubberBandRect = new Rectangle(
                Math.Min(e.X, _rubberBandStart.X),
                Math.Min(e.Y, _rubberBandStart.Y),
                Math.Abs(e.X - _rubberBandStart.X),
                Math.Abs(e.Y - _rubberBandStart.Y));
            Invalidate();
        }

        var hovered = (_dragPeg == null && !_isRubberBanding) ? HitTest(e.Location) : null;
        if (hovered != _hoveredPeg)
        {
            _hoveredPeg = hovered;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredPeg != null)
        {
            _hoveredPeg = null;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;

        if (_isDragging)
        {
            PegMoved?.Invoke(_dragPeg, _dragStartPositions);
        }
        else if (_dragPeg != null)
        {
            PegClicked?.Invoke(_dragPeg);
        }
        else if (_isRubberBanding)
        {
            if (_rubberBandRect.Width > 2 && _rubberBandRect.Height > 2)
            {
                foreach (var peg in Pegs)
                {
                    var (x, y, sizeX, sizeY) = PegScreenBounds(peg);
                    if (_rubberBandRect.Contains((int)(x + sizeX / 2), (int)(y + sizeY / 2)))
                        _selectedPegs.Add(peg);
                }
                foreach (var peg in _selectedPegs) { PegClicked?.Invoke(peg); break; }
            }
        }

        _dragPeg = null;
        _dragStartPositions = null;
        _isDragging = false;
        _isRubberBanding = false;
        _rubberBandRect = Rectangle.Empty;
        Invalidate();
    }

    private Color GetBaseColor(PegboardParser.TransformData peg)
    {
        if (peg.prefab is PegboardParser.RegularPegData rpd && (int)rpd.pegType == 128)
            return Color.SlateGray;
        string ct = peg.prefab?.componentType ?? FallbackComponentType(peg.prefab);
        return colorByPegType.TryGetValue(ct, out Color c) ? c : Color.Gold;
    }

    private static string FallbackComponentType(PegboardParser.Component prefab) => prefab switch
    {
        PegboardParser.BombData => "peg_bomb",
        _ => "unknown"
    };

    private static bool IsInvisiblePeg(PegboardParser.TransformData peg) =>
        peg.prefab is PegboardParser.RegularPegData rpd && rpd.color.a < 0.01f;

    private static bool IsFakePeg(PegboardParser.TransformData peg) =>
        peg.prefab is PegboardParser.RegularPegData rpd2 && rpd2.isFakePeg;

    private static bool IsSlimePeg(PegboardParser.TransformData peg) =>
        peg.prefab is PegboardParser.RegularPegData rpd3 && (int)rpd3.slimeType != 0;

    private static bool IsFlickeringPeg(PegboardParser.TransformData peg) =>
        peg.components?.Any(c => c is PegboardParser.FlickeringPegData || c is PegboardParser.InvisibleOnAwakeData) == true;

    private static PegboardParser.LinearPegMovementData GetLinearMovement(PegboardParser.TransformData peg) =>
        peg.components?.OfType<PegboardParser.LinearPegMovementData>().FirstOrDefault();

    private static PegboardParser.MirroredPegData GetMirrorData(PegboardParser.TransformData peg) =>
        peg.components?.OfType<PegboardParser.MirroredPegData>().FirstOrDefault();

    private static PegboardParser.PegMoveAndReturnData GetMoveAndReturn(PegboardParser.TransformData peg) =>
        peg.components?.OfType<PegboardParser.PegMoveAndReturnData>().FirstOrDefault();

    private static PegboardParser.RotatingPegCircleData GetRotatingCircle(PegboardParser.TransformData peg) =>
        peg.components?.OfType<PegboardParser.RotatingPegCircleData>().FirstOrDefault();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (ShowGrid && GridSize > 0)
        {
            float sg = 50f * GridSize;
            using var gridPen = new Pen(Color.FromArgb(50, 0, 0, 0));

            int nxMin = (int)MathF.Floor(-400f / sg) - 1;
            int nxMax = (int)MathF.Ceiling((ClientSize.Width - 400f) / sg) + 1;
            for (int n = nxMin; n <= nxMax; n++)
            {
                float sx = n * sg + 400;
                if (sx >= 0 && sx <= ClientSize.Width)
                    g.DrawLine(gridPen, sx, 0, sx, ClientSize.Height);
            }

            int nyMin = (int)MathF.Floor(-ClientSize.Height / sg) - 1;
            for (int n = nyMin; n <= 0; n++)
            {
                float sy = -n * sg;
                if (sy >= 0 && sy <= ClientSize.Height)
                    g.DrawLine(gridPen, 0, sy, ClientSize.Width, sy);
            }
        }

        // Play-area boundary
        {
            float bLeft = 50f * -8.5f + 400f;
            float bTop = 0f;
            float bWidth = 50f * 17f;
            float bHeight = 50f * 10f;
            using var boundsPen = new Pen(Color.FromArgb(55, 200, 200, 200), 1f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(boundsPen, bLeft, bTop, bWidth, bHeight);
        }

        // Mirrored ghost positions (drawn below normal pegs)
        foreach (var peg in Pegs)
        {
            var mirror = GetMirrorData(peg);
            if (mirror == null) continue;
            float ghostX = (mirror.mirrorX != 0 ? mirror.mirrorX * peg.posX : peg.posX);
            float ghostY = (mirror.mirrorY != 0 ? mirror.mirrorY * peg.posY : peg.posY);
            float gSizeX = 20 * peg.scaleX;
            float gSizeY = 20 * peg.scaleY;
            float gx = 50 * ghostX + 400 - gSizeX / 2;
            float gy = -50 * ghostY - gSizeY / 2;
            Color baseCol = GetBaseColor(peg);
            using var ghostFill = new SolidBrush(Color.FromArgb(40, baseCol));
            using var ghostPen = new Pen(Color.FromArgb(100, baseCol), 1f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.FillEllipse(ghostFill, gx, gy, gSizeX, gSizeY);
            g.DrawEllipse(ghostPen, gx, gy, gSizeX, gSizeY);
        }

        foreach (var peg in Pegs)
        {
            bool isInvisible = IsInvisiblePeg(peg);
            bool isFake = IsFakePeg(peg);
            bool isDisabled = !peg.enabled;
            bool isHighlighted = peg == HighlightedPeg;
            bool isSelected = _selectedPegs.Contains(peg);

            Color baseColor = GetBaseColor(peg);
            Color pegColor = isHighlighted ? Color.Red
                           : isSelected    ? Color.Orange
                           : baseColor;

            var (x, y, sizeX, sizeY) = PegScreenBounds(peg);

            // Invisible pegs get a faint fill when not selected/highlighted
            int fillAlpha = isInvisible && !isHighlighted && !isSelected ? 55 : 255;
            using var brush = new SolidBrush(Color.FromArgb(fillAlpha, pegColor));
            string ct = peg.prefab?.componentType ?? "";

            g.FillEllipse(brush, x, y, sizeX, sizeY);

            // Long peg: Bezier spline through control points (local-space, offset by peg world pos).
            // Requires SerializationPolicies.Everything so private m_ControlPoints on Spline is populated.
            if (ct == "peg_long" && peg.prefab is PegboardParser.LongPegData lpd && lpd.spline != null)
            {
                try
                {
                    int ptCount = lpd.spline.GetPointCount();
                    if (ptCount >= 2)
                    {
                        using var splinePath = new System.Drawing.Drawing2D.GraphicsPath();
                        for (int si = 0; si < ptCount - 1; si++)
                        {
                            var p0 = lpd.spline.GetPosition(si);
                            var p1 = lpd.spline.GetPosition(si + 1);
                            var rt = lpd.spline.GetRightTangent(si);
                            var lt = lpd.spline.GetLeftTangent(si + 1);
                            float sx0  = 50 * (peg.posX + p0.x) + 400;
                            float sy0  = -50 * (peg.posY + p0.y);
                            float scx0 = 50 * (peg.posX + p0.x + rt.x) + 400;
                            float scy0 = -50 * (peg.posY + p0.y + rt.y);
                            float scx1 = 50 * (peg.posX + p1.x + lt.x) + 400;
                            float scy1 = -50 * (peg.posY + p1.y + lt.y);
                            float sx1  = 50 * (peg.posX + p1.x) + 400;
                            float sy1  = -50 * (peg.posY + p1.y);
                            splinePath.AddBezier(sx0, sy0, scx0, scy0, scx1, scy1, sx1, sy1);
                        }
                        using var splinePen = new Pen(pegColor, 24f * peg.scaleX)
                        {
                            StartCap = System.Drawing.Drawing2D.LineCap.Flat,
                            EndCap   = System.Drawing.Drawing2D.LineCap.Flat,
                            LineJoin = System.Drawing.Drawing2D.LineJoin.Miter,
                        };
                        g.DrawPath(splinePen, splinePath);
                    }
                }
                catch { }
            }

            // Obstacle bumper: thick outer ring to distinguish from pegs
            if (ct is "obstacle_bouncer" or "obstacle_bouncer_mines")
            {
                using var bumperPen = new Pen(Color.FromArgb(180, pegColor), 3f);
                g.DrawEllipse(bumperPen, x - 4, y - 4, sizeX + 8, sizeY + 8);
            }

            // Black hole: dashed gravity-range ring
            if (ct == "obstacle_black_hole")
            {
                float gravRange = peg.prefab.GetType().GetField("gravityRange")?.GetValue(peg.prefab) is float gr ? gr : 5f;
                float grSx = gravRange * 20;
                float gcx = x + sizeX / 2;
                float gcy = y + sizeY / 2;
                using var gravPen = new Pen(Color.FromArgb(75, Color.FromArgb(110, 0, 160)), 1.5f)
                    { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawEllipse(gravPen, gcx - grSx, gcy - grSx, grSx * 2, grSx * 2);
            }

            // Disabled: dark overlay so the peg looks grayed out
            if (isDisabled && !isHighlighted)
            {
                using var disabledOverlay = new SolidBrush(Color.FromArgb(150, 40, 40, 40));
                g.FillEllipse(disabledOverlay, x, y, sizeX, sizeY);
            }

            // Invisible peg: dashed white outline so editors can locate them
            if (isInvisible)
            {
                using var dashedPen = new Pen(Color.FromArgb(180, Color.White), 1.5f)
                    { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawEllipse(dashedPen, x, y, sizeX, sizeY);
            }

            // Fake peg: X glyph (no collision in-game)
            if (isFake)
            {
                float pad = Math.Max(2f, sizeX * 0.2f);
                using var xPen = new Pen(Color.FromArgb(180, Color.White), 1.5f);
                g.DrawLine(xPen, x + pad, y + pad, x + sizeX - pad, y + sizeY - pad);
                g.DrawLine(xPen, x + sizeX - pad, y + pad, x + pad, y + sizeY - pad);
            }

            // Slime tint
            if (IsSlimePeg(peg) && !isHighlighted && !isSelected)
            {
                using var slimeBrush = new SolidBrush(Color.FromArgb(80, 20, 200, 50));
                g.FillEllipse(slimeBrush, x, y, sizeX, sizeY);
            }

            // Flickering/invisible-on-awake: outer dashed cyan ring
            if (IsFlickeringPeg(peg))
            {
                using var flickPen = new Pen(Color.FromArgb(160, Color.Cyan), 1f)
                    { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawEllipse(flickPen, x - 2, y - 2, sizeX + 4, sizeY + 4);
            }

            // Moving peg: yellow directional arrow
            var lmd = GetLinearMovement(peg);
            if (lmd != null)
            {
                float cx = x + sizeX / 2;
                float cy = y + sizeY / 2;
                float len = Math.Min(sizeX, sizeY) * 0.38f;
                float vx = lmd.xMovement;
                float vy = -lmd.yMovement;
                float mag = MathF.Sqrt(vx * vx + vy * vy);
                if (mag > 0)
                {
                    vx /= mag; vy /= mag;
                    using var arrowPen = new Pen(Color.FromArgb(200, Color.Yellow), 1.5f);
                    float ex = cx + vx * len;
                    float ey = cy + vy * len;
                    g.DrawLine(arrowPen, cx - vx * len, cy - vy * len, ex, ey);
                    float hLen = len * 0.45f;
                    float px = -vy; float py = vx;
                    g.DrawLine(arrowPen, ex, ey, ex - vx * hLen + px * hLen * 0.4f, ey - vy * hLen + py * hLen * 0.4f);
                    g.DrawLine(arrowPen, ex, ey, ex - vx * hLen - px * hLen * 0.4f, ey - vy * hLen - py * hLen * 0.4f);
                }
            }

            // Oscillating movement path
            var mar = GetMoveAndReturn(peg);
            if (mar != null)
            {
                float axS = 50 * (peg.posX + mar.targetOffsetAX) + 400;
                float ayS = -50 * (peg.posY + mar.targetOffsetAY);
                float bxS = 50 * (peg.posX + mar.targetOffsetBX) + 400;
                float byS = -50 * (peg.posY + mar.targetOffsetBY);
                using var pathPen = new Pen(Color.FromArgb(180, Color.Orange), 1.5f)
                    { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(pathPen, axS, ayS, bxS, byS);
                using var endBrush = new SolidBrush(Color.FromArgb(180, Color.Orange));
                g.FillEllipse(endBrush, axS - 3, ayS - 3, 6, 6);
                g.FillEllipse(endBrush, bxS - 3, byS - 3, 6, 6);
            }

            if (peg == _hoveredPeg && !isHighlighted)
            {
                using var hoverPen = new Pen(Color.White, 2f);
                g.DrawEllipse(hoverPen, x, y, sizeX, sizeY);
            }
        }

        if (_isRubberBanding && _rubberBandRect.Width > 0 && _rubberBandRect.Height > 0)
        {
            using var fill = new SolidBrush(Color.FromArgb(40, 70, 130, 180));
            using var border = new Pen(Color.SteelBlue, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.FillRectangle(fill, _rubberBandRect);
            g.DrawRectangle(border, _rubberBandRect);
        }

        // Movement boundary lines for selected peg with linear movement
        if (HighlightedPeg != null)
        {
            var selLmd = GetLinearMovement(HighlightedPeg);
            if (selLmd != null)
            {
                float leftSx = 50 * selLmd.leftBoundary + 400;
                float rightSx = 50 * selLmd.rightBoundary + 400;
                using var boundPen = new Pen(Color.FromArgb(160, Color.Yellow), 1f)
                    { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(boundPen, leftSx, 0, leftSx, ClientSize.Height);
                g.DrawLine(boundPen, rightSx, 0, rightSx, ClientSize.Height);
            }
        }

        // Rotating circle ring previews
        foreach (var peg in Pegs)
        {
            var rpc = GetRotatingCircle(peg);
            if (rpc == null || rpc.numPegs <= 0 || rpc.radius <= 0) continue;
            float cx = 50 * peg.posX + 400;
            float cy = -50 * peg.posY;
            float radiusSx = rpc.radius * 50;
            using var ringPen = new Pen(Color.FromArgb(80, Color.White), 1f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawEllipse(ringPen, cx - radiusSx, cy - radiusSx, radiusSx * 2, radiusSx * 2);
            using var dotBrush = new SolidBrush(Color.FromArgb(150, Color.White));
            for (int i = 0; i < rpc.numPegs; i++)
            {
                float angle = 2 * MathF.PI * i / rpc.numPegs;
                float dx = cx + radiusSx * MathF.Cos(angle);
                float dy = cy + radiusSx * MathF.Sin(angle);
                g.FillEllipse(dotBrush, dx - 2.5f, dy - 2.5f, 5, 5);
            }
        }
    }
}
