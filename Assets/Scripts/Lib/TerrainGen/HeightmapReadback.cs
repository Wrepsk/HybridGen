using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lib.TerrainGen
{
    
    public class HeightmapReadback
    {
        public void Request(Chunk chunk, Action<Chunk> onComplete)
        {
            chunk.State = ChunkState.ReadingBack;

            AsyncGPUReadback.Request(chunk.GpuTexture, 0, TextureFormat.RFloat, req =>
            {
                if (req.hasError)
                {
                    Debug.LogWarning($"[HeightmapReadback] GPU readback failed for chunk {chunk.Coord}");
                    chunk.State = ChunkState.Failed;
                    chunk.ReleaseGpuTexture();
                    onComplete?.Invoke(chunk);
                    return;
                }

                NativeArray<float> raw = req.GetData<float>();
                chunk.Heightmap = raw.ToArray();
                chunk.State = ChunkState.Ready;
                chunk.ReleaseGpuTexture();

                onComplete?.Invoke(chunk);
            });
        }
    }
}