using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Renderer;

namespace Coyote.App.Nodes;

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
                Name = string.Empty
            },
            new ScaleComponent());
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
}

public class DecoratorNode : NodeBehavior
{
    public override bool AcceptsChildConnection(Entity entity)
    {
        return entity.Get<NodeComponent>().ChildrenRef.Instance.Count == 0;
    }

    public DecoratorNode(TextureSampler icon, string name) : base(icon, new(0.5f, 0.5f, 0.5f, 0.7f), name) { }
}

#endregion