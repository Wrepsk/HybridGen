using System.Collections.Generic;

namespace Lib.WFC
{
    public class WFCModuleLibrary
    {
        readonly bool[,,] _allowed;

        public IReadOnlyList<WFCModule> Modules { get; }

        WFCModuleLibrary(IReadOnlyList<WFCModule> modules, bool[,,] allowed)
        {
            Modules = modules;
            _allowed = allowed;
        }

        public WFCModule Get(int id) => Modules[id];

        public bool IsAllowed(int sourceModuleId, WFCDirection direction, int neighborModuleId)
        {
            return _allowed[(int)direction, sourceModuleId, neighborModuleId];
        }

        public static WFCModuleLibrary CreateDefault()
        {
            var modules = new[]
            {
                new WFCModule(0, "Empty", WFCModuleType.Empty, 2.5f),
                new WFCModule(1, "Floor", WFCModuleType.Floor, 6f),
                new WFCModule(2, "Wall", WFCModuleType.Wall, 5f),
                new WFCModule(3, "Door", WFCModuleType.Door, 0.8f)
            };

            var allowed = new bool[4, modules.Length, modules.Length];

            for (int direction = 0; direction < 4; direction++)
            {
                for (int source = 0; source < modules.Length; source++)
                {
                    for (int neighbor = 0; neighbor < modules.Length; neighbor++)
                    {
                        allowed[direction, source, neighbor] =
                            CanTouch(modules[source].Type, modules[neighbor].Type);
                    }
                }
            }

            return new WFCModuleLibrary(modules, allowed);
        }

        static bool CanTouch(WFCModuleType source, WFCModuleType neighbor)
        {
            return source switch
            {
                WFCModuleType.Empty => neighbor is WFCModuleType.Empty or WFCModuleType.Wall or WFCModuleType.Door,
                WFCModuleType.Floor => neighbor is WFCModuleType.Floor or WFCModuleType.Wall or WFCModuleType.Door,
                WFCModuleType.Wall => true,
                WFCModuleType.Door => neighbor is WFCModuleType.Empty or WFCModuleType.Floor or WFCModuleType.Wall,
                _ => false
            };
        }
    }
}
