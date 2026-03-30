using UnityEngine;

namespace Lib.TerrainGen
{
    public enum ChunkState { Requested, GeneratingNoise, ReadingBack, Ready, Failed }

    public class Chunk
    {
        public Vector2Int    Coord           { get; }
        public ChunkState    State           { get; set; } = ChunkState.Requested;
        public float[]       Heightmap       { get; set; }

        public RenderTexture GpuTexture      { get; set; }

        public RenderTexture MoistureTexture { get; set; }

        public float RequestTime { get; set; }

        public Chunk(Vector2Int coord) => Coord = coord;

        public void ReleaseGpuTexture()
        {
            if (GpuTexture == null) return;
            GpuTexture.Release();
            Object.Destroy(GpuTexture);
            GpuTexture = null;
        }

        public void ReleaseMoistureTexture()
        {
            if (MoistureTexture == null) return;
            MoistureTexture.Release();
            Object.Destroy(MoistureTexture);
            MoistureTexture = null;
        }
    }
}
