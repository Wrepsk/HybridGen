using UnityEngine;

namespace Lib.TerrainAnalysis
{
    public class TerrainChunkAnalysis
    {
        public Vector2Int ChunkCoord { get; }
        public int CellsPerAxis { get; }
        public float CellWorldSize { get; }
        public int BuildableCount { get; }
        public int BlockedCount { get; }
        public TerrainBuildCell[] Cells { get; }
        public int TotalCellCount  => Cells?.Length ?? 0;

        public TerrainChunkAnalysis(
            Vector2Int chunkCoord,
            int cellsPerAxis,
            float cellWorldSize,
            int buildableCount,
            int blockedCount,
            TerrainBuildCell[] cells)
        {
            ChunkCoord = chunkCoord;
            CellsPerAxis = cellsPerAxis;
            CellWorldSize = cellWorldSize;
            BuildableCount = buildableCount;
            BlockedCount = blockedCount;
            Cells = cells;
        }
    }
}
