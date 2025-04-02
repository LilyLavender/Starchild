public class PegboardPanel : Panel
{
    public List<PegboardParser.TransformData> Pegs { get; set; } = new();
    public PegboardParser.TransformData HighlightedPeg { get; set; }

    public PegboardPanel()
    {
        this.DoubleBuffered = true;
    }

    private readonly Dictionary<string, Color> colorByPegType = new()
    {
        { "peg_regular", Color.DimGray },
        { "peg_long", Color.Cyan },
        { "peg_slime_only", Color.Violet },
        { "peg_bomb", Color.Black },
        { "indestructible_peg", Color.Gainsboro },
        { "bouncer_peg", Color.OliveDrab },
        //{ "attr_invisible_on_awake", Color.DimGray },
        //{ "attr_flickering_peg", Color.DimGray },
        //{ "attr_moving_peg_linear", Color.DimGray },
        //{ "attr_mirrored_peg", Color.DimGray },
        //{ "attr_offset_pos_on_start", Color.DimGray },
        //{ "attr_moving_peg_return", Color.DimGray },
        //{ "parent_peg_block", Color.DimGray },
        //{ "parent_random_peg_field", Color.DimGray },
        //{ "parent_random_peg_field", Color.DimGray },
        //{ "parent_rotating_peg_circle", Color.DimGray },
        //{ "parent_firework_movement", Color.DimGray },
        //{ "parent_flickering_loop", Color.DimGray },
        //{ "obstacle_black_hole", Color.DimGray },
        //{ "obstacle_obscured_peg_grid", Color.DimGray },
        //{ "obstacle_bouncer", Color.DimGray },
        //{ "obstacle_bouncer_mines", Color.DimGray },
        //{ "dragon_peg_layout_simulator", Color.DimGray },
        //{ "peg_square_movement", Color.DimGray },
        //{ "disappear_nav", Color.DimGray },
        //{ "boids_peg_layout", Color.DimGray },
    };
    
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach (var peg in Pegs)
        {
            string pegType = peg.prefab?.componentType ?? "unknown";
            Color pegColor = (peg == HighlightedPeg) 
                ? Color.Red 
                : colorByPegType.TryGetValue(pegType, out Color typeColor) ? typeColor : Color.Gold;

            using (SolidBrush brush = new(pegColor))
            {
                float x = 50 * peg.posX + 400;
                float y = -50 * peg.posY;
                float sizeX = 20 * peg.scaleX;
                float sizeY = 20 * peg.scaleY;
                e.Graphics.FillEllipse(brush, x, y, sizeX, sizeY);
            }
        }
    }
}
