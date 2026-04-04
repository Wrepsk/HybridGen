using System.Collections.Generic;
using Lib.TerrainGen;
using UnityEngine;
using UnityEngine.Profiling;

namespace Rendering
{
    public struct ChunkMetrics
    {
        public int   ActiveChunks;
        public int   TotalGenerated;
        public float LastGenTimeMs;
        public float AvgGenTimeMs;
        public float MinGenTimeMs;
        public float MaxGenTimeMs;
        public float AllocatedMemoryMB;
        public float GpuTextureMB;
    }

    public class ChunkManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ComputeShader noiseShader;
        [SerializeField] Material      terrainMaterial;

        [Header("Height")]
        [SerializeField] float          heightMultiplier = 40f;
        [SerializeField] AnimationCurve heightCurve      = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Settings")]
        [SerializeField] NoiseSettings noiseSettings = new();
        [SerializeField] int           viewDistance   = 3;

        [Header("Fog")]
        [SerializeField] Color fogColor = new Color(0.65f, 0.77f, 0.90f);

        Lib.TerrainGen.NoiseGenerator _noiseGen;
        HeightmapReadback _readback;
        MeshGenerator _meshGen;

        readonly Dictionary<Vector2Int, Chunk>     _chunks  = new();
        readonly Dictionary<Vector2Int, ChunkView> _views   = new();
        readonly HashSet<Vector2Int>               _pending = new();
        readonly List<Vector2Int> _removeBuffer = new();

        Transform  _viewer;
        GameObject _waterPlane;

        int   _genCount;
        float _lastGenTime;
        float _minGenTime = float.MaxValue;
        float _maxGenTime;
        float _genTimeSum;

        public ChunkMetrics Metrics
        {
            get
            {
                int chunkSize = noiseSettings.chunkSize;
                float gpuBytes = _chunks.Count * chunkSize * chunkSize * 4f;

                return new ChunkMetrics
                {
                    ActiveChunks    = _chunks.Count,
                    TotalGenerated  = _genCount,
                    LastGenTimeMs   = _lastGenTime,
                    AvgGenTimeMs    = _genCount > 0 ? _genTimeSum / _genCount : 0f,
                    MinGenTimeMs    = _genCount > 0 ? _minGenTime : 0f,
                    MaxGenTimeMs    = _maxGenTime,
                    AllocatedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    GpuTextureMB      = gpuBytes / (1024f * 1024f)
                };
            }
        }

        void Awake()
        {
            _noiseGen = new Lib.TerrainGen.NoiseGenerator(noiseShader);
            _readback = new HeightmapReadback();
            _meshGen  = new MeshGenerator(heightMultiplier, heightCurve);
            _viewer   = Camera.main.transform;

            SyncTerrainMaterialSettings();
            SetupFog();
            CreateWaterPlane();
        }

        void SyncTerrainMaterialSettings()
        {
            if (terrainMaterial == null) return;

            terrainMaterial.SetFloat("_MaxHeight", heightMultiplier);

            float normalizedWaterLevel = Mathf.Clamp01(heightCurve.Evaluate(noiseSettings.seaLevel));
            terrainMaterial.SetFloat("_WaterLevel", normalizedWaterLevel);
            terrainMaterial.SetFloat("_SandLevel", normalizedWaterLevel);
        }

        void SetupFog()
        {
            float chunkWorldSize = noiseSettings.chunkSize - 1;
            float maxViewDist    = viewDistance * chunkWorldSize;

            RenderSettings.fog               = true;
            RenderSettings.fogMode           = FogMode.Linear;
            RenderSettings.fogStartDistance  = maxViewDist * 0.45f;
            RenderSettings.fogEndDistance    = maxViewDist * 0.92f;
            RenderSettings.fogColor          = fogColor;

            Camera.main.backgroundColor = fogColor;
        }

