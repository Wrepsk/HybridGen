using UnityEngine;

namespace Lib.TerrainGen
{
    public class NoiseGenerator
    {
        readonly ComputeShader _shader;
        readonly int _kernel;

        Vector2 _seedOffset;
        int _currentSeed = int.MinValue;

        public NoiseGenerator(ComputeShader shader)
        {
            _shader = shader;
            _kernel = shader.FindKernel("GenerateHeightmap");
        }

        void EnsureSeedOffset(int seed)
        {
            if (_currentSeed == seed) return;
            var rng = new System.Random(seed);
            _seedOffset = new Vector2(
                rng.Next(-1000000, 1000000),
                rng.Next(-1000000, 1000000));
            _currentSeed = seed;
        }

        public RenderTexture Dispatch(NoiseSettings s, Vector2Int chunkCoord,
                                      out RenderTexture moistureTexture)
        {
            EnsureSeedOffset(s.seed);

            var heightRT = new RenderTexture(s.chunkSize, s.chunkSize, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            heightRT.Create();

            var moistRT = new RenderTexture(s.chunkSize, s.chunkSize, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            moistRT.Create();

            Vector2 chunkOffset = new Vector2(
                chunkCoord.x * (s.chunkSize - 1),
                chunkCoord.y * (s.chunkSize - 1)) + _seedOffset;

            _shader.SetTexture(_kernel, "HeightmapOutput", heightRT);
            _shader.SetTexture(_kernel, "MoistureOutput", moistRT);
            _shader.SetInt("ChunkSize", s.chunkSize);
            _shader.SetFloat("Scale", s.scale);
            _shader.SetVector("ChunkOffset", chunkOffset);
            _shader.SetInt("Octaves", s.octaves);
            _shader.SetFloat("Persistence", s.persistence);
            _shader.SetFloat("Lacunarity", s.lacunarity);
            _shader.SetFloat("ContinentScale", s.continentScale);
            _shader.SetFloat("WarpStrength", s.warpStrength);
            _shader.SetFloat("RidgeWeight", s.ridgeWeight);
            _shader.SetFloat("ContinentBias", s.continentBias);
            _shader.SetFloat("SeaLevel", s.seaLevel);
            _shader.SetFloat("CoastBlend", s.coastBlend);
            _shader.SetFloat("MountainStart", s.mountainStart);
            _shader.SetFloat("MountainBlend", s.mountainBlend);
            _shader.SetFloat("OceanDepth", s.oceanDepth);
            _shader.SetFloat("PlainsHeight", s.plainsHeight);
            _shader.SetFloat("MountainHeight", s.mountainHeight);

            int groups = Mathf.CeilToInt(s.chunkSize / 8f);
            _shader.Dispatch(_kernel, groups, groups, 1);

            moistureTexture = moistRT;
            return heightRT;
        }
    }
}
