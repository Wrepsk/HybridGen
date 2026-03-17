using System.Collections.Generic;
using Lib.TerrainGen;
using UnityEngine;

namespace Rendering
{
    public class ChunkManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ComputeShader noiseShader;
        [SerializeField] Material terrainMaterial;

        [Header("Height")]
        [SerializeField] float heightMultiplier = 40f;
        [SerializeField] AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Settings")]
        [SerializeField] NoiseSettings noiseSettings = new();
        [SerializeField] int viewDistance = 3;

        NoiseGenerator _noiseGen;
        HeightmapReadback _readback;
        MeshGenerator _meshGen;

        readonly Dictionary<Vector2Int, Chunk> _chunks = new();
        readonly Dictionary<Vector2Int, ChunkView> _views = new();
        readonly HashSet<Vector2Int> _pending = new();
        Transform _viewer;

        void Awake()
        {
            _noiseGen = new NoiseGenerator(noiseShader);
            _readback = new HeightmapReadback();
            _meshGen = new MeshGenerator(heightMultiplier, heightCurve);
            _viewer = Camera.main.transform;
        }

        void Update()
        {
            Vector2Int viewerChunk = WorldToChunkCoord(_viewer.position);
            RequestVisibleChunks(viewerChunk);
        }

        void RequestVisibleChunks(Vector2Int center)
        {
            for (int x = -viewDistance; x <= viewDistance; x++)
                for (int y = -viewDistance; y <= viewDistance; y++)
                {
                    var coord = new Vector2Int(center.x + x, center.y + y);
                    if (_chunks.ContainsKey(coord) || _pending.Contains(coord)) continue;
                    RequestChunk(coord);
                }
        }

        void RequestChunk(Vector2Int coord)
        {
            _pending.Add(coord);
            var chunk = new Chunk(coord) { State = ChunkState.GeneratingNoise };
            chunk.GpuTexture = _noiseGen.Dispatch(noiseSettings, coord);
            _readback.Request(chunk, OnChunkReady);
        }

        void OnChunkReady(Chunk chunk)
        {
            _pending.Remove(chunk.Coord);
            _chunks[chunk.Coord] = chunk;
            if (chunk.State == ChunkState.Ready)
                SpawnChunkView(chunk);
        }

        void SpawnChunkView(Chunk chunk)
        {
            Mesh mesh = _meshGen.BuildMesh(chunk.Heightmap, noiseSettings.chunkSize);

            var view = new ChunkView(chunk.Coord, noiseSettings.chunkSize,
                                     terrainMaterial, transform);
            view.ApplyMesh(mesh);
            _views[chunk.Coord] = view;
        }

        Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            int size = noiseSettings.chunkSize - 1;
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / size),
                Mathf.FloorToInt(worldPos.z / size));
        }
        
        [ContextMenu("Regenerate Terrain")]
        public void RegenerateTerrain()
        {
            _meshGen = new MeshGenerator(heightMultiplier, heightCurve);
            _noiseGen = new NoiseGenerator(noiseShader); 

            _chunks.Clear();
            _views.Clear();
            _pending.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            if (Application.isPlaying && _viewer != null)
            {
                Vector2Int viewerChunk = WorldToChunkCoord(_viewer.position);
                RequestVisibleChunks(viewerChunk);
            }
        }
    }
}