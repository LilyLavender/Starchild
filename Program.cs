using System;
using System.Collections.Generic;
using System.Drawing;
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
            this.Controls.Add(pegboardPanel);
            this.Controls.Add(pegTreeView);
            this.Controls.Add(pegInfoPanel);
        }

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

        private void OnPegMoved(PegboardParser.TransformData peg)
        {
            if (peg == selectedPeg)
            {
                posXBox.Text = peg.posX.ToString();
                posYBox.Text = peg.posY.ToString();
            }
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
            if (e.KeyCode == Keys.Delete && pegboardPanel.SelectedPegs.Count > 0)
            {
                var toDelete = new List<PegboardParser.TransformData>(pegboardPanel.SelectedPegs);
                string msg = toDelete.Count == 1
                    ? $"Delete {toDelete[0].name}?"
                    : $"Delete {toDelete.Count} selected pegs?";

                if (MessageBox.Show(msg, "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                foreach (var peg in toDelete)
                {
                    TreeNode node = FindTreeNode(pegTreeView.Nodes, peg);
                    if (node?.Parent != null)
                        ((PegboardParser.TransformData)node.Parent.Tag).child?.Remove(peg);
                    DeletePeg(peg);
                    node?.Remove();
                }

                selectedPeg = null;
                pegTreeView.SelectedNode = null;
                pegboardPanel.HighlightedPeg = null;
                pegboardPanel.ClearSelection();
                pegInfoPanel.Controls.Clear();
                e.Handled = true;
            }
        }

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

        private void DrawTransform(PegboardParser.TransformData transform, TreeNode parentNode)
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
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
