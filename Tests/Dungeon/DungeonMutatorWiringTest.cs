using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.Dungeon;

/// <summary>
/// The mutator must be present in the generated dungeon scene; its behaviour is covered by the
/// scene-free model and topology suites.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DungeonMutatorWiringTest
{
    [TestCase]
    public void DungeonGeneratorScene_HasAMutatorChild()
    {
        var scene = GD.Load<PackedScene>("res://Src/Dungeon/DungeonGenerator.tscn");
        var generator = scene.Instantiate();

        var mutator = generator.GetNodeOrNull<DungeonMutator>("Mutator");

        AssertObject(mutator).IsNotNull();
        AssertObject(mutator!.GetParent()).IsSame(generator);

        generator.Free();
    }
}
