namespace Lib.WFC
{
    public class WFCResult
    {
        public int Width { get; }
        public int Height { get; }
        public WFCModule[] Modules { get; }

        public WFCResult(int width, int height, WFCModule[] modules)
        {
            Width = width;
            Height = height;
            Modules = modules;
        }

        public WFCModule Get(int x, int y) => Modules[y * Width + x];
    }
}
