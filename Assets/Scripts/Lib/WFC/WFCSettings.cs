using System;
using UnityEngine;

namespace Lib.WFC
{
    [Serializable]
    public class WFCSettings
    {
        public bool enabled = true;

        [Min(0)]
        public int buildingChunkRadius = 2;

        [Min(1)]
        public int maxBuildingsPerChunk = 1;

        [Min(3)]
        public int minFootprintCells = 3;

        [Min(3)]
        public int maxFootprintCells = 5;

        [Range(0f, 1f)]
        public float minCellScore = 0.25f;

        [Min(0)]
        public int plotPaddingCells = 1;

        [Min(1)]
        public int maxSolverRetries = 8;

        public int seedOffset = 918273;

        [Range(0.1f, 1f)]
        public float moduleScale = 0.82f;

        [Min(0.05f)]
        public float floorThickness = 0.25f;

        [Min(0.1f)]
        public float wallHeight = 3f;

        [Min(0.05f)]
        public float roofThickness = 0.35f;

        [Min(0f)]
        public float roofOverhang = 0.35f;
    }
}
