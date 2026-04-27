using System;
using UnityEngine;

namespace Lib.TerrainAnalysis
{
    [Serializable]
    public class TerrainAnalysisSettings
    {
        public bool enabled = true;

        [Min(1)]
        public int cellsPerAxis = 16;

        [Min(0f)]
        public float maxSlopeDegrees = 12f;

        [Min(0f)]
        public float maxRoughnessWorld = 1.25f;

        [Min(0f)]
        public float waterClearanceWorld = 2f;

        public bool debugOverlayEnabled = true;

        [Min(0)]
        public int debugOverlayChunkRadius = 2;
    }
}
