using System.Runtime.InteropServices;
using UnityEngine;

namespace Structs
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Bond
    {
        public float restDistance;
        public Vector3 restDir;
        public uint cellA;
        public uint cellB;
    }
}
