static const int3 bucket_offsets[27] = {
    int3(0,0,0),

    int3(1,0,0), int3(-1,0,0),
    int3(0,1,0), int3(0,-1,0),
    int3(0,0,1), int3(0,0,-1),

    int3(1,1,0), int3(1,-1,0), int3(-1,1,0), int3(-1,-1,0),
    int3(1,0,1), int3(1,0,-1), int3(-1,0,1), int3(-1,0,-1),
    int3(0,1,1), int3(0,1,-1), int3(0,-1,1), int3(0,-1,-1),

    int3(1,1,1), int3(1,1,-1), int3(1,-1,1), int3(1,-1,-1),
    int3(-1,1,1), int3(-1,1,-1), int3(-1,-1,1), int3(-1,-1,-1)
};

uint wang_hash(uint seed)
{
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    return seed;
}

float random(uint seed)
{
    uint h = wang_hash(seed);
    return asfloat(int(0x3f800000 | (h >> 9))) - 1.0;
}

uint fast_hash_coord(int3 coord)
{
    uint h = (uint)coord.x * 73856093u;
    h ^= (uint)coord.y * 19349663u;
    h ^= (uint)coord.z * 83492791u;
    h ^= h >> 15;
    h *= 2246822519u;
    h ^= h >> 13;
    return h;
}

float3 safe_normalize(float3 v)
{
    float len_sq = dot(v, v);
    if (len_sq < 1e-8)
        return float3(0, 1, 0); // probably shouldn't default to up? Idk?
    return v * rsqrt(len_sq);
}

float smooth_step(float t)
{
    // Smooth polynomial kernel (smoothstep(t) = 3t² - 2t³)
    return t * t * (3.0 - 2.0 * t);
}