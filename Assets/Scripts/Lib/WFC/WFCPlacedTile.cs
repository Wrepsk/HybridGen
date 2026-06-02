using UnityEngine;

namespace Lib.WFC
{
    public readonly struct WFCPlacedTile
    {
        public Vector2Int LocalCoord { get; }
        public Vector2Int GlobalCoord { get; }
        public Vector3 WorldCenter { get; }
        public float CellWorldSize { get; }
        public WFCModule Module { get; }

        public WFCPlacedTile(
            Vector2Int localCoord,
            Vector2Int globalCoord,
            Vector3 worldCenter,
            float cellWorldSize,
            WFCModule module)
        {
            LocalCoord = localCoord;
            GlobalCoord = globalCoord;
            WorldCenter = worldCenter;
            CellWorldSize = cellWorldSize;
            Module = module;
        }
    }
}
