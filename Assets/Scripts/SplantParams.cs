using System;
using UnityEngine;

public enum GrowthMode { Blob, String }

[Serializable]
public struct GrowthParams
{
    public GrowthMode mode;
    [Min(1)]      public int   splitsPerCell;
    [Min(0.001f)] public float splitAge;
    [Min(0f)]     public float splitAgeJitter;

    public void Apply(ComputeShader cs)
    {
        cs.SetInt(ShaderID.GrowthMode,       (int)mode);
        cs.SetInt(ShaderID.MaxSplitsPerCell, splitsPerCell);
        cs.SetFloat(ShaderID.SplitAge,       splitAge);
        cs.SetFloat(ShaderID.SplitAgeJitter, splitAgeJitter);
    }
}

[Serializable]
public struct SpatialHashParams
{
    public float bucketSize;
    [Tooltip("Must be a power of 2")]
    public int bucketTableSize;

    public void Apply(ComputeShader cs)
    {
        cs.SetFloat(ShaderID.BucketSize,    bucketSize);
        cs.SetInt(ShaderID.BucketTableSize, bucketTableSize);
    }

    public void Apply(Material mat)
    {
        mat.SetFloat(ShaderID.BucketSize,    bucketSize);
        mat.SetInt(ShaderID.BucketTableSize, bucketTableSize);
    }

    public void Clamp()
    {
        if (!(bucketTableSize > 0 && (bucketTableSize & (bucketTableSize - 1)) == 0))
            bucketTableSize = 1 << 18;
    }
}

[Serializable]
public struct PhysicsParams
{
    [Tooltip("Must be <= 0.5x bucketSize")]
    public float neighborRadius;
    public float   repulsionStrength;
    public float   damping;
    public Vector3 gravity;

    public void Apply(ComputeShader cs)
    {
        cs.SetFloat(ShaderID.NeighborRadius,    neighborRadius);
        cs.SetFloat(ShaderID.NeighborRadiusSq,  neighborRadius * neighborRadius);
        cs.SetFloat(ShaderID.RepulsionStrength,  repulsionStrength);
        cs.SetFloat(ShaderID.Damping,            damping);
        cs.SetVector(ShaderID.Gravity,           gravity);
    }

    public void Apply(Material mat)
    {
        mat.SetFloat(ShaderID.NeighborRadiusSq, neighborRadius * neighborRadius);
    }

    public void Clamp(float bucketSize)
    {
        neighborRadius = Mathf.Clamp(neighborRadius, 0, bucketSize * 0.5f);
        damping        = Mathf.Max(0f, damping);
    }
}

[Serializable]
public struct WindParams
{
    public Vector3 direction;
    public float   strength;
    public float   frequency;
    public float   speed;
    public float   variation;

    public void Apply(ComputeShader cs)
    {
        cs.SetVector(ShaderID.WindDirection, direction);
        cs.SetFloat(ShaderID.WindStrength,   strength);
        cs.SetFloat(ShaderID.WindFrequency,  frequency);
        cs.SetFloat(ShaderID.WindSpeed,      speed);
        cs.SetFloat(ShaderID.WindVariation,  variation);
    }

    public void Clamp()
    {
        strength  = Mathf.Max(0f, strength);
        frequency = Mathf.Max(0f, frequency);
        speed     = Mathf.Max(0f, speed);
        variation = Mathf.Max(0f, variation);
    }
}

[Serializable]
public struct BondParams
{
    [Tooltip("Must be <= 0.5x bucketSize")]
    public float range;
    public float linearStiffness;
    public float bendStiffness;
    public uint  maxBondsPerCell;

    public void Apply(ComputeShader cs, int maxCells)
    {
        cs.SetFloat(ShaderID.BondBendStiffness,   bendStiffness);
        cs.SetFloat(ShaderID.BondLinearStiffness, linearStiffness);
        cs.SetFloat(ShaderID.BondRangeSq,         range * range);
        cs.SetInt(ShaderID.MaxBonds,              maxCells * (int)maxBondsPerCell);
        cs.SetInt(ShaderID.MaxBondsPerCell,       (int)maxBondsPerCell);
    }

    public void Clamp(float bucketSize)
    {
        range = Mathf.Clamp(range, 0, bucketSize * 0.5f);
    }
}

[Serializable]
public struct RenderingParams
{
    [Range(1, 512)]   public int   maxSteps;
    [Tooltip("Must be <= 0.5x bucketSize")]
    public float cellRadius;
    [Tooltip("Must be <= 0.5x bucketSize")]
    public float minStepSize;
    [Tooltip("Must be <= 0.5x bucketSize")]
    public float maxStepSize;
    [Range(0.001f, 1f)] public float surfaceThreshold;
    public float fresnelPow;
    public float fresnelStrength;
    public Color baseColour;
    public bool  debugSdfGrad;
    public bool  debugAtlasSlice;
    public int   debugAtlasSliceIndex;

