using System.Numerics;

namespace Coyote.App;

internal interface ITabStyle
{
    public Vector4 SelectedColor => new(1, 0.5f, 0.5f, 0.5f);
    public Vector4 IdleColor => new(0.1f, 0.05f, 0.15f, 0.8f);
}

internal interface IProjectTab
{
    void Save();
}