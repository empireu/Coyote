using Arch.Core.Extensions;
using GameFramework.Layers;

namespace Coyote.App;

internal class LayerController
{
    private readonly List<Layer> _layers = new();
  
    public IEnumerable<Layer> Layers => _layers;

    public Layer? Selected { get; set; }

    public void SwitchSelection(Layer newSelection)
    {
        foreach (var layer in _layers.Where(x => x != newSelection))
        {
            if (layer.IsEnabled)
            {
                layer.Disable();
            }
        }

        if (!newSelection.IsEnabled)
        {
            newSelection.Enable();
        }

        Selected = newSelection;
    }

    public void Add(Layer layer)
    {
        if (_layers.Contains(layer))
        {
            throw new Exception("Duplicate layer");
        }

        _layers.Add(layer);
    }

    public void Remove(Layer layer)
    {
        if (!_layers.Contains(layer))
        {
            throw new Exception("Invalid layer");
        }

        if (Selected == layer)
        {
            Selected = null;
        }

        _layers.Remove(layer);
    }
}