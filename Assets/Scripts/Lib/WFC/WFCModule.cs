namespace Lib.WFC
{
    public class WFCModule
    {
        public int           Id          { get; }
        public string        Name        { get; }
        public WFCModuleType Type        { get; }
        public float         Weight      { get; }

        public WFCModule(int id, string name, WFCModuleType type, float weight)
        {
            Id = id;
            Name = name;
            Type = type;
            Weight = weight;
        }
    }
}
