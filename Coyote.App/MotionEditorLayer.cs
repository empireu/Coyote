using GameFramework;
using GameFramework.Layers;

namespace Coyote.App;

internal class MotionEditorLayer : Layer
{
    protected override void Render(FrameInfo frameInfo)
    {
        Console.WriteLine("Render Motion Editor");

        base.Render(frameInfo);
    }
}