uint hash_coord(int3 coord)
{
    uint h = (uint)coord.x * 73856093u;
    h ^= (uint)coord.y * 19349663u;
    h ^= (uint)coord.z * 83492791u;
    return h;
}