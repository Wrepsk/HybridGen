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
                rng.Next(-1000000, 1000000)
            );
            _currentSeed = seed;
        }

        public RenderTexture Dispatch(NoiseSettings s, Vector2Int chunkCoord)
        {
            EnsureSeedOffset(s.seed);

            var rt = new RenderTexture(s.chunkSize, s.chunkSize, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();

            Vector2 chunkOffset = new Vector2(
                chunkCoord.x * (s.chunkSize - 1),
                chunkCoord.y * (s.chunkSize - 1)
            ) + _seedOffset;

            _shader.SetTexture(_kernel, "HeightmapOutput", rt);
            _shader.SetInt("ChunkSize", s.chunkSize);
            _shader.SetFloat("Scale", s.scale);
            _shader.SetVector("ChunkOffset", chunkOffset);
            _shader.SetInt("Octaves", s.octaves);
            _shader.SetFloat("Persistence", s.persistence);
            _shader.SetFloat("Lacunarity", s.lacunarity);

            int groups = Mathf.CeilToInt(s.chunkSize / 8f);
            _shader.Dispatch(_kernel, groups, groups, 1);

            return rt;
        }
    }
}