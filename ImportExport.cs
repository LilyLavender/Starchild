using OdinSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Starchild
{
    partial class MainForm
    {
        private void ImportButton_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog()
            {
                Filter = "Data Files|*.dat;*.bytes;*.json|All Files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            foreach (var path in dlg.FileNames)
            {
                var session = LoadSessionFromFile(path);
                if (session != null) AddSession(session);
            }
        }

        private BoardSession LoadSessionFromFile(string filePath)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                PegboardParser.PegboardData data;
                string pegboardName = "";

                using var ms = new MemoryStream(fileBytes);
                using var reader = new BinaryReader(ms);

                if (filePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                {
                    if (HasUABEAHeader(reader))
                    {
                        pegboardName = ReadString(reader);
                        reader.ReadInt32();
                    }
                    else
                    {
                        ms.Position = 0;
                    }
                    var ctx = new DeserializationContext { Config = new SerializationConfig { SerializationPolicy = SerializationPolicies.Everything } };
                    data = SerializationUtility.DeserializeValue<PegboardParser.PegboardData>(ms, DataFormat.Binary, ctx);
                }
                else
                {
                    var ctx = new DeserializationContext { Config = new SerializationConfig { SerializationPolicy = SerializationPolicies.Everything } };
                    data = SerializationUtility.DeserializeValue<PegboardParser.PegboardData>(ms, DataFormat.JSON, ctx);
                }

                return new BoardSession
                {
                    Data = data,
                    PegboardName = pegboardName,
                    FilePath = filePath,
                    TabLabel = Path.GetFileName(filePath)
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open {Path.GetFileName(filePath)}:\n{ex.Message}", "Open Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private (bool json, bool header, string ext) GetExportFormat()
        {
            bool json = _formatJson.Checked;
            bool header = !json && _withHeader.Checked;
            string ext = json ? ".json" : (_formatBytes.Checked ? ".bytes" : ".dat");
            return (json, header, ext);
        }

        private void SaveActive()
        {
            if (_active == null) return;
            if (_active.FilePath == null) { ExportButton_Click(null, EventArgs.Empty); return; }

            string ext = Path.GetExtension(_active.FilePath).ToLowerInvariant();
            bool json = ext == ".json";
            bool header = !json && _withHeader.Checked;

            try
            {
                SerializeAndWrite(_active, json, header, _active.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (_active == null) return;

            var (json, header, ext) = GetExportFormat();
            var dlg = new SaveFileDialog()
            {
                Filter = ext switch
                {
                    ".dat" => "Dat Files|*.dat|All Files|*.*",
                    ".bytes" => "Bytes Files|*.bytes|All Files|*.*",
                    _ => "JSON Files|*.json|All Files|*.*"
                },
                FileName = _active.Data.name
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;
            SerializeAndWrite(_active, json, header, dlg.FileName);
        }

        private void BatchExportButton_Click(object sender, EventArgs e)
        {
            if (_sessions.Count == 0)
            {
                MessageBox.Show("No boards are open.", "Export All", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (json, header, ext) = GetExportFormat();
            var dirDlg = new FolderBrowserDialog() { Description = "Select output directory" };
            if (dirDlg.ShowDialog() != DialogResult.OK) return;
            string outDir = dirDlg.SelectedPath;

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int succeeded = 0;
            var errors = new List<string>();

            foreach (var session in _sessions)
            {
                string baseName = Path.GetFileNameWithoutExtension(
                    session.FilePath ?? session.Data.name ?? "board");
                string candidate = baseName + ext;
                if (!usedNames.Add(candidate))
                {
                    int suffix = 2;
                    while (!usedNames.Add($"{baseName}_{suffix}{ext}")) suffix++;
                    candidate = $"{baseName}_{suffix}{ext}";
                }

                try
                {
                    SerializeAndWrite(session, json, header, Path.Combine(outDir, candidate));
                    succeeded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{candidate}: {ex.Message}");
                }
            }

            string summary = $"Exported {succeeded}/{_sessions.Count} board(s) to:\n{outDir}";
            if (errors.Count > 0)
                summary += "\n\nErrors:\n" + string.Join("\n", errors);
            MessageBox.Show(summary, "Export All", MessageBoxButtons.OK,
                errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void SerializeAndWrite(BoardSession session, bool serializeAsJson, bool includeHeader, string outputPath)
        {
            byte[] serializedData;

            using (var dataStream = new MemoryStream())
            {
                var context = new SerializationContext { Binder = new MscorlibSerializationBinder(), Config = new SerializationConfig { SerializationPolicy = SerializationPolicies.Everything } };
                SerializationUtility.SerializeValue(session.Data, dataStream,
                    serializeAsJson ? DataFormat.JSON : DataFormat.Binary, context);
                serializedData = dataStream.ToArray();
            }

            byte[] oldBytes = new byte[] { 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x67, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x62, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] newBytes = new byte[] { 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F, 0x20, 0x00, 0x00, 0x80, 0x3F };
            serializedData = ReplaceBytes(serializedData, oldBytes, newBytes);

            using var finalStream = new MemoryStream();
            using var writer = new BinaryWriter(finalStream);

            if (includeHeader)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(session.Data.name);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);
                AlignTo4(writer, nameBytes.Length);
                writer.Write(serializedData.Length);
            }

            writer.Write(serializedData);
            if (!serializeAsJson) AlignTo4(writer, serializedData.Length);
            File.WriteAllBytes(outputPath, finalStream.ToArray());
        }

        private bool HasUABEAHeader(BinaryReader reader)
        {
            long startPos = reader.BaseStream.Position;
            byte firstByte = reader.ReadByte();
            reader.BaseStream.Position = startPos;
            return firstByte > 2;
        }

        private string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(length);
            string str = Encoding.UTF8.GetString(bytes);
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
                    if (data[i + j] != pattern[j]) { found = false; break; }
                }
                if (found) return i;
            }
            return -1;
        }
    }
}
