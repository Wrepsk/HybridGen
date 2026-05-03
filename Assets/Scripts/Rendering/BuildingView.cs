using UnityEngine;

namespace Rendering
{
    public class BuildingView
    {
        readonly GameObject _root;

        public BuildingView(GameObject root)
        {
            _root = root;
        }

        public void Destroy()
        {
            Object.Destroy(_root);
        }
    }
}
