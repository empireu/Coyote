namespace Coyote.App.Nodes;

internal sealed class NodeBehaviorRegistry
{
    private readonly List<NodeBehavior> _behaviors = new();

    public TNode Register<TNode>(TNode behavior) where TNode : NodeBehavior
    {
        if (_behaviors.Contains(behavior))
        {
            throw new InvalidOperationException("Duplicate behavior");
        }

        if (_behaviors.Any(x => x.Name.Equals(behavior.Name)))
        {
            throw new InvalidOperationException("Duplicate node name");
        }

        _behaviors.Add(behavior);

        return behavior;
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