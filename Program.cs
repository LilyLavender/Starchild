using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Starchild
{
    public partial class MainForm : Form
    {
        private PegboardPanel pegboardPanel;
        private PegboardParser.PegboardData pegboardData;
        private string pegboardName = "";
        private ComboBox exportDropdown;
        private TreeView pegTreeView;
        private TreeNode selectedNode = null;

        private Panel pegInfoPanel;
        private TextBox nameBox, posXBox, posYBox, scaleXBox, scaleYBox, typeBox;
        private PegboardParser.TransformData selectedPeg;

        private readonly Stack<(Action undo, Action redo)> _undoStack = new();
        private readonly Stack<(Action undo, Action redo)> _redoStack = new();

        public MainForm()
        {
            this.Text = "Starchild";
            this.Icon = new Icon("resources/starchild-icon.ico");
            this.Size = new Size(1000, 600);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            pegboardPanel = new PegboardPanel()
            {
                BackColor = Color.LightGray,
                AutoScroll = true,
                Location = new Point(10, 40),
                Size = new Size(800, 700)
            };

            pegTreeView = new TreeView()
            {
                Location = new Point(820, 40),
                Size = new Size(250, 700),
                BorderStyle = BorderStyle.FixedSingle
            };
            pegTreeView.AfterSelect += PegTreeView_AfterSelect;
            pegboardPanel.PegClicked += OnCanvasPegClicked;
            pegboardPanel.PegMoved += OnPegMoved;

            pegInfoPanel = new Panel()
            {
                Location = new Point(1080, 40),
                Size = new Size(330, 700),
                BorderStyle = BorderStyle.FixedSingle
            };

            Button importButton = new Button()
            {
                Text = "Open",
                Location = new Point(10, 10),
                Width = 60
            };
            importButton.Click += ImportButton_Click;

            Button exportButton = new Button()
            {
                Text = "Export as...",
                Location = new Point(80, 10),
                Width = 100
            };
            exportButton.Click += ExportButton_Click;

            exportDropdown = new ComboBox()
            {
                Location = new Point(190, 10),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            exportDropdown.Items.AddRange(new string[]
            {
                ".dat (With UABEA Header)",
                ".dat (No Header)",
                ".bytes (With UABEA Header)",
                ".bytes (No Header)",
                ".JSON"
            });
            exportDropdown.SelectedIndex = 0;

            CheckBox snapCheck = new CheckBox()
            {
                Text = "Snap to Grid",
                Location = new Point(385, 12),
                AutoSize = true
            };
            snapCheck.CheckedChanged += (s, e) => pegboardPanel.SnapToGrid = snapCheck.Checked;

            CheckBox gridCheck = new CheckBox()
            {
                Text = "Show Grid",
                Location = new Point(480, 12),
                AutoSize = true
            };
            gridCheck.CheckedChanged += (s, e) => { pegboardPanel.ShowGrid = gridCheck.Checked; pegboardPanel.Invalidate(); };

            Button undoButton = new Button()
            {
                Text = "Undo (Ctrl+Z)",
                Location = new Point(560, 8),
                Width = 110
            };
            undoButton.Click += (s, e) => ExecuteUndo();

            Button redoButton = new Button()
            {
                Text = "Redo (Ctrl+Shift+Z)",
                Location = new Point(675, 8),
                Width = 130
            };
            redoButton.Click += (s, e) => ExecuteRedo();

            ContextMenuStrip canvasMenu = new ContextMenuStrip();
            ToolStripMenuItem toggleEnabledItem = new ToolStripMenuItem("Toggle Enabled");
            toggleEnabledItem.Click += ToggleEnabled_Click;
            canvasMenu.Opening += (s, e) => { if (pegboardPanel.SelectedPegs.Count == 0) e.Cancel = true; };
            canvasMenu.Items.Add(toggleEnabledItem);
            pegboardPanel.ContextMenuStrip = canvasMenu;

            this.Controls.Add(importButton);
            this.Controls.Add(exportDropdown);
            this.Controls.Add(exportButton);
            this.Controls.Add(snapCheck);
            this.Controls.Add(gridCheck);
            this.Controls.Add(undoButton);
            this.Controls.Add(redoButton);
            this.Controls.Add(pegboardPanel);
            this.Controls.Add(pegTreeView);
            this.Controls.Add(pegInfoPanel);
        }

        // ── Undo / Redo ──────────────────────────────────────────────────────

        internal void PushUndo(Action undo, Action redo)
        {
            _undoStack.Push((undo, redo));
            _redoStack.Clear();
        }

        private void ExecuteUndo()
        {
            if (_undoStack.Count == 0) return;
            var (undo, redo) = _undoStack.Pop();
            undo();
            _redoStack.Push((undo, redo));
        }

        private void ExecuteRedo()
        {
            if (_redoStack.Count == 0) return;
            var (undo, redo) = _redoStack.Pop();
            redo();
            _undoStack.Push((undo, redo));
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
            var node = new TreeNode(peg.name) { Tag = peg };
            if (peg.child != null)
                foreach (var child in peg.child)
                    node.Nodes.Add(CreateTreeNode(child));
            return node;
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
            if (node != null) node.Text = peg.name;
            if (peg == selectedPeg) UpdatePegInfoPanel(peg);
            pegboardPanel.Invalidate();
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void PegTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
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

        private void ToggleEnabled_Click(object sender, EventArgs e)
        {
            foreach (var peg in pegboardPanel.SelectedPegs)
                peg.enabled = !peg.enabled;
            if (selectedPeg != null && pegboardPanel.SelectedPegs.Contains(selectedPeg))
                UpdatePegInfoPanel(selectedPeg);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && !e.Shift && e.KeyCode == Keys.Z) { ExecuteUndo(); e.Handled = true; return; }
            if (e.Control && e.Shift && e.KeyCode == Keys.Z) { ExecuteRedo(); e.Handled = true; return; }

            if (e.KeyCode == Keys.Delete && pegboardPanel.SelectedPegs.Count > 0)
            {
                ExecuteDeletePegs(new List<PegboardParser.TransformData>(pegboardPanel.SelectedPegs), confirm: true);
                e.Handled = true;
            }
        }

        // ── Shared delete logic ──────────────────────────────────────────────

        internal void ExecuteDeletePegs(List<PegboardParser.TransformData> toDelete, bool confirm)
        {
            if (toDelete.Count == 0) return;

            if (confirm)
            {
                string msg = toDelete.Count == 1
                    ? $"Delete {toDelete[0].name}?"
                    : $"Delete {toDelete.Count} selected pegs?";
                if (MessageBox.Show(msg, "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            var records = toDelete
                .Select(peg =>
                {
                    var node = FindTreeNode(pegTreeView.Nodes, peg);
                    var parentPeg = node?.Parent != null ? (PegboardParser.TransformData)node.Parent.Tag : null;
                    int dataIdx = parentPeg?.child?.IndexOf(peg) ?? pegboardData.transforms.IndexOf(peg);
                    return (peg, parentPeg, dataIdx, nodeIdx: node?.Index ?? 0, parentNode: node?.Parent);
                })
                .OrderByDescending(r => r.dataIdx)
                .ToList();

            foreach (var (peg, parentPeg, _, _, _) in records)
            {
                parentPeg?.child?.Remove(peg);
                DeletePeg(peg);
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
                            pegboardData.transforms.Insert(Math.Min(dataIdx, pegboardData.transforms.Count), peg);
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
                        DeletePeg(peg);
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

            if (pegboardData?.transforms != null)
            {
                foreach (var transform in pegboardData.transforms)
                {
                    pegboardPanel.Pegs.Add(transform);
                    DrawTransform(transform, null);
                }
            }
            pegboardPanel.Invalidate();
        }

        internal TreeNode DrawTransform(PegboardParser.TransformData transform, TreeNode parentNode)
        {
            TreeNode pegNode = new TreeNode(transform.name) { Tag = transform };

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
