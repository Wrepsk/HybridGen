using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Lib.TerrainAnalysis;
using Lib.TerrainGen;
using Lib.WFC;
using Rendering;
using UnityEngine;

namespace Benchmark
{
    public class GenerationBenchmarkRunner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ChunkManager chunkManager;
        [SerializeField] Transform cameraTransform;
        [SerializeField] MonoBehaviour cameraControllerToDisable;

        [Header("Run")]
        [SerializeField] bool runOnStart = false;
        [SerializeField] bool regenerateBeforeRun = true;
        [SerializeField] float warmupSeconds = 3f;
        [SerializeField] float durationSeconds = 60f;
        [SerializeField] float moveSpeed = 35f;
        [SerializeField] Vector3 worldDirection = Vector3.forward;
        [SerializeField] float sampleIntervalSeconds = 0.5f;
        [SerializeField] string runLabel = "hybridgen_forward";

        [Header("Output")]
        [SerializeField] bool writeToProjectRoot = true;
        [SerializeField] string outputFolderName = "BenchmarkResults";
        [SerializeField] bool pauseEditorOnComplete;

        readonly List<BenchmarkSample> _samples = new();

        BenchmarkConfigSnapshot _config;
        ChunkMetrics _baselineMetrics;
        Vector3 _startPosition;
        Vector3 _direction;
        float _startedAt;
        float _nextSampleAt;
        bool _running;
        bool _subscribed;
        bool _cameraControllerWasEnabled;

        int _wfcChunkGenerations;
        int _wfcChunksWithBuildings;
        int _wfcChunksWithoutBuildings;
        int _wfcBlueprintsGenerated;

        void Reset()
        {
            chunkManager = FindObjectOfType<ChunkManager>();
            cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            cameraControllerToDisable = cameraTransform != null
                ? cameraTransform.GetComponent<FreeFlyCamera>()
                : null;
        }

        void Start()
        {
            if (runOnStart)
                BeginBenchmark();
        }

        void Update()
        {
            if (!_running)
                return;

            cameraTransform.position += _direction * (moveSpeed * Time.unscaledDeltaTime);

            float now = Time.realtimeSinceStartup;
            float elapsed = now - _startedAt;

            if (now >= _nextSampleAt)
            {
                RecordSample(elapsed);
                _nextSampleAt = now + Mathf.Max(0.05f, sampleIntervalSeconds);
            }

            if (elapsed >= durationSeconds)
                FinishBenchmark();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
            RestoreCameraController();
        }

        [ContextMenu("Begin Benchmark")]
        public void BeginBenchmark()
        {
            if (_running)
                return;

            ResolveReferences();
            if (chunkManager == null || cameraTransform == null)
            {
                Debug.LogWarning("[GenerationBenchmarkRunner] Missing ChunkManager or camera transform.");
                return;
            }

            StartCoroutine(BeginAfterWarmup());
        }

        IEnumerator BeginAfterWarmup()
        {
            ResolveReferences();
            SubscribeToEvents();
            DisableCameraController();

            if (regenerateBeforeRun)
                chunkManager.RegenerateTerrain();

            if (warmupSeconds > 0f)
                yield return new WaitForSecondsRealtime(warmupSeconds);

            _samples.Clear();
            _config = SnapshotConfig();
            _baselineMetrics = chunkManager.Metrics;
            _wfcChunkGenerations = 0;
            _wfcChunksWithBuildings = 0;
            _wfcChunksWithoutBuildings = 0;
            _wfcBlueprintsGenerated = 0;

            _direction = worldDirection.sqrMagnitude > 0.0001f
                ? worldDirection.normalized
                : Vector3.forward;
            _startPosition = cameraTransform.position;
            _startedAt = Time.realtimeSinceStartup;
            _nextSampleAt = _startedAt;
            _running = true;

            RecordSample(0f);
            Debug.Log($"[GenerationBenchmarkRunner] Benchmark started: {runLabel}");
        }

