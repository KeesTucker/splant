using System.Runtime.InteropServices;
using UnityEngine;

namespace Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Cell
    {
        public float age;
        public Vector3 polarity;
        public Vector3 position;
    }
}
