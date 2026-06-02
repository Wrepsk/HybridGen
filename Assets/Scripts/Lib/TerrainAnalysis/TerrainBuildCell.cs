using UnityEngine;

namespace Lib.TerrainAnalysis
{
    public readonly struct TerrainBuildCell
    {
        public Vector2Int LocalCoord { get; }
        public Vector2Int GlobalCoord { get; }
        public Vector3 WorldCenter { get; }
        public float AvgHeightWorld { get; }
        public float MinHeightWorld { get; }
        public float MaxHeightWorld { get; }
        public float SlopeDegrees { get; }
        public float Roughness { get; }
        public float Score { get; }
        public BuildabilityFlags Flags { get; }
        public bool IsBuildable { get; }

        public TerrainBuildCell(
            Vector2Int localCoord,
            Vector2Int globalCoord,
            Vector3 worldCenter,
            float avgHeightWorld,
            float minHeightWorld,
            float maxHeightWorld,
            float slopeDegrees,
            float roughness,
            float score,
            BuildabilityFlags flags,
            bool isBuildable)
        {
            LocalCoord = localCoord;
            GlobalCoord = globalCoord;
            WorldCenter = worldCenter;
            AvgHeightWorld = avgHeightWorld;
            MinHeightWorld = minHeightWorld;
            MaxHeightWorld = maxHeightWorld;
            SlopeDegrees = slopeDegrees;
            Roughness = roughness;
            Score = score;
            Flags = flags;
            IsBuildable = isBuildable;
        }
    }
}
