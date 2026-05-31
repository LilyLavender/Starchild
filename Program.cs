using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Starchild
{
    public partial class MainForm : Form
    {
        private PegboardPanel pegboardPanel;
        private RadioButton _formatDat, _formatBytes, _formatJson;
        private CheckBox _withHeader;
        private TreeView pegTreeView;
        private TreeNode selectedNode = null;

        private Panel pegInfoPanel;
        private TextBox nameBox, posXBox, posYBox, scaleXBox, scaleYBox, typeBox;
        private PegboardParser.TransformData selectedPeg;

        private readonly List<BoardSession> _sessions = new();
        private BoardSession _active;
        private TabControl _tabControl;
        private bool _switchingSession;
        private ToolStripStatusLabel _coordStatusLabel;
        private ToolStripStatusLabel _pegCountLabel;
        private TreeNode _dragNode;
        private bool _isDraggingTree;

        public MainForm()
        {
            this.Text = "Starchild";
            var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("starchild.resources.starchild-icon.ico");
            if (iconStream != null)
                this.Icon = new Icon(iconStream);
            this.Size = new Size(1000, 600);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            pegboardPanel = new PegboardPanel()
            {
                BackColor = Color.LightGray,
                AutoScroll = true,
                Location = new Point(10, 68),
                Size = new Size(800, 700)
            };

            pegTreeView = new TreeView()
            {
                Location = new Point(820, 68),
                Size = new Size(250, 700),
                BorderStyle = BorderStyle.FixedSingle
            };
            pegTreeView.AfterSelect += PegTreeView_AfterSelect;
            pegTreeView.AllowDrop = true;
            pegTreeView.ItemDrag += PegTreeView_ItemDrag;
            pegTreeView.DragEnter += PegTreeView_DragEnter;
            pegTreeView.DragOver += PegTreeView_DragOver;
            pegTreeView.DragDrop += PegTreeView_DragDrop;
            pegboardPanel.PegClicked += OnCanvasPegClicked;
            pegboardPanel.PegMoved += OnPegMoved;
            pegboardPanel.MouseMove += (s, e) =>
            {
                float posX = (e.X - 400) / 50f;
                float posY = -e.Y / 50f;
                _coordStatusLabel.Text = $"Canvas: ({posX:F2}, {posY:F2})";
            };
            pegboardPanel.MouseLeave += (s, e) => _coordStatusLabel.Text = "Canvas: (-, -)";

            pegInfoPanel = new Panel()
            {
                Location = new Point(1080, 68),
                Size = new Size(330, 700),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };

            _tabControl = new TabControl()
            {
                Location = new Point(10, 38),
                Size = new Size(1400, 28),
            };
            _tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (_switchingSession) return;
                SwitchToSession(_tabControl.SelectedIndex);
            };
            _tabControl.MouseDown += TabControl_MouseDown;

            Button importButton = new Button()
            {
                Text = "Open",
                Location = new Point(10, 10),
                Width = 60
            };
            importButton.Click += ImportButton_Click;

            Button exportButton = new Button()
            {
                Text = "Export",
                Location = new Point(80, 10),
                Width = 65
            };
            exportButton.Click += ExportButton_Click;

            Button exportAllButton = new Button()
            {
                Text = "Export All",
                Location = new Point(153, 10),
                Width = 75
            };
            exportAllButton.Click += BatchExportButton_Click;

            Label formatLabel = new Label()
            {
                Text = "Format:",
                Location = new Point(238, 13),
                AutoSize = true
            };

            _formatDat = new RadioButton()
            {
                Text = ".dat",
                Location = new Point(290, 11),
                AutoSize = true,
                Checked = true
            };

            _formatBytes = new RadioButton()
            {
                Text = ".bytes",
                Location = new Point(335, 11),
                AutoSize = true
            };

            _formatJson = new RadioButton()
            {
                Text = ".json",
                Location = new Point(395, 11),
                AutoSize = true
            };

            _withHeader = new CheckBox()
            {
                Text = "With Header",
                Location = new Point(448, 12),
                AutoSize = true,
                Checked = true
            };
            _formatJson.CheckedChanged += (s, e) => _withHeader.Enabled = !_formatJson.Checked;

            CheckBox snapCheck = new CheckBox()
            {
                Text = "Snap to Grid",
                Location = new Point(570, 12),
                AutoSize = true
            };
            snapCheck.CheckedChanged += (s, e) => pegboardPanel.SnapToGrid = snapCheck.Checked;

            CheckBox gridCheck = new CheckBox()
            {
                Text = "Show Grid",
                Location = new Point(665, 12),
                AutoSize = true
            };
            gridCheck.CheckedChanged += (s, e) => { pegboardPanel.ShowGrid = gridCheck.Checked; pegboardPanel.Invalidate(); };

            Button undoButton = new Button()
            {
                Text = "Undo (Ctrl+Z)",
                Location = new Point(755, 8),
                Width = 110
            };
            undoButton.Click += (s, e) => ExecuteUndo();

            Button redoButton = new Button()
            {
                Text = "Redo (Ctrl+Shift+Z)",
                Location = new Point(873, 8),
                Width = 130
            };
            redoButton.Click += (s, e) => ExecuteRedo();

            Label placeLabel = new Label()
            {
                Text = "Right click to place:",
                Location = new Point(1013, 13),
                AutoSize = true
            };

            ComboBox placeTypeCombo = new ComboBox()
            {
                Location = new Point(1124, 9),
                Width = 155,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            placeTypeCombo.Items.AddRange(new[]
            {
                "peg_regular", "peg_bomb", "indestructible_peg", "bouncer_peg",
                "obstacle_black_hole", "peg_long", "peg_slime_only",
                "obstacle_bouncer", "obstacle_bouncer_mines", "parent_firework_movement", "(group)"
            });
            placeTypeCombo.SelectedIndex = 0;

            pegboardPanel.CanvasClicked += (posX, posY) =>
            {
                string type = placeTypeCombo.SelectedItem?.ToString() ?? "peg_regular";
                float s = type == "bouncer_peg" ? 3f : 1f;
                InsertNewPeg(type, posX, posY, s, s);
            };

            _coordStatusLabel = new ToolStripStatusLabel("Canvas: (-, -)") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            _pegCountLabel = new ToolStripStatusLabel("") { AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(_coordStatusLabel);
            statusStrip.Items.Add(_pegCountLabel);

            this.Controls.Add(importButton);
            this.Controls.Add(exportButton);
            this.Controls.Add(exportAllButton);
            this.Controls.Add(formatLabel);
            this.Controls.Add(_formatDat);
            this.Controls.Add(_formatBytes);
            this.Controls.Add(_formatJson);
            this.Controls.Add(_withHeader);
            this.Controls.Add(snapCheck);
            this.Controls.Add(gridCheck);
            this.Controls.Add(undoButton);
            this.Controls.Add(redoButton);
            this.Controls.Add(placeLabel);
            this.Controls.Add(placeTypeCombo);
            this.Controls.Add(_tabControl);
            this.Controls.Add(pegboardPanel);
            this.Controls.Add(pegTreeView);
            this.Controls.Add(pegInfoPanel);
            this.Controls.Add(statusStrip);
        }

        // ── Session management ────────────────────────────────────────────────

        internal void AddSession(BoardSession session)
        {
            if (session.FilePath != null)
            {
                int existing = _sessions.FindIndex(s => s.FilePath == session.FilePath);
                if (existing >= 0)
                {
                    _tabControl.SelectedIndex = existing;
                    return;
                }
            }

            _sessions.Add(session);
            _tabControl.TabPages.Add(new TabPage(session.TabLabel));
            int newIndex = _tabControl.TabPages.Count - 1;
            _switchingSession = true;
            _tabControl.SelectedIndex = newIndex;
            _switchingSession = false;
            SwitchToSession(newIndex);
        }

        private void SwitchToSession(int index)
        {
            if (_active != null)
            {
                _active.SelectedPeg = selectedPeg;
                _active.SelectedPegs = new HashSet<PegboardParser.TransformData>(pegboardPanel.SelectedPegs);
            }

            _active = index >= 0 && index < _sessions.Count ? _sessions[index] : null;

            pegboardPanel.Pegs.Clear();
            pegboardPanel.ClearSelection();
            pegTreeView.Nodes.Clear();
            pegInfoPanel.Controls.Clear();
            selectedPeg = null;
            selectedNode = null;
            pegTreeView.SelectedNode = null;
            pegboardPanel.HighlightedPeg = null;

            this.Text = _active != null ? $"Starchild — {_active.TabLabel}" : "Starchild";
            _pegCountLabel.Text = "";
            if (_active == null) { pegboardPanel.Invalidate(); return; }

            DrawPegboard();
            UpdatePegCount();

            selectedPeg = _active.SelectedPeg;
            if (selectedPeg != null)
            {
                pegboardPanel.HighlightedPeg = selectedPeg;
                var node = FindTreeNode(pegTreeView.Nodes, selectedPeg);
                if (node != null) pegTreeView.SelectedNode = node;
                UpdatePegInfoPanel(selectedPeg);
            }

            if (_active.SelectedPegs.Count > 0)
                pegboardPanel.SetSelection(_active.SelectedPegs);

            pegboardPanel.Invalidate();
        }

        private void CloseSession(int index)
        {
            _switchingSession = true;
            _sessions.RemoveAt(index);
            _tabControl.TabPages.RemoveAt(index);
            _switchingSession = false;

            int newIndex = Math.Min(index, _sessions.Count - 1);
            if (newIndex >= 0)
            {
                _tabControl.SelectedIndex = newIndex;
                SwitchToSession(newIndex);
            }
            else
            {
                _active = null;
                pegboardPanel.Pegs.Clear();
                pegboardPanel.ClearSelection();
                pegTreeView.Nodes.Clear();
                pegInfoPanel.Controls.Clear();
                selectedPeg = null;
                pegTreeView.SelectedNode = null;
                pegboardPanel.HighlightedPeg = null;
                pegboardPanel.Invalidate();
            }
        }

        private void CloseAllSessions()
        {
            _switchingSession = true;
            _sessions.Clear();
            _tabControl.TabPages.Clear();
            _switchingSession = false;

            _active = null;
            pegboardPanel.Pegs.Clear();
            pegboardPanel.ClearSelection();
            pegTreeView.Nodes.Clear();
            pegInfoPanel.Controls.Clear();
            selectedPeg = null;
            pegTreeView.SelectedNode = null;
            pegboardPanel.HighlightedPeg = null;
            pegboardPanel.Invalidate();
        }

        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            for (int i = 0; i < _tabControl.TabCount; i++)
            {
                if (!_tabControl.GetTabRect(i).Contains(e.Location)) continue;
                int capturedI = i;
                var menu = new ContextMenuStrip();
                menu.Items.Add("Close", null, (ms, me) => CloseSession(capturedI));
                menu.Items.Add("Close All", null, (ms, me) => CloseAllSessions());
                menu.Show(_tabControl, e.Location);
                break;
            }
        }

        // ── Undo / Redo ──────────────────────────────────────────────────────

        internal void PushUndo(Action undo, Action redo)
        {
            if (_active == null) return;
            _active.UndoStack.Push((undo, redo));
            _active.RedoStack.Clear();
        }

        private void ExecuteUndo()
        {
            if (_active == null || _active.UndoStack.Count == 0) return;
            var (undo, redo) = _active.UndoStack.Pop();
            undo();
            _active.RedoStack.Push((undo, redo));
        }

        private void ExecuteRedo()
        {
            if (_active == null || _active.RedoStack.Count == 0) return;
            var (undo, redo) = _active.RedoStack.Pop();
            redo();
            _active.UndoStack.Push((undo, redo));
        }

        // ── Helpers shared across partial files ───────────────────────────────

        internal void AddPegRecursive(PegboardParser.TransformData peg)
        {
            pegboardPanel.Pegs.Add(peg);
            if (peg.child != null)
                foreach (var child in peg.child)
                    AddPegRecursive(child);
        }

        internal void RemovePegFromPanel(PegboardParser.TransformData peg)
        {
            pegboardPanel.Pegs.Remove(peg);
            if (peg.child != null)
                foreach (var child in peg.child)
                    RemovePegFromPanel(child);
        }

        internal TreeNode CreateTreeNode(PegboardParser.TransformData peg)
        {
            var node = new TreeNode(TreeNodeLabel(peg)) { Tag = peg };
            StyleTreeNode(node, peg);
            if (peg.child != null)
                foreach (var child in peg.child)
                    node.Nodes.Add(CreateTreeNode(child));
            return node;
        }

        private static string TreeNodeLabel(PegboardParser.TransformData td)
        {
            int compCount = td.components?.Count ?? 0;
            string compSuffix = compCount > 0 ? $" +{compCount}" : "";

            if (td.prefab == null)
                return compCount > 0 ? $"{td.name}  [{compSuffix.Trim()}]" : td.name;

            string ct = td.prefab.componentType;
            string shortType = ct switch
            {
                "peg_regular"             => "regular",
                "peg_long"                => "long peg",
                "peg_bomb"                => "bomb",
                "peg_slime_only"          => "slime",
                "indestructible_peg"      => "indestructible",
                "bouncer_peg"             => "bouncer",
                "obstacle_black_hole"     => "black hole",
                "obstacle_bouncer"        => "bumper",
                "obstacle_bouncer_mines"  => "mine bumper",
                "parent_firework_movement" => "firework",
                null => td.prefab switch
                {
                    PegboardParser.BombData => "bomb",
                    _ => td.prefab.GetType().Name
                },
                _                         => ct,
            };

            if (td.prefab is PegboardParser.RegularPegData rpd)
            {
                if ((int)rpd.pegType == 128)   shortType = "dull";
                else if (rpd.isFakePeg)        shortType += " [fake]";
                else if (rpd.color.a < 0.01f)  shortType += " [invis]";
            }

            return $"{td.name}  [{shortType}{compSuffix}]";
        }

        private void UpdatePegCount()
        {
            if (_active == null || pegboardPanel.Pegs.Count == 0) { _pegCountLabel.Text = ""; return; }
            var counts = pegboardPanel.Pegs
                .Where(p => p.prefab != null)
                .GroupBy(p => p.prefab.componentType ?? (p.prefab is PegboardParser.BombData ? "peg_bomb" : p.prefab.GetType().Name))
                .OrderByDescending(g => g.Count())
                .Select(g =>
                {
                    string label = g.Key switch
                    {
                        "peg_regular"            => "reg",
                        "peg_long"               => "long",
                        "peg_bomb"               => "bomb",
                        "peg_slime_only"         => "slime",
                        "indestructible_peg"     => "indestr",
                        "bouncer_peg"            => "bounce",
                        "obstacle_black_hole"    => "hole",
                        "obstacle_bouncer"       => "bumper",
                        "obstacle_bouncer_mines" => "mine",
                        _ => g.Key
                    };
                    return $"{g.Count()} {label}";
                });
            _pegCountLabel.Text = string.Join("  ", counts);
        }

        private static void StyleTreeNode(TreeNode node, PegboardParser.TransformData peg)
        {
            if (!peg.enabled)
                node.ForeColor = Color.DarkGray;
            else if (peg.prefab == null)
                node.ForeColor = Color.SteelBlue;
        }

        internal static void RestoreTransformData(PegboardParser.TransformData target, PegboardParser.TransformData source)
        {
            target.name = source.name;
            target.posX = source.posX;
            target.posY = source.posY;
            target.scaleX = source.scaleX;
            target.scaleY = source.scaleY;
            target.enabled = source.enabled;
            if (target.prefab != null && source.prefab != null)
                foreach (var field in target.prefab.GetType().GetFields())
                    field.SetValue(target.prefab, field.GetValue(source.prefab));
        }

        internal void RefreshPegUI(PegboardParser.TransformData peg)
        {
            var node = FindTreeNode(pegTreeView.Nodes, peg);
            if (node != null) { node.Text = TreeNodeLabel(peg); StyleTreeNode(node, peg); }
            if (peg == selectedPeg) UpdatePegInfoPanel(peg);
            pegboardPanel.Invalidate();
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void PegTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_isDraggingTree) return;
            selectedPeg = (PegboardParser.TransformData)e.Node.Tag;
            pegboardPanel.HighlightedPeg = selectedPeg;
            pegboardPanel.Invalidate();
            UpdatePegInfoPanel(selectedPeg);
        }

        private void OnCanvasPegClicked(PegboardParser.TransformData peg)
        {
            TreeNode node = FindTreeNode(pegTreeView.Nodes, peg);
            if (node != null)
                pegTreeView.SelectedNode = node;
        }

        private void OnPegMoved(PegboardParser.TransformData dragPeg,
            Dictionary<PegboardParser.TransformData, (float posX, float posY)> oldPositions)
        {
            if (dragPeg == selectedPeg)
            {
                posXBox.Text = dragPeg.posX.ToString();
                posYBox.Text = dragPeg.posY.ToString();
            }

            var newPositions = oldPositions.ToDictionary(kv => kv.Key, kv => (kv.Key.posX, kv.Key.posY));

            PushUndo(
                undo: () =>
                {
                    foreach (var (peg, (ox, oy)) in oldPositions) { peg.posX = ox; peg.posY = oy; }
                    if (dragPeg == selectedPeg) { posXBox.Text = dragPeg.posX.ToString(); posYBox.Text = dragPeg.posY.ToString(); }
                    pegboardPanel.Invalidate();
                },
                redo: () =>
                {
                    foreach (var (peg, (nx, ny)) in newPositions) { peg.posX = nx; peg.posY = ny; }
                    if (dragPeg == selectedPeg) { posXBox.Text = dragPeg.posX.ToString(); posYBox.Text = dragPeg.posY.ToString(); }
                    pegboardPanel.Invalidate();
                }
            );
        }

        private void PegTreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            _dragNode = (TreeNode)e.Item;
            pegTreeView.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void PegTreeView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void PegTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
            var pt = pegTreeView.PointToClient(new Point(e.X, e.Y));
            var hovered = pegTreeView.GetNodeAt(pt);
            var highlight = ResolveGroupTarget(hovered) ?? hovered;
            if (highlight != null && highlight != _dragNode)
            {
                _isDraggingTree = true;
                pegTreeView.SelectedNode = highlight;
                _isDraggingTree = false;
            }
        }

        private void PegTreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragNode == null || _active == null) return;
            var pt = pegTreeView.PointToClient(new Point(e.X, e.Y));
            var hovered = pegTreeView.GetNodeAt(pt);
            if (hovered == null || hovered == _dragNode) { _dragNode = null; return; }

            var targetNode = ResolveGroupTarget(hovered);
            if (targetNode == _dragNode || (targetNode != null && TreeNodeIsDescendant(_dragNode, targetNode)))
            { _dragNode = null; return; }
            if (targetNode == _dragNode.Parent) { _dragNode = null; return; }

            var dragPeg = (PegboardParser.TransformData)_dragNode.Tag;
            var targetPeg = targetNode != null ? (PegboardParser.TransformData)targetNode.Tag : null;

            var oldParentNode = _dragNode.Parent;
            var oldParentPeg = oldParentNode != null ? (PegboardParser.TransformData)oldParentNode.Tag : null;
            int oldDataIdx = oldParentPeg?.child?.IndexOf(dragPeg) ?? _active.Data.transforms.IndexOf(dragPeg);
            int oldNodeIdx = _dragNode.Index;

            if (oldParentPeg != null) oldParentPeg.child?.Remove(dragPeg);
            else _active.Data.transforms.Remove(dragPeg);
            _dragNode.Remove();

            if (targetNode != null)
            {
                targetPeg.child ??= new List<PegboardParser.TransformData>();
                targetPeg.child.Add(dragPeg);
                targetNode.Nodes.Add(_dragNode);
                targetNode.Expand();
            }
            else
            {
                _active.Data.transforms.Add(dragPeg);
                pegTreeView.Nodes.Add(_dragNode);
            }
            pegTreeView.SelectedNode = _dragNode;

            var capturedDragNode = _dragNode;
            var data = _active.Data;
            PushUndo(
                undo: () =>
                {
                    if (targetNode != null) targetPeg.child?.Remove(dragPeg);
                    else data.transforms.Remove(dragPeg);
                    capturedDragNode.Remove();
                    if (oldParentPeg != null) { oldParentPeg.child ??= new List<PegboardParser.TransformData>(); oldParentPeg.child.Insert(Math.Min(oldDataIdx, oldParentPeg.child.Count), dragPeg); }
                    else data.transforms.Insert(Math.Min(oldDataIdx, data.transforms.Count), dragPeg);
                    var col = oldParentNode?.Nodes ?? pegTreeView.Nodes;
                    col.Insert(Math.Min(oldNodeIdx, col.Count), capturedDragNode);
                    pegTreeView.SelectedNode = capturedDragNode;
                },
                redo: () =>
                {
                    if (oldParentPeg != null) oldParentPeg.child?.Remove(dragPeg);
                    else data.transforms.Remove(dragPeg);
                    capturedDragNode.Remove();
                    if (targetNode != null) { targetPeg.child ??= new List<PegboardParser.TransformData>(); targetPeg.child.Add(dragPeg); targetNode.Nodes.Add(capturedDragNode); targetNode.Expand(); }
                    else { data.transforms.Add(dragPeg); pegTreeView.Nodes.Add(capturedDragNode); }
                    pegTreeView.SelectedNode = capturedDragNode;
                }
            );

            _dragNode = null;
        }

        private static TreeNode ResolveGroupTarget(TreeNode hovered)
        {
            if (hovered == null) return null;
            var peg = hovered.Tag as PegboardParser.TransformData;
            if (peg?.prefab == null) return hovered; // it's a group
            return hovered.Parent; // null = root level
        }

        private static bool TreeNodeIsDescendant(TreeNode ancestor, TreeNode node)
        {
            var n = node.Parent;
            while (n != null)
            {
                if (n == ancestor) return true;
                n = n.Parent;
            }
            return false;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && !e.Shift && e.KeyCode == Keys.Z) { ExecuteUndo(); e.Handled = true; return; }
            if (e.Control && e.Shift && e.KeyCode == Keys.Z) { ExecuteRedo(); e.Handled = true; return; }
            if (e.Control && !e.Shift && e.KeyCode == Keys.S) { SaveActive(); e.Handled = true; return; }
            if (e.Control && !e.Shift && e.KeyCode == Keys.O) { ImportButton_Click(null, EventArgs.Empty); e.Handled = true; return; }

            if (e.KeyCode == Keys.Delete && pegboardPanel.SelectedPegs.Count > 0)
            {
                ExecuteDeletePegs(new List<PegboardParser.TransformData>(pegboardPanel.SelectedPegs), confirm: true);
                e.Handled = true;
            }
        }

        // ── Shared delete logic ──────────────────────────────────────────────

        internal void ExecuteDeletePegs(List<PegboardParser.TransformData> toDelete, bool confirm)
        {
            if (toDelete.Count == 0 || _active == null) return;

            if (confirm)
            {
                string msg = toDelete.Count == 1
                    ? $"Delete {toDelete[0].name}?"
                    : $"Delete {toDelete.Count} selected pegs?";
                if (MessageBox.Show(msg, "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            var data = _active.Data;
            var records = toDelete
                .Select(peg =>
                {
                    var node = FindTreeNode(pegTreeView.Nodes, peg);
                    var parentPeg = node?.Parent != null ? (PegboardParser.TransformData)node.Parent.Tag : null;
                    int dataIdx = parentPeg?.child?.IndexOf(peg) ?? data.transforms.IndexOf(peg);
                    return (peg, parentPeg, dataIdx, nodeIdx: node?.Index ?? 0, parentNode: node?.Parent);
                })
                .OrderByDescending(r => r.dataIdx)
                .ToList();

            foreach (var (peg, parentPeg, _, _, _) in records)
            {
                parentPeg?.child?.Remove(peg);
                DeletePeg(peg, data);
                FindTreeNode(pegTreeView.Nodes, peg)?.Remove();
            }

            selectedPeg = null;
            pegTreeView.SelectedNode = null;
            pegboardPanel.HighlightedPeg = null;
            pegboardPanel.ClearSelection();
            pegInfoPanel.Controls.Clear();
            pegboardPanel.Invalidate();

            var restoreRecords = records.OrderBy(r => r.dataIdx).ToList();
            PushUndo(
                undo: () =>
                {
                    foreach (var (peg, parentPeg, dataIdx, nodeIdx, parentNode) in restoreRecords)
                    {
                        if (parentPeg != null)
                        {
                            parentPeg.child ??= new List<PegboardParser.TransformData>();
                            parentPeg.child.Insert(Math.Min(dataIdx, parentPeg.child.Count), peg);
                        }
                        else
                        {
                            data.transforms.Insert(Math.Min(dataIdx, data.transforms.Count), peg);
                        }
                        AddPegRecursive(peg);
                        var col = parentNode?.Nodes ?? pegTreeView.Nodes;
                        col.Insert(Math.Min(nodeIdx, col.Count), CreateTreeNode(peg));
                    }
                    pegboardPanel.Invalidate();
                },
                redo: () =>
                {
                    foreach (var (peg, parentPeg, _, _, _) in records)
                    {
                        parentPeg?.child?.Remove(peg);
                        DeletePeg(peg, data);
                        FindTreeNode(pegTreeView.Nodes, peg)?.Remove();
                    }
                    selectedPeg = null;
                    pegTreeView.SelectedNode = null;
                    pegboardPanel.HighlightedPeg = null;
                    pegInfoPanel.Controls.Clear();
                    pegboardPanel.Invalidate();
                }
            );
        }

        // ── Navigation / draw ────────────────────────────────────────────────

        private TreeNode FindTreeNode(TreeNodeCollection nodes, PegboardParser.TransformData peg)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == peg) return node;
                TreeNode found = FindTreeNode(node.Nodes, peg);
                if (found != null) return found;
            }
            return null;
        }

        private void DrawPegboard()
        {
            pegboardPanel.Controls.Clear();
            pegTreeView.Nodes.Clear();

            if (_active?.Data?.transforms != null)
            {
                foreach (var transform in _active.Data.transforms)
                {
                    pegboardPanel.Pegs.Add(transform);
                    DrawTransform(transform, null);
                }
            }
            pegboardPanel.Invalidate();
        }

        internal TreeNode DrawTransform(PegboardParser.TransformData transform, TreeNode parentNode)
        {
            TreeNode pegNode = new TreeNode(TreeNodeLabel(transform)) { Tag = transform };
            StyleTreeNode(pegNode, transform);

            if (parentNode == null)
                pegTreeView.Nodes.Add(pegNode);
            else
                parentNode.Nodes.Add(pegNode);

            if (transform.child != null)
            {
                foreach (var child in transform.child)
                {
                    pegboardPanel.Pegs.Add(child);
                    DrawTransform(child, pegNode);
                }
            }

            return pegNode;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
