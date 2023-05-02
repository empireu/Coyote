using System.Numerics;
using Arch.Core;

namespace Coyote.App.Nodes;

public interface INodeEditor
{
    void FocusCamera(Vector2 txWorldTarget);

    void FocusCamera(Entity entity);

    Project CoyoteProject { get; }
}