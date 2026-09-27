namespace MiniCapture;

// 편집 동작 1회당 "그 동작 직전 상태" 1개를 쌓는 Undo/Redo 스택. 상태 복제·복원은 호출 측이 맡는다.
internal sealed class EditHistory<T>
{
    private readonly List<T> _undo = new();
    private readonly Stack<T> _redo = new();
    private readonly int _capacity;

    public EditHistory(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    // 새 동작이 들어오면 되돌린 뒤의 갈래(redo)는 버린다. 가장 오래된 상태부터 잘라 용량을 지킨다.
    public void Push(T before)
    {
        _undo.Add(before);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveRange(0, _undo.Count - _capacity);
        }

        _redo.Clear();
    }

    public bool TryUndo(T current, out T previous)
    {
        if (_undo.Count == 0)
        {
            previous = default!;
            return false;
        }

        previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Push(current);
        return true;
    }

    public bool TryRedo(T current, out T next)
    {
        if (_redo.Count == 0)
        {
            next = default!;
            return false;
        }

        next = _redo.Pop();
        _undo.Add(current);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
