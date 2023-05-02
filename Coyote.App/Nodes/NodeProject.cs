using System.Text.Json.Serialization;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Utilities;

namespace Coyote.App.Nodes;

public struct JsonChild
{
    [JsonInclude]
    public int TerminalId { get; set; }

    [JsonInclude]
    public JsonNode Node { get; set; }
}

public sealed class JsonNode
{
    [JsonInclude]
    public JsonVector2 Position { get; set; }

    [JsonInclude]
    public string BehaviorId { get; set; }

    [JsonInclude]
    public string Name { get; set; }

    [JsonInclude]
    public string Description { get; set; }

    [JsonInclude]
    public bool ExecuteOnce { get; set; }

    [JsonInclude]
    public string SavedData { get; set; }
    
    [JsonInclude]
    public JsonChild[] Children { get; set; }
}

public class NodeProject
{
    [JsonInclude]
    public JsonNode[] RootNodes { get; set; }

    public static NodeProject FromNodeWorld(World world)
    {
        var rootEntities = new List<Entity>();
        world.GetEntities(new QueryDescription().WithAll<PositionComponent, NodeComponent>(), rootEntities);
        rootEntities.RemoveAll(x => x.Get<NodeComponent>().Parent.HasValue);

        JsonNode Traverse(Entity entity)
        {
            var component = entity.Get<NodeComponent>();

            return new JsonNode
            {
                Position = entity.Get<PositionComponent>().Position,
                BehaviorId = component.Behavior.Name,
                Name = component.Name,
                Description = component.Description,
                ExecuteOnce = component.ExecuteOnce,
                SavedData = component.Behavior.Save(entity),
                Children = component
                    .ChildrenRef
                    .Instance
                    .OrderBy(n => n.Entity.Get<PositionComponent>().Position.X)
                    .Select(childConnection =>
                        new JsonChild
                        {
                            Node = Traverse(childConnection.Entity),
                            TerminalId = childConnection.Terminal.Id
                        }
                    )
                    .ToArray()
            };
        }

        return new NodeProject
        {
            RootNodes = rootEntities.Select(Traverse).ToArray()
        };
    }

    public void Load(World world, IEnumerable<NodeBehavior> behaviors)
    {
        if (world.Size != 0)
        {
            throw new InvalidOperationException("Cannot load nodes in non-fresh world");
        }

        var behaviorMap = behaviors.ToDictionary(b => b.Name, b => b);

        var savedDataMap = new Dictionary<Entity, string>();

        Entity Traverse(JsonNode node)
        {
            if (!Assert.NotNull(behaviorMap).TryGetValue(node.BehaviorId, out var behavior))
            {
                throw new Exception($"Failed to deserialize node with internal name \"{node.Name}\"");
            }

            var entity = behavior.CreateEntity(world, node.Position);

            savedDataMap.Add(entity, node.SavedData);
            
            ref var parentComp = ref entity.Get<NodeComponent>();

            parentComp.Name = node.Name;
            parentComp.Description = node.Description;
            parentComp.ExecuteOnce = node.ExecuteOnce;

            behavior.InitialLoad(entity, node.SavedData);

            foreach (var nodeChild in node.Children)
            {
                entity.LinkTo(Traverse(nodeChild.Node), parentComp.Terminals.GetChildTerminal(nodeChild.TerminalId));
            }

            behavior.AfterLoad(entity);

            return entity;
        }

        RootNodes.ForEach(node =>
        {
            Traverse(node);
        });

        world.Query(new QueryDescription().WithAll<NodeComponent>(), ((in Entity entity) =>
        {
            entity.Behavior().AfterWorldLoad(entity, savedDataMap[entity]);
        }));
    }
}