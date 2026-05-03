namespace Lib.WFC
{
    public enum WFCDirection
    {
        North,
        East,
        South,
        West
    }

    public static class WFCDirectionExtensions
    {
        public static WFCDirection Opposite(this WFCDirection direction)
        {
            return direction switch
            {
                WFCDirection.North => WFCDirection.South,
                WFCDirection.East => WFCDirection.West,
                WFCDirection.South => WFCDirection.North,
                _ => WFCDirection.East
            };
        }
    }
}
