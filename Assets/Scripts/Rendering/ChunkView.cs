using UnityEngine;

namespace Rendering
{
    public class ChunkView
    {
        readonly GameObject   _go;
        readonly MeshFilter   _filter;
        readonly MeshRenderer _renderer;

        public ChunkView(Vector2Int coord, int chunkSize, Material material, Transform parent)
        {
            _go = new GameObject($"Chunk_{coord.x}_{coord.y}");
            _go.transform.parent   = parent;
            _go.transform.position = new Vector3(
                coord.x * (chunkSize - 1), 0, coord.y * (chunkSize - 1));

            _filter   = _go.AddComponent<MeshFilter>();
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
        }

        public void ApplyMesh(Mesh mesh) => _filter.sharedMesh = mesh;

        public void SetTerrainProperties(RenderTexture moistureMap, float maxHeight)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_MoistureMap", moistureMap);
            mpb.SetFloat  ("_MaxHeight",   maxHeight);
            _renderer.SetPropertyBlock(mpb);
        }

        public void SetVisible(bool visible) => _go.SetActive(visible);

        public void Destroy()
        {
            if (_filter != null && _filter.sharedMesh != null)
                Object.Destroy(_filter.sharedMesh);

            Object.Destroy(_go);
        }
    }
}