        void CreateWaterPlane()
        {
            _waterPlane = new GameObject("WaterPlane");
            _waterPlane.transform.parent = transform;

            var mf = _waterPlane.AddComponent<MeshFilter>();
            var mr = _waterPlane.AddComponent<MeshRenderer>();

            float extent = (viewDistance + 3) * (noiseSettings.chunkSize - 1);
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-extent, 0, -extent),
                new Vector3(-extent, 0,  extent),
                new Vector3( extent, 0,  extent),
                new Vector3( extent, 0, -extent)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals   = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv        = new[] { new Vector2(0, 0), new Vector2(0, 1),
                                     new Vector2(1, 1), new Vector2(1, 0) };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0.05f, 0.18f, 0.50f);
            mat.SetFloat("_Smoothness", 0.92f);
            mr.material = mat;
        }

        void Update()
        {
            Vector2Int viewerChunk = WorldToChunkCoord(_viewer.position);
            RequestVisibleChunks(viewerChunk);
            UnloadDistantChunks(viewerChunk);

            if (_waterPlane != null)
            {
                float waterY = heightCurve.Evaluate(noiseSettings.seaLevel) * heightMultiplier;
                _waterPlane.transform.position = new Vector3(
                    _viewer.position.x, waterY, _viewer.position.z);
            }
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

        void UnloadDistantChunks(Vector2Int center)
        {
            int unloadDist = viewDistance + 2;

            _removeBuffer.Clear();
            foreach (var coord in _chunks.Keys)
            {
                if (Mathf.Abs(coord.x - center.x) > unloadDist ||
                    Mathf.Abs(coord.y - center.y) > unloadDist)
                    _removeBuffer.Add(coord);
            }

            foreach (var coord in _removeBuffer)
            {
                var chunk = _chunks[coord];
                chunk.ReleaseMoistureTexture();
                chunk.ReleaseGpuTexture();
                chunk.Heightmap = null;
                _chunks.Remove(coord);

                if (_views.TryGetValue(coord, out var view))
                {
                    view.Destroy();
                    _views.Remove(coord);
                }
            }
        }

        void RequestChunk(Vector2Int coord)
        {
            _pending.Add(coord);
            var chunk = new Chunk(coord) { State = ChunkState.GeneratingNoise };
            chunk.RequestTime     = Time.realtimeSinceStartup;
            chunk.GpuTexture      = _noiseGen.Dispatch(noiseSettings, coord, out var moistRT);
            chunk.MoistureTexture = moistRT;
            _readback.Request(chunk, OnChunkReady);
        }

        void OnChunkReady(Chunk chunk)
        {
            _pending.Remove(chunk.Coord);

            float elapsed = (Time.realtimeSinceStartup - chunk.RequestTime) * 1000f;
            _lastGenTime = elapsed;
            _genTimeSum += elapsed;
            _genCount++;
            if (elapsed < _minGenTime) _minGenTime = elapsed;
            if (elapsed > _maxGenTime) _maxGenTime = elapsed;

            Vector2Int viewerChunk = WorldToChunkCoord(_viewer.position);
            if (Mathf.Abs(chunk.Coord.x - viewerChunk.x) > viewDistance + 1 ||
                Mathf.Abs(chunk.Coord.y - viewerChunk.y) > viewDistance + 1)
            {
                chunk.ReleaseMoistureTexture();
                chunk.ReleaseGpuTexture();
                chunk.Heightmap = null;
                return;
            }

            _chunks[chunk.Coord] = chunk;
            if (chunk.State == ChunkState.Ready)
                SpawnChunkView(chunk);
        }

        void SpawnChunkView(Chunk chunk)
        {
            Mesh mesh = _meshGen.BuildMesh(chunk.Heightmap, noiseSettings.chunkSize);
            var view  = new ChunkView(chunk.Coord, noiseSettings.chunkSize, terrainMaterial, transform);
            view.ApplyMesh(mesh);
            view.SetTerrainProperties(chunk.MoistureTexture, heightMultiplier);
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
            foreach (var chunk in _chunks.Values)
            {
                chunk.ReleaseMoistureTexture();
                chunk.ReleaseGpuTexture();
                chunk.Heightmap = null;
            }
            foreach (var view in _views.Values)
                view.Destroy();

            _meshGen  = new MeshGenerator(heightMultiplier, heightCurve);
            _noiseGen = new Lib.TerrainGen.NoiseGenerator(noiseShader);

            SyncTerrainMaterialSettings();

            _chunks.Clear();
            _views.Clear();
            _pending.Clear();

            _genCount    = 0;
            _genTimeSum  = 0f;
            _lastGenTime = 0f;
            _minGenTime  = float.MaxValue;
            _maxGenTime  = 0f;

            SetupFog();

            if (Application.isPlaying && _viewer != null)
            {
                Vector2Int viewerChunk = WorldToChunkCoord(_viewer.position);
                RequestVisibleChunks(viewerChunk);
            }
        }
    }
}