        void FinishBenchmark()
        {
            if (!_running)
                return;

            _running = false;
            RecordSample(durationSeconds);

            string outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            string runId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{SanitizeFileName(runLabel)}";
            string samplesPath = Path.Combine(outputDirectory, $"samples_{runId}.csv");
            string summaryPath = Path.Combine(outputDirectory, $"summary_{runId}.csv");

            File.WriteAllText(samplesPath, BuildSamplesCsv(), Encoding.UTF8);
            File.WriteAllText(summaryPath, BuildSummaryCsv(runId, samplesPath), Encoding.UTF8);

            RestoreCameraController();
            UnsubscribeFromEvents();

            Debug.Log($"[GenerationBenchmarkRunner] Benchmark finished. Summary: {summaryPath}");

#if UNITY_EDITOR
            if (pauseEditorOnComplete)
                UnityEditor.EditorApplication.isPaused = true;
#endif
        }

        void ResolveReferences()
        {
            if (chunkManager == null)
                chunkManager = FindObjectOfType<ChunkManager>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (cameraControllerToDisable == null && cameraTransform != null)
                cameraControllerToDisable = cameraTransform.GetComponent<FreeFlyCamera>();
        }

        void SubscribeToEvents()
        {
            if (_subscribed || chunkManager == null)
                return;

            chunkManager.BuildingsGenerated += OnBuildingsGenerated;
            _subscribed = true;
        }

        void UnsubscribeFromEvents()
        {
            if (!_subscribed || chunkManager == null)
                return;

            chunkManager.BuildingsGenerated -= OnBuildingsGenerated;
            _subscribed = false;
        }

        void OnBuildingsGenerated(WFCChunkGeneration generation)
        {
            if (!_running || generation == null)
                return;

            _wfcChunkGenerations++;
            _wfcBlueprintsGenerated += generation.SuccessCount;

            if (generation.SuccessCount > 0)
                _wfcChunksWithBuildings++;
            else
                _wfcChunksWithoutBuildings++;
        }

        void DisableCameraController()
        {
            if (cameraControllerToDisable == null)
                return;

            _cameraControllerWasEnabled = cameraControllerToDisable.enabled;
            cameraControllerToDisable.enabled = false;
        }

        void RestoreCameraController()
        {
            if (cameraControllerToDisable != null)
                cameraControllerToDisable.enabled = _cameraControllerWasEnabled;
        }

        void RecordSample(float elapsedSeconds)
        {
            ChunkMetrics metrics = chunkManager.Metrics;
            _samples.Add(new BenchmarkSample
            {
                ElapsedSeconds = elapsedSeconds,
                DistanceWorld = Vector3.Distance(_startPosition, cameraTransform.position),
                Fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f,
                Metrics = metrics
            });
        }

        string BuildSamplesCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "run_label,view_distance,cells_per_axis,min_footprint_cells,max_footprint_cells,seed," +
                "chunk_size,building_chunk_radius,max_buildings_per_chunk," +
                "elapsed_seconds,distance_world,fps,active_chunks,generated_chunks_delta,analyzed_chunks_delta," +
                "wfc_attempts_delta,wfc_successes_delta,wfc_failures_delta,active_building_chunks," +
                "active_building_blueprints,avg_chunk_generation_ms,avg_analysis_ms,avg_wfc_ms," +
                "last_chunk_generation_ms,last_analysis_ms,last_wfc_ms,visible_buildable_cells," +
                "visible_buildable_ratio,allocated_memory_mb,gpu_texture_mb");

            foreach (BenchmarkSample sample in _samples)
            {
                ChunkMetrics m = sample.Metrics;
                sb.AppendLine(string.Join(",",
                    Csv(runLabel),
                    _config.ViewDistance.ToString(CultureInfo.InvariantCulture),
                    _config.CellsPerAxis.ToString(CultureInfo.InvariantCulture),
                    _config.MinFootprintCells.ToString(CultureInfo.InvariantCulture),
                    _config.MaxFootprintCells.ToString(CultureInfo.InvariantCulture),
                    _config.Seed.ToString(CultureInfo.InvariantCulture),
                    _config.ChunkSize.ToString(CultureInfo.InvariantCulture),
                    _config.BuildingChunkRadius.ToString(CultureInfo.InvariantCulture),
                    _config.MaxBuildingsPerChunk.ToString(CultureInfo.InvariantCulture),
                    F(sample.ElapsedSeconds),
                    F(sample.DistanceWorld),
                    F(sample.Fps),
                    m.ActiveChunks.ToString(CultureInfo.InvariantCulture),
                    Delta(m.TotalGenerated, _baselineMetrics.TotalGenerated),
                    Delta(m.TotalAnalyzed, _baselineMetrics.TotalAnalyzed),
                    Delta(m.TotalWfcAttempts, _baselineMetrics.TotalWfcAttempts),
                    Delta(m.TotalWfcSucceeded, _baselineMetrics.TotalWfcSucceeded),
                    Delta(m.TotalWfcFailed, _baselineMetrics.TotalWfcFailed),
                    m.ActiveBuildingChunks.ToString(CultureInfo.InvariantCulture),
                    m.ActiveBuildingBlueprints.ToString(CultureInfo.InvariantCulture),
                    F(m.AvgGenTimeMs),
                    F(m.AvgAnalysisTimeMs),
                    F(m.AvgWfcTimeMs),
                    F(m.LastGenTimeMs),
                    F(m.LastAnalysisTimeMs),
                    F(m.LastWfcTimeMs),
                    m.VisibleBuildableCells.ToString(CultureInfo.InvariantCulture),
                    F(m.VisibleBuildableRatio),
                    F(m.AllocatedMemoryMB),
                    F(m.GpuTextureMB)));
            }

            return sb.ToString();
        }

        string BuildSummaryCsv(string runId, string samplesPath)
        {
            ChunkMetrics final = chunkManager.Metrics;
            int generatedChunks = Mathf.Max(0, final.TotalGenerated - _baselineMetrics.TotalGenerated);
            int analyzedChunks = Mathf.Max(0, final.TotalAnalyzed - _baselineMetrics.TotalAnalyzed);
            int solverAttempts = Mathf.Max(0, final.TotalWfcAttempts - _baselineMetrics.TotalWfcAttempts);
            int solverSuccesses = Mathf.Max(0, final.TotalWfcSucceeded - _baselineMetrics.TotalWfcSucceeded);
            int solverFailures = Mathf.Max(0, final.TotalWfcFailed - _baselineMetrics.TotalWfcFailed);

            float chunkSuccessRate = _wfcChunkGenerations > 0
                ? _wfcChunksWithBuildings / (float)_wfcChunkGenerations
                : 0f;
            float solverSuccessRate = solverAttempts > 0
                ? solverSuccesses / (float)solverAttempts
                : 0f;

            float peakMemory = 0f;
            float peakGpuTexture = 0f;
            foreach (BenchmarkSample sample in _samples)
            {
                peakMemory = Mathf.Max(peakMemory, sample.Metrics.AllocatedMemoryMB);
                peakGpuTexture = Mathf.Max(peakGpuTexture, sample.Metrics.GpuTextureMB);
            }

            var headers = new[]
            {
                "run_id",
                "run_label",
                "started_utc",
                "view_distance",
                "cells_per_axis",
                "min_footprint_cells",
                "max_footprint_cells",
                "seed",
                "chunk_size",
                "building_chunk_radius",
                "max_buildings_per_chunk",
                "duration_seconds",
                "move_speed_world_units_per_second",
                "distance_world",
                "sample_count",
                "generated_chunks",
                "analyzed_chunks",
                "wfc_chunk_generation_events",
                "wfc_chunks_with_buildings",
                "wfc_chunks_without_buildings",
                "wfc_chunk_success_rate",
                "wfc_solver_attempts",
                "wfc_solver_successes",
                "wfc_solver_failures",
                "wfc_solver_success_rate",
                "avg_chunk_generation_ms",
                "min_chunk_generation_ms",
                "max_chunk_generation_ms",
                "avg_analysis_ms",
                "min_analysis_ms",
                "max_analysis_ms",
                "avg_wfc_ms",
                "min_wfc_ms",
                "max_wfc_ms",
                "final_active_chunks",
                "final_active_building_blueprints",
                "final_visible_buildable_ratio",
                "peak_allocated_memory_mb",
                "peak_gpu_texture_mb",
                "cpu",
                "gpu",
                "system_memory_mb",
                "unity_version",
                "platform",
                "samples_file"
            };

            var values = new[]
            {
                Csv(runId),
                Csv(runLabel),
                Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                _config.ViewDistance.ToString(CultureInfo.InvariantCulture),
                _config.CellsPerAxis.ToString(CultureInfo.InvariantCulture),
                _config.MinFootprintCells.ToString(CultureInfo.InvariantCulture),
                _config.MaxFootprintCells.ToString(CultureInfo.InvariantCulture),
                _config.Seed.ToString(CultureInfo.InvariantCulture),
                _config.ChunkSize.ToString(CultureInfo.InvariantCulture),
                _config.BuildingChunkRadius.ToString(CultureInfo.InvariantCulture),
                _config.MaxBuildingsPerChunk.ToString(CultureInfo.InvariantCulture),
                F(durationSeconds),
                F(moveSpeed),
                F(Vector3.Distance(_startPosition, cameraTransform.position)),
                _samples.Count.ToString(CultureInfo.InvariantCulture),
                generatedChunks.ToString(CultureInfo.InvariantCulture),
                analyzedChunks.ToString(CultureInfo.InvariantCulture),
                _wfcChunkGenerations.ToString(CultureInfo.InvariantCulture),
                _wfcChunksWithBuildings.ToString(CultureInfo.InvariantCulture),
                _wfcChunksWithoutBuildings.ToString(CultureInfo.InvariantCulture),
                F(chunkSuccessRate),
                solverAttempts.ToString(CultureInfo.InvariantCulture),
                solverSuccesses.ToString(CultureInfo.InvariantCulture),
                solverFailures.ToString(CultureInfo.InvariantCulture),
                F(solverSuccessRate),
                F(final.AvgGenTimeMs),
                F(final.MinGenTimeMs),
                F(final.MaxGenTimeMs),
                F(final.AvgAnalysisTimeMs),
                F(final.MinAnalysisTimeMs),
                F(final.MaxAnalysisTimeMs),
                F(final.AvgWfcTimeMs),
                F(final.MinWfcTimeMs),
                F(final.MaxWfcTimeMs),
                final.ActiveChunks.ToString(CultureInfo.InvariantCulture),
                final.ActiveBuildingBlueprints.ToString(CultureInfo.InvariantCulture),
                F(final.VisibleBuildableRatio),
                F(peakMemory),
                F(peakGpuTexture),
                Csv(SystemInfo.processorType),
                Csv(SystemInfo.graphicsDeviceName),
                SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture),
                Csv(Application.unityVersion),
                Csv(Application.platform.ToString()),
                Csv(samplesPath)
            };

            return string.Join(",", headers) + Environment.NewLine
                 + string.Join(",", values) + Environment.NewLine;
        }

        BenchmarkConfigSnapshot SnapshotConfig()
        {
            NoiseSettings noise = chunkManager.NoiseSettings;
            TerrainAnalysisSettings analysis = chunkManager.TerrainAnalysisSettings;
            WFCSettings wfc = chunkManager.WfcSettings;

            return new BenchmarkConfigSnapshot
            {
                ViewDistance = chunkManager.ViewDistance,
                CellsPerAxis = analysis != null ? analysis.cellsPerAxis : 0,
                MinFootprintCells = wfc != null ? wfc.minFootprintCells : 0,
                MaxFootprintCells = wfc != null ? wfc.maxFootprintCells : 0,
                Seed = noise != null ? noise.seed : 0,
                ChunkSize = noise != null ? noise.chunkSize : 0,
                BuildingChunkRadius = wfc != null ? wfc.buildingChunkRadius : 0,
                MaxBuildingsPerChunk = wfc != null ? wfc.maxBuildingsPerChunk : 0
            };
        }

        string ResolveOutputDirectory()
        {
            if (Path.IsPathRooted(outputFolderName))
                return outputFolderName;

            string root = writeToProjectRoot
                ? Path.Combine(Application.dataPath, "..")
                : Application.persistentDataPath;

            return Path.GetFullPath(Path.Combine(root, outputFolderName));
        }

        static string Delta(int current, int baseline)
        {
            return Mathf.Max(0, current - baseline).ToString(CultureInfo.InvariantCulture);
        }

        static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            bool needsQuotes = value.Contains(",")
                            || value.Contains("\"")
                            || value.Contains("\n")
                            || value.Contains("\r");

            if (!needsQuotes)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "benchmark";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Replace(' ', '_');
        }

        struct BenchmarkSample
        {
            public float ElapsedSeconds;
            public float DistanceWorld;
            public float Fps;
            public ChunkMetrics Metrics;
        }

        struct BenchmarkConfigSnapshot
        {
            public int ViewDistance;
            public int CellsPerAxis;
            public int MinFootprintCells;
            public int MaxFootprintCells;
            public int Seed;
            public int ChunkSize;
            public int BuildingChunkRadius;
            public int MaxBuildingsPerChunk;
        }
    }
}
