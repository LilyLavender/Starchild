using OdinSerializer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Starchild
{
    partial class MainForm
    {
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

            if (selectedPeg.prefab != null)
            {
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
                        Text = value?.ToString(),
                        Name = field.Name
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
            yOffset += 30;

            Button duplicateButton = new Button() { Text = "Duplicate", Location = new Point(10, yOffset), Width = 145 };
            duplicateButton.Click += DuplicateButton_Click;
            pegInfoPanel.Controls.Add(duplicateButton);

            Button deleteButton = new Button() { Text = "Delete", Location = new Point(165, yOffset), Width = 145 };
            deleteButton.Click += DeleteButton_Click;
            pegInfoPanel.Controls.Add(deleteButton);
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
                                field.SetValue(component, Enum.Parse(field.FieldType, control.Text));
                            else if (field.FieldType == typeof(float))
                                field.SetValue(component, float.Parse(control.Text));
                            else if (field.FieldType == typeof(int))
                                field.SetValue(component, int.Parse(control.Text));
                            else if (field.FieldType == typeof(bool) && control is CheckBox checkBox)
                                field.SetValue(component, checkBox.Checked);
                            else
                                field.SetValue(component, Convert.ChangeType(control.Text, field.FieldType));

                            control.BackColor = SystemColors.Window;
                        }
                        catch
                        {
                            control.BackColor = Color.LightCoral;
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

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (selectedPeg == null || pegTreeView.SelectedNode == null) return;

            DialogResult result = MessageBox.Show($"Are you sure you want to delete {selectedPeg.name}?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (pegTreeView.SelectedNode.Parent != null)
                {
                    var parentPeg = (PegboardParser.TransformData)pegTreeView.SelectedNode.Parent.Tag;
                    parentPeg.child?.Remove(selectedPeg);
                }
                DeletePeg(selectedPeg);
                pegTreeView.Nodes.Remove(pegTreeView.SelectedNode);

                selectedPeg = null;
                pegTreeView.SelectedNode = null;
                pegboardPanel.HighlightedPeg = null;
                pegboardPanel.Invalidate();
                pegInfoPanel.Controls.Clear();
            }
        }

        private void DuplicateButton_Click(object sender, EventArgs e)
        {
            if (selectedPeg == null || pegTreeView.SelectedNode == null) return;

            var copy = DeepCopy(selectedPeg);
            copy.name = selectedPeg.name + "_copy";
            copy.posX += 0.5f;
            copy.posY += 0.5f;

            TreeNode parentNode = pegTreeView.SelectedNode.Parent;
            TreeNode newNode = new TreeNode(copy.name) { Tag = copy };

            if (parentNode != null)
            {
                var parentPeg = (PegboardParser.TransformData)parentNode.Tag;
                parentPeg.child ??= new List<PegboardParser.TransformData>();
                parentPeg.child.Add(copy);
                parentNode.Nodes.Add(newNode);
            }
            else
            {
                pegboardData.transforms.Add(copy);
                pegTreeView.Nodes.Add(newNode);
            }

            pegboardPanel.Pegs.Add(copy);
            pegboardPanel.Invalidate();
        }

        private PegboardParser.TransformData DeepCopy(PegboardParser.TransformData original)
        {
            byte[] bytes = SerializationUtility.SerializeValue(original, DataFormat.Binary);
            return SerializationUtility.DeserializeValue<PegboardParser.TransformData>(bytes, DataFormat.Binary);
        }

        private void DeletePeg(PegboardParser.TransformData peg)
        {
            pegboardData.transforms.Remove(peg);
            pegboardPanel.Pegs.Remove(peg);
            if (peg.child != null)
            {
                foreach (var child in peg.child)
                    DeletePeg(child);
            }
        }
    }
}
