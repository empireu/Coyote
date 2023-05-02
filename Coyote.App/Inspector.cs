using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.App.Nodes;
using GameFramework.Utilities.Extensions;
using GameFramework.Utilities;
using ImGuiNET;

namespace Coyote.App;

[AttributeUsage(AttributeTargets.Field)]
internal abstract class EditorAttribute : Attribute { }

internal sealed class BoolEditorAttribute : EditorAttribute { }

internal sealed class FloatEditorAttribute : EditorAttribute
{
    public float Min { get; }
    public float Max { get; }

    public FloatEditorAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}

internal sealed class StringEditorAttribute : EditorAttribute
{
    public uint MaxLength { get; }

    public StringEditorAttribute(uint maxLength = 1024)
    {
        MaxLength = maxLength;
    }
}

internal static class Inspector
{
    public delegate object EditDelegate(object instance, string name, EditorAttribute baseAttribute);

    public delegate void ApplyDelegate(object newValue, Entity entity);

    [ThreadStatic]
    private static readonly Dictionary<string, bool> HeaderVisible;

    private static readonly ConcurrentDictionary<Type, ApplyDelegate> Applicators;
    private static readonly ConcurrentDictionary<Type, EditDelegate> Editors;

    private sealed class ClipboardElement
    {
        public readonly Type Applicator;
        public readonly FieldInfo Field;
        public readonly object Value;

        public ClipboardElement(Type applicator, FieldInfo field, object value)
        {
            Applicator = applicator;
            Field = field;
            Value = value;
        }
    }

    private static ClipboardElement? _clipboard;

    static Inspector()
    {
        HeaderVisible = new Dictionary<string, bool>();

        Editors = new ConcurrentDictionary<Type, EditDelegate>();
        Editors.TryAdd(typeof(string), StringEditor);
        Editors.TryAdd(typeof(float), FloatEditor);
        Editors.TryAdd(typeof(Vector2), Vector2Editor);
        Editors.TryAdd(typeof(bool), BoolEditor);

        Applicators = new ConcurrentDictionary<Type, ApplyDelegate>();

        Applicators.TryAdd(typeof(PositionComponent), ApplyPositionComponent);
        Applicators.TryAdd(typeof(NodeComponent), ApplyNodeComponent);
        Applicators.TryAdd(typeof(MarkerComponent), ApplyMarkerComponent);
    }

    private static void ApplyPositionComponent(object newValue, Entity entity)
    {
        var component = Assert.Is<PositionComponent>(newValue);

        entity.Move(component.Position);
    }

    private static void ApplyNodeComponent(object newValue, Entity entity)
    {
        var component = Assert.Is<NodeComponent>(newValue);

        entity.Set(component);
    }

    private static void ApplyMarkerComponent(object newValue, Entity entity)
    {
        var component = Assert.Is<MarkerComponent>(newValue);

        entity.Set(component);
    }

    private static string StringEditor(object instance, string name, EditorAttribute baseAttribute)
    {
        var text = Assert.Is<string>(instance);
        var attribute = Assert.Is<StringEditorAttribute>(baseAttribute);

        ImGui.InputTextMultiline(name, ref text, attribute.MaxLength, Vector2.Zero);

        return text;
    }

    private static object FloatEditor(object instance, string name, EditorAttribute baseAttribute)
    {
        var value = Assert.Is<float>(instance);
        var attribute = Assert.Is<FloatEditorAttribute>(baseAttribute);

        ImGui.SliderFloat(name, ref value, attribute.Min, attribute.Max);

        return value;
    }

    private static object Vector2Editor(object instance, string name, EditorAttribute baseAttribute)
    {
        var value = Assert.Is<Vector2>(instance);
        var attribute = Assert.Is<FloatEditorAttribute>(baseAttribute);

        ImGui.SliderFloat2(name, ref value, attribute.Min, attribute.Max);

        return value;
    }

    private static object BoolEditor(object instance, string name, EditorAttribute baseAttribute)
    {
        var value = Assert.Is<bool>(instance);
        var attribute = Assert.Is<BoolEditorAttribute>(baseAttribute);

        ImGui.Checkbox(name, ref value);

        return value;
    }

    public static bool SubmitEditor(Entity entity)
    {
        var components = entity.GetAllComponents();

        var changed = false;

        foreach (var component in components)
        {
            var componentType = component.GetType();
            var fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);

            if (fields.All(x => x.GetCustomAttribute<EditorAttribute>() == null))
            {
                continue;
            }

            var componentName = componentType.Name.AddSpacesToSentence(true);
            var visible = HeaderVisible.GetOrAdd(componentName, _ => true);

            if (ImGui.CollapsingHeader(componentName, ref visible))
            {
                foreach (var fieldInfo in fields)
                {
                    var attribute = fieldInfo.GetCustomAttribute<EditorAttribute>();

                    if (attribute == null)
                    {
                        continue;
                    }

                    var storedType = fieldInfo.FieldType;

                    var storedInstance = fieldInfo.GetValue(component);

                    if (storedInstance == null)
                    {
                        continue;
                    }

                    if (!Editors.TryGetValue(storedType, out var editor))
                    {
                        throw new Exception($"No editor is defined for {storedType}");
                    }

                    object newValue;

                    if (_clipboard != null && _clipboard.Applicator == componentType && _clipboard.Field == fieldInfo && ImGui.Button("Paste"))
                    {
                        newValue = _clipboard!.Value;
                    }
                    else
                    {
                        newValue = editor(
                            storedInstance,
                            fieldInfo.Name.AddSpacesToSentence(true),
                            attribute);
                    }

                    if (newValue != storedInstance)
                    {
                        changed = true;
                    }

                    if (ImGui.Button("Copy"))
                    {
                        _clipboard = new ClipboardElement(
                            componentType,
                            fieldInfo,
                            newValue
                        );
                    }

                    ImGui.Separator();

                    fieldInfo.SetValue(component, newValue);
                }

                if (!Applicators.TryGetValue(componentType, out var applicator))
                {
                    throw new Exception($"Failed to get applicator for component of type {componentType}");
                }

                applicator(component, entity);
            }

            HeaderVisible[componentName] = visible;
        }

        return changed;
    }
}