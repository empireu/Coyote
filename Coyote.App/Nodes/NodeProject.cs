using System.Text.Json.Serialization;
using Arch.Core;
using Arch.Core.Extensions;

namespace Coyote.App.Nodes;

internal struct JsonChild
{
    [JsonInclude]
    public int TerminalId { get; set; }

    [JsonInclude]
    public JsonNode Node { get; set; }
}

internal class JsonNode
{
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

internal class NodeProject
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
                BehaviorId = component.Behavior.Name,
                Name = component.Name,
                Description = component.Description,
                ExecuteOnce = component.ExecuteOnce,
                SavedData = component.Behavior.Save(entity),
                Children = component
                    .ChildrenRef
                    .Instance
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
}