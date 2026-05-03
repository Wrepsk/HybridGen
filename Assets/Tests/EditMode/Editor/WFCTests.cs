using Lib.TerrainAnalysis;
using Lib.WFC;
using NUnit.Framework;
using UnityEngine;

public class WFCTests
{
    [Test]
    public void Solver_DefaultLibrary_ProducesValidAdjacency()
    {
        WFCModuleLibrary library = WFCModuleLibrary.CreateDefault();
        var solver = new WFCSolver(library);

        bool solved = solver.TrySolve(5, 5, new System.Random(1234), out WFCResult result);

        Assert.That(solved, Is.True);
        Assert.That(result, Is.Not.Null);

        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                WFCModule module = result.Get(x, y);
                bool isBoundary = x == 0 || y == 0 || x == result.Width - 1 || y == result.Height - 1;
                if (isBoundary)
                    Assert.That(module.Type, Is.Not.EqualTo(WFCModuleType.Floor));

                AssertValidNeighbor(library, result, x, y, x + 1, y, WFCDirection.East);
                AssertValidNeighbor(library, result, x, y, x, y + 1, WFCDirection.North);
            }
        }
    }

    [Test]
    public void Generator_BuildableAnalysis_CreatesBlueprint()
    {
        var generator = new WFCGenerator();
        TerrainChunkAnalysis analysis = CreateAnalysis(8, true);
        var settings = new WFCSettings
        {
            maxBuildingsPerChunk = 1,
            minFootprintCells = 5,
            maxFootprintCells = 5,
            minCellScore = 0f,
            maxSolverRetries = 4
        };

        WFCChunkGeneration generation = generator.Generate(analysis, settings, worldSeed: 42);

        Assert.That(generation.SuccessCount, Is.EqualTo(1));
        Assert.That(generation.AttemptCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(generation.Blueprints[0].Tiles.Length, Is.EqualTo(25));
    }

    [Test]
    public void Generator_BlockedAnalysis_CreatesNoBlueprints()
    {
        var generator = new WFCGenerator();
        TerrainChunkAnalysis analysis = CreateAnalysis(8, false);

        WFCChunkGeneration generation = generator.Generate(analysis, new WFCSettings(), worldSeed: 42);

        Assert.That(generation.SuccessCount, Is.EqualTo(0));
        Assert.That(generation.AttemptCount, Is.EqualTo(0));
    }

    [Test]
    public void Generator_SameInput_IsDeterministic()
    {
        var generator = new WFCGenerator();
        TerrainChunkAnalysis analysis = CreateAnalysis(8, true);
        var settings = new WFCSettings
        {
            maxBuildingsPerChunk = 1,
            minFootprintCells = 5,
            maxFootprintCells = 5,
            minCellScore = 0f,
            maxSolverRetries = 4
        };

        WFCChunkGeneration first = generator.Generate(analysis, settings, worldSeed: 99);
        WFCChunkGeneration second = generator.Generate(analysis, settings, worldSeed: 99);

        Assert.That(first.SuccessCount, Is.EqualTo(1));
        Assert.That(second.SuccessCount, Is.EqualTo(first.SuccessCount));
        Assert.That(second.Blueprints[0].FootprintOrigin, Is.EqualTo(first.Blueprints[0].FootprintOrigin));

        for (int i = 0; i < first.Blueprints[0].Tiles.Length; i++)
        {
            Assert.That(
                second.Blueprints[0].Tiles[i].Module.Type,
                Is.EqualTo(first.Blueprints[0].Tiles[i].Module.Type));
        }
    }

    static void AssertValidNeighbor(
        WFCModuleLibrary library,
        WFCResult result,
        int x,
        int y,
        int neighborX,
        int neighborY,
        WFCDirection direction)
    {
        if (neighborX < 0 || neighborY < 0 || neighborX >= result.Width || neighborY >= result.Height)
            return;

        WFCModule module = result.Get(x, y);
        WFCModule neighbor = result.Get(neighborX, neighborY);
        Assert.That(library.IsAllowed(module.Id, direction, neighbor.Id), Is.True);
    }

    static TerrainChunkAnalysis CreateAnalysis(int cellsPerAxis, bool buildable)
    {
        var cells = new TerrainBuildCell[cellsPerAxis * cellsPerAxis];
        int buildableCount = 0;

        for (int y = 0; y < cellsPerAxis; y++)
        {
            for (int x = 0; x < cellsPerAxis; x++)
            {
                BuildabilityFlags flags = buildable ? BuildabilityFlags.None : BuildabilityFlags.TooSteep;
                if (buildable)
                    buildableCount++;

                cells[y * cellsPerAxis + x] = new TerrainBuildCell(
                    new Vector2Int(x, y),
                    new Vector2Int(x, y),
                    new Vector3(x * 8f, 0f, y * 8f),
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    buildable ? 1f : 0f,
                    flags,
                    buildable);
            }
        }

        return new TerrainChunkAnalysis(
            Vector2Int.zero,
            cellsPerAxis,
            8f,
            buildableCount,
            cells.Length - buildableCount,
            cells);
    }
}
