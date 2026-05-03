using Lib.WFC;
using UnityEngine;

namespace Rendering
{
    public class BuildingSpawner
    {
        readonly Material _floorMaterial;
        readonly Material _wallMaterial;
        readonly Material _doorMaterial;
        readonly Material _roofMaterial;

        public BuildingSpawner()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            _floorMaterial = CreateMaterial(shader, new Color(0.36f, 0.34f, 0.30f));
            _wallMaterial = CreateMaterial(shader, new Color(0.72f, 0.66f, 0.56f));
            _doorMaterial = CreateMaterial(shader, new Color(0.30f, 0.17f, 0.08f));
            _roofMaterial = CreateMaterial(shader, new Color(0.44f, 0.08f, 0.06f));
        }

        public BuildingView SpawnChunkBuildings(
            Vector2Int chunkCoord,
            BuildingBlueprint[] blueprints,
            WFCSettings settings,
            Transform parent)
        {
            var root = new GameObject($"Buildings_{chunkCoord.x}_{chunkCoord.y}");
            root.transform.parent = parent;

            if (blueprints == null)
                return new BuildingView(root);

            foreach (BuildingBlueprint blueprint in blueprints)
                SpawnBlueprint(root.transform, blueprint, settings);

            return new BuildingView(root);
        }

        public void DestroyMaterials()
        {
            Object.Destroy(_floorMaterial);
            Object.Destroy(_wallMaterial);
            Object.Destroy(_doorMaterial);
            Object.Destroy(_roofMaterial);
        }

        void SpawnBlueprint(Transform parent, BuildingBlueprint blueprint, WFCSettings settings)
        {
            if (blueprint?.Tiles == null)
                return;

            var group = new GameObject(
                $"Building_{blueprint.FootprintOrigin.x}_{blueprint.FootprintOrigin.y}_{blueprint.FootprintSize.x}");
            group.transform.parent = parent;

            foreach (WFCPlacedTile tile in blueprint.Tiles)
            {
                if (tile.Module.Type == WFCModuleType.Empty)
                    continue;

                SpawnFloor(group.transform, tile, settings);

                if (tile.Module.Type == WFCModuleType.Wall)
                    SpawnWall(group.transform, tile, settings, _wallMaterial, settings.wallHeight);
                else if (tile.Module.Type == WFCModuleType.Door)
                    SpawnWall(group.transform, tile, settings, _doorMaterial, settings.wallHeight * 0.68f);

                SpawnRoof(group.transform, tile, settings);
            }
        }

        void SpawnFloor(Transform parent, WFCPlacedTile tile, WFCSettings settings)
        {
            float size = tile.CellWorldSize * Mathf.Clamp(settings.moduleScale, 0.1f, 1f);
            float thickness = Mathf.Max(0.05f, settings.floorThickness);
            Vector3 position = tile.WorldCenter + Vector3.up * (thickness * 0.5f);
            Vector3 scale = new Vector3(size, thickness, size);

            SpawnCube(parent, "Floor", position, scale, _floorMaterial);
        }

        void SpawnWall(
            Transform parent,
            WFCPlacedTile tile,
            WFCSettings settings,
            Material material,
            float height)
        {
            float size = tile.CellWorldSize * Mathf.Clamp(settings.moduleScale, 0.1f, 1f);
            float floorThickness = Mathf.Max(0.05f, settings.floorThickness);
            float wallHeight = Mathf.Max(0.1f, height);
            Vector3 position = tile.WorldCenter + Vector3.up * (floorThickness + wallHeight * 0.5f);
            Vector3 scale = new Vector3(size, wallHeight, size);

            SpawnCube(parent, tile.Module.Name, position, scale, material);
        }

        void SpawnRoof(Transform parent, WFCPlacedTile tile, WFCSettings settings)
        {
            float size = tile.CellWorldSize * Mathf.Clamp(settings.moduleScale, 0.1f, 1f)
                       + Mathf.Max(0f, settings.roofOverhang);
            float floorThickness = Mathf.Max(0.05f, settings.floorThickness);
            float roofThickness = Mathf.Max(0.05f, settings.roofThickness);
            float wallHeight = Mathf.Max(0.1f, settings.wallHeight);
            Vector3 position = tile.WorldCenter + Vector3.up * (floorThickness + wallHeight + roofThickness * 0.5f);
            Vector3 scale = new Vector3(size, roofThickness, size);

            SpawnCube(parent, "Roof", position, scale, _roofMaterial);
        }

        static void SpawnCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.parent = parent;
            cube.transform.position = position;
            cube.transform.localScale = scale;

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        static Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader)
            {
                color = color
            };
            return material;
        }
    }
}
