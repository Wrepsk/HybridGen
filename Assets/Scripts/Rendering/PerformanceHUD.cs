using UnityEngine;

namespace Rendering
{
    public class PerformanceHUD : MonoBehaviour
    {
        [SerializeField] ChunkManager chunkManager;

        GUIStyle _boxStyle;
        GUIStyle _labelStyle;
        GUIStyle _headerStyle;

        float _fps;
        float _fpsTimer;
        int   _fpsFrames;

        void Update()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps       = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer  = 0f;
            }
        }

        void OnGUI()
        {
            if (chunkManager == null) return;
            EnsureStyles();

            ChunkMetrics m = chunkManager.Metrics;

            float w = 330f;
            float h = 560f;
            Rect box = new Rect(10, 10, w, h);
            GUI.Box(box, GUIContent.none, _boxStyle);

            float x = 18f;
            float y = 16f;
            float lineH = 22f;

            GUI.Label(new Rect(x, y, w, lineH), "Performance", _headerStyle);
            y += lineH + 4f;

            Line(ref y, x, w, lineH, $"FPS:              {_fps:F0}");
            Line(ref y, x, w, lineH, $"Active Chunks:    {m.ActiveChunks}");
            Line(ref y, x, w, lineH, $"Total Generated:  {m.TotalGenerated}");
            Line(ref y, x, w, lineH, $"Last Gen Time:    {m.LastGenTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Avg Gen Time:     {m.AvgGenTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Min / Max:        {m.MinGenTimeMs:F2} / {m.MaxGenTimeMs:F2} ms");

            y += 4f;
            GUI.Label(new Rect(x, y, w, lineH), "Memory", _headerStyle);
            y += lineH + 4f;

            Line(ref y, x, w, lineH, $"Allocated:        {m.AllocatedMemoryMB:F1} MB");
            Line(ref y, x, w, lineH, $"GPU Textures:     {m.GpuTextureMB:F2} MB");

            y += 4f;
            GUI.Label(new Rect(x, y, w, lineH), "Analysis", _headerStyle);
            y += lineH + 4f;

            Line(ref y, x, w, lineH, $"Total Analyzed:   {m.TotalAnalyzed}");
            Line(ref y, x, w, lineH, $"Last Analyze:     {m.LastAnalysisTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Avg Analyze:      {m.AvgAnalysisTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Min / Max:        {m.MinAnalysisTimeMs:F2} / {m.MaxAnalysisTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Buildable Cells:  {m.VisibleBuildableCells}");
            Line(ref y, x, w, lineH, $"Buildable Ratio:  {m.VisibleBuildableRatio:P1}");

            y += 4f;
            GUI.Label(new Rect(x, y, w, lineH), "WFC Buildings", _headerStyle);
            y += lineH + 4f;

            Line(ref y, x, w, lineH, $"Building Chunks:  {m.ActiveBuildingChunks}");
            Line(ref y, x, w, lineH, $"Blueprints:       {m.ActiveBuildingBlueprints}");
            Line(ref y, x, w, lineH, $"Attempts:         {m.TotalWfcAttempts}");
            Line(ref y, x, w, lineH, $"Success / Fail:   {m.TotalWfcSucceeded} / {m.TotalWfcFailed}");
            Line(ref y, x, w, lineH, $"Last WFC:         {m.LastWfcTimeMs:F2} ms");
            Line(ref y, x, w, lineH, $"Avg WFC:          {m.AvgWfcTimeMs:F2} ms");
        }

        void Line(ref float y, float x, float w, float h, string text)
        {
            GUI.Label(new Rect(x, y, w, h), text, _labelStyle);
            y += h;
        }

        void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.70f));
            bgTex.Apply();
            _boxStyle.normal.background = bgTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Normal
            };
            _labelStyle.normal.textColor = Color.white;

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
