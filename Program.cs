using OdinSerializer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace Starchild
{
    public class MainForm : Form
    {
        private PegboardPanel pegboardPanel;
        private PegboardParser.PegboardData pegboardData;
        private string pegboardName = "";
        private ComboBox exportDropdown;
        private TreeView pegTreeView;
        private Dictionary<TreeNode, PictureBox> pegMap = new Dictionary<TreeNode, PictureBox>();
        private TreeNode selectedNode = null;

        private Panel pegInfoPanel;
        private TextBox nameBox, posXBox, posYBox, scaleXBox, scaleYBox, typeBox;
        private PegboardParser.TransformData selectedPeg;

        public MainForm()
        {
            this.Text = "Starchild";
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

        private void ImportButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Data Files|*.dat;*.bytes;*.json|All Files|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                byte[] fileBytes = File.ReadAllBytes(filePath);

                using (MemoryStream ms = new MemoryStream(fileBytes))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    if (filePath.EndsWith(".dat") || filePath.EndsWith(".bytes"))
                    {
                        if (HasUABEAHeader(reader))
                        {
                            pegboardName = ReadString(reader);
                            reader.ReadInt32();
                        }
                        else
                        {
                            pegboardName = "";
                            ms.Position = 0;
                        }
                        pegboardData = SerializationUtility.DeserializeValue<PegboardParser.PegboardData>(ms, DataFormat.Binary);
                    }
                    else if (filePath.EndsWith(".json"))
                    {
                        pegboardData = SerializationUtility.DeserializeValue<PegboardParser.PegboardData>(ms, DataFormat.JSON);
                    }
                }
                pegboardPanel.Pegs.Clear();
                DrawPegboard();
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            string selectedFormat = exportDropdown.SelectedItem.ToString();
            string filter = exportDropdown.SelectedIndex switch
            {
                0 => "Dat Files|*.dat|Bytes Files|*.bytes|All Files|*.*",
                1 => "Dat Files|*.dat|Bytes Files|*.bytes|All Files|*.*",
                2 => "Bytes Files|*.bytes|Dat Files|*.dat|All Files|*.*",
                3 => "Bytes Files|*.bytes|Dat Files|*.dat|All Files|*.*",
                4 => "JSON Files|*.json|All Files|*.*",
                _ => "All Files|*.*"
            };
            saveFileDialog.Filter = filter;
            saveFileDialog.FileName = pegboardData.name;
            
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                bool includeHeader = selectedFormat.Contains("(With UABEA Header)");
                bool serializeAsJson = selectedFormat.Contains("JSON");
                byte[] serializedData;

                using (MemoryStream dataStream = new MemoryStream())
                {
                    SerializationContext context = new SerializationContext();
                    context.Binder = new MscorlibSerializationBinder();
                    SerializationUtility.SerializeValue(pegboardData, dataStream, 
                        serializeAsJson ? DataFormat.JSON : DataFormat.Binary, context);
                    serializedData = dataStream.ToArray();
                }

                // manually fix colors
                byte[] oldBytes = new byte[] { 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x67, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x62, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00 };
                byte[] newBytes = new byte[] { 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F };
                serializedData = ReplaceBytes(serializedData, oldBytes, newBytes);

                using (MemoryStream finalStream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(finalStream))
                {
                    if (includeHeader)
                    {
                        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(pegboardData.name);
                        writer.Write(nameBytes.Length);
                        writer.Write(nameBytes);
                        AlignTo4(writer, nameBytes.Length);
                        writer.Write(serializedData.Length);
                    }

                    writer.Write(serializedData);
                    if (!serializeAsJson) { AlignTo4(writer, serializedData.Length); }
                    File.WriteAllBytes(saveFileDialog.FileName, finalStream.ToArray());
                }
            }
        }

        private void PegTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            selectedPeg = (PegboardParser.TransformData)e.Node.Tag;
            pegboardPanel.HighlightedPeg = selectedPeg;
            pegboardPanel.Invalidate();
            UpdatePegInfoPanel(selectedPeg);
        }

        private void UpdatePegInfoPanel(PegboardParser.TransformData selectedPeg)
        {
            pegInfoPanel.Controls.Clear();

            int yOffset = 10;
            int textBoxWidth = 150;
            int labelWidth = 150;

            TextBox CreateTextBox(string labelText, string value)
            {
                Label label = new Label() { Text = labelText, Location = new Point(10, yOffset), Width = labelWidth };
                TextBox textBox = new TextBox() { Location = new Point(labelWidth + 10, yOffset), Width = textBoxWidth, Text = value };
                pegInfoPanel.Controls.Add(label);
                pegInfoPanel.Controls.Add(textBox);
                yOffset += 30;
                return textBox;
            }

            nameBox = CreateTextBox("name", selectedPeg.name);
            posXBox = CreateTextBox("posX", selectedPeg.posX.ToString());
            posYBox = CreateTextBox("posY", selectedPeg.posY.ToString());
            scaleXBox = CreateTextBox("scaleX", selectedPeg.scaleX.ToString());
            scaleYBox = CreateTextBox("scaleY", selectedPeg.scaleY.ToString());

            Label enabledLabel = new Label() { Text = "enabled", Location = new Point(10, yOffset), Width = labelWidth };
            CheckBox enabledBox = new CheckBox() { Location = new Point(labelWidth + 10, yOffset), Checked = selectedPeg.enabled };
            pegInfoPanel.Controls.Add(enabledLabel);
            pegInfoPanel.Controls.Add(enabledBox);
            yOffset += 30;

            // Additional properties
            if (selectedPeg.prefab != null) {
                Type pegType = selectedPeg.prefab.GetType();
                var fields = pegType.GetFields();
                var properties = pegType.GetProperties();

                foreach (var field in fields)
                {
                    if (field.Name == "color") continue;

                    object value = field.GetValue(selectedPeg.prefab);
                    Label label = new Label()
                    {
                        Text = field.Name,
                        Location = new Point(10, yOffset),
                        Width = labelWidth
                    };
                    TextBox textBox = new TextBox()
                    {
                        Location = new Point(labelWidth + 10, yOffset),
                        Width = textBoxWidth,
                        Text = value?.ToString()
                    };
                    textBox.Tag = field;
                    pegInfoPanel.Controls.Add(label);
                    pegInfoPanel.Controls.Add(textBox);
                    yOffset += 30;
                }

                foreach (var property in properties)
                {
                    object value = property.GetValue(selectedPeg.prefab);
                    Label label = new Label() { Text = property.Name + ":", Location = new Point(10, yOffset) };
                    TextBox textBox = new TextBox()
                    {
                        Location = new Point(110, yOffset),
                        Width = 150,
                        Text = value?.ToString()
                    };
                    textBox.Tag = property;
                    pegInfoPanel.Controls.Add(label);
                    pegInfoPanel.Controls.Add(textBox);
                    yOffset += 30;
                }
            }
            
            Button saveButton = new Button() { Text = "Apply Changes", Location = new Point(10, yOffset), Width = 300 };
            saveButton.Click += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            pegInfoPanel.Controls.Add(saveButton);
        }

        private void SaveButton_Click(PegboardParser.TransformData selectedPeg, CheckBox enabledBox)
        {
            if (selectedPeg == null) return;

            selectedPeg.name = nameBox.Text;
            if (float.TryParse(posXBox.Text, out float posX)) selectedPeg.posX = posX;
            if (float.TryParse(posYBox.Text, out float posY)) selectedPeg.posY = posY;
            if (float.TryParse(scaleXBox.Text, out float scaleX)) selectedPeg.scaleX = scaleX;
            if (float.TryParse(scaleYBox.Text, out float scaleY)) selectedPeg.scaleY = scaleY;
            selectedPeg.enabled = enabledBox.Checked;

            var component = selectedPeg.prefab;
            if (component != null)
            {
                foreach (var field in component.GetType().GetFields())
                {
                    if (pegInfoPanel.Controls.ContainsKey(field.Name))
                    {
                        Control control = pegInfoPanel.Controls[field.Name];

                        try
                        {
                            if (field.FieldType.IsEnum)
                            {
                                field.SetValue(component, Enum.Parse(field.FieldType, control.Text));
                            }
                            else if (field.FieldType == typeof(float))
                            {
                                field.SetValue(component, float.Parse(control.Text));
                            }
                            else if (field.FieldType == typeof(int))
                            {
                                field.SetValue(component, int.Parse(control.Text));
                            }
                            else if (field.FieldType == typeof(bool) && control is CheckBox checkBox)
                            {
                                field.SetValue(component, checkBox.Checked);
                            }
                            else
                            {
                                field.SetValue(component, Convert.ChangeType(control.Text, field.FieldType));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error setting {field.Name}: {ex.Message}");
                        }
                    }
                }
            }

            if (pegTreeView.SelectedNode != null)
            {
                pegTreeView.SelectedNode.Text = selectedPeg.name;
                pegTreeView.SelectedNode.Tag = selectedPeg;
            }

            pegboardPanel.Invalidate();
        }

        private bool HasUABEAHeader(BinaryReader reader)
        {
            // Assumes pegboard does not have a name 2 characters in length
            long startPos = reader.BaseStream.Position;
            byte firstByte = reader.ReadByte();
            reader.BaseStream.Position = startPos;
            return firstByte > 2;
        }

        private string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(length);
            string str = System.Text.Encoding.UTF8.GetString(bytes);
            AlignTo4(reader, length);
            return str;
        }

        private void AlignTo4(BinaryReader reader, int length)
        {
            int padding = (4 - (length % 4)) % 4;
            reader.BaseStream.Position += padding;
        }

        private void AlignTo4(BinaryWriter writer, int length)
        {
            int padding = (4 - (length % 4)) % 4;
            writer.Write(new byte[padding]);
        }

        private void DrawPegboard()
        {
            pegboardPanel.Controls.Clear();
            pegTreeView.Nodes.Clear();
            pegMap.Clear();

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
            TreeNode pegNode = new TreeNode($"{transform.name}")
            {
                Tag = transform
            };

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

        static byte[] ReplaceBytes(byte[] original, byte[] search, byte[] replace)
        {
            List<byte> result = new List<byte>(original);
            int index = 0;

            while ((index = FindBytePattern(result, search, index)) != -1)
            {
                result.RemoveRange(index, search.Length);
                result.InsertRange(index, replace);
                index += replace.Length;
            }

            return result.ToArray();
        }

        static int FindBytePattern(List<byte> data, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= data.Count - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}