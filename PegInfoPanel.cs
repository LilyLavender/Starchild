using OdinSerializer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using UnityEngine.U2D;

namespace Starchild
{
    partial class MainForm
    {
        private sealed class ComponentFieldRef
        {
            public readonly PegboardParser.Component Component;
            public readonly FieldInfo Field;
            public ComponentFieldRef(PegboardParser.Component c, FieldInfo f) { Component = c; Field = f; }
        }

        private sealed class SplinePointRef
        {
            public readonly Spline Spline;
            public readonly int PointIndex;
            public readonly string FieldName;
            public SplinePointRef(Spline s, int i, string f) { Spline = s; PointIndex = i; FieldName = f; }
        }

        private static readonly FieldInfo s_splineMcpField =
            typeof(Spline).GetField("m_ControlPoints", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_splineModeField =
            typeof(SplineControlPoint).GetField("mode");

        private List<PegboardParser.TransformData> GetTargets(PegboardParser.TransformData primary)
        {
            if (pegboardPanel.SelectedPegs.Count > 1 && pegboardPanel.SelectedPegs.Contains(primary))
                return new List<PegboardParser.TransformData>(pegboardPanel.SelectedPegs);
            return new List<PegboardParser.TransformData> { primary };
        }

        private void UpdatePegInfoPanel(PegboardParser.TransformData selectedPeg)
        {
            pegInfoPanel.Controls.Clear();

            int selCount = pegboardPanel.SelectedPegs.Count;
            if (selCount > 1)
            {
                pegInfoPanel.Controls.Add(new Label()
                {
                    Text = $"{selCount} pegs selected — changes apply to all",
                    Location = new Point(10, 10),
                    Width = 310,
                    ForeColor = System.Drawing.Color.DarkBlue
                });
            }

            int yOffset = selCount > 1 ? 40 : 10;
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

            TextBox SplineTextBox(string labelText, string fieldName, string value, Spline spline, int ptIdx)
            {
                var tb = CreateTextBox(labelText, value);
                tb.Tag = new SplinePointRef(spline, ptIdx, fieldName);
                return tb;
            }

            bool multi = selCount > 1;

            nameBox = CreateTextBox("name", selectedPeg.name);
            posXBox = CreateTextBox("posX", selectedPeg.posX.ToString());
            posYBox = CreateTextBox("posY", selectedPeg.posY.ToString());
            scaleXBox = CreateTextBox("scaleX", selectedPeg.scaleX.ToString());
            scaleYBox = CreateTextBox("scaleY", selectedPeg.scaleY.ToString());

            if (multi)
            {
                nameBox.Enabled = false;
                posXBox.Enabled = false;
                posYBox.Enabled = false;
            }

            Label enabledLabel = new Label() { Text = "enabled", Location = new Point(10, yOffset), Width = labelWidth };
            CheckBox enabledBox = new CheckBox() { Location = new Point(labelWidth + 10, yOffset), Checked = selectedPeg.enabled };
            pegInfoPanel.Controls.Add(enabledLabel);
            pegInfoPanel.Controls.Add(enabledBox);
            yOffset += 30;

            if (!multi && selectedPeg.prefab != null)
            {
                Label typeLabel = new Label() { Text = "Peg type:", Location = new Point(10, yOffset), Width = labelWidth };
                var typeChangeCombo = new ComboBox()
                {
                    Location = new Point(labelWidth + 10, yOffset),
                    Width = textBoxWidth - 56,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                typeChangeCombo.Items.AddRange(new[]
                {
                    "peg_regular", "peg_bomb", "indestructible_peg", "bouncer_peg",
                    "obstacle_black_hole", "peg_long", "peg_slime_only",
                    "obstacle_bouncer", "obstacle_bouncer_mines", "parent_firework_movement"
                });
                typeChangeCombo.SelectedItem = selectedPeg.prefab.componentType;
                if (typeChangeCombo.SelectedIndex < 0) typeChangeCombo.SelectedIndex = 0;
                Button changeTypeBtn = new Button() { Text = "Change", Location = new Point(labelWidth + textBoxWidth - 42, yOffset - 1), Width = 55 };
                changeTypeBtn.Click += (s, ev) =>
                {
                    string chosen = typeChangeCombo.SelectedItem?.ToString();
                    if (chosen == null || chosen == selectedPeg.prefab?.componentType) return;
                    var oldPrefab = selectedPeg.prefab;
                    var newPrefab = CreatePrefab(chosen);
                    if (newPrefab == null) return;
                    selectedPeg.prefab = newPrefab;
                    PushUndo(
                        undo: () => { selectedPeg.prefab = oldPrefab; RefreshPegUI(selectedPeg); },
                        redo: () => { selectedPeg.prefab = newPrefab; RefreshPegUI(selectedPeg); }
                    );
                    UpdatePegInfoPanel(selectedPeg);
                    pegboardPanel.Invalidate();
                };
                pegInfoPanel.Controls.Add(typeLabel);
                pegInfoPanel.Controls.Add(typeChangeCombo);
                pegInfoPanel.Controls.Add(changeTypeBtn);
                yOffset += 32;
            }

            if (selectedPeg.prefab != null)
            {
                Type pegType = selectedPeg.prefab.GetType();
                var fields = pegType.GetFields();
                var properties = pegType.GetProperties();

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Spline)) continue;
                    object value = field.GetValue(selectedPeg.prefab);
                    Label label = new Label() { Text = field.Name, Location = new Point(10, yOffset), Width = labelWidth };

                    if (field.Name == "pegType" && selectedPeg.prefab is PegboardParser.RegularPegData)
                    {
                        object rawVal = field.GetValue(selectedPeg.prefab);
                        int intVal = rawVal == null ? 0 : field.FieldType == typeof(int) ? (int)rawVal : Convert.ToInt32(rawVal);
                        var combo = new ComboBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Width = textBoxWidth,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Name = field.Name,
                        };
                        combo.Tag = field;
                        combo.Items.AddRange(new[] { "0 (Unknown)", "1 (Normal)", "128 (Dull)" });
                        combo.SelectedItem = intVal switch { 1 => "1 (Normal)", 128 => "128 (Dull)", _ => "0 (Unknown)" };
                        pegInfoPanel.Controls.Add(label);
                        pegInfoPanel.Controls.Add(combo);
                        yOffset += 30;
                        continue;
                    }

                    if (field.Name == "slimeType" && selectedPeg.prefab is PegboardParser.RegularPegData)
                    {
                        object rawVal = field.GetValue(selectedPeg.prefab);
                        int intVal = rawVal == null ? 0 : field.FieldType == typeof(int) ? (int)rawVal : Convert.ToInt32(rawVal);
                        var combo = new ComboBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Width = textBoxWidth,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Name = field.Name,
                        };
                        combo.Tag = field;
                        combo.Items.AddRange(new[] { "0 (None)", "3 (Spider)" });
                        combo.SelectedItem = intVal == 3 ? "3 (Spider)" : "0 (None)";
                        pegInfoPanel.Controls.Add(label);
                        pegInfoPanel.Controls.Add(combo);
                        yOffset += 30;
                        continue;
                    }

                    if (field.Name == "snapData" && selectedPeg.prefab is PegboardParser.LongPegData)
                    {
                        object rawVal = field.GetValue(selectedPeg.prefab);
                        int intVal = rawVal == null ? 0 : field.FieldType == typeof(int) ? (int)rawVal : Convert.ToInt32(rawVal);
                        var combo = new ComboBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Width = textBoxWidth,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Name = field.Name,
                        };
                        combo.Tag = field;
                        combo.Items.AddRange(new[] { "0 (Default)", "1 (Extra)", "2 (BothSides)", "3 (BothSidesExtraLong)" });
                        combo.SelectedItem = intVal switch { 1 => "1 (Extra)", 2 => "2 (BothSides)", 3 => "3 (BothSidesExtraLong)", _ => "0 (Default)" };
                        pegInfoPanel.Controls.Add(label);
                        pegInfoPanel.Controls.Add(combo);
                        yOffset += 30;
                        continue;
                    }

                    if (field.Name == "color")
                    {
                        Type colorType = value?.GetType();
                        float r = colorType?.GetField("r")?.GetValue(value) is float rf ? rf : 1f;
                        float g = colorType?.GetField("g")?.GetValue(value) is float gf ? gf : 1f;
                        float b = colorType?.GetField("b")?.GetValue(value) is float bf ? bf : 1f;
                        float a = colorType?.GetField("a")?.GetValue(value) is float af ? af : 1f;
                        var drawColor = Color.FromArgb(
                            (int)Math.Clamp(a * 255, 0, 255),
                            (int)Math.Clamp(r * 255, 0, 255),
                            (int)Math.Clamp(g * 255, 0, 255),
                            (int)Math.Clamp(b * 255, 0, 255));
                        Button colorBtn = new Button()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Width = textBoxWidth,
                            BackColor = drawColor,
                            Name = "color",
                            Tag = field
                        };
                        colorBtn.Click += (s, ev) =>
                        {
                            var dlg = new ColorDialog() { Color = colorBtn.BackColor, FullOpen = true };
                            if (dlg.ShowDialog() == DialogResult.OK)
                                colorBtn.BackColor = dlg.Color;
                        };
                        pegInfoPanel.Controls.Add(label);
                        pegInfoPanel.Controls.Add(colorBtn);
                        yOffset += 30;
                        continue;
                    }

                    Control inputCtrl;
                    if (field.FieldType == typeof(bool))
                    {
                        inputCtrl = new CheckBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Checked = value is bool bv && bv,
                            Name = field.Name
                        };
                    }
                    else
                    {
                        inputCtrl = new TextBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Width = textBoxWidth,
                            Text = value?.ToString(),
                            Name = field.Name
                        };
                    }
                    inputCtrl.Tag = field;
                    pegInfoPanel.Controls.Add(label);
                    pegInfoPanel.Controls.Add(inputCtrl);
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

            if (!multi && selectedPeg.prefab is PegboardParser.LongPegData longPeg && longPeg.spline != null)
            {
                var cpList = s_splineMcpField?.GetValue(longPeg.spline) as System.Collections.Generic.List<SplineControlPoint>;
                if (cpList != null)
                {
                    pegInfoPanel.Controls.Add(new Label()
                    {
                        Text = $"Spline Control Points ({cpList.Count}):",
                        Location = new Point(10, yOffset),
                        Width = 300,
                        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
                    });
                    yOffset += 25;

                    for (int si = 0; si < cpList.Count; si++)
                    {
                        var cp = cpList[si];
                        int ptIdx = si;
                        var spl = longPeg.spline;

                        pegInfoPanel.Controls.Add(new Label()
                        {
                            Text = $"Point {si}:",
                            Location = new Point(10, yOffset),
                            Width = 215,
                            ForeColor = Color.DarkSlateGray
                        });
                        {
                            int removedIdx = ptIdx;
                            SplineControlPoint removedVal = cp;
                            var delPtBtn = new Button() { Text = "Delete", Location = new Point(225, yOffset), Width = 85 };
                            delPtBtn.Click += (s2, ev2) =>
                            {
                                var cpl = s_splineMcpField?.GetValue(spl) as System.Collections.Generic.List<SplineControlPoint>;
                                if (cpl == null || removedIdx >= cpl.Count) return;
                                cpl.RemoveAt(removedIdx);
                                PushUndo(
                                    undo: () => { cpl.Insert(removedIdx, removedVal); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); },
                                    redo: () => { if (removedIdx < cpl.Count + 1) cpl.RemoveAt(removedIdx); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); }
                                );
                                UpdatePegInfoPanel(selectedPeg);
                                pegboardPanel.Invalidate();
                            };
                            pegInfoPanel.Controls.Add(delPtBtn);
                        }
                        yOffset += 28;

                        SplineTextBox("pos.x",       "pos.x",       cp.position.x.ToString(),     spl, ptIdx);
                        SplineTextBox("pos.y",       "pos.y",       cp.position.y.ToString(),     spl, ptIdx);
                        SplineTextBox("lt.x",        "lt.x",        cp.leftTangent.x.ToString(),  spl, ptIdx);
                        SplineTextBox("lt.y",        "lt.y",        cp.leftTangent.y.ToString(),  spl, ptIdx);
                        SplineTextBox("rt.x",        "rt.x",        cp.rightTangent.x.ToString(), spl, ptIdx);
                        SplineTextBox("rt.y",        "rt.y",        cp.rightTangent.y.ToString(), spl, ptIdx);
                        SplineTextBox("height",      "height",      cp.height.ToString(),         spl, ptIdx);
                        SplineTextBox("bevelCutoff", "bevelCutoff", cp.bevelCutoff.ToString(),    spl, ptIdx);
                        SplineTextBox("bevelSize",   "bevelSize",   cp.bevelSize.ToString(),      spl, ptIdx);
                        SplineTextBox("spriteIndex", "spriteIndex", cp.spriteIndex.ToString(),    spl, ptIdx);

                        if (s_splineModeField != null)
                        {
                            var modeType = s_splineModeField.FieldType;
                            string modeName = s_splineModeField.GetValue((object)cp)?.ToString() ?? "0";
                            Label modeLabel = new Label() { Text = "mode", Location = new Point(10, yOffset), Width = labelWidth };
                            var modeBox = new ComboBox()
                            {
                                Location = new Point(labelWidth + 10, yOffset),
                                Width = textBoxWidth,
                                DropDownStyle = ComboBoxStyle.DropDownList,
                                Tag = new SplinePointRef(spl, ptIdx, "mode")
                            };
                            foreach (string n in Enum.GetNames(modeType)) modeBox.Items.Add(n);
                            modeBox.SelectedItem = modeName;
                            pegInfoPanel.Controls.Add(modeLabel);
                            pegInfoPanel.Controls.Add(modeBox);
                            yOffset += 30;
                        }
                        else
                        {
                            SplineTextBox("mode", "mode", "0", spl, ptIdx);
                        }

                        Label cornerLabel = new Label() { Text = "corner", Location = new Point(10, yOffset), Width = labelWidth };
                        var cornerBox = new CheckBox()
                        {
                            Location = new Point(labelWidth + 10, yOffset),
                            Checked = cp.corner,
                            Tag = new SplinePointRef(spl, ptIdx, "corner")
                        };
                        pegInfoPanel.Controls.Add(cornerLabel);
                        pegInfoPanel.Controls.Add(cornerBox);
                        yOffset += 30;
                    }

                    var addPtBtn = new Button() { Text = "Add Point", Location = new Point(10, yOffset), Width = 300 };
                    addPtBtn.Click += (s2, ev2) =>
                    {
                        var newCp = new SplineControlPoint();
                        cpList.Add(newCp);
                        PushUndo(
                            undo: () => { cpList.RemoveAt(cpList.Count - 1); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); },
                            redo: () => { cpList.Add(newCp); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); }
                        );
                        UpdatePegInfoPanel(selectedPeg);
                        pegboardPanel.Invalidate();
                    };
                    pegInfoPanel.Controls.Add(addPtBtn);
                    yOffset += 32;
                }
            }

            if (!multi && selectedPeg.components != null && selectedPeg.components.Count > 0)
            {
                Label compHeader = new Label()
                {
                    Text = $"Components ({selectedPeg.components.Count}):",
                    Location = new Point(10, yOffset),
                    Width = 300,
                    Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
                };
                pegInfoPanel.Controls.Add(compHeader);
                yOffset += 25;

                foreach (var component in selectedPeg.components)
                {
                    string compType = component.componentType ?? component.GetType().Name;
                    Label compTypeLabel = new Label()
                    {
                        Text = compType,
                        Location = new Point(10, yOffset),
                        Width = 215,
                        ForeColor = Color.DarkSlateGray
                    };
                    pegInfoPanel.Controls.Add(compTypeLabel);
                    {
                        var delComp = component;
                        var delCompBtn = new Button() { Text = "Delete", Location = new Point(225, yOffset), Width = 85 };
                        delCompBtn.Click += (s2, ev2) =>
                        {
                            selectedPeg.components?.Remove(delComp);
                            PushUndo(
                                undo: () => { selectedPeg.components ??= new List<PegboardParser.Component>(); selectedPeg.components.Add(delComp); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); },
                                redo: () => { selectedPeg.components?.Remove(delComp); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); }
                            );
                            UpdatePegInfoPanel(selectedPeg);
                            pegboardPanel.Invalidate();
                        };
                        pegInfoPanel.Controls.Add(delCompBtn);
                    }
                    yOffset += 25;

                    foreach (var compField in component.GetType().GetFields())
                    {
                        object val = compField.GetValue(component);
                        Label lbl = new Label() { Text = compField.Name, Location = new Point(20, yOffset), Width = labelWidth - 10 };
                        Control compCtrl;
                        if (compField.FieldType == typeof(bool))
                        {
                            compCtrl = new CheckBox() { Location = new Point(labelWidth + 10, yOffset), Checked = val is bool b && b };
                        }
                        else
                        {
                            compCtrl = new TextBox() { Location = new Point(labelWidth + 10, yOffset), Width = textBoxWidth, Text = val?.ToString() };
                        }
                        compCtrl.Tag = new ComponentFieldRef(component, compField);
                        pegInfoPanel.Controls.Add(lbl);
                        pegInfoPanel.Controls.Add(compCtrl);
                        yOffset += 30;
                    }
                }
            }

            if (!multi)
            {
                var compDropdown = new ComboBox()
                {
                    Location = new Point(10, yOffset),
                    Width = 210,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                compDropdown.Items.AddRange(new[]
                {
                    "attr_moving_peg_linear",
                    "attr_mirrored_peg",
                    "attr_flickering_peg",
                    "attr_invisible_on_awake",
                    "attr_moving_peg_return",
                    "attr_offset_pos_on_start",
                    "parent_rotating_peg_circle",
                    "parent_flickering_loop",
                    "peg_square_movement",
                    "boids_peg_layout",
                    "dragon_peg_layout_simulator ",
                    "obstacle_obscured_peg_grid",
                });
                compDropdown.SelectedIndex = 0;

                var addCompBtn = new Button() { Text = "Add", Location = new Point(226, yOffset - 1), Width = 84 };
                addCompBtn.Click += (s, ev) =>
                {
                    string chosen = compDropdown.SelectedItem?.ToString();
                    if (chosen == null) return;
                    Type compType = chosen switch
                    {
                        "attr_moving_peg_linear"      => typeof(PegboardParser.LinearPegMovementData),
                        "attr_mirrored_peg"           => typeof(PegboardParser.MirroredPegData),
                        "attr_flickering_peg"         => typeof(PegboardParser.FlickeringPegData),
                        "attr_invisible_on_awake"     => typeof(PegboardParser.InvisibleOnAwakeData),
                        "attr_moving_peg_return"      => typeof(PegboardParser.PegMoveAndReturnData),
                        "attr_offset_pos_on_start"    => typeof(PegboardParser.OffsetPositionOnStartData),
                        "parent_rotating_peg_circle"  => typeof(PegboardParser.RotatingPegCircleData),
                        "parent_flickering_loop"      => typeof(PegboardParser.FlickeringPegLoopCreatorData),
                        "peg_square_movement"         => typeof(PegboardParser.PegSquareMovementData),
                        "boids_peg_layout"            => typeof(PegboardParser.BoidsPegLayoutData),
                        "dragon_peg_layout_simulator " => typeof(PegboardParser.DragonPegLayoutSimData),
                        "obstacle_obscured_peg_grid"  => typeof(PegboardParser.RandomObscuredPegGridData),
                        _ => null
                    };
                    if (compType == null) return;
                    var newComp = (PegboardParser.Component)RuntimeHelpers.GetUninitializedObject(compType);
                    newComp.componentType = chosen;
                    if (newComp is PegboardParser.LinearPegMovementData lpm)
                    {
                        var lpmType = typeof(PegboardParser.LinearPegMovementData);
                        lpmType.GetField("isKinematic")?.SetValue(lpm, true);
                        lpmType.GetField("mass")?.SetValue(lpm, 1f);
                        lpmType.GetField("gravityScale")?.SetValue(lpm, 1f);
                        lpmType.GetField("angularDrag")?.SetValue(lpm, 0.05f);
                    }
                    selectedPeg.components ??= new List<PegboardParser.Component>();
                    selectedPeg.components.Add(newComp);
                    PushUndo(
                        undo: () => { selectedPeg.components.Remove(newComp); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); },
                        redo: () => { selectedPeg.components.Add(newComp); RefreshPegUI(selectedPeg); pegboardPanel.Invalidate(); }
                    );
                    UpdatePegInfoPanel(selectedPeg);
                    pegboardPanel.Invalidate();
                };

                pegInfoPanel.Controls.Add(compDropdown);
                pegInfoPanel.Controls.Add(addCompBtn);
                yOffset += 32;
            }

            foreach (Control c in pegInfoPanel.Controls)
            {
                if (c.Tag is System.Reflection.FieldInfo || c.Tag is ComponentFieldRef || c.Tag is SplinePointRef)
                {
                    if (c is TextBox ltb) ltb.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
                    else if (c is CheckBox lcb) lcb.CheckedChanged += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
                    else if (c is ComboBox lcombo) lcombo.SelectedIndexChanged += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
                }
            }
            nameBox.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            posXBox.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            posYBox.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            scaleXBox.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            scaleYBox.Leave += (s, e) => SaveButton_Click(selectedPeg, enabledBox);
            enabledBox.CheckedChanged += (s, e) => SaveButton_Click(selectedPeg, enabledBox);

            Button duplicateButton = new Button() { Text = "Duplicate", Location = new Point(10, yOffset), Width = 145 };
            duplicateButton.Click += DuplicateButton_Click;
            pegInfoPanel.Controls.Add(duplicateButton);

            Button deleteButton = new Button() { Text = "Delete", Location = new Point(165, yOffset), Width = 145 };
            deleteButton.Click += DeleteButton_Click;
            pegInfoPanel.Controls.Add(deleteButton);
        }

        private void SaveButton_Click(PegboardParser.TransformData peg, CheckBox enabledBox)
        {
            if (peg == null) return;

            var targets = GetTargets(peg);
            var snapshots = targets.Select(t => (t, before: DeepCopy(t))).ToList();

            bool posXValid = float.TryParse(posXBox.Text, out float posX);
            bool posYValid = float.TryParse(posYBox.Text, out float posY);
            bool scaleXValid = float.TryParse(scaleXBox.Text, out float scaleX);
            bool scaleYValid = float.TryParse(scaleYBox.Text, out float scaleY);

            bool isMulti = targets.Count > 1;
            foreach (var target in targets)
            {
                if (!isMulti) target.name = nameBox.Text;
                if (!isMulti && posXValid) target.posX = posX;
                if (!isMulti && posYValid) target.posY = posY;
                if (scaleXValid) target.scaleX = scaleX;
                if (scaleYValid) target.scaleY = scaleY;
                target.enabled = enabledBox.Checked;

                var component = target.prefab;
                if (component != null)
                {
                    foreach (var field in component.GetType().GetFields())
                    {
                        if (pegInfoPanel.Controls.ContainsKey(field.Name))
                        {
                            Control control = pegInfoPanel.Controls[field.Name];
                            try
                            {
                                string numericText = control is ComboBox ? control.Text.Split(' ')[0] : null;
                                if (field.FieldType.IsEnum)
                                {
                                    object enumVal = numericText != null && int.TryParse(numericText, out int ni)
                                        ? Enum.ToObject(field.FieldType, ni)
                                        : Enum.Parse(field.FieldType, control.Text);
                                    field.SetValue(component, enumVal);
                                }
                                else if (field.FieldType == typeof(float))
                                    field.SetValue(component, float.Parse(control.Text));
                                else if (field.FieldType == typeof(int))
                                    field.SetValue(component, int.Parse(numericText ?? control.Text));
                                else if (field.FieldType == typeof(bool) && control is CheckBox checkBox)
                                    field.SetValue(component, checkBox.Checked);
                                else if (field.Name == "color" && control is Button colorPickerBtn)
                                {
                                    var c = colorPickerBtn.BackColor;
                                    object colorObj = field.GetValue(component);
                                    var cType = colorObj?.GetType();
                                    cType?.GetField("r")?.SetValue(colorObj, c.R / 255f);
                                    cType?.GetField("g")?.SetValue(colorObj, c.G / 255f);
                                    cType?.GetField("b")?.SetValue(colorObj, c.B / 255f);
                                    cType?.GetField("a")?.SetValue(colorObj, c.A / 255f);
                                    field.SetValue(component, colorObj);
                                }
                                else
                                    field.SetValue(component, Convert.ChangeType(numericText ?? control.Text, field.FieldType));
                                control.BackColor = SystemColors.Window;
                            }
                            catch
                            {
                                control.BackColor = Color.LightCoral;
                            }
                        }
                    }
                }
            }

            foreach (Control ctrl in pegInfoPanel.Controls)
            {
                if (ctrl.Tag is not ComponentFieldRef cfr) continue;
                try
                {
                    foreach (var target in targets)
                    {
                        if (target.components == null) continue;
                        foreach (var tc in target.components)
                        {
                            if (tc.GetType() != cfr.Component.GetType()) continue;
                            if (cfr.Field.FieldType == typeof(bool) && ctrl is CheckBox tcb)
                                cfr.Field.SetValue(tc, tcb.Checked);
                            else if (cfr.Field.FieldType == typeof(float))
                                cfr.Field.SetValue(tc, float.Parse(ctrl.Text));
                            else if (cfr.Field.FieldType == typeof(int))
                                cfr.Field.SetValue(tc, int.Parse(ctrl.Text));
                            else
                                cfr.Field.SetValue(tc, Convert.ChangeType(ctrl.Text, cfr.Field.FieldType));
                        }
                    }
                    ctrl.BackColor = SystemColors.Window;
                }
                catch
                {
                    ctrl.BackColor = Color.LightCoral;
                }
            }

            var splineGroups = new Dictionary<(Spline, int), List<(Control ctrl, SplinePointRef spr)>>();
            foreach (Control ctrl in pegInfoPanel.Controls)
            {
                if (ctrl.Tag is not SplinePointRef spr) continue;
                var key = (spr.Spline, spr.PointIndex);
                if (!splineGroups.TryGetValue(key, out var list))
                    splineGroups[key] = list = new List<(Control, SplinePointRef)>();
                list.Add((ctrl, spr));
            }
            foreach (var ((spline, ptIndex), ctrlList) in splineGroups)
            {
                var cpList2 = s_splineMcpField?.GetValue(spline) as System.Collections.Generic.List<SplineControlPoint>;
                if (cpList2 == null || ptIndex >= cpList2.Count) continue;
                var cp = cpList2[ptIndex];
                foreach (var (ctrl, spr) in ctrlList)
                {
                    try
                    {
                        switch (spr.FieldName)
                        {
                            case "pos.x":       { var v = cp.position;     v.x = float.Parse(ctrl.Text); cp.position     = v; break; }
                            case "pos.y":       { var v = cp.position;     v.y = float.Parse(ctrl.Text); cp.position     = v; break; }
                            case "lt.x":        { var v = cp.leftTangent;  v.x = float.Parse(ctrl.Text); cp.leftTangent  = v; break; }
                            case "lt.y":        { var v = cp.leftTangent;  v.y = float.Parse(ctrl.Text); cp.leftTangent  = v; break; }
                            case "rt.x":        { var v = cp.rightTangent; v.x = float.Parse(ctrl.Text); cp.rightTangent = v; break; }
                            case "rt.y":        { var v = cp.rightTangent; v.y = float.Parse(ctrl.Text); cp.rightTangent = v; break; }
                            case "height":      cp.height      = float.Parse(ctrl.Text); break;
                            case "bevelCutoff": cp.bevelCutoff = float.Parse(ctrl.Text); break;
                            case "bevelSize":   cp.bevelSize   = float.Parse(ctrl.Text); break;
                            case "spriteIndex": cp.spriteIndex = int.Parse(ctrl.Text);   break;
                            case "corner":      if (ctrl is CheckBox cb) cp.corner = cb.Checked; break;
                            case "mode":
                                if (s_splineModeField != null)
                                {
                                    string modeText = ctrl is ComboBox cmb
                                        ? (cmb.SelectedItem as string ?? cmb.Text)
                                        : ctrl.Text;
                                    object modeVal = int.TryParse(modeText, out int mi)
                                        ? Enum.ToObject(s_splineModeField.FieldType, mi)
                                        : Enum.Parse(s_splineModeField.FieldType, modeText);
                                    s_splineModeField.SetValueDirect(__makeref(cp), modeVal);
                                }
                                break;
                        }
                        ctrl.BackColor = SystemColors.Window;
                    }
                    catch { ctrl.BackColor = Color.LightCoral; }
                }
                cpList2[ptIndex] = cp;
            }

            if (pegTreeView.SelectedNode != null)
            {
                pegTreeView.SelectedNode.Text = TreeNodeLabel(peg);
                pegTreeView.SelectedNode.Tag = peg;
            }

            pegboardPanel.Invalidate();

            var afters = snapshots.Select(s => (s.t, after: DeepCopy(s.t))).ToList();
            PushUndo(
                undo: () => { foreach (var (t, before) in snapshots) { RestoreTransformData(t, before); RefreshPegUI(t); } },
                redo: () => { foreach (var (t, after) in afters) { RestoreTransformData(t, after); RefreshPegUI(t); } }
            );
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (selectedPeg == null) return;
            ExecuteDeletePegs(GetTargets(selectedPeg), confirm: true);
        }

        private void DuplicateButton_Click(object sender, EventArgs e)
        {
            if (selectedPeg == null) return;

            var targets = GetTargets(selectedPeg);
            var dupeRecords = new List<(PegboardParser.TransformData copy, PegboardParser.TransformData parentPeg, TreeNode parentNode)>();

            foreach (var target in targets)
            {
                var node = FindTreeNode(pegTreeView.Nodes, target);
                if (node == null) continue;

                var copy = DeepCopy(target);
                copy.name = target.name + "_copy";
                copy.posX += 0.33f;
                copy.posY += 0.33f;

                var parentNode = node.Parent;
                var parentPeg = parentNode != null ? (PegboardParser.TransformData)parentNode.Tag : null;

                if (parentPeg != null)
                {
                    parentPeg.child ??= new List<PegboardParser.TransformData>();
                    parentPeg.child.Add(copy);
                }
                else
                {
                    _active.Data.transforms.Add(copy);
                }

                pegboardPanel.Pegs.Add(copy);
                DrawTransform(copy, parentNode);

                dupeRecords.Add((copy, parentPeg, parentNode));
            }

            pegboardPanel.Invalidate();

            var data = _active.Data;
            PushUndo(
                undo: () =>
                {
                    foreach (var (copy, parentPeg, _) in dupeRecords)
                    {
                        if (parentPeg != null) parentPeg.child?.Remove(copy);
                        else data.transforms.Remove(copy);
                        RemovePegFromPanel(copy);
                        FindTreeNode(pegTreeView.Nodes, copy)?.Remove();
                    }
                    pegboardPanel.Invalidate();
                },
                redo: () =>
                {
                    foreach (var (copy, parentPeg, parentNode) in dupeRecords)
                    {
                        if (parentPeg != null) { parentPeg.child ??= new List<PegboardParser.TransformData>(); parentPeg.child.Add(copy); }
                        else data.transforms.Add(copy);
                        AddPegRecursive(copy);
                        DrawTransform(copy, parentNode);
                    }
                    pegboardPanel.Invalidate();
                }
            );
        }

        private PegboardParser.TransformData DeepCopy(PegboardParser.TransformData original)
        {
            byte[] bytes = SerializationUtility.SerializeValue(original, DataFormat.Binary);
            return SerializationUtility.DeserializeValue<PegboardParser.TransformData>(bytes, DataFormat.Binary);
        }

        private void DeletePeg(PegboardParser.TransformData peg, PegboardParser.PegboardData data)
        {
            data.transforms.Remove(peg);
            pegboardPanel.Pegs.Remove(peg);
            if (peg.child != null)
                foreach (var child in peg.child)
                    DeletePeg(child, data);
        }
    }
}
