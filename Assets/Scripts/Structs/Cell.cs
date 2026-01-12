using System.Runtime.InteropServices;
using UnityEngine;

namespace Structs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Cell
    {
        public Vector3 polarity;
        public Vector3 position;
        public float age;
    }
}