using System.Numerics;
using Coyote.App.Nodes;
using GameFramework.Assets;
using MoonSharp.Interpreter;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Coyote.App.Plugins;

internal static class LuaPlugin
{
    public static IEnumerable<NodeBehavior> LoadBehaviors(App app, string fileName)
    {
        var script = new Script();

        var results = new List<NodeBehavior>();

        script.Globals.Set("Node", DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            if (args.Count < 3)
            {
                throw new InvalidOperationException($"Insufficient arguments in {fileName}");
            }

            if (args.Count > 4)
            {
                throw new InvalidOperationException($"Too many arguments in {fileName}");
            }

            var nodeType = args[0].String;
            var nodeName = args[1].String;
            var nodeTexture = args[2].String;

            var color = new Vector4(1.0f, 0.8f, 0.8f, 0.6f);

            if (args.Count == 4)
            {
                var baseColor = Color.ParseHex(args[3].String).ToPixel<Rgba32>();

                color = new Vector4(baseColor.R / 255f, baseColor.G / 255f, baseColor.B / 255f, baseColor.A / 255f);
            }

            var tex = app.Resources.AssetManager.GetSpriteForTexture(
                new FileResourceKey(
                    Path.Combine(
                        Path.GetDirectoryName(fileName)
                        ?? throw new InvalidOperationException($"Failed to find directory of {fileName}"),
                        nodeTexture
                    )
                )
            ).Texture;

            switch (nodeType)
            {
                case "proxy":
                    results.Add(new ProxyNode(tex, color, nodeName));
                    break;

                default:
                    throw new InvalidOperationException($"Unknown node type {nodeType}");
            }

            return DynValue.Void;
        })));

        script.DoFile(fileName);

        return results;
    }



}