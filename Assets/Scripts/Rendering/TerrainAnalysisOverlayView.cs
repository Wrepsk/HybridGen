using Lib.TerrainAnalysis;
using UnityEngine;

namespace Rendering
{
    public class TerrainAnalysisOverlayView
    {
        const float OverlayHeightOffset = 0.25f;
        static readonly Color BuildableColor = new Color(0.18f, 0.78f, 0.24f, 0.42f);
        static readonly Color NearWaterColor = new Color(0.15f, 0.48f, 0.95f, 0.48f);
        static readonly Color TooSteepColor = new Color(0.92f, 0.22f, 0.20f, 0.48f);
        static readonly Color TooRoughColor = new Color(0.95f, 0.78f, 0.15f, 0.48f);
        static readonly Color BlockedColor = new Color(0.55f, 0.55f, 0.55f, 0.35f);

        readonly GameObject   _go;
        readonly MeshFilter   _filter;
        readonly MeshRenderer _renderer;

        public TerrainAnalysisOverlayView(TerrainChunkAnalysis analysis, Material material, Transform parent)
        {
            _go = new GameObject($"AnalysisOverlay_{analysis.ChunkCoord.x}_{analysis.ChunkCoord.y}");
            _go.transform.parent = parent;
            _go.transform.position = new Vector3(
                analysis.ChunkCoord.x * analysis.CellWorldSize * analysis.CellsPerAxis,
                0f,
                analysis.ChunkCoord.y * analysis.CellWorldSize * analysis.CellsPerAxis);

            _filter = _go.AddComponent<MeshFilter>();
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;

            _filter.sharedMesh = BuildMesh(analysis);
        }

        public void SetVisible(bool visible) => _go.SetActive(visible);

        public void Destroy()
        {
            if (_filter != null && _filter.sharedMesh != null)
                Object.Destroy(_filter.sharedMesh);

            Object.Destroy(_go);
        }

        static Mesh BuildMesh(TerrainChunkAnalysis analysis)
        {
            int cellCount = analysis.TotalCellCount;
            var vertices = new Vector3[cellCount * 4];
            var colors = new Color[cellCount * 4];
            var triangles = new int[cellCount * 6];
            float chunkWorldSize = analysis.CellWorldSize * analysis.CellsPerAxis;

            int vertIndex = 0;
            int triIndex = 0;

            foreach (TerrainBuildCell cell in analysis.Cells)
            {
                float minX = cell.LocalCoord.x * analysis.CellWorldSize;
                float maxX = cell.LocalCoord.x == analysis.CellsPerAxis - 1
                    ? chunkWorldSize
                    : (cell.LocalCoord.x + 1) * analysis.CellWorldSize;
                float minZ = cell.LocalCoord.y * analysis.CellWorldSize;
                float maxZ = cell.LocalCoord.y == analysis.CellsPerAxis - 1
                    ? chunkWorldSize
                    : (cell.LocalCoord.y + 1) * analysis.CellWorldSize;
                float y = cell.MaxHeightWorld + OverlayHeightOffset;
                Color color = ResolveColor(cell);

                vertices[vertIndex] = new Vector3(minX, y, minZ);
                vertices[vertIndex + 1] = new Vector3(minX, y, maxZ);
                vertices[vertIndex + 2] = new Vector3(maxX, y, maxZ);
                vertices[vertIndex + 3] = new Vector3(maxX, y, minZ);

                colors[vertIndex] = color;
                colors[vertIndex + 1] = color;
                colors[vertIndex + 2] = color;
                colors[vertIndex + 3] = color;

                triangles[triIndex] = vertIndex;
                triangles[triIndex + 1] = vertIndex + 1;
                triangles[triIndex + 2] = vertIndex + 2;
                triangles[triIndex + 3] = vertIndex;
                triangles[triIndex + 4] = vertIndex + 2;
                triangles[triIndex + 5] = vertIndex + 3;

                vertIndex += 4;
                triIndex += 6;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color ResolveColor(TerrainBuildCell cell)
        {
            if (cell.IsBuildable)
                return BuildableColor;

            if ((cell.Flags & BuildabilityFlags.NearWater) != 0)
                return NearWaterColor;
            if ((cell.Flags & BuildabilityFlags.TooSteep) != 0)
                return TooSteepColor;
            if ((cell.Flags & BuildabilityFlags.TooRough) != 0)
                return TooRoughColor;

            return BlockedColor;
        }
    }
}
