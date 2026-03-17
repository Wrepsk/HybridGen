using UnityEngine;


namespace Rendering
{

    public class MeshGenerator
    {
        readonly float _heightMultiplier;
        readonly AnimationCurve _heightCurve;

        public MeshGenerator(float heightMultiplier, AnimationCurve heightCurve)
        {
            _heightMultiplier = heightMultiplier;
            _heightCurve = heightCurve;
        }

        public Mesh BuildMesh(float[] heightmap, int chunkSize)
        {
            int vertCount = chunkSize * chunkSize;

            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[(chunkSize - 1) * (chunkSize - 1) * 6];

            int triIndex = 0;

            for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int i = y * chunkSize + x;

                    float h = _heightCurve.Evaluate(heightmap[i]) * _heightMultiplier;
                    vertices[i] = new Vector3(x, h, y);
                    uvs[i] = new Vector2(x / (float)(chunkSize - 1),
                        1f - y / (float)(chunkSize - 1));

                    if (x < chunkSize - 1 && y < chunkSize - 1)
                    {
                        triangles[triIndex]     = i;
                        triangles[triIndex + 1] = i + chunkSize;
                        triangles[triIndex + 2] = i + chunkSize + 1;
                        triangles[triIndex + 3] = i;
                        triangles[triIndex + 4] = i + chunkSize + 1;
                        triangles[triIndex + 5] = i + 1;
                        triIndex += 6;
                    }
                }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}