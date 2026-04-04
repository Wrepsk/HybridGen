using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lib.TerrainGen
{
    [Serializable]
    public class NoiseSettings
    {
        [Header("Base Noise")]
        public int   seed          = 12345;
        public int   chunkSize     = 128;
        public float scale         = 150f;
        public int   octaves       = 6;
        public float persistence   = 0.45f;
        public float lacunarity    = 2.0f;

        [Header("Continents")]
        public float continentScale = 5f;
        public float continentBias  = 0.25f;

        [Header("Terrain Warping")]
        public float warpStrength   = 0.5f;

        [Header("Mountains")]
        public float ridgeWeight    = 0.65f;
        public float mountainStart  = 0.72f;
        public float mountainBlend  = 0.16f;
        public float mountainHeight = 0.42f;

        [Header("Sea And Plains")]
        [FormerlySerializedAs("waterLevel")]
        public float seaLevel       = 0.42f;
        public float coastBlend     = 0.08f;
        public float oceanDepth     = 0.32f;
        public float plainsHeight   = 0.18f;
    }
}