    public void Apply(ComputeShader cs)
    {
        cs.SetFloat(ShaderID.CellRadius, cellRadius);
    }

    public void Apply(Material mat)
    {
        mat.SetFloat(ShaderID.CellRadius,       cellRadius);
        mat.SetFloat(ShaderID.DebugGrad,        debugSdfGrad ? 1f : 0f);
        mat.SetFloat(ShaderID.DebugAtlas,       debugAtlasSlice ? 1f : 0f);
        mat.SetFloat(ShaderID.DebugAtlasSlice,  debugAtlasSliceIndex);
        mat.SetFloat(ShaderID.FresnelPow,       fresnelPow);
        mat.SetFloat(ShaderID.FresnelStrength,  fresnelStrength);
        mat.SetFloat(ShaderID.MaxStepSize,      maxStepSize);
        mat.SetFloat(ShaderID.MinStepSize,      minStepSize);
        mat.SetFloat(ShaderID.SurfaceThreshold, surfaceThreshold);
        mat.SetInt(ShaderID.MaxSteps,           maxSteps);
        mat.SetVector(ShaderID.BaseColor,       baseColour);
    }

    public void Clamp(float bucketSize)
    {
        cellRadius  = Mathf.Clamp(cellRadius,  0, bucketSize * 0.5f);
        maxStepSize = Mathf.Clamp(maxStepSize, 0, bucketSize * 0.5f);
        minStepSize = Mathf.Clamp(minStepSize, 0, bucketSize * 0.5f);
    }
}

[Serializable]
public struct SssParams
{
    public Color color;
    [Range(0f, 2f)]     public float strength;
    [Range(0.1f, 20f)]  public float falloff;
    [Range(1, 50)]      public int   steps;
    [Range(0.001f, 1f)] public float stepSize;

    public void Apply(Material mat)
    {
        mat.SetVector(ShaderID.SSSColor,    color);
        mat.SetFloat(ShaderID.SSSStrength,  strength);
        mat.SetFloat(ShaderID.SSSFalloff,   falloff);
        mat.SetInt(ShaderID.SSSSteps,       steps);
        mat.SetFloat(ShaderID.SSSStepSize,  stepSize);
    }
}

[Serializable]
public struct SdfBrickParams
{
    public float maxDistance;
    public int   maxBricks;
    public int   samplesPerBucket;

    public int SamplesPerBucketPow2 => Mathf.NextPowerOfTwo(Mathf.Max(1, samplesPerBucket));

    public void Apply(ComputeShader cs)
    {
        cs.SetFloat(ShaderID.SdfMaxDistance,    maxDistance);
        cs.SetInt(ShaderID.SdfSamplesPerBucket, SamplesPerBucketPow2);
    }

    public void Apply(Material mat)
    {
        mat.SetFloat(ShaderID.SdfMaxDistance,    maxDistance);
        mat.SetFloat(ShaderID.SdfSamplesPerBucket, SamplesPerBucketPow2);
    }

    public void Clamp()
    {
        maxDistance      = Mathf.Max(0f, maxDistance);
        maxBricks        = Mathf.Max(1, maxBricks);
        samplesPerBucket = Mathf.Max(1, samplesPerBucket);
    }
}

// Not serialized — computed at runtime from SdfBrickParams + bounds + bucketSize.
public struct SdfLayout
{
    public Vector3Int BrickDim;
    public Vector3Int AtlasTexDim;
    private Vector3Int bucketMin;
    private Vector3Int bucketDim;
    private Vector3Int sampleDim;
    private Vector3Int atlasBrickDim;
    private int        maxBricksApplied;

    public static SdfLayout Compute(SdfBrickParams p, Bounds bounds, float bucketSize, int brickSize)
    {
        if (bucketSize <= 1e-6f)
            return new SdfLayout
            {
                bucketMin = Vector3Int.zero, bucketDim = Vector3Int.one,
                sampleDim = Vector3Int.one,  BrickDim  = Vector3Int.one,
                atlasBrickDim = Vector3Int.one, AtlasTexDim = Vector3Int.one,
                maxBricksApplied = 1
            };

        var min       = bounds.min;
        var max       = bounds.max;
        var bucketMin = new Vector3Int(
            Mathf.FloorToInt(min.x / bucketSize),
            Mathf.FloorToInt(min.y / bucketSize),
            Mathf.FloorToInt(min.z / bucketSize));
        var bucketMax = new Vector3Int(
            Mathf.FloorToInt(max.x / bucketSize),
            Mathf.FloorToInt(max.y / bucketSize),
            Mathf.FloorToInt(max.z / bucketSize));
        var bucketDim = new Vector3Int(
            Mathf.Max(1, bucketMax.x - bucketMin.x + 1),
            Mathf.Max(1, bucketMax.y - bucketMin.y + 1),
            Mathf.Max(1, bucketMax.z - bucketMin.z + 1));

        var samplesPow2 = p.SamplesPerBucketPow2;
        var sampleDim   = bucketDim * samplesPow2 + Vector3Int.one;
        var brickDim    = new Vector3Int(
            Mathf.CeilToInt(sampleDim.x / (float)brickSize),
            Mathf.CeilToInt(sampleDim.y / (float)brickSize),
            Mathf.CeilToInt(sampleDim.z / (float)brickSize));

        var targetBricks  = Mathf.Clamp(p.maxBricks, 1, brickDim.x * brickDim.y * brickDim.z);
        var side          = Mathf.CeilToInt(Mathf.Pow(targetBricks, 1f / 3f));
        var depth         = Mathf.CeilToInt(targetBricks / (float)(side * side));
        // Pack the atlas into a near-cubic grid of bricks for predictable layout.
        var atlasBrickDim = new Vector3Int(side, side, Mathf.Max(1, depth));

        return new SdfLayout
        {
            bucketMin        = bucketMin,
            bucketDim        = bucketDim,
            sampleDim        = sampleDim,
            BrickDim         = brickDim,
            atlasBrickDim    = atlasBrickDim,
            AtlasTexDim      = atlasBrickDim * brickSize,
            maxBricksApplied = targetBricks
        };
    }

    public void Apply(ComputeShader cs)
    {
        cs.SetInt(ShaderID.SdfMaxBricks,      Mathf.Max(1, maxBricksApplied));
        cs.SetInts(ShaderID.SdfBucketDim,     bucketDim.x,     bucketDim.y,     bucketDim.z);
        cs.SetInts(ShaderID.SdfBucketMin,     bucketMin.x,     bucketMin.y,     bucketMin.z);
        cs.SetInts(ShaderID.SdfSampleDim,     sampleDim.x,     sampleDim.y,     sampleDim.z);
        cs.SetInts(ShaderID.SdfBrickDim,      BrickDim.x,      BrickDim.y,      BrickDim.z);
        cs.SetInts(ShaderID.SdfAtlasBrickDim, atlasBrickDim.x, atlasBrickDim.y, atlasBrickDim.z);
    }

    public void Apply(Material mat)
    {
        mat.SetVector(ShaderID.SdfBucketMin,     (Vector3)bucketMin);
        mat.SetVector(ShaderID.SdfSampleDim,     (Vector3)sampleDim);
        mat.SetVector(ShaderID.SdfAtlasBrickDim, (Vector3)atlasBrickDim);
    }
}

