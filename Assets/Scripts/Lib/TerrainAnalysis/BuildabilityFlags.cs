using System;

namespace Lib.TerrainAnalysis
{
    [Flags]
    public enum BuildabilityFlags
    {
        None = 0,
        NearWater = 1 << 0,
        TooSteep = 1 << 1,
        TooRough = 1 << 2
    }
}
