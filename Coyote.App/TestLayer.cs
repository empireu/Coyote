using System.Numerics;
using GameFramework;
using GameFramework.Layers;

namespace Coyote.App;

internal class TestLayer : Layer, ITabStyle
{
    protected override void Render(FrameInfo frameInfo)
    {
        Console.WriteLine("Render Test Layer");

        base.Render(frameInfo);
    }

    Vector4 ITabStyle.SelectedColor => new(0, 1, 0, 1);
}