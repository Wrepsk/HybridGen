using System;
using System.Collections.Generic;
using Lib.TerrainAnalysis;
using UnityEngine;
using Random = System.Random;

namespace Lib.WFC
{
    public class WFCGenerator
    {
        readonly WFCModuleLibrary _moduleLibrary;

        struct Candidate
        {
            public Vector2Int Origin;
            public int        Size;
            public float      AverageScore;
            public float      SortKey;
        }

        struct RuntimeSettings
        {
            public int   MaxBuildingsPerChunk;
            public int   MinFootprintCells;
            public int   MaxFootprintCells;
            public float MinCellScore;
            public int   PlotPaddingCells;
            public int   MaxSolverRetries;
            public int   SeedOffset;
        }

        public WFCGenerator() : this(WFCModuleLibrary.CreateDefault())
        {
        }

        public WFCGenerator(WFCModuleLibrary moduleLibrary)
        {
            _moduleLibrary = moduleLibrary;
        }

        public WFCChunkGeneration Generate(TerrainChunkAnalysis analysis, WFCSettings settings, int worldSeed)
        {
            if (analysis == null || analysis.Cells == null || analysis.Cells.Length == 0)
                return new WFCChunkGeneration(Vector2Int.zero, Array.Empty<BuildingBlueprint>(), 0, 0);

            RuntimeSettings runtime = Sanitize(settings, analysis.CellsPerAxis);
            var rng = new Random(Hash(worldSeed + runtime.SeedOffset, analysis.ChunkCoord.x, analysis.ChunkCoord.y));
            List<Candidate> candidates = CollectCandidates(analysis, runtime, rng);
            var blueprints = new List<BuildingBlueprint>();
            var reserved = new bool[analysis.CellsPerAxis, analysis.CellsPerAxis];

            int attempts = 0;
            int failures = 0;

            foreach (Candidate candidate in candidates)
            {
                if (blueprints.Count >= runtime.MaxBuildingsPerChunk)
                    break;

                if (OverlapsReserved(candidate, reserved, runtime.PlotPaddingCells))
                    continue;

                bool solved = false;
                for (int retry = 0; retry < runtime.MaxSolverRetries; retry++)
                {
                    attempts++;
                    var solver = new WFCSolver(_moduleLibrary);
                    var solveRng = new Random(Hash(
                        worldSeed + runtime.SeedOffset + retry,
                        analysis.ChunkCoord.x * 31 + candidate.Origin.x,
                        analysis.ChunkCoord.y * 31 + candidate.Origin.y));

                    if (solver.TrySolve(candidate.Size, candidate.Size, solveRng, out WFCResult result))
                    {
                        blueprints.Add(BuildBlueprint(analysis, candidate, result));
                        Reserve(candidate, reserved, runtime.PlotPaddingCells);
                        solved = true;
                        break;
                    }

                    failures++;
                }

                if (!solved && runtime.MaxSolverRetries <= 0)
                    failures++;
            }

            return new WFCChunkGeneration(
                analysis.ChunkCoord,
                blueprints.ToArray(),
                attempts,
                failures);
        }

        static RuntimeSettings Sanitize(WFCSettings settings, int cellsPerAxis)
        {
            if (settings == null)
                settings = new WFCSettings();

            int minSize = Mathf.Clamp(settings.minFootprintCells, 3, Mathf.Max(3, cellsPerAxis));
            int maxSize = Mathf.Clamp(settings.maxFootprintCells, minSize, Mathf.Max(minSize, cellsPerAxis));

            return new RuntimeSettings
            {
                MaxBuildingsPerChunk = Mathf.Max(0, settings.maxBuildingsPerChunk),
                MinFootprintCells = minSize,
                MaxFootprintCells = maxSize,
                MinCellScore = Mathf.Clamp01(settings.minCellScore),
                PlotPaddingCells = Mathf.Max(0, settings.plotPaddingCells),
                MaxSolverRetries = Mathf.Max(1, settings.maxSolverRetries),
                SeedOffset = settings.seedOffset
            };
        }

