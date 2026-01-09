using System.Runtime.InteropServices;
using UnityEngine;

namespace Structs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Cell
    {
        public Color color;
        public Vector3 position;
        public float age;
        public float depth;
    }
}