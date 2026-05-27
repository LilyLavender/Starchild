using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using UnityEngine.U2D;

namespace Starchild
{
    partial class MainForm
    {
        internal static PegboardParser.Component CreatePrefab(string componentType)
        {
            Type t = componentType switch
            {
                "peg_regular"              => typeof(PegboardParser.RegularPegData),
                "peg_long"                 => typeof(PegboardParser.LongPegData),
                "peg_bomb"                 => typeof(PegboardParser.BombData),
                "peg_slime_only"           => typeof(PegboardParser.SlimeOnlyPegData),
                "indestructible_peg"       => typeof(PegboardParser.IndestructiblePegData),
                "bouncer_peg"              => typeof(PegboardParser.BouncerPegData),
                "obstacle_black_hole"      => typeof(PegboardParser.PegboardBlackHoleData),
                "obstacle_bouncer"         => typeof(PegboardParser.BouncerData),
                "obstacle_bouncer_mines"   => typeof(PegboardParser.BouncerMinesData),
                "parent_firework_movement" => typeof(PegboardParser.FireworkMovementData),
                _ => null
            };
            if (t == null) return null;

            var prefab = (PegboardParser.Component)RuntimeHelpers.GetUninitializedObject(t);
            prefab.componentType = componentType;
            prefab.enabled = true;
            ApplyPrefabDefaults(prefab);
            return prefab;
        }

        private static void ApplyPrefabDefaults(PegboardParser.Component prefab)
        {
            var ft = prefab.GetType();
            switch (prefab.componentType)
            {
                case "peg_regular":
                {
                    var pegTypeField = ft.GetField("pegType");
                    if (pegTypeField != null)
                        pegTypeField.SetValue(prefab, pegTypeField.FieldType == typeof(int)
                            ? (object)1 : Enum.ToObject(pegTypeField.FieldType, 1));
                    ft.GetField("color")?.SetValue(prefab, new UnityEngine.Color(1f, 1f, 1f, 1f));
                    ft.GetField("canBeDull")?.SetValue(prefab, true);
                    break;
                }
                case "peg_long":
                {
                    var splineField = ft.GetField("spline");
                    if (splineField == null) break;
                    var spline = new Spline();
                    var cpField = typeof(Spline).GetField("m_ControlPoints", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cpField != null)
                    {
                        var cpList = new List<SplineControlPoint>();
                        var posField = typeof(SplineControlPoint).GetField("position");
                        object cp0 = new SplineControlPoint();
                        object cp1 = new SplineControlPoint();
                        posField?.SetValue(cp0, new UnityEngine.Vector3(-1f, 0f, 0f));
                        posField?.SetValue(cp1, new UnityEngine.Vector3(1f, 0f, 0f));
                        cpList.Add((SplineControlPoint)cp0);
                        cpList.Add((SplineControlPoint)cp1);
                        cpField.SetValue(spline, cpList);
                    }
                    splineField.SetValue(prefab, spline);
                    break;
                }
                case "bouncer_peg":
                    ft.GetField("bounceScaleX")?.SetValue(prefab, 3.5f);
                    ft.GetField("bounceScaleY")?.SetValue(prefab, 3.5f);
                    ft.GetField("removeDuringNav")?.SetValue(prefab, true);
                    break;
                case "obstacle_black_hole":
                    ft.GetField("gravityRange")?.SetValue(prefab, 5f);
                    ft.GetField("gravityStrength")?.SetValue(prefab, 6f);
                    break;
            }
        }

        private static PegboardParser.TransformData CreateTransformData(string componentType, float posX, float posY, float scaleX, float scaleY)
        {
            var td = (PegboardParser.TransformData)RuntimeHelpers.GetUninitializedObject(typeof(PegboardParser.TransformData));
            td.name = componentType switch
            {
                "peg_regular"              => "RegularPeg",
                "peg_long"                 => "LongPeg",
                "peg_bomb"                 => "Bomb",
                "peg_slime_only"           => "SlimeOnlyPeg",
                "indestructible_peg"       => "IndestructiblePeg",
                "bouncer_peg"              => "BouncerPeg",
                "obstacle_black_hole"      => "BlackHole",
                "obstacle_bouncer"         => "Bouncer",
                "obstacle_bouncer_mines"   => "BounceMinePeg",
                "parent_firework_movement" => "FireworkMovement",
                "(group)"                  => "Group",
                _ => componentType
            };
            td.posX = posX;
            td.posY = posY;
            td.scaleX = scaleX;
            td.scaleY = scaleY;
            td.enabled = true;
            td.child = new List<PegboardParser.TransformData>();
            td.components = new List<PegboardParser.Component>();
            td.prefab = componentType == "(group)" ? null : CreatePrefab(componentType);
            return td;
        }

        internal void InsertNewPeg(string componentType, float posX, float posY, float scaleX = 1f, float scaleY = 1f)
        {
            if (_active == null) return;

            var newPeg = CreateTransformData(componentType, posX, posY, scaleX, scaleY);

            var root = _active.Data?.transforms?.Count > 0 ? _active.Data.transforms[0] : null;
            TreeNode rootNode = pegTreeView.Nodes.Count > 0 ? pegTreeView.Nodes[0] : null;

            if (root != null)
            {
                root.child ??= new List<PegboardParser.TransformData>();
                root.child.Add(newPeg);
            }
            else
            {
                _active.Data.transforms.Add(newPeg);
            }

            pegboardPanel.Pegs.Add(newPeg);
            var newNode = DrawTransform(newPeg, rootNode);
            pegboardPanel.Invalidate();
            UpdatePegCount();
            pegTreeView.SelectedNode = newNode;

            var data = _active.Data;
            PushUndo(
                undo: () =>
                {
                    if (root != null) root.child?.Remove(newPeg);
                    else data.transforms.Remove(newPeg);
                    RemovePegFromPanel(newPeg);
                    FindTreeNode(pegTreeView.Nodes, newPeg)?.Remove();
                    if (selectedPeg == newPeg) { selectedPeg = null; pegTreeView.SelectedNode = null; pegboardPanel.HighlightedPeg = null; pegInfoPanel.Controls.Clear(); }
                    UpdatePegCount();
                    pegboardPanel.Invalidate();
                },
                redo: () =>
                {
                    if (root != null) { root.child ??= new List<PegboardParser.TransformData>(); root.child.Add(newPeg); }
                    else data.transforms.Add(newPeg);
                    AddPegRecursive(newPeg);
                    DrawTransform(newPeg, rootNode);
                    UpdatePegCount();
                    pegboardPanel.Invalidate();
                }
            );
        }

    }
}
