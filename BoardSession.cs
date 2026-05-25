using System;
using System.Collections.Generic;

namespace Starchild
{
    class BoardSession
    {
        public PegboardParser.PegboardData Data;
        public string PegboardName = "";
        public string FilePath;
        public string TabLabel;

        public Stack<(Action undo, Action redo)> UndoStack = new();
        public Stack<(Action undo, Action redo)> RedoStack = new();

        public PegboardParser.TransformData SelectedPeg;
        public HashSet<PegboardParser.TransformData> SelectedPegs = new();
    }
}
