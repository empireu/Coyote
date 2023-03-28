using GameFramework.Layers;

namespace Coyote.App;

internal class LayerController
{
    private readonly Layer[] _layers;
    private int _selected;

    public LayerController(params Layer[] layers)
    {
        _layers = layers;

        if (_layers.Length == 0)
        {
            throw new ArgumentException(nameof(layers));
        }

        var selected = _layers[_selected];

        selected.Enable();

        foreach (var layer in layers)
        {
            if (layer != selected)
            {
                layer.Disable();
            }
        }
    }

    private void SwitchSelection(int newSelection)
    {
        _layers[_selected].IsEnabled = false;
        _layers[newSelection].IsEnabled = true;
        _selected = newSelection;
    }

    public void Select(int index)
    {
        SwitchSelection(index);
    }

    public IEnumerable<int> Indices => Enumerable.Range(0, _layers.Length);

    public Layer this[int index] => _layers[index];
}