using UnityEngine;

public struct SplantKernels
{
    public int AddBonds;
    public int ApplyBondForces;
    public int ApplyCollisionForces;
    public int BuildBucket;
    public int BuildSdfBricks;
    public int ClearSdfIndex;
    public int BuildSpatial;
    public int ClearBuckets;
    public int CompactCells;
    public int CopyBack;
    public int FinalizeCompact;
    public int Grow;
    public int InitBonds;
    public int PrepareIndirectArgs;
    public int RemapBonds;
    public int ResolveForceBuffer;
    public int WriteCompacted;

    public static SplantKernels Find(ComputeShader cs) => new()
    {
        AddBonds             = cs.FindKernel("add_bonds"),
        ApplyBondForces      = cs.FindKernel("apply_bond_forces"),
        ApplyCollisionForces = cs.FindKernel("apply_collision_forces"),
        BuildBucket          = cs.FindKernel("build_bucket_ranges"),
        BuildSdfBricks       = cs.FindKernel("build_sdf_bricks"),
        ClearSdfIndex        = cs.FindKernel("clear_sdf_index"),
        BuildSpatial         = cs.FindKernel("build_spatial_hash"),
        ClearBuckets         = cs.FindKernel("clear_buckets"),
        CompactCells         = cs.FindKernel("compact_cells"),
        CopyBack             = cs.FindKernel("copy_back"),
        FinalizeCompact      = cs.FindKernel("finalize_compact"),
        Grow                 = cs.FindKernel("grow"),
        InitBonds            = cs.FindKernel("init_bonds"),
        PrepareIndirectArgs  = cs.FindKernel("prepare_indirect_args"),
        RemapBonds           = cs.FindKernel("remap_bonds"),
        ResolveForceBuffer   = cs.FindKernel("resolve_force_buffer"),
        WriteCompacted       = cs.FindKernel("write_compacted"),
    };
}
