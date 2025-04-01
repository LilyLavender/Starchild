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
        private Panel pegboardPanel;
        private PegboardParser.PegboardData pegboardData;
        private string pegboardName = "";
        private ComboBox exportDropdown;

        public MainForm()
        {
            this.Text = "Starchild";
            this.Size = new Size(800, 600);

            pegboardPanel = new Panel()
            {
                BackColor = Color.LightGray,
                AutoScroll = true,
                Location = new Point(10, 40),
                Size = new Size(800, 600)
            };

            Button importButton = new Button() { 
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
            exportDropdown.Items.AddRange([
                ".dat (With UABEA Header)",
                ".dat (No Header)",
                ".bytes (With UABEA Header)",
                ".bytes (No Header)",
                ".JSON"
            ]);
            exportDropdown.SelectedIndex = 0;

            this.Controls.Add(importButton);
            this.Controls.Add(exportDropdown);
            this.Controls.Add(exportButton);
            this.Controls.Add(pegboardPanel);
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
                byte[] serializedData;

                using (MemoryStream dataStream = new MemoryStream())
                {
                    SerializationContext context = new SerializationContext();
                    context.Binder = new MscorlibSerializationBinder();
                    SerializationUtility.SerializeValue(pegboardData, dataStream, 
                        selectedFormat.Contains("JSON") ? DataFormat.JSON : DataFormat.Binary, context);
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
                    AlignTo4(writer, serializedData.Length);
                    File.WriteAllBytes(saveFileDialog.FileName, finalStream.ToArray());
                }
            }
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
            if (pegboardData?.transforms != null)
            {
                foreach (var transform in pegboardData.transforms)
                {
                    if (transform.enabled)
                        DrawTransform(transform);
                }
            }
        }

        private void DrawTransform(PegboardParser.TransformData transform)
        {
            PictureBox peg = new PictureBox()
            {
                Size = new Size((int)(15 * transform.scaleX), (int)(15 * transform.scaleY)),
                Location = new Point((int)(50 * transform.posX + 400), (int)(-50 * transform.posY)),
                BackColor = Color.Transparent
            };

            peg.Paint += (sender, e) =>
            {
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(brush, 0, 0, peg.Width, peg.Height);
                }
            };

            peg.Region = new Region(new System.Drawing.Drawing2D.GraphicsPath());
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(0, 0, peg.Width, peg.Height);
                peg.Region = new Region(path);
            }

            pegboardPanel.Controls.Add(peg);

            if (transform.child != null)
            {
                foreach (var child in transform.child)
                {
                    if (child.enabled)
                        DrawTransform(child);
                }
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
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
    }
}