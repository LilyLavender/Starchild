using OdinSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Starchild
{
    partial class MainForm
    {
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

        private static byte[] ReplaceBytes(byte[] original, byte[] search, byte[] replace)
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

        private static int FindBytePattern(List<byte> data, byte[] pattern, int startIndex)
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
