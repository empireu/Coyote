using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Renderer;
using GameFramework.Utilities;

namespace Coyote.App.Nodes;

public enum NodeTerminalType
{
    Parent,
    Children
}

public class NodeTerminal
{
    public NodeTerminal(NodeTerminalType type)
    {
        Type = type;
    }

    public NodeTerminalType Type { get; }

    public virtual bool AcceptsConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {
        return true;
    }

    public virtual void PrepareConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {

    }

    public virtual void FinishConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {

    }
}

public class NodeConnectionSet
{
    public NodeConnectionSet(NodeTerminal parentTerminal, List<NodeTerminal> childrenTerminals)
    {
        ParentTerminal = parentTerminal;
        ChildrenTerminals = childrenTerminals;
    }

    public NodeTerminal ParentTerminal { get; }
    public List<NodeTerminal> ChildrenTerminals { get; }

    public Vector2 GetParentPosition(Entity entity, float borderSize)
    {
        return entity.Get<PositionComponent>().Position + entity.Get<ScaleComponent>().Scale with { Y = borderSize / 2f } / 2f;
    }

    public Vector2 GetChildPosition(NodeTerminal terminal, Entity entity, float borderSize)
    {
        var i = ChildrenTerminals.IndexOf(terminal);

        if (i == -1)
        {
            throw new ArgumentException("Invalid child terminal", nameof(terminal));
        }

        var positionComponent = entity.Get<PositionComponent>();
        var scaleComponent = entity.Get<ScaleComponent>();

        var startX = positionComponent.Position.X + scaleComponent.Scale.X * 0.1f;
        var endX = positionComponent.Position.X + scaleComponent.Scale.X * 0.9f;

        var x = MathUtilities.MapRange((i + 0.5f) / ChildrenTerminals.Count, 0f, 1f, startX, endX);
        
        return new Vector2(x, positionComponent.Position.Y - scaleComponent.Scale.Y - borderSize / 4f);
    }
}

public readonly struct NodeChild
{
    public Entity Entity { get; }
    public NodeTerminal Terminal { get; }

    public NodeChild(Entity entity, NodeTerminal terminal)
    {
        Entity = entity;
        Terminal = terminal;
    }
}

public struct NodeComponent
{
    public NodeBehavior Behavior;

    [StringEditor]
    public string Description;

    public Entity? Parent;
    public Ref<List<NodeChild>> ChildrenRef;

    [StringEditor]
    public string Name;

    [BoolEditor]
    public bool ExecuteOnce;

    public NodeConnectionSet Connections;
}

public abstract class NodeBehavior
{
    private readonly string _name;

    protected NodeBehavior(TextureSampler icon, Vector4 backgroundColor, string name)
    {
        _name = name;
        Icon = icon;
        BackgroundColor = backgroundColor;
    }

    public TextureSampler Icon { get; set; }
    public Vector4 BackgroundColor { get; set; }

    public Entity CreateEntity(World world, Vector2 position)
    {
        return world.Create(
            new PositionComponent { Position = position },
            new NodeComponent
            {
                Behavior = this,
                ChildrenRef = new Ref<List<NodeChild>>(new List<NodeChild>()),
                Description = ToString() ?? string.Empty,
                ExecuteOnce = true,
                Name = string.Empty,
                Connections = new NodeConnectionSet(new NodeTerminal(NodeTerminalType.Parent), new List<NodeTerminal>()).Also(AttachTerminals)
            },
            new ScaleComponent());
    }

    protected virtual void AttachTerminals(NodeConnectionSet connections)
    {

    }

    public virtual void AttachComponents(Entity entity)
    {
        
    }

    public virtual bool AcceptsChild(Entity actualEntity, Entity remoteEntity)
    {
        return true;
    }

    public virtual bool AcceptsParent(Entity actualEntity, Entity remoteEntity, NodeTerminal remoteTerminal)
    {
        return true;
    }

    public override string ToString()
    {
        return _name;
    }
}

public class LeafNode : NodeBehavior
{
    public LeafNode(TextureSampler icon, string name) : base(icon, new(0.1f, 0.6f, 0.0f, 0.7f), name) { }
}

public class ProxyNode : NodeBehavior
{
    public ProxyNode(TextureSampler icon, string name) : base(icon, new(0.3f, 0.6f, 0.1f, 0.7f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.ChildrenTerminals.Add(new NodeTerminal(NodeTerminalType.Children));
        connections.ChildrenTerminals.Add(new NodeTerminal(NodeTerminalType.Children));
    }
}

public class DecoratorTerminal : NodeTerminal
{
    public DecoratorTerminal() : base(NodeTerminalType.Children) { }

    public override void PrepareConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {
        if (actualEntity.Get<NodeComponent>().ChildrenRef.Instance.Count > 0)
        {
            actualEntity.UnlinkChildren();
        }
    }
}

public class DecoratorNode : NodeBehavior
{
    public DecoratorNode(TextureSampler icon, string name) : base(icon, new(0.25f, 0.25f, 0.75f, 0.5f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.ChildrenTerminals.Add(new DecoratorTerminal());
    }
}

public static class NodeExtensions
{
    public static void UnlinkChildren(this Entity parent)
    {
        var children = parent.Get<NodeComponent>().ChildrenRef.Instance.ToArray();

        foreach (var child in children)
        {
            parent.Unlink(child.Entity);
        }

        parent.Get<NodeComponent>().ChildrenRef.Instance.Clear();
    }


    public static void Unlink(this Entity parent, Entity child)
    {
        ref var parentComp = ref parent.Get<NodeComponent>();
        ref var childComp = ref child.Get<NodeComponent>();

        Assert.IsTrue(parentComp.ChildrenRef.Instance.RemoveAll(x => x.Entity == child) == 1, "Unlink invalid child");
        childComp.Parent = null;
    }
}