# Coyote Editor

Simple graphical path editor and behavior tree editor for FTC. Designed to be used in conjunction with [the JVM library.](https://github.com/empireu/Coyote-Lib)

### Amazing libraries

- [GitHub - veldrid/veldrid: A low-level, portable graphics library for .NET.](https://github.com/veldrid/veldrid)

- [GitHub - ImGuiNET/ImGui.NET: An ImGui wrapper for .NET.](https://github.com/ImGuiNET/ImGui.NET)

- [GitHub - genaray/Arch: A high-performance C# based Archetype &amp; Chunks Entity Component System (ECS) with optional multithreading.](https://github.com/genaray/Arch/)

- [GitHub - aaubry/YamlDotNet: YamlDotNet is a .NET library for YAML](https://github.com/aaubry/YamlDotNet/)

### Custom behavior nodes

Custom action nodes with user-editable data may be defined using standard YAML. Two files are required: an icon (a square image) and a YAML file. Those will live under `definitions/`

A `Name` field must be defined, which will be used to match the node to an implementation in the robot runtime. Thus, this name must be unique. 

The texture is optional, and can be bound using the `Texture` field.

An RGBA background color can also be specified in this manner:

```yaml
Color:
    X: 0.9
    Y: 0.1
    Z: 0.23
    W: 0.9
```

#### Structure

Structure fields are used to define data stored in the node. All of them share the following fields:

- `Name` - the unique name of the node

- `Label` - the name displayed in the GUI editor

Fields are provided based on the data they store and the way it is presented to the user:

- `RealSliders`/`IntegerSliders`
  
  - `Min`, `Max` - interval bounds (default: *0*-*10*)
  
  - `Value` - initial value (default: *0*)

- `RealInputFields`/`IntegerInputFields`
  
  - `Step` - step size used when scrolling in the GUI (default: *0.01*)
  
  - `Value` - initial value (default: *0*)

- `TextInputFields`
  
  - `MaxLength` - maximum length of the text (default: *512*)
  
  - `Value` - initial value (default: **empty**)

- `Enums`
  
  - `Options` - set of options to choose from (default: {*None*})
  
  - `Selected` - initial value (default: **empty**)

- `Flags`
  
  - `State` - initial state (default: Off)

 Apart from data fields, special flags that change the behavior of the node can be set:

- `IsDriveBehavior` (default: false)
  
  - Marks a node that uses the drive system
  
  - Rules ensure that two (different) drive behaviors cannot run in parallel

- `IsNonParallel` (default: false)
  
  - Rules ensure that two nodes of this type cannot run in parallel



Example:

```yaml
---
  Texture: "coyote-tex.png"
  NodeName: "Node"
      Color:
        X: 0.9
        Y: 0.1
        Z: 0.23
        W: 0.9
  Structure:
    IsNonParallel: True
    Flags:
      -
        Name: "myFlag"
        Label: "My Flag"
      -
        Name: "myFlag2"
        State: True
    Enums:
      -
        Name: "myOptions"
        Options: 
          - "Option1"
          - "Option2"
    RealSliders:
      -
        Name: "myRealSlider"
        Label: "My Real Slider"
        Min: -1
        Max: 2
        Value: 1.5
    IntegerSliders:
      -
        Name: "mySlider"
        Label: "My Integer Slider"
        Min: -10
    TextInputFields:
      -
        Name: "myText"
    RealInputFields:
      -
        Name: "myRealInput"
    IntegerInputFields:
      -
        Name: "myIntegerInput"
```









































