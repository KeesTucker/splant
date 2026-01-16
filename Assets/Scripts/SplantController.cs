using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting.Runtime;

public class SplantController : MonoBehaviour
{
    private int _addBondsKernel;
    private int _applyBondForcesKernel;
    private int _applyCollisionForcesKernel;
    private int _buildBucketKernel;
    private int _buildSpatialKernel;
    private int _clearBucketsKernel;
    private int _compactCellsKernel;
    private int _copyBackKernel;
    private int _finalizeCompactKernel;
    private int _growKernel;
    private int _initBondsKernel;
    private int _prepareIndirectArgsKernel;
    private int _remapBondsKernel;
    private int _resolveForceBufferKernel;
    private int _writeCompactedKernel;
    
    private static readonly int BaseColorID = Shader.PropertyToID("base_color");
    private static readonly int BondBendStiffnessID = Shader.PropertyToID("bond_bend_stiffness");
    private static readonly int BondLinearStiffnessID = Shader.PropertyToID("bond_linear_stiffness");
    private static readonly int BondRangeSqID = Shader.PropertyToID("bond_range_sq");
    private static readonly int BondCellsID = Shader.PropertyToID("bond_cells");
    private static readonly int BondRestID = Shader.PropertyToID("bond_rest");
    private static readonly int BoundsMaxId = Shader.PropertyToID("bounds_max");
    private static readonly int BoundsMinId = Shader.PropertyToID("bounds_min");
    private static readonly int BucketCheckId = Shader.PropertyToID("bucket_check");
    private static readonly int BucketEndID = Shader.PropertyToID("bucket_end");
    private static readonly int BucketSizeID = Shader.PropertyToID("bucket_size");
    private static readonly int BucketStartID = Shader.PropertyToID("bucket_start");
    private static readonly int BucketTableSizeID = Shader.PropertyToID("bucket_table_size");
    private static readonly int CameraPosID = Shader.PropertyToID("camera_pos");
    private static readonly int CellCountID = Shader.PropertyToID("cell_count");
    private static readonly int CellFullHashID = Shader.PropertyToID("cell_full_hash");
    private static readonly int CellIndicesID = Shader.PropertyToID("cell_indices");
    private static readonly int CellRadiusID = Shader.PropertyToID("cell_radius");
    private static readonly int CellRadiusSqID = Shader.PropertyToID("cell_radius_sq");
    private static readonly int CellRemapID = Shader.PropertyToID("cell_remap");
    private static readonly int CellTableKeysID = Shader.PropertyToID("cell_table_keys");
    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellsTempID = Shader.PropertyToID("cells_temp");
    private static readonly int CompactCountID = Shader.PropertyToID("compact_count");
    private static readonly int DeltaTimeID = Shader.PropertyToID("delta_time");
    private static readonly int DampingID = Shader.PropertyToID("damping");
    private static readonly int FresnelPowID = Shader.PropertyToID("fresnel_pow");
    private static readonly int FresnelStrengthID = Shader.PropertyToID("fresnel_strength");
    private static readonly int ForceBufferID = Shader.PropertyToID("force_buffer");
    private static readonly int GravityID = Shader.PropertyToID("gravity");
    private static readonly int IndirectArgsID = Shader.PropertyToID("indirect_args");
    private static readonly int InvCellRadiusSqID = Shader.PropertyToID("inv_cell_radius_sq");
    private static readonly int InvViewProjID = Shader.PropertyToID("inv_view_proj");
    private static readonly int LightDirID = Shader.PropertyToID("light_dir");
    private static readonly int MaxBondsId = Shader.PropertyToID("max_bonds");
    private static readonly int MaxBondsPerCellID = Shader.PropertyToID("max_bonds_per_cell");
    private static readonly int MaxCellsID   = Shader.PropertyToID("max_cells");
    private static readonly int MaxPosID = Shader.PropertyToID("max_pos"); // replace with bounds max
    private static readonly int MaxStepSizeID = Shader.PropertyToID("max_step_size");
    private static readonly int MaxStepsID = Shader.PropertyToID("max_steps");
    private static readonly int MinPosID = Shader.PropertyToID("min_pos"); // replace with bounds min 
    private static readonly int MinStepSizeID = Shader.PropertyToID("min_step_size");
    private static readonly int NeighborRadiusID = Shader.PropertyToID("neighbor_radius");
    private static readonly int NeighborRadiusSqID = Shader.PropertyToID("neighbor_radius_sq");
    private static readonly int RepulsionStrengthID = Shader.PropertyToID("repulsion_strength");
    private static readonly int SSSColorID = Shader.PropertyToID("sss_color");
    private static readonly int SSSFalloffID = Shader.PropertyToID("sss_falloff");
    private static readonly int SSSSmoothingBandID = Shader.PropertyToID("sss_smoothing_band");
    private static readonly int SSSStepSizeID = Shader.PropertyToID("sss_step_size");
    private static readonly int SSSStepsID = Shader.PropertyToID("sss_steps");
    private static readonly int SSSStrengthID = Shader.PropertyToID("sss_strength");
    private static readonly int SurfaceThresholdID = Shader.PropertyToID("surface_threshold");
    private static readonly int TimeID = Shader.PropertyToID("time");
    private static readonly int VelocitiesID = Shader.PropertyToID("velocities");
    private static readonly int VelocitiesTempID = Shader.PropertyToID("velocities_temp");
    
    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 10);
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Transform mainLight;
    [SerializeField] private Material raymarchMaterial;
    [SerializeField] private uint maxCells = 100000;
    
    [Header("Compaction")]
    [SerializeField] private int compactEveryNFrames = 60;
    
    [Header("Spatial Hashing")]
    [SerializeField] private float bucketSize = 0.1f;
    [Tooltip("Must be a power of 2")]
    [SerializeField] private int bucketTableSize = 1 << 18;
    
    [Header("Physics")] 
    [Tooltip("Must be <= 0.5x bucketSize")]
    [SerializeField] private float neighborCollisionRadius = 0.5f;
    [SerializeField] private float repulsionStrength = 1f;
    [SerializeField] private float damping = 1.5f;
    [SerializeField] private Vector3 gravity = new(0f, -0.25f, 0f);
    
    [Header("Bonds")] 
    [Tooltip("Must be <= 0.5x bucketSize")]
    [SerializeField] private float bondRange = 0.05f;
    [SerializeField] private float bondLinearStiffness = 1f;
    [SerializeField] private float bondBendStiffness = 1f;
    [SerializeField] private uint maxBondsPerCell = 6;
    
    [Header("Rendering")]
    [Range(1, 512)] 
    [SerializeField] private int maxSteps = 256;
    [Tooltip("Must be <= 0.5x bucketSize")]
    [SerializeField] private float cellRadius = 0.05f;
    [Tooltip("Must be <= 0.5x bucketSize")]
    [SerializeField] private float minStepSize = 0.02f;
    [Tooltip("Must be <= 0.5x bucketSize")]
    [SerializeField] private float maxStepSize = 0.0667f;
    [Range(0.001f, 1f)]
    [SerializeField] private float surfaceThreshold = 0.5f;
    [SerializeField] private float fresnelPow = 3f;
    [SerializeField] private float fresnelStrength = 0.15f;
    [SerializeField] private Color baseColour = new(0.2f, 0.9f, 0.5f, 1f);
    
    [Header("SSS")] 
    [SerializeField] private Color sssColor = new(0.95f, 0.6f, 0.5f, 1f);
    [Range(0f, 2f)]
    [SerializeField] private float sssStrength = 0.6f;
    [Range(0.1f, 20f)]
    [SerializeField] private float sssFalloff = 1.5f;
    [Range(1, 50)] 
    [SerializeField] private int sssSteps = 12;
    [Range(0.001f, 1f)] 
    [SerializeField] private float sssStepSize = 0.02f;
    [Range(0.001f, 1f)] 
    [SerializeField] private float sssSmoothingBand = 0.2f;
    
    private ComputeBuffer _bondCells;
    private ComputeBuffer _bondRest;
    private ComputeBuffer _bucketCheck;
    private ComputeBuffer _bucketEnd;
    private ComputeBuffer _bucketStart;
    private ComputeBuffer _cellComputeArgs;
    private ComputeBuffer _cellCount;
    private ComputeBuffer _cellFullHashes;
    private ComputeBuffer _cellIndices;
    private ComputeBuffer _cellRemap;
    private ComputeBuffer _cellTableKeys;
    private ComputeBuffer _cells;
    private ComputeBuffer _cellsTemp;
    private ComputeBuffer _compactCount;
    private ComputeBuffer _forceBuffer;
    private ComputeBuffer _velocities;
    private ComputeBuffer _velocitiesTemp;
    private ComputeBuffer _temp0;
    private ComputeBuffer _temp1;
    private ComputeBuffer _temp2;
    private ComputeBuffer _temp3;
    
    private DeviceRadixSort _deviceRadixSort;
    private int _frameCount;

    private void Awake()
    {
        FindKernels();
        InitBuffers();
        InitRadixSort();
    }

    private void FindKernels()
    {
        _addBondsKernel = compute.FindKernel("add_bonds");
        _applyBondForcesKernel = compute.FindKernel("apply_bond_forces");
        _applyCollisionForcesKernel = compute.FindKernel("apply_collision_forces");
        _buildBucketKernel = compute.FindKernel("build_bucket_ranges");
        _buildSpatialKernel = compute.FindKernel("build_spatial_hash");
        _clearBucketsKernel = compute.FindKernel("clear_buckets");
        _compactCellsKernel = compute.FindKernel("compact_cells");
        _copyBackKernel = compute.FindKernel("copy_back");
        _finalizeCompactKernel = compute.FindKernel("finalize_compact");
        _growKernel = compute.FindKernel("grow");
        _initBondsKernel = compute.FindKernel("init_bonds");
        _prepareIndirectArgsKernel = compute.FindKernel("prepare_indirect_args");
        _remapBondsKernel = compute.FindKernel("remap_bonds");
        _resolveForceBufferKernel = compute.FindKernel("resolve_force_buffer");
        _writeCompactedKernel = compute.FindKernel("write_compacted");
    }

    private void InitBuffers()
    {
        var cellStride = Marshal.SizeOf<Cell>();
        var maxBonds = (int)maxCells * (int)maxBondsPerCell;

        _bondCells = new ComputeBuffer(maxBonds, sizeof(uint) * 2);
        _bondRest = new ComputeBuffer(maxBonds, sizeof(float) * 4);
        _bucketCheck = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketEnd = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketStart = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _cellComputeArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
        _cellCount = new ComputeBuffer(1, sizeof(uint));
        _cellFullHashes = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellIndices = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellRemap = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellTableKeys = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cells = new ComputeBuffer((int)maxCells, cellStride);
        _cellsTemp = new ComputeBuffer((int)maxCells, cellStride);
        _compactCount = new ComputeBuffer(1, sizeof(uint));
        _forceBuffer = new ComputeBuffer((int)maxCells * 3, sizeof(uint));
        _velocities = new ComputeBuffer((int)maxCells, sizeof(float) * 3);
        _velocitiesTemp = new ComputeBuffer((int)maxCells, sizeof(float) * 3);

        // Init
        _cellComputeArgs.SetData(new uint[] { 1, 1, 1 });
        _cellCount.SetData(new[] { 2u });
        _compactCount.SetData(new[] { 0u });
        _forceBuffer.SetData(new uint[(int)maxCells * 3]);
        _velocities.SetData(new Vector3[(int)maxCells]);
        _velocitiesTemp.SetData(new Vector3[(int)maxCells]);
        
        // Seed with one cell
        _cells.SetData(new[] {
            new Cell
            {
                age = 0,
                position = Vector3.zero,
                polarity = Vector3.up
            },
            new Cell
            {
                age = 0,
                position = Vector3.one * 2,
                polarity = Vector3.up
            }
        });
        
        // Init entire bond buffer to invalid
        compute.SetInt(MaxBondsId, maxBonds);
        compute.SetBuffer(_initBondsKernel, BondCellsID, _bondCells);
        compute.SetBuffer(_initBondsKernel, BondRestID, _bondRest);
        var bondGroups = Mathf.CeilToInt(maxBonds / 64f);
        compute.Dispatch(_initBondsKernel, bondGroups, 1, 1);
    }

    private void InitRadixSort()
    {
        _deviceRadixSort = new DeviceRadixSort(
            compute: sortCompute,
            allocationSize: (int)maxCells,
            ref _temp0, ref _temp1, ref _temp2, ref _temp3
        );
    }

    private void Start()
    {
        SetParams();
        SetComputeBuffers();
        SetRaymarchBuffers();
    }

    private void OnValidate()
    {
        // enforce bucket size constrained vars
        bondRange = Mathf.Clamp(bondRange, 0, bucketSize * 0.5f);
        cellRadius = Mathf.Clamp(cellRadius, 0, bucketSize * 0.5f);
        maxStepSize = Mathf.Clamp(maxStepSize, 0, bucketSize * 0.5f);
        minStepSize = Mathf.Clamp(minStepSize, 0, bucketSize * 0.5f);
        neighborCollisionRadius = Mathf.Clamp(neighborCollisionRadius, 0, bucketSize * 0.5f);
        damping = Mathf.Max(0f, damping);
        
        // ensure bucket table size is a power of 2 (has exactly 1 bit)
        if (!(bucketTableSize > 0 && (bucketTableSize & (bucketTableSize - 1)) == 0))
        {
            // reset
            bucketTableSize = 1 << 18;
        }
        
        SetParams();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void Update()
    {
        // Set time
        compute.SetFloat(DeltaTimeID, Time.deltaTime);
        compute.SetFloat(TimeID, Time.time);
        
        // Set light dir
        raymarchMaterial.SetVector(LightDirID, -mainLight.transform.forward);
    
        // Grow cells
        compute.DispatchIndirect(_growKernel, _cellComputeArgs);
        
        // Compact every N frames to remove dead cells
        _frameCount++;
        if (compactEveryNFrames != 0 && _frameCount >= compactEveryNFrames)
        {
            _frameCount = 0;
            CompactCells();
        }
        
        // Prepare indirect args
        compute.Dispatch(_prepareIndirectArgsKernel, 1, 1, 1);
    
        // Sort needs CPU count. TODO: modify to allow DispatchIndirect
        uint[] count = { 0 };
        _cellCount.GetData(count);
        var cellCount = (int)Mathf.Min(count[0], maxCells);
        
        // Clear buckets
        var bucketGroups = Mathf.CeilToInt(bucketTableSize / 64f);
        compute.Dispatch(_clearBucketsKernel, bucketGroups, 1, 1);
        
        // Build spatial hash
        compute.DispatchIndirect(_buildSpatialKernel, _cellComputeArgs);
        
        // Sort by hash
        _deviceRadixSort.Sort(
            cellCount,
            _cellTableKeys,
            _cellIndices,
            _temp0, _temp1, _temp2, _temp3,
            typeof(uint), typeof(uint),
            true
        );
        
        // Build bucket ranges
        compute.DispatchIndirect(_buildBucketKernel, _cellComputeArgs);
        
        // Bonds
        compute.DispatchIndirect(_addBondsKernel, _cellComputeArgs);
        
        // Physics
        compute.DispatchIndirect(_applyCollisionForcesKernel, _cellComputeArgs);
        compute.DispatchIndirect(_applyBondForcesKernel, _cellComputeArgs);
        compute.DispatchIndirect(_resolveForceBufferKernel, _cellComputeArgs);

        // Rendering
        var view = mainCamera.worldToCameraMatrix;
        var proj = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
        var invViewProj = (proj * view).inverse;

        raymarchMaterial.SetMatrix(InvViewProjID, invViewProj);
        raymarchMaterial.SetVector(CameraPosID, mainCamera.transform.position);
        
        Graphics.DrawProcedural(
            raymarchMaterial,
            new Bounds(Vector3.zero, Vector3.one * 100000),
            MeshTopology.Triangles,
            3
        );
    }
    
    private void SetParams()
    {
        compute.SetFloat(BondBendStiffnessID, bondBendStiffness);
        compute.SetFloat(BondLinearStiffnessID, bondLinearStiffness);
        compute.SetFloat(BondRangeSqID, bondRange * bondRange);
        compute.SetFloat(BucketSizeID, bucketSize);
        compute.SetFloat(CellRadiusID, cellRadius);
        compute.SetFloat(DampingID, damping);
        compute.SetVector(GravityID, gravity);
        compute.SetFloat(NeighborRadiusID, neighborCollisionRadius);
        compute.SetFloat(NeighborRadiusSqID, neighborCollisionRadius * neighborCollisionRadius);
        compute.SetFloat(RepulsionStrengthID, repulsionStrength);
        compute.SetInt(BucketTableSizeID, bucketTableSize);
        compute.SetInt(MaxBondsId, (int)maxCells * (int)maxBondsPerCell);
        compute.SetInt(MaxBondsPerCellID, (int)maxBondsPerCell);
        compute.SetInt(MaxCellsID, (int)maxCells);
        compute.SetVector(MaxPosID, bounds.max);
        compute.SetVector(MinPosID, bounds.min);
        
        raymarchMaterial.SetFloat(BucketSizeID, bucketSize);
        raymarchMaterial.SetFloat(CellRadiusID, cellRadius);
        raymarchMaterial.SetFloat(CellRadiusSqID, cellRadius * cellRadius);
        raymarchMaterial.SetFloat(FresnelPowID, fresnelPow);
        raymarchMaterial.SetFloat(FresnelStrengthID, fresnelStrength);
        raymarchMaterial.SetFloat(InvCellRadiusSqID, 1 / (cellRadius * cellRadius));
        raymarchMaterial.SetFloat(MaxStepSizeID, maxStepSize);
        raymarchMaterial.SetFloat(MinStepSizeID, minStepSize);
        raymarchMaterial.SetFloat(NeighborRadiusSqID, neighborCollisionRadius * neighborCollisionRadius);
        raymarchMaterial.SetFloat(SSSFalloffID, sssFalloff);
        raymarchMaterial.SetFloat(SSSSmoothingBandID, sssSmoothingBand);
        raymarchMaterial.SetFloat(SSSStepSizeID, sssStepSize);
        raymarchMaterial.SetFloat(SSSStrengthID, sssStrength);
        raymarchMaterial.SetFloat(SurfaceThresholdID, surfaceThreshold);
        raymarchMaterial.SetInt(BucketTableSizeID, bucketTableSize);
        raymarchMaterial.SetInt(MaxStepsID, maxSteps);
        raymarchMaterial.SetInt(SSSStepsID, sssSteps);
        raymarchMaterial.SetVector(BaseColorID, baseColour);
        raymarchMaterial.SetVector(BoundsMaxId, bounds.max);
        raymarchMaterial.SetVector(BoundsMinId, bounds.min);
        raymarchMaterial.SetVector(SSSColorID, sssColor);
    }
    
    private void SetComputeBuffers()
    {
        compute.SetBuffer(_prepareIndirectArgsKernel, IndirectArgsID, _cellComputeArgs);
        compute.SetBuffer(_prepareIndirectArgsKernel, CellCountID, _cellCount);

        SetAddBondsKernelBuffers();
        SetApplyBondForcesKernelBuffers();
        SetApplyCollisionForcesKernelBuffers();
        SetBuildBucketKernelBuffers();
        SetBuildSpatialKernelBuffers();
        SetClearBucketsKernelBuffers();
        SetGrowKernelBuffers();
        SetResolveForceKernelBuffers();
        
        // Compaction kernels
        compute.SetBuffer(_compactCellsKernel, CellCountID, _cellCount);
        compute.SetBuffer(_compactCellsKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_compactCellsKernel, CellsID, _cells);
        compute.SetBuffer(_compactCellsKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_writeCompactedKernel, CellCountID, _cellCount);
        compute.SetBuffer(_writeCompactedKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_writeCompactedKernel, CellsID, _cells);
        compute.SetBuffer(_writeCompactedKernel, CellsTempID, _cellsTemp);
        compute.SetBuffer(_writeCompactedKernel, VelocitiesID, _velocities);
        compute.SetBuffer(_writeCompactedKernel, VelocitiesTempID, _velocitiesTemp);
        
        compute.SetBuffer(_remapBondsKernel, BondCellsID, _bondCells);
        compute.SetBuffer(_remapBondsKernel, BondRestID, _bondRest);
        compute.SetBuffer(_remapBondsKernel, CellCountID, _cellCount);
        compute.SetBuffer(_remapBondsKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_remapBondsKernel, CellsID, _cells);
        compute.SetBuffer(_remapBondsKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_copyBackKernel, CellsID, _cells);
        compute.SetBuffer(_copyBackKernel, CellsTempID, _cellsTemp);
        compute.SetBuffer(_copyBackKernel, VelocitiesID, _velocities);
        compute.SetBuffer(_copyBackKernel, VelocitiesTempID, _velocitiesTemp);
        compute.SetBuffer(_copyBackKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_finalizeCompactKernel, CellCountID, _cellCount);
        compute.SetBuffer(_finalizeCompactKernel, CompactCountID, _compactCount);
    }
    
    private void SetAddBondsKernelBuffers()
    {
        compute.SetBuffer(_addBondsKernel, BondCellsID, _bondCells);
        compute.SetBuffer(_addBondsKernel, BondRestID, _bondRest);
        compute.SetBuffer(_addBondsKernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(_addBondsKernel, BucketStartID, _bucketStart);
        compute.SetBuffer(_addBondsKernel, CellCountID, _cellCount);
        compute.SetBuffer(_addBondsKernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(_addBondsKernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(_addBondsKernel, CellsID, _cells);
    }

    private void SetApplyBondForcesKernelBuffers()
    {
        compute.SetBuffer(_applyBondForcesKernel, BondCellsID, _bondCells);
        compute.SetBuffer(_applyBondForcesKernel, BondRestID, _bondRest);
        compute.SetBuffer(_applyBondForcesKernel, CellCountID, _cellCount);
        compute.SetBuffer(_applyBondForcesKernel, CellsID, _cells);
        compute.SetBuffer(_applyBondForcesKernel, ForceBufferID, _forceBuffer);
    }

    private void SetApplyCollisionForcesKernelBuffers()
    {
        compute.SetBuffer(_applyCollisionForcesKernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(_applyCollisionForcesKernel, BucketStartID, _bucketStart);
        compute.SetBuffer(_applyCollisionForcesKernel, CellCountID, _cellCount);
        compute.SetBuffer(_applyCollisionForcesKernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(_applyCollisionForcesKernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(_applyCollisionForcesKernel, CellsID, _cells);
        compute.SetBuffer(_applyCollisionForcesKernel, ForceBufferID, _forceBuffer);
    }

    private void SetBuildBucketKernelBuffers()
    {
        compute.SetBuffer(_buildBucketKernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(_buildBucketKernel, BucketStartID, _bucketStart);
        compute.SetBuffer(_buildBucketKernel, CellCountID, _cellCount);
        compute.SetBuffer(_buildBucketKernel, CellTableKeysID, _cellTableKeys);
    }

    private void SetBuildSpatialKernelBuffers()
    {
        compute.SetBuffer(_buildSpatialKernel, BucketCheckId, _bucketCheck);
        compute.SetBuffer(_buildSpatialKernel, CellCountID, _cellCount);
        compute.SetBuffer(_buildSpatialKernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(_buildSpatialKernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(_buildSpatialKernel, CellTableKeysID, _cellTableKeys);
        compute.SetBuffer(_buildSpatialKernel, CellsID, _cells);
    }

    private void SetClearBucketsKernelBuffers()
    {
        compute.SetBuffer(_clearBucketsKernel, BucketCheckId, _bucketCheck);
        compute.SetBuffer(_clearBucketsKernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(_clearBucketsKernel, BucketStartID, _bucketStart);
    }

    private void SetGrowKernelBuffers()
    {
        compute.SetBuffer(_growKernel, CellCountID, _cellCount);
        compute.SetBuffer(_growKernel, CellsID, _cells);
        compute.SetBuffer(_growKernel, VelocitiesID, _velocities);
    }

    private void SetResolveForceKernelBuffers()
    {
        compute.SetBuffer(_resolveForceBufferKernel, CellCountID, _cellCount);
        compute.SetBuffer(_resolveForceBufferKernel, CellsID, _cells);
        compute.SetBuffer(_resolveForceBufferKernel, ForceBufferID, _forceBuffer);
        compute.SetBuffer(_resolveForceBufferKernel, VelocitiesID, _velocities);
    }

    private void SetRaymarchBuffers()
    {
        raymarchMaterial.SetBuffer(BucketCheckId, _bucketCheck);
        raymarchMaterial.SetBuffer(BucketEndID, _bucketEnd);
        raymarchMaterial.SetBuffer(BucketStartID, _bucketStart);
        raymarchMaterial.SetBuffer(CellFullHashID, _cellFullHashes);
        raymarchMaterial.SetBuffer(CellIndicesID, _cellIndices);
        raymarchMaterial.SetBuffer(CellsID, _cells);
    }

    private void ReleaseBuffers()
    {
        _bondCells?.Release();
        _bondRest?.Release();
        _bucketCheck?.Release();
        _bucketEnd?.Release();
        _bucketStart?.Release();
        _cellComputeArgs?.Release();
        _cellCount?.Release();
        _cellFullHashes?.Release();
        _cellIndices?.Release();
        _cellRemap?.Release();
        _cellTableKeys?.Release();
        _cells?.Release();
        _cellsTemp?.Release();
        _compactCount?.Release();
        _forceBuffer?.Release();
        _velocities?.Release();
        _velocitiesTemp?.Release();
        _temp0?.Release();
        _temp1?.Release();
        _temp2?.Release();
        _temp3?.Release();
    }
    
    private void CompactCells()
    {
        // Reset compact counter
        _compactCount.SetData(new[] { 0u });
        
        // Pass 1: Mark alive cells and assign new indices
        compute.DispatchIndirect(_compactCellsKernel, _cellComputeArgs);
        
        // Pass 2: Write to temp buffer
        compute.DispatchIndirect(_writeCompactedKernel, _cellComputeArgs);
        
        // Bond Pass
        compute.DispatchIndirect(_remapBondsKernel, _cellComputeArgs);
        
        // Pass 3: Copy back
        compute.DispatchIndirect(_copyBackKernel, _cellComputeArgs);
        
        // Pass 4: Update cell_count
        compute.Dispatch(_finalizeCompactKernel, 1, 1, 1);
    }
}
