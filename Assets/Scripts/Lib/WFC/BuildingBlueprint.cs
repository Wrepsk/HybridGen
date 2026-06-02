using UnityEngine;

namespace Lib.WFC
{
    public class BuildingBlueprint
    {
        public Vector2Int ChunkCoord { get; }
        public Vector2Int FootprintOrigin { get; }
        public Vector2Int FootprintSize { get; }
        public float AverageScore { get; }
        public WFCPlacedTile[] Tiles { get; }

        public BuildingBlueprint(
            Vector2Int chunkCoord,
            Vector2Int footprintOrigin,
            Vector2Int footprintSize,
            float averageScore,
            WFCPlacedTile[] tiles)
        {
            ChunkCoord = chunkCoord;
            FootprintOrigin = footprintOrigin;
            FootprintSize = footprintSize;
            AverageScore = averageScore;
            Tiles = tiles;
        }
    }
}
