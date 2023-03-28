using GameFramework.Layers;

namespace Coyote.App;

internal class LayerController
{
    private readonly Layer[] _layers;

    public int SelectedIndex { get; private set; }

    public Layer Selected => _layers[SelectedIndex];

    public LayerController(params Layer[] layers)
    {
        _layers = layers;

        if (_layers.Length == 0)
        {
            throw new ArgumentException(nameof(layers));
        }

        var selected = _layers[SelectedIndex];

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
        _layers[SelectedIndex].IsEnabled = false;
        _layers[newSelection].IsEnabled = true;
        SelectedIndex = newSelection;
    }

    public void Select(int index)
    {
        SwitchSelection(index);
    }

    public IEnumerable<int> Indices => Enumerable.Range(0, _layers.Length);

    public Layer this[int index] => _layers[index];
}