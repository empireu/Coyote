namespace Coyote.App.Movement;

readonly struct SegmentRange
{
    public float Start { get; }
    public float End { get; }

    public SegmentRange(float start, float end)
    {
        Start = start;
        End = end;

        if (Start >= End)
        {
            throw new ArgumentException("Start cannot be larger or equal to end");
        }
    }

    public bool Contains(float point)
    {
        return point >= Start && point <= End;
    }
}

class SegmentTreeNode<T>
{
    private readonly SegmentTreeNode<T>? _left;
    private readonly SegmentTreeNode<T>? _right;
    public SegmentRange Range { get; }
    public T? Data { get; }

    public SegmentTreeNode(SegmentRange range, T? data, SegmentTreeNode<T>? left, SegmentTreeNode<T>? right)
    {
        _left = left;
        _right = right;
        Range = range;
        Data = data;
    }

    public bool Contains(float point)
    {
        return Range.Contains(point);
    }

    public T Query(float point)
    {
        if (!Contains(point))
        {
            throw new InvalidOperationException($"This tree does not contain {point}");
        }

        if (_left != null && _left.Contains(point))
        {
            return _left.Query(point);
        }

        if (_right != null && _right.Contains(point))
        {
            return _right.Query(point);
        }

        return Data ?? throw new Exception("Invalid tree");
    }
}

class SegmentTreeBuilder<T>
{
    private struct PendingSegment
    {
        public T Data;
        public SegmentRange Range;
    }

    private readonly List<PendingSegment> _pending = new();

    public void Insert(T item, SegmentRange range)
    {
        _pending.Add(new PendingSegment
        {
            Data = item,
            Range = range
        });
    }

    public SegmentTree<T> Build()
    {
        if (_pending.Count == 0)
        {
            throw new InvalidOperationException("Cannot build empty segment tree");
        }

        // Check continuity

        if (_pending.Count > 1)
        {
            for (int i = 1; i < _pending.Count; i++)
            {
                var previous = _pending[i - 1];
                var current = _pending[i];

                if (!previous.Range.End.Equals(current.Range.Start))
                {
                    throw new InvalidOperationException("Segment tree continuity broken");
                }
            }
        }

        return new SegmentTree<T>(BuildSegment(0, _pending.Count - 1));
    }

    private SegmentTreeNode<T> BuildSegment(int leftIndex, int rightIndex)
    {
        if (leftIndex == rightIndex)
        {
            var pendingSegment = _pending[leftIndex];

            return new SegmentTreeNode<T>(pendingSegment.Range, pendingSegment.Data, null, null);
        }

        var mid = leftIndex + (rightIndex - leftIndex) / 2;

        return new SegmentTreeNode<T>(
            new SegmentRange(_pending[leftIndex].Range.Start, _pending[rightIndex].Range.End),
            default,
            BuildSegment(leftIndex, mid),
            BuildSegment(mid + 1, rightIndex));
    }
}

class SegmentTree<T>
{
    private readonly SegmentTreeNode<T> _root;

    public SegmentRange Range => _root.Range;

    public SegmentTree(SegmentTreeNode<T> root)
    {
        _root = root;
    }

    public T Query(float point)
    {
        return _root.Query(point);
    }
}