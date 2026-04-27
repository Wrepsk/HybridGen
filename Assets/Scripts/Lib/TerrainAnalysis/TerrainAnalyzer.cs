using Lib.TerrainGen;
using UnityEngine;

namespace Lib.TerrainAnalysis
{
    public class TerrainAnalyzer
    {
        static readonly float[] SamplePattern = { 1f / 6f, 0.5f, 5f / 6f };

        struct RuntimeSettings
        {
            public int   CellsPerAxis;
            public float MaxSlopeDegrees;
            public float MaxRoughnessWorld;
            public float WaterClearanceWorld;
        }

        public TerrainChunkAnalysis Analyze(
            Vector2Int chunkCoord,
            float[] heightmap,
            NoiseSettings noiseSettings,
            TerrainAnalysisSettings settings,
            float heightMultiplier,
            AnimationCurve heightCurve)
        {
            if (heightmap == null || noiseSettings == null || heightCurve == null)
            {
                Debug.LogWarning($"[TerrainAnalyzer] Skipping analysis for chunk {chunkCoord}: missing input data.");
                return null;
            }

            int chunkSize = noiseSettings.chunkSize;
            if (chunkSize <= 1 || heightmap.Length != chunkSize * chunkSize)
            {
                Debug.LogWarning(
                    $"[TerrainAnalyzer] Skipping analysis for chunk {chunkCoord}: invalid heightmap length {heightmap.Length} for chunk size {chunkSize}.");
                return null;
            }

            RuntimeSettings runtime = Sanitize(settings, chunkSize);
            float chunkWorldSize = chunkSize - 1;
            float cellWorldSize = chunkWorldSize / runtime.CellsPerAxis;
            float waterHeight = heightCurve.Evaluate(noiseSettings.seaLevel) * heightMultiplier;

            var cells = new TerrainBuildCell[runtime.CellsPerAxis * runtime.CellsPerAxis];
            int buildableCount = 0;

            for (int y = 0; y < runtime.CellsPerAxis; y++)
            {
                float cellMinZ = y * cellWorldSize;
                float cellMaxZ = y == runtime.CellsPerAxis - 1
                    ? chunkWorldSize
                    : (y + 1) * cellWorldSize;

                for (int x = 0; x < runtime.CellsPerAxis; x++)
                {
                    float cellMinX = x * cellWorldSize;
                    float cellMaxX = x == runtime.CellsPerAxis - 1
                        ? chunkWorldSize
                        : (x + 1) * cellWorldSize;

                    TerrainBuildCell cell = AnalyzeCell(
                        chunkCoord,
                        x,
                        y,
                        cellMinX,
                        cellMaxX,
                        cellMinZ,
                        cellMaxZ,
                        heightmap,
                        chunkSize,
                        chunkWorldSize,
                        heightCurve,
                        heightMultiplier,
                        runtime,
                        waterHeight);

                    if (cell.IsBuildable)
                        buildableCount++;

                    cells[y * runtime.CellsPerAxis + x] = cell;
                }
            }

            return new TerrainChunkAnalysis(
                chunkCoord,
                runtime.CellsPerAxis,
                cellWorldSize,
                buildableCount,
                cells.Length - buildableCount,
                cells);
        }

        static RuntimeSettings Sanitize(TerrainAnalysisSettings settings, int chunkSize)
        {
            if (settings == null)
                settings = new TerrainAnalysisSettings();

            return new RuntimeSettings
            {
                CellsPerAxis = Mathf.Clamp(settings.cellsPerAxis, 1, Mathf.Max(1, chunkSize - 1)),
                MaxSlopeDegrees = Mathf.Max(0f, settings.maxSlopeDegrees),
                MaxRoughnessWorld = Mathf.Max(0f, settings.maxRoughnessWorld),
                WaterClearanceWorld = Mathf.Max(0f, settings.waterClearanceWorld)
            };
        }

        TerrainBuildCell AnalyzeCell(
            Vector2Int chunkCoord,
            int localX,
            int localY,
            float cellMinX,
            float cellMaxX,
            float cellMinZ,
            float cellMaxZ,
            float[] heightmap,
            int chunkSize,
            float chunkWorldSize,
            AnimationCurve heightCurve,
            float heightMultiplier,
            RuntimeSettings runtime,
            float waterHeight)
        {
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            float sumHeight = 0f;

            foreach (float sampleZ in SamplePattern)
            {
                float localZ = Mathf.Lerp(cellMinZ, cellMaxZ, sampleZ);
                foreach (float sampleX in SamplePattern)
                {
                    float localPosX = Mathf.Lerp(cellMinX, cellMaxX, sampleX);
                    float worldHeight = SampleWorldHeight(
                        heightmap,
                        chunkSize,
                        localPosX,
                        localZ,
                        heightCurve,
                        heightMultiplier);

                    minHeight = Mathf.Min(minHeight, worldHeight);
                    maxHeight = Mathf.Max(maxHeight, worldHeight);
                    sumHeight += worldHeight;
                }
            }

            float avgHeight = sumHeight / 9f;
            float centerX = (cellMinX + cellMaxX) * 0.5f;
            float centerZ = (cellMinZ + cellMaxZ) * 0.5f;
            float halfWidth = Mathf.Max((cellMaxX - cellMinX) * 0.5f, 0.0001f);
            float halfDepth = Mathf.Max((cellMaxZ - cellMinZ) * 0.5f, 0.0001f);

            float heightLeft = SampleWorldHeight(
                heightmap,
                chunkSize,
                centerX - halfWidth,
                centerZ,
                heightCurve,
                heightMultiplier);
            float heightRight = SampleWorldHeight(
                heightmap,
                chunkSize,
                centerX + halfWidth,
                centerZ,
                heightCurve,
                heightMultiplier);
            float heightBack = SampleWorldHeight(
                heightmap,
                chunkSize,
                centerX,
                centerZ - halfDepth,
                heightCurve,
                heightMultiplier);
            float heightForward = SampleWorldHeight(
                heightmap,
                chunkSize,
                centerX,
                centerZ + halfDepth,
                heightCurve,
                heightMultiplier);

            float slopeX = (heightRight - heightLeft) / (halfWidth * 2f);
            float slopeZ = (heightForward - heightBack) / (halfDepth * 2f);
            float slopeDegrees = Mathf.Atan(Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ)) * Mathf.Rad2Deg;
            float roughness = maxHeight - minHeight;

            BuildabilityFlags flags = BuildabilityFlags.None;
            if (minHeight <= waterHeight + runtime.WaterClearanceWorld)
                flags |= BuildabilityFlags.NearWater;
            if (slopeDegrees > runtime.MaxSlopeDegrees)
                flags |= BuildabilityFlags.TooSteep;
            if (roughness > runtime.MaxRoughnessWorld)
                flags |= BuildabilityFlags.TooRough;

            bool isBuildable = flags == BuildabilityFlags.None;
            float score = isBuildable
                ? ComputeScore(slopeDegrees, roughness, runtime.MaxSlopeDegrees, runtime.MaxRoughnessWorld)
                : 0f;

            var localCoord = new Vector2Int(localX, localY);
            var globalCoord = new Vector2Int(
                chunkCoord.x * runtime.CellsPerAxis + localX,
                chunkCoord.y * runtime.CellsPerAxis + localY);

            float chunkOriginX = chunkCoord.x * chunkWorldSize;
            float chunkOriginZ = chunkCoord.y * chunkWorldSize;

            return new TerrainBuildCell(
                localCoord,
                globalCoord,
                new Vector3(chunkOriginX + centerX, avgHeight, chunkOriginZ + centerZ),
                avgHeight,
                minHeight,
                maxHeight,
                slopeDegrees,
                roughness,
                score,
                flags,
                isBuildable);
        }

        static float ComputeScore(float slopeDegrees, float roughness, float maxSlopeDegrees, float maxRoughnessWorld)
        {
            float slopeScore = 1f - Mathf.Clamp01(slopeDegrees / Mathf.Max(maxSlopeDegrees, 0.0001f));
            float roughnessScore = 1f - Mathf.Clamp01(roughness / Mathf.Max(maxRoughnessWorld, 0.0001f));
            return Mathf.Clamp01((slopeScore + roughnessScore) * 0.5f);
        }

        static float SampleWorldHeight(
            float[] heightmap,
            int chunkSize,
            float localX,
            float localZ,
            AnimationCurve heightCurve,
            float heightMultiplier)
        {
            float normalizedHeight = SampleNormalizedHeight(heightmap, chunkSize, localX, localZ);
            return heightCurve.Evaluate(normalizedHeight) * heightMultiplier;
        }

        static float SampleNormalizedHeight(float[] heightmap, int chunkSize, float localX, float localZ)
        {
            float clampedX = Mathf.Clamp(localX, 0f, chunkSize - 1);
            float clampedZ = Mathf.Clamp(localZ, 0f, chunkSize - 1);

            int x0 = Mathf.FloorToInt(clampedX);
            int z0 = Mathf.FloorToInt(clampedZ);
            int x1 = Mathf.Min(x0 + 1, chunkSize - 1);
            int z1 = Mathf.Min(z0 + 1, chunkSize - 1);

            float tx = clampedX - x0;
            float tz = clampedZ - z0;

            float h00 = heightmap[z0 * chunkSize + x0];
            float h10 = heightmap[z0 * chunkSize + x1];
            float h01 = heightmap[z1 * chunkSize + x0];
            float h11 = heightmap[z1 * chunkSize + x1];

            float hx0 = Mathf.Lerp(h00, h10, tx);
            float hx1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(hx0, hx1, tz);
        }
    }
}
