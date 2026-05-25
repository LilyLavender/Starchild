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

    private readonly HashSet<PegboardParser.TransformData> _selectedPegs = new();

    private PegboardParser.TransformData _dragPeg;
    private Point _dragStartMouse;
    private Dictionary<PegboardParser.TransformData, (float posX, float posY)> _dragStartPositions;
    private bool _isDragging;

    private bool _isRubberBanding;
    private Point _rubberBandStart;
    private Rectangle _rubberBandRect;

    private const int DragThreshold = 4;

    private readonly Dictionary<string, Color> colorByPegType = new()
    {
        { "peg_regular", Color.DimGray },
        { "peg_long", Color.Cyan },
        { "peg_slime_only", Color.Violet },
        { "peg_bomb", Color.Black },
        { "indestructible_peg", Color.Gainsboro },
        { "bouncer_peg", Color.OliveDrab },
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
        float sizeX = 20 * peg.scaleX;
        float sizeY = 20 * peg.scaleY;
        return (50 * peg.posX + 400 - sizeX / 2, -50 * peg.posY - sizeY / 2, sizeX, sizeY);
    }

    private PegboardParser.TransformData HitTest(Point pt)
    {
        foreach (var peg in Pegs)
        {
            var (x, y, sizeX, sizeY) = PegScreenBounds(peg);
            if (pt.X >= x && pt.X <= x + sizeX && pt.Y >= y && pt.Y <= y + sizeY)
                return peg;
        }
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Right)
        {
            var hit = HitTest(e.Location);
            if (hit != null && !_selectedPegs.Contains(hit))
            {
                _selectedPegs.Clear();
                _selectedPegs.Add(hit);
                PegClicked?.Invoke(hit);
                Invalidate();
            }
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
        else if (_isRubberBanding && _rubberBandRect.Width > 2 && _rubberBandRect.Height > 2)
        {
            foreach (var peg in Pegs)
            {
                var (x, y, sizeX, sizeY) = PegScreenBounds(peg);
                if (_rubberBandRect.Contains((int)(x + sizeX / 2), (int)(y + sizeY / 2)))
                    _selectedPegs.Add(peg);
            }
            foreach (var peg in _selectedPegs) { PegClicked?.Invoke(peg); break; }
        }

        _dragPeg = null;
        _dragStartPositions = null;
        _isDragging = false;
        _isRubberBanding = false;
        _rubberBandRect = Rectangle.Empty;
        Invalidate();
    }

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

        foreach (var peg in Pegs)
        {
            string pegType = peg.prefab?.componentType ?? "unknown";
            Color pegColor;
            if (peg == HighlightedPeg)
                pegColor = Color.Red;
            else if (_selectedPegs.Contains(peg))
                pegColor = Color.Orange;
            else
                pegColor = colorByPegType.TryGetValue(pegType, out Color c) ? c : Color.Gold;

            var (x, y, sizeX, sizeY) = PegScreenBounds(peg);
            using var brush = new SolidBrush(pegColor);
            g.FillEllipse(brush, x, y, sizeX, sizeY);
        }

        if (_isRubberBanding && _rubberBandRect.Width > 0 && _rubberBandRect.Height > 0)
        {
            using var fill = new SolidBrush(Color.FromArgb(40, 70, 130, 180));
            using var border = new Pen(Color.SteelBlue, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.FillRectangle(fill, _rubberBandRect);
            g.DrawRectangle(border, _rubberBandRect);
        }
    }
}
