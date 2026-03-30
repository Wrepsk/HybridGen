using System;

namespace Lib.TerrainGen
{
    [Serializable]
    public class NoiseSettings
    {
        public int   seed          = 12345;
        public int   chunkSize     = 128;
        public float scale         = 150f;
        public int   octaves       = 6;
        public float persistence   = 0.45f;
        public float lacunarity    = 2.0f;

        public float continentScale = 5f;

        public float warpStrength   = 0.5f;

        public float ridgeWeight    = 0.65f;

        public float continentBias  = 0.25f;

        public float waterLevel     = 0.15f;
    }
}