internal static class ShaderID
{
    public static readonly int BaseColor           = Shader.PropertyToID("base_color");
    public static readonly int BondBendStiffness   = Shader.PropertyToID("bond_bend_stiffness");
    public static readonly int BondLinearStiffness = Shader.PropertyToID("bond_linear_stiffness");
    public static readonly int BondRangeSq         = Shader.PropertyToID("bond_range_sq");
    public static readonly int BondCells           = Shader.PropertyToID("bond_cells");
    public static readonly int BondRest            = Shader.PropertyToID("bond_rest");
    public static readonly int BoundsMax           = Shader.PropertyToID("bounds_max");
    public static readonly int BoundsMin           = Shader.PropertyToID("bounds_min");
    public static readonly int BucketCheck         = Shader.PropertyToID("bucket_check");
    public static readonly int BucketEnd           = Shader.PropertyToID("bucket_end");
    public static readonly int BucketSize          = Shader.PropertyToID("bucket_size");
    public static readonly int BucketStart         = Shader.PropertyToID("bucket_start");
    public static readonly int BucketTableSize     = Shader.PropertyToID("bucket_table_size");
    public static readonly int CameraPos           = Shader.PropertyToID("camera_pos");
    public static readonly int CellCount           = Shader.PropertyToID("cell_count");
    public static readonly int CellFullHash        = Shader.PropertyToID("cell_full_hash");
    public static readonly int CellIndices         = Shader.PropertyToID("cell_indices");
    public static readonly int CellRadius          = Shader.PropertyToID("cell_radius");
    public static readonly int CellRemap           = Shader.PropertyToID("cell_remap");
    public static readonly int CellTableKeys       = Shader.PropertyToID("cell_table_keys");
    public static readonly int Cells               = Shader.PropertyToID("cells");
    public static readonly int CellsTemp           = Shader.PropertyToID("cells_temp");
    public static readonly int CompactCount        = Shader.PropertyToID("compact_count");
    public static readonly int DeltaTime           = Shader.PropertyToID("delta_time");
    public static readonly int Damping             = Shader.PropertyToID("damping");
    public static readonly int DebugGrad           = Shader.PropertyToID("debug_grad");
    public static readonly int DebugAtlas          = Shader.PropertyToID("debug_atlas");
    public static readonly int DebugAtlasSlice     = Shader.PropertyToID("debug_atlas_slice");
    public static readonly int FresnelPow          = Shader.PropertyToID("fresnel_pow");
    public static readonly int FresnelStrength     = Shader.PropertyToID("fresnel_strength");
    public static readonly int ForceBuffer         = Shader.PropertyToID("force_buffer");
    public static readonly int Gravity             = Shader.PropertyToID("gravity");
    public static readonly int GrowthMode          = Shader.PropertyToID("growth_mode");
    public static readonly int MaxSplitsPerCell    = Shader.PropertyToID("max_splits_per_cell");
    public static readonly int SplitAge            = Shader.PropertyToID("split_age");
    public static readonly int SplitAgeJitter      = Shader.PropertyToID("split_age_jitter");
    public static readonly int IndirectArgs        = Shader.PropertyToID("indirect_args");
    public static readonly int InvViewProj         = Shader.PropertyToID("inv_view_proj");
    public static readonly int LightDir            = Shader.PropertyToID("light_dir");
    public static readonly int MaxBonds            = Shader.PropertyToID("max_bonds");
    public static readonly int MaxBondsPerCell     = Shader.PropertyToID("max_bonds_per_cell");
    public static readonly int MaxCells            = Shader.PropertyToID("max_cells");
    public static readonly int MaxStepSize         = Shader.PropertyToID("max_step_size");
    public static readonly int MaxSteps            = Shader.PropertyToID("max_steps");
    public static readonly int MinStepSize         = Shader.PropertyToID("min_step_size");
    public static readonly int NeighborRadius      = Shader.PropertyToID("neighbor_radius");
    public static readonly int NeighborRadiusSq    = Shader.PropertyToID("neighbor_radius_sq");
    public static readonly int RepulsionStrength   = Shader.PropertyToID("repulsion_strength");
    public static readonly int SSSColor            = Shader.PropertyToID("sss_color");
    public static readonly int SSSFalloff          = Shader.PropertyToID("sss_falloff");
    public static readonly int SSSStepSize         = Shader.PropertyToID("sss_step_size");
    public static readonly int SSSSteps            = Shader.PropertyToID("sss_steps");
    public static readonly int SSSStrength         = Shader.PropertyToID("sss_strength");
    public static readonly int SdfBrickAtlas       = Shader.PropertyToID("sdf_brick_atlas");
    public static readonly int SdfBrickIndex       = Shader.PropertyToID("sdf_brick_index");
    public static readonly int SdfBrickCounter     = Shader.PropertyToID("sdf_brick_counter");
    public static readonly int SdfBrickDim         = Shader.PropertyToID("sdf_brick_dim");
    public static readonly int SdfBucketCheckRo    = Shader.PropertyToID("bucket_check_ro");
    public static readonly int SdfBucketEndRo      = Shader.PropertyToID("bucket_end_ro");
    public static readonly int SdfBucketStartRo    = Shader.PropertyToID("bucket_start_ro");
    public static readonly int SdfCellsRo          = Shader.PropertyToID("cells_ro");
    public static readonly int SdfCellFullHashRo   = Shader.PropertyToID("cell_full_hash_ro");
    public static readonly int SdfCellIndicesRo    = Shader.PropertyToID("cell_indices_ro");
    public static readonly int SdfBucketDim        = Shader.PropertyToID("sdf_bucket_dim");
    public static readonly int SdfBucketMin        = Shader.PropertyToID("sdf_bucket_min");
    public static readonly int SdfAtlasBrickDim    = Shader.PropertyToID("sdf_atlas_brick_dim");
    public static readonly int SdfMaxDistance      = Shader.PropertyToID("sdf_max_distance");
    public static readonly int SdfMaxBricks        = Shader.PropertyToID("sdf_max_bricks");
    public static readonly int SdfSampleDim        = Shader.PropertyToID("sdf_sample_dim");
    public static readonly int SdfSamplesPerBucket = Shader.PropertyToID("sdf_samples_per_bucket");
    public static readonly int SurfaceThreshold    = Shader.PropertyToID("surface_threshold");
    public static readonly int Time                = Shader.PropertyToID("time");
    public static readonly int Velocities          = Shader.PropertyToID("velocities");
    public static readonly int VelocitiesTemp      = Shader.PropertyToID("velocities_temp");
    public static readonly int WindDirection       = Shader.PropertyToID("wind_direction");
    public static readonly int WindFrequency       = Shader.PropertyToID("wind_frequency");
    public static readonly int WindSpeed           = Shader.PropertyToID("wind_speed");
    public static readonly int WindStrength        = Shader.PropertyToID("wind_strength");
    public static readonly int WindVariation       = Shader.PropertyToID("wind_variation");
}