        List<Candidate> CollectCandidates(TerrainChunkAnalysis analysis, RuntimeSettings settings, Random rng)
        {
            var candidates = new List<Candidate>();

            for (int size = settings.MaxFootprintCells; size >= settings.MinFootprintCells; size--)
            {
                for (int y = 0; y <= analysis.CellsPerAxis - size; y++)
                {
                    for (int x = 0; x <= analysis.CellsPerAxis - size; x++)
                    {
                        if (!TryEvaluateCandidate(analysis, x, y, size, settings.MinCellScore, out float avgScore))
                            continue;

                        candidates.Add(new Candidate
                        {
                            Origin = new Vector2Int(x, y),
                            Size = size,
                            AverageScore = avgScore,
                            SortKey = avgScore + (float)rng.NextDouble() * 0.025f
                        });
                    }
                }
            }

            candidates.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));
            return candidates;
        }

        static bool TryEvaluateCandidate(
            TerrainChunkAnalysis analysis,
            int originX,
            int originY,
            int size,
            float minScore,
            out float averageScore)
        {
            float scoreSum = 0f;

            for (int y = originY; y < originY + size; y++)
            {
                for (int x = originX; x < originX + size; x++)
                {
                    TerrainBuildCell cell = analysis.Cells[y * analysis.CellsPerAxis + x];
                    if (!cell.IsBuildable || cell.Score < minScore)
                    {
                        averageScore = 0f;
                        return false;
                    }

                    scoreSum += cell.Score;
                }
            }

            averageScore = scoreSum / (size * size);
            return true;
        }

        BuildingBlueprint BuildBlueprint(TerrainChunkAnalysis analysis, Candidate candidate, WFCResult result)
        {
            var tiles = new WFCPlacedTile[result.Width * result.Height];
            int tileIndex = 0;

            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    TerrainBuildCell terrainCell = analysis.Cells[
                        (candidate.Origin.y + y) * analysis.CellsPerAxis + candidate.Origin.x + x];
                    WFCModule module = result.Get(x, y);

                    tiles[tileIndex++] = new WFCPlacedTile(
                        new Vector2Int(x, y),
                        terrainCell.GlobalCoord,
                        terrainCell.WorldCenter,
                        analysis.CellWorldSize,
                        module);
                }
            }

            return new BuildingBlueprint(
                analysis.ChunkCoord,
                candidate.Origin,
                new Vector2Int(candidate.Size, candidate.Size),
                candidate.AverageScore,
                tiles);
        }

        static bool OverlapsReserved(Candidate candidate, bool[,] reserved, int padding)
        {
            int minX = Mathf.Max(0, candidate.Origin.x - padding);
            int minY = Mathf.Max(0, candidate.Origin.y - padding);
            int maxX = Mathf.Min(reserved.GetLength(0) - 1, candidate.Origin.x + candidate.Size - 1 + padding);
            int maxY = Mathf.Min(reserved.GetLength(1) - 1, candidate.Origin.y + candidate.Size - 1 + padding);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (reserved[x, y])
                        return true;

            return false;
        }

        static void Reserve(Candidate candidate, bool[,] reserved, int padding)
        {
            int minX = Mathf.Max(0, candidate.Origin.x - padding);
            int minY = Mathf.Max(0, candidate.Origin.y - padding);
            int maxX = Mathf.Min(reserved.GetLength(0) - 1, candidate.Origin.x + candidate.Size - 1 + padding);
            int maxY = Mathf.Min(reserved.GetLength(1) - 1, candidate.Origin.y + candidate.Size - 1 + padding);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    reserved[x, y] = true;
        }

        static int Hash(int seed, int x, int y)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + seed;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                return hash & int.MaxValue;
            }
        }
    }
}
