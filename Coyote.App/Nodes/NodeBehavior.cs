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

    public virtual bool AcceptConnection(NodeTerminal child, Entity entity)
    {
        return true;
    }

    public virtual void Unlink()
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

public struct NodeComponent
{
    public NodeBehavior Behavior;

    [StringEditor]
    public string Description;

    public Entity? Parent;
    public Ref<List<Entity>> ChildrenRef;

    [StringEditor]
    public string Name;

    [BoolEditor]
    public bool ExecuteOnce;

    public NodeConnectionSet Connections;
}

#region Abstract

public abstract class NodeBehavior
{
    private readonly string _name;

    protected NodeBehavior(TextureSampler icon, Vector4 backgroundColor, string name)
    {
        _name = name;
        Icon = icon;
        BackgroundColor = backgroundColor;
    }

    public TextureSampler Icon { get; }
    public Vector4 BackgroundColor { get; }

    public Entity CreateEntity(World world, Vector2 position)
    {
        return world.Create(
            new PositionComponent { Position = position },
            new NodeComponent
            {
                Behavior = this,
                ChildrenRef = new Ref<List<Entity>>(new List<Entity>()),
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

    public virtual void AttachComponents(in Entity entity)
    {
        
    }

    public abstract bool AcceptsChildConnection(Entity entity);

    public override string ToString()
    {
        return _name;
    }
}

public class LeafNode : NodeBehavior
{
    public override bool AcceptsChildConnection(Entity entity)
    {
        return false;
    }

    public LeafNode(TextureSampler icon, string name) : base(icon, new(0.1f, 0.6f, 0.0f, 0.7f), name) { }
}

public class ProxyNode : NodeBehavior
{
    public override bool AcceptsChildConnection(Entity entity)
    {
        return true;
    }

    public ProxyNode(TextureSampler icon, string name) : base(icon, new(0.3f, 0.6f, 0.1f, 0.7f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.ChildrenTerminals.Add(new NodeTerminal(NodeTerminalType.Children));
    }
}

public class DecoratorTerminal : NodeTerminal
{
    public DecoratorTerminal() : base(NodeTerminalType.Children)
    {

    }

    public override bool AcceptConnection(NodeTerminal child, Entity entity)
    {
        if (entity.Get<NodeComponent>().ChildrenRef.Instance.Count > 0)
        {
            entity.UnlinkChildren();
        }

        return true;
    }
}

public class DecoratorNode : NodeBehavior
{
    public override bool AcceptsChildConnection(Entity entity)
    {
        return entity.Get<NodeComponent>().ChildrenRef.Instance.Count == 0;
    }

    public DecoratorNode(TextureSampler icon, string name) : base(icon, new(0.5f, 0.5f, 0.5f, 0.7f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.ChildrenTerminals.Add(new DecoratorTerminal());
    }
}

#endregion

public static class NodeExtensions
{
    public static void UnlinkChildren(this Entity parent)
    {
        parent.Get<NodeComponent>().ChildrenRef.Instance.RemoveAll(child =>
        {
            var component = child.Get<NodeComponent>();

            component.Parent = null;
            component.Connections.ParentTerminal.Unlink();
        });
    }
}