using System.Collections.Generic;

namespace Nelir.Services
{
    public class UndoEntry
    {
        public string UniqueKey { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public UndoEntry(string uniqueKey, string oldValue, string newValue)
        {
            UniqueKey = uniqueKey;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class UndoRedoService
    {
        private readonly LinkedList<UndoEntry> _undoStack = new();
        private readonly Stack<UndoEntry> _redoStack = new();
        private const int MaxEntries = 200;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Push(string uniqueKey, string oldValue, string newValue)
        {
            if (oldValue == newValue) return;

            _undoStack.AddLast(new UndoEntry(uniqueKey, oldValue, newValue));

            // Bounded stack logic: remove oldest items from the bottom (first item in the linked list)
            while (_undoStack.Count > MaxEntries)
            {
                _undoStack.RemoveFirst();
            }

            _redoStack.Clear();
        }

        public UndoEntry? Undo()
        {
            if (_undoStack.Count == 0) return null;

            var entry = _undoStack.Last!.Value;
            _undoStack.RemoveLast();
            _redoStack.Push(entry);
            return entry;
        }

        public UndoEntry? Redo()
        {
            if (_redoStack.Count == 0) return null;

            var entry = _redoStack.Pop();
            _undoStack.AddLast(entry);

            while (_undoStack.Count > MaxEntries)
            {
                _undoStack.RemoveFirst();
            }

            return entry;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
