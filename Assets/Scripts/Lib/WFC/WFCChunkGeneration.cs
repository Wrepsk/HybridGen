using UnityEngine;

namespace Lib.WFC
{
    public class WFCChunkGeneration
    {
        public Vector2Int ChunkCoord { get; }
        public BuildingBlueprint[] Blueprints { get; }
        public int AttemptCount { get; }
        public int FailureCount { get; }
        public int SuccessCount => Blueprints?.Length ?? 0;

        public WFCChunkGeneration(
            Vector2Int chunkCoord,
            BuildingBlueprint[] blueprints,
            int attemptCount,
            int failureCount)
        {
            ChunkCoord = chunkCoord;
            Blueprints = blueprints;
            AttemptCount = attemptCount;
            FailureCount = failureCount;
        }
    }
}
