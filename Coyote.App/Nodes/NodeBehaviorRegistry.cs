namespace Coyote.App.Nodes;

internal sealed class NodeBehaviorRegistry
{
    private readonly List<NodeBehavior> _behaviors = new();

    public void Register(NodeBehavior behavior)
    {
        if (_behaviors.Contains(behavior))
        {
            throw new InvalidOperationException("Duplicate behavior");
        }

        _behaviors.Add(behavior);
    }

    public NodeBehavior[] CreateSet()
    {
        if (_behaviors.Count == 0)
        {
            throw new InvalidOperationException("No behaviors are registered");
        }

        return _behaviors.ToArray();
    }
}