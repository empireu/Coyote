namespace Coyote.App.Nodes;

/// <summary>
///     Reference wrapper for Arch components.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct Ref<T>
{
    public T Instance { get; }

    public Ref(T instance)
    {
        Instance = instance;
    }
}