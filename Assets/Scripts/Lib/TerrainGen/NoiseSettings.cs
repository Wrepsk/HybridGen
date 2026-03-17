using System;

namespace Lib.TerrainGen
{
    [Serializable]
    public class NoiseSettings
    {
        public int seed = 12345;
        public int chunkSize = 128;
        public float scale = 100f;
        public int octaves = 6;
        public float persistence = 0.5f;
        public float lacunarity = 2.0f;
    }
}