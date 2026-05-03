using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Lib.WFC
{
    public class WFCSolver
    {
        readonly WFCModuleLibrary _library;

        bool[,] _possible;
        int[] _possibleCounts;
        int _width;
        int _height;
        int _moduleCount;

        public WFCSolver(WFCModuleLibrary library)
        {
            _library = library;
        }

        public bool TrySolve(int width, int height, Random rng, out WFCResult result)
        {
            result = null;
            if (width < 3 || height < 3 || rng == null)
                return false;

            Initialize(width, height);

            int centerIndex = Index(width / 2, height / 2);
            if (!ForceModule(centerIndex, WFCModuleType.Floor))
                return false;

            if (!Propagate(new Queue<int>(new[] { centerIndex })))
                return false;

            while (TryFindLowestEntropyCell(rng, out int cellIndex))
            {
                if (!CollapseCell(cellIndex, rng))
                    return false;

                if (!Propagate(new Queue<int>(new[] { cellIndex })))
                    return false;
            }

            result = BuildResult();
            return true;
        }

        void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _moduleCount = _library.Modules.Count;
            int cellCount = width * height;

            _possible = new bool[cellCount, _moduleCount];
            _possibleCounts = new int[cellCount];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = Index(x, y);
                    bool isBoundary = x == 0 || y == 0 || x == width - 1 || y == height - 1;

                    for (int module = 0; module < _moduleCount; module++)
                    {
                        bool allowed = !isBoundary || _library.Get(module).Type != WFCModuleType.Floor;
                        _possible[index, module] = allowed;
                        if (allowed)
                            _possibleCounts[index]++;
                    }
                }
            }
        }

        bool ForceModule(int cellIndex, WFCModuleType moduleType)
        {
            int targetId = -1;
            for (int module = 0; module < _moduleCount; module++)
            {
                if (_library.Get(module).Type == moduleType)
                {
                    targetId = module;
                    break;
                }
            }

            if (targetId < 0 || !_possible[cellIndex, targetId])
                return false;

            for (int module = 0; module < _moduleCount; module++)
                _possible[cellIndex, module] = module == targetId;

            _possibleCounts[cellIndex] = 1;
            return true;
        }

        bool TryFindLowestEntropyCell(Random rng, out int cellIndex)
        {
            int bestCount = int.MaxValue;
            var candidates = new List<int>();

            for (int index = 0; index < _possibleCounts.Length; index++)
            {
                int count = _possibleCounts[index];
                if (count <= 1)
                    continue;

                if (count < bestCount)
                {
                    bestCount = count;
                    candidates.Clear();
                    candidates.Add(index);
                }
                else if (count == bestCount)
                {
                    candidates.Add(index);
                }
            }

            if (candidates.Count == 0)
            {
                cellIndex = -1;
                return false;
            }

            cellIndex = candidates[rng.Next(candidates.Count)];
            return true;
        }

        bool CollapseCell(int cellIndex, Random rng)
        {
            float totalWeight = 0f;
            for (int module = 0; module < _moduleCount; module++)
            {
                if (_possible[cellIndex, module])
                    totalWeight += Mathf.Max(0.0001f, _library.Get(module).Weight);
            }

            if (totalWeight <= 0f)
                return false;

            float target = (float)rng.NextDouble() * totalWeight;
            int chosen = -1;

            for (int module = 0; module < _moduleCount; module++)
            {
                if (!_possible[cellIndex, module])
                    continue;

                target -= Mathf.Max(0.0001f, _library.Get(module).Weight);
                if (target <= 0f)
                {
                    chosen = module;
                    break;
                }
            }

            if (chosen < 0)
                chosen = FirstPossibleModule(cellIndex);

            for (int module = 0; module < _moduleCount; module++)
                _possible[cellIndex, module] = module == chosen;

            _possibleCounts[cellIndex] = 1;
            return true;
        }

        bool Propagate(Queue<int> queue)
        {
            while (queue.Count > 0)
            {
                int sourceIndex = queue.Dequeue();
                int sourceX = sourceIndex % _width;
                int sourceY = sourceIndex / _width;

                for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                {
                    var direction = (WFCDirection)directionIndex;
                    int neighborX = sourceX;
                    int neighborY = sourceY;

                    switch (direction)
                    {
                        case WFCDirection.North:
                            neighborY += 1;
                            break;
                        case WFCDirection.East:
                            neighborX += 1;
                            break;
                        case WFCDirection.South:
                            neighborY -= 1;
                            break;
                        case WFCDirection.West:
                            neighborX -= 1;
                            break;
                    }

                    if (neighborX < 0 || neighborY < 0 || neighborX >= _width || neighborY >= _height)
                        continue;

                    int neighborIndex = Index(neighborX, neighborY);
                    if (ConstrainNeighbor(sourceIndex, neighborIndex, direction))
                    {
                        if (_possibleCounts[neighborIndex] == 0)
                            return false;

                        queue.Enqueue(neighborIndex);
                    }
                }
            }

            return true;
        }

        bool ConstrainNeighbor(int sourceIndex, int neighborIndex, WFCDirection direction)
        {
            bool changed = false;

            for (int neighborModule = 0; neighborModule < _moduleCount; neighborModule++)
            {
                if (!_possible[neighborIndex, neighborModule])
                    continue;

                bool hasSupport = false;
                for (int sourceModule = 0; sourceModule < _moduleCount; sourceModule++)
                {
                    if (_possible[sourceIndex, sourceModule]
                        && _library.IsAllowed(sourceModule, direction, neighborModule))
                    {
                        hasSupport = true;
                        break;
                    }
                }

                if (!hasSupport)
                {
                    _possible[neighborIndex, neighborModule] = false;
                    _possibleCounts[neighborIndex]--;
                    changed = true;
                }
            }

            return changed;
        }

        WFCResult BuildResult()
        {
            var modules = new WFCModule[_width * _height];
            for (int index = 0; index < modules.Length; index++)
                modules[index] = _library.Get(FirstPossibleModule(index));

            return new WFCResult(_width, _height, modules);
        }

        int FirstPossibleModule(int cellIndex)
        {
            for (int module = 0; module < _moduleCount; module++)
            {
                if (_possible[cellIndex, module])
                    return module;
            }

            return 0;
        }

        int Index(int x, int y) => y * _width + x;
    }
}
