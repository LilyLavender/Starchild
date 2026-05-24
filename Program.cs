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

            this.Controls.Add(importButton);
            this.Controls.Add(exportDropdown);
            this.Controls.Add(exportButton);
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
