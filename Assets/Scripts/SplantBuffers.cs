using UnityEngine;
using UnityEngine.Experimental.Rendering;

public struct SplantBuffers
{
    public ComputeBuffer BondCells;
    public ComputeBuffer BondRest;
    public ComputeBuffer CellComputeArgs;
    public ComputeBuffer CellCount;
    public ComputeBuffer CellIndices;
    public ComputeBuffer CellTableKeys;
    public ComputeBuffer Cells;
    public ComputeBuffer CompactCount;
    public ComputeBuffer SdfBrickCounter;
    public ComputeBuffer Temp0;
    public ComputeBuffer Temp1;
    public ComputeBuffer Temp2;
    public ComputeBuffer Temp3;
    public RenderTexture SdfBrickAtlas;
    
    private ComputeBuffer bucketCheck;
    private ComputeBuffer bucketEnd;
    private ComputeBuffer bucketStart;
    private ComputeBuffer cellFullHashes;
    private ComputeBuffer cellRemap;
    private ComputeBuffer cellsTemp;
    private ComputeBuffer forceBuffer;
    private ComputeBuffer velocities;
    private ComputeBuffer velocitiesTemp;
    private RenderTexture sdfBrickIndex;

    public bool IsAllocated => CellComputeArgs != null;

    public void Alloc(int maxCells, int maxBonds, int bucketTableSize, int cellStride)
    {
        BondCells       = new ComputeBuffer(maxBonds, sizeof(uint) * 2);
        BondRest        = new ComputeBuffer(maxBonds, sizeof(float) * 4);
        bucketCheck     = new ComputeBuffer(bucketTableSize, sizeof(uint));
        bucketEnd       = new ComputeBuffer(bucketTableSize, sizeof(uint));
        bucketStart     = new ComputeBuffer(bucketTableSize, sizeof(uint));
        CellComputeArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
        CellCount       = new ComputeBuffer(1, sizeof(uint));
        cellFullHashes  = new ComputeBuffer(maxCells, sizeof(uint));
        CellIndices     = new ComputeBuffer(maxCells, sizeof(uint));
        cellRemap       = new ComputeBuffer(maxCells, sizeof(uint));
        CellTableKeys   = new ComputeBuffer(maxCells, sizeof(uint));
        Cells           = new ComputeBuffer(maxCells, cellStride);
        cellsTemp       = new ComputeBuffer(maxCells, cellStride);
        CompactCount    = new ComputeBuffer(1, sizeof(uint));
        forceBuffer     = new ComputeBuffer(maxCells * 3, sizeof(uint));
        velocities      = new ComputeBuffer(maxCells, sizeof(float) * 3);
        velocitiesTemp  = new ComputeBuffer(maxCells, sizeof(float) * 3);

        CellComputeArgs.SetData(new uint[] { 1, 1, 1 });
        CellCount.SetData(new[] { 2u });
        CompactCount.SetData(new[] { 0u });
        forceBuffer.SetData(new uint[maxCells * 3]);
        velocities.SetData(new Vector3[maxCells]);
        velocitiesTemp.SetData(new Vector3[maxCells]);
    }

    public void BindKernels(ComputeShader cs, SplantKernels k)
    {
        cs.SetBuffer(k.PrepareIndirectArgs, ShaderID.IndirectArgs, CellComputeArgs);
        cs.SetBuffer(k.PrepareIndirectArgs, ShaderID.CellCount,    CellCount);

        cs.SetBuffer(k.AddBonds, ShaderID.BondCells,    BondCells);
        cs.SetBuffer(k.AddBonds, ShaderID.BondRest,     BondRest);
        cs.SetBuffer(k.AddBonds, ShaderID.BucketEnd,    bucketEnd);
        cs.SetBuffer(k.AddBonds, ShaderID.BucketStart,  bucketStart);
        cs.SetBuffer(k.AddBonds, ShaderID.CellCount,    CellCount);
        cs.SetBuffer(k.AddBonds, ShaderID.CellFullHash, cellFullHashes);
        cs.SetBuffer(k.AddBonds, ShaderID.CellIndices,  CellIndices);
        cs.SetBuffer(k.AddBonds, ShaderID.Cells,        Cells);

        cs.SetBuffer(k.ApplyBondForces, ShaderID.BondCells,   BondCells);
        cs.SetBuffer(k.ApplyBondForces, ShaderID.BondRest,    BondRest);
        cs.SetBuffer(k.ApplyBondForces, ShaderID.CellCount,   CellCount);
        cs.SetBuffer(k.ApplyBondForces, ShaderID.Cells,       Cells);
        cs.SetBuffer(k.ApplyBondForces, ShaderID.ForceBuffer, forceBuffer);

        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.BucketEnd,    bucketEnd);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.BucketStart,  bucketStart);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.CellCount,    CellCount);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.CellFullHash, cellFullHashes);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.CellIndices,  CellIndices);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.Cells,        Cells);
        cs.SetBuffer(k.ApplyCollisionForces, ShaderID.ForceBuffer,  forceBuffer);

        cs.SetBuffer(k.BuildBucket, ShaderID.BucketEnd,     bucketEnd);
        cs.SetBuffer(k.BuildBucket, ShaderID.BucketStart,   bucketStart);
        cs.SetBuffer(k.BuildBucket, ShaderID.CellCount,     CellCount);
        cs.SetBuffer(k.BuildBucket, ShaderID.CellTableKeys, CellTableKeys);

        cs.SetBuffer(k.BuildSpatial, ShaderID.BucketCheck,   bucketCheck);
        cs.SetBuffer(k.BuildSpatial, ShaderID.CellCount,     CellCount);
        cs.SetBuffer(k.BuildSpatial, ShaderID.CellFullHash,  cellFullHashes);
        cs.SetBuffer(k.BuildSpatial, ShaderID.CellIndices,   CellIndices);
        cs.SetBuffer(k.BuildSpatial, ShaderID.CellTableKeys, CellTableKeys);
        cs.SetBuffer(k.BuildSpatial, ShaderID.Cells,         Cells);

        if (SdfBrickAtlas && sdfBrickIndex && SdfBrickCounter != null)
        {
            cs.SetTexture(k.BuildSdfBricks, ShaderID.SdfBrickAtlas,     SdfBrickAtlas);
            cs.SetTexture(k.BuildSdfBricks, ShaderID.SdfBrickIndex,     sdfBrickIndex);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfBrickCounter,   SdfBrickCounter);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfBucketCheckRo,  bucketCheck);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfBucketEndRo,    bucketEnd);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfBucketStartRo,  bucketStart);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfCellFullHashRo, cellFullHashes);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfCellIndicesRo,  CellIndices);
            cs.SetBuffer(k.BuildSdfBricks,  ShaderID.SdfCellsRo,        Cells);
            cs.SetTexture(k.ClearSdfIndex,  ShaderID.SdfBrickIndex,     sdfBrickIndex);
        }

        cs.SetBuffer(k.ClearBuckets, ShaderID.BucketCheck, bucketCheck);
        cs.SetBuffer(k.ClearBuckets, ShaderID.BucketEnd,   bucketEnd);
        cs.SetBuffer(k.ClearBuckets, ShaderID.BucketStart, bucketStart);

        cs.SetBuffer(k.Grow, ShaderID.CellCount,  CellCount);
        cs.SetBuffer(k.Grow, ShaderID.Cells,      Cells);
        cs.SetBuffer(k.Grow, ShaderID.Velocities, velocities);

        cs.SetBuffer(k.ResolveForceBuffer, ShaderID.CellCount,   CellCount);
        cs.SetBuffer(k.ResolveForceBuffer, ShaderID.Cells,       Cells);
        cs.SetBuffer(k.ResolveForceBuffer, ShaderID.ForceBuffer, forceBuffer);
        cs.SetBuffer(k.ResolveForceBuffer, ShaderID.Velocities,  velocities);

        cs.SetBuffer(k.CompactCells, ShaderID.CellCount,    CellCount);
        cs.SetBuffer(k.CompactCells, ShaderID.CellRemap,    cellRemap);
        cs.SetBuffer(k.CompactCells, ShaderID.Cells,        Cells);
        cs.SetBuffer(k.CompactCells, ShaderID.CompactCount, CompactCount);

        cs.SetBuffer(k.WriteCompacted, ShaderID.CellCount,      CellCount);
        cs.SetBuffer(k.WriteCompacted, ShaderID.CellRemap,      cellRemap);
        cs.SetBuffer(k.WriteCompacted, ShaderID.Cells,          Cells);
        cs.SetBuffer(k.WriteCompacted, ShaderID.CellsTemp,      cellsTemp);
        cs.SetBuffer(k.WriteCompacted, ShaderID.Velocities,     velocities);
        cs.SetBuffer(k.WriteCompacted, ShaderID.VelocitiesTemp, velocitiesTemp);

        cs.SetBuffer(k.RemapBonds, ShaderID.BondCells,    BondCells);
        cs.SetBuffer(k.RemapBonds, ShaderID.BondRest,     BondRest);
        cs.SetBuffer(k.RemapBonds, ShaderID.CellCount,    CellCount);
        cs.SetBuffer(k.RemapBonds, ShaderID.CellRemap,    cellRemap);
        cs.SetBuffer(k.RemapBonds, ShaderID.Cells,        Cells);
        cs.SetBuffer(k.RemapBonds, ShaderID.CompactCount, CompactCount);

        cs.SetBuffer(k.CopyBack, ShaderID.Cells,          Cells);
        cs.SetBuffer(k.CopyBack, ShaderID.CellsTemp,      cellsTemp);
        cs.SetBuffer(k.CopyBack, ShaderID.Velocities,     velocities);
        cs.SetBuffer(k.CopyBack, ShaderID.VelocitiesTemp, velocitiesTemp);
        cs.SetBuffer(k.CopyBack, ShaderID.CompactCount,   CompactCount);

        cs.SetBuffer(k.FinalizeCompact, ShaderID.CellCount,    CellCount);
        cs.SetBuffer(k.FinalizeCompact, ShaderID.CompactCount, CompactCount);
    }

    public void BindMaterial(Material mat)
    {
        mat.SetBuffer(ShaderID.BucketCheck,  bucketCheck);
        mat.SetBuffer(ShaderID.BucketEnd,    bucketEnd);
        mat.SetBuffer(ShaderID.BucketStart,  bucketStart);
        mat.SetBuffer(ShaderID.CellFullHash, cellFullHashes);
        mat.SetBuffer(ShaderID.CellIndices,  CellIndices);
        mat.SetBuffer(ShaderID.Cells,        Cells);
        if (SdfBrickAtlas)
        {
            mat.SetTexture(ShaderID.SdfBrickAtlas, SdfBrickAtlas);
            if (sdfBrickIndex)
                mat.SetTexture(ShaderID.SdfBrickIndex, sdfBrickIndex);
        }
    }

    public void Release(bool immediate)
    {
        ReleaseBuffer(ref BondCells);
        ReleaseBuffer(ref BondRest);
        ReleaseBuffer(ref bucketCheck);
        ReleaseBuffer(ref bucketEnd);
        ReleaseBuffer(ref bucketStart);
        ReleaseBuffer(ref CellComputeArgs);
        ReleaseBuffer(ref CellCount);
        ReleaseBuffer(ref cellFullHashes);
        ReleaseBuffer(ref CellIndices);
        ReleaseBuffer(ref cellRemap);
        ReleaseBuffer(ref CellTableKeys);
        ReleaseBuffer(ref Cells);
        ReleaseBuffer(ref cellsTemp);
        ReleaseBuffer(ref CompactCount);
        ReleaseBuffer(ref forceBuffer);
        ReleaseBuffer(ref velocities);
        ReleaseBuffer(ref velocitiesTemp);
        ReleaseBuffer(ref Temp0);
        ReleaseBuffer(ref Temp1);
        ReleaseBuffer(ref Temp2);
        ReleaseBuffer(ref Temp3);
        ReleaseBuffer(ref SdfBrickCounter);
        ReleaseTexture(ref SdfBrickAtlas, immediate);
        ReleaseTexture(ref sdfBrickIndex, immediate);
    }

    private static void ReleaseBuffer(ref ComputeBuffer buf)
    {
        buf?.Release();
        buf = null;
    }

    public void ReallocSdfTextures(SdfLayout layout)
    {
        if (SdfBrickAtlas &&
            SdfBrickAtlas.width       == layout.AtlasTexDim.x &&
            SdfBrickAtlas.height      == layout.AtlasTexDim.y &&
            SdfBrickAtlas.volumeDepth == layout.AtlasTexDim.z &&
            sdfBrickIndex &&
            sdfBrickIndex.width       == layout.BrickDim.x &&
            sdfBrickIndex.height      == layout.BrickDim.y &&
            sdfBrickIndex.volumeDepth == layout.BrickDim.z &&
            SdfBrickCounter != null)
        {
            return;
        }

        ReleaseSdfResources(false);

        SdfBrickAtlas   = CreateVolume(layout.AtlasTexDim, GraphicsFormat.R8_UNorm);
        sdfBrickIndex   = CreateVolume(layout.BrickDim,    GraphicsFormat.R32_UInt);
        SdfBrickCounter = new ComputeBuffer(1, sizeof(uint));
    }

    public void ReleaseSdfResources(bool immediate)
    {
        ReleaseTexture(ref SdfBrickAtlas, immediate);
        ReleaseTexture(ref sdfBrickIndex, immediate);
        SdfBrickCounter?.Release();
        SdfBrickCounter = null;
    }

    private static RenderTexture CreateVolume(Vector3Int size, GraphicsFormat format)
    {
        var desc = new RenderTextureDescriptor(size.x, size.y, format, 0)
        {
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = size.z,
            enableRandomWrite = true
        };
        var rt = new RenderTexture(desc) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        rt.Create();
        return rt;
    }

    private static void ReleaseTexture(ref RenderTexture tex, bool immediate)
    {
        if (!tex) return;
        tex.Release();
        if (immediate) Object.DestroyImmediate(tex); else Object.Destroy(tex);
        tex = null;
    }
}
