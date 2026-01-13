using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting.Runtime;
using UnityEngine.Serialization;

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
    private int _writeCompactedKernel;
    
    private static readonly int BondBendStiffnessID       = Shader.PropertyToID("bond_bend_stiffness");
    private static readonly int BondLinearStiffnessID = Shader.PropertyToID("bond_linear_stiffness");
    private static readonly int BondRangeSqID = Shader.PropertyToID("bond_range_sq");
    private static readonly int BondsID = Shader.PropertyToID("bonds");
    private static readonly int BoundsMaxId = Shader.PropertyToID("bounds_max");
    private static readonly int BoundsMinId = Shader.PropertyToID("bounds_min");
    private static readonly int BucketEndID = Shader.PropertyToID("bucket_end");
    private static readonly int BucketSizeID = Shader.PropertyToID("bucket_size");
    private static readonly int BucketStartID = Shader.PropertyToID("bucket_start");
    private static readonly int BucketTableSizeID = Shader.PropertyToID("bucket_table_size");
    private static readonly int CameraPosID = Shader.PropertyToID("camera_pos");
    private static readonly int CellCountID   = Shader.PropertyToID("cell_count");
    private static readonly int CellFullHashID = Shader.PropertyToID("cell_full_hash");
    private static readonly int CellIndicesID = Shader.PropertyToID("cell_indices");
    private static readonly int CellRemapID = Shader.PropertyToID("cell_remap");
    private static readonly int CellTableKeysID = Shader.PropertyToID("cell_table_keys");
    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellsTempID = Shader.PropertyToID("cells_temp");
    private static readonly int CompactCountID = Shader.PropertyToID("compact_count");
    private static readonly int DeltaTimeID = Shader.PropertyToID("delta_time");
    private static readonly int IndirectArgsId = Shader.PropertyToID("indirect_args");
    private static readonly int InvViewProjID = Shader.PropertyToID("inv_view_proj");
    private static readonly int MaxBondsId = Shader.PropertyToID("max_bonds");
    private static readonly int MaxBondsPerCellID = Shader.PropertyToID("max_bonds_per_cell");
    private static readonly int MaxCellsID   = Shader.PropertyToID("max_cells");
    private static readonly int MaxPosID = Shader.PropertyToID("max_pos"); // replace with bounds max
    private static readonly int MinPosID = Shader.PropertyToID("min_pos"); // replace with bounds min 
    private static readonly int NeighborRadiusID = Shader.PropertyToID("neighbor_radius");
    private static readonly int NeighborRadiusSqID = Shader.PropertyToID("neighbor_radius_sq");
    private static readonly int RepulsionStrengthID = Shader.PropertyToID("repulsion_strength");
    private static readonly int TimeID       = Shader.PropertyToID("time");

    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Material raymarchMaterial;
    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 10);
    [SerializeField] private uint maxCells = 100000;
    
    [Header("Compaction")]
    [SerializeField] private int compactEveryNFrames = 60;
    
    [Header("Spatial Hashing")]
    [Tooltip("Must be >= neighborRadius, should be 1.5x or more")]
    [SerializeField] private float bucketSize = 0.1f;
    [Tooltip("Must be a power of 2")]
    [SerializeField] private int bucketTableSize = 1 << 18;
    
    [Header("Physics")] 
    [Tooltip("Must be <= bucketSize, should be 0.667x or more")]
    [SerializeField] private float neighborCollisionRadius = 0.5f;
    [SerializeField] private float repulsionStrength = 1f;
    
    [Header("Bonds")] 
    [SerializeField] private float bondRange = 0.05f;
    [SerializeField] private float bondLinearStiffness = 1f;
    [SerializeField] private float bondBendStiffness = 1f;
    [SerializeField] private uint maxBondsPerCell = 6;
    
    private ComputeBuffer _bonds;
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
    private ComputeBuffer _temp0;
    private ComputeBuffer _temp1;
    private ComputeBuffer _temp2;
    private ComputeBuffer _temp3;

    private Camera _mainCamera;
    private DeviceRadixSort _deviceRadixSort;
    private int _frameCount;

    private void Awake()
    {
        _mainCamera = Camera.main;
        
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
        _writeCompactedKernel = compute.FindKernel("write_compacted");
    }

    private void InitBuffers()
    {
        var cellStride = Marshal.SizeOf<Cell>();
        var bondStride = Marshal.SizeOf<Bond>();  
        
        _bonds = new ComputeBuffer((int)maxCells * (int)maxBondsPerCell, bondStride); // this is probs overkill but can reduce with testing
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

        // Init
        _cellComputeArgs.SetData(new uint[] { 1, 1, 1 });
        _cellCount.SetData(new[] { 1u });
        _compactCount.SetData(new[] { 0u });
        
        // Seed with one cell
        _cells.SetData(new[] {
            new Cell
            {
                age = 0,
                position = Vector3.zero,
                polarity = Vector3.up
            }
        });
        
        // Init entire bond buffer to invalid
        compute.SetBuffer(_initBondsKernel, BondsID, _bonds);
        compute.DispatchIndirect(_initBondsKernel, _cellComputeArgs);
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
        // enforce neighborRadius constraints
        neighborCollisionRadius = Mathf.Clamp(neighborCollisionRadius, 0, bucketSize * 0.667f);
        // bondRange has the same constraints as neighborRadius due to spatial lookups
        bondRange = Mathf.Clamp(bondRange, 0, bucketSize * 0.667f);
        
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
    
        // Grow cells
        compute.DispatchIndirect(_growKernel, _cellComputeArgs);
        
        // Compact every N frames to remove dead cells
        _frameCount++;
        if (_frameCount >= compactEveryNFrames)
        {
            _frameCount = 0;
            CompactCells();
        }
        
        // Prepare indirect args
        compute.Dispatch(_prepareIndirectArgsKernel, 1, 1, 1);
    
        // Sort needs CPU count. TODO: modify to allow DispatchIndirect
        uint[] count = { 0 };
        _cellCount.GetData(count);
        var cellCount = (int)count[0];
        
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
        
        // Clear and build buckets
        var bucketGroups = Mathf.CeilToInt(bucketTableSize / 256f);
        compute.Dispatch(_clearBucketsKernel, bucketGroups, 1, 1);
        
        // Build bucket ranges
        compute.DispatchIndirect(_buildBucketKernel, _cellComputeArgs);
        
        // Bonds
        //compute.DispatchIndirect(_addBondsKernel, _cellComputeArgs);
        
        // Physics
        compute.DispatchIndirect(_applyCollisionForcesKernel, _cellComputeArgs);
        //compute.DispatchIndirect(_applyBondForcesKernel, _cellComputeArgs);

        // Rendering
        var view = _mainCamera.worldToCameraMatrix;
        var proj = GL.GetGPUProjectionMatrix(_mainCamera.projectionMatrix, false);
        var invViewProj = (proj * view).inverse;

        raymarchMaterial.SetMatrix(InvViewProjID, invViewProj);
        raymarchMaterial.SetVector(CameraPosID, _mainCamera.transform.position);
        
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
        raymarchMaterial.SetFloat(NeighborRadiusID, neighborCollisionRadius);
        raymarchMaterial.SetFloat(NeighborRadiusSqID, neighborCollisionRadius * neighborCollisionRadius);
        raymarchMaterial.SetInt(BucketTableSizeID, bucketTableSize);
        raymarchMaterial.SetVector(BoundsMaxId, bounds.max);
        raymarchMaterial.SetVector(BoundsMinId, bounds.min);
    }
    
    private void SetComputeBuffers()
    {
        compute.SetBuffer(_prepareIndirectArgsKernel, IndirectArgsId, _cellComputeArgs);
        compute.SetBuffer(_prepareIndirectArgsKernel, CellCountID, _cellCount);

        SetCellKernalBuffers(_addBondsKernel);
        SetCellKernalBuffers(_applyBondForcesKernel);
        SetCellKernalBuffers(_applyCollisionForcesKernel);
        SetCellKernalBuffers(_buildBucketKernel);
        SetCellKernalBuffers(_buildSpatialKernel);
        SetCellKernalBuffers(_clearBucketsKernel);
        SetCellKernalBuffers(_growKernel);
        
        // Compaction kernels
        compute.SetBuffer(_compactCellsKernel, CellCountID, _cellCount);
        compute.SetBuffer(_compactCellsKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_compactCellsKernel, CellsID, _cells);
        compute.SetBuffer(_compactCellsKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_writeCompactedKernel, CellCountID, _cellCount);
        compute.SetBuffer(_writeCompactedKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_writeCompactedKernel, CellsID, _cells);
        compute.SetBuffer(_writeCompactedKernel, CellsTempID, _cellsTemp);
        
        compute.SetBuffer(_remapBondsKernel, BondsID, _bonds);
        compute.SetBuffer(_remapBondsKernel, CellCountID, _cellCount);
        compute.SetBuffer(_remapBondsKernel, CellRemapID, _cellRemap);
        compute.SetBuffer(_remapBondsKernel, CellsID, _cells);
        compute.SetBuffer(_remapBondsKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_copyBackKernel, CellsID, _cells);
        compute.SetBuffer(_copyBackKernel, CellsTempID, _cellsTemp);
        compute.SetBuffer(_copyBackKernel, CompactCountID, _compactCount);
        
        compute.SetBuffer(_finalizeCompactKernel, CellCountID, _cellCount);
        compute.SetBuffer(_finalizeCompactKernel, CompactCountID, _compactCount);
    }
    
    private void SetCellKernalBuffers(int kernel)
    {
        compute.SetBuffer(kernel, BondsID, _bonds);
        compute.SetBuffer(kernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(kernel, BucketStartID, _bucketStart);
        compute.SetBuffer(kernel, CellCountID, _cellCount);
        compute.SetBuffer(kernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(kernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(kernel, CellTableKeysID, _cellTableKeys);
        compute.SetBuffer(kernel, CellsID, _cells);
    }

    private void SetRaymarchBuffers()
    {
        raymarchMaterial.SetBuffer(BucketEndID, _bucketEnd);
        raymarchMaterial.SetBuffer(BucketStartID, _bucketStart);
        raymarchMaterial.SetBuffer(CellFullHashID, _cellFullHashes);
        raymarchMaterial.SetBuffer(CellIndicesID, _cellIndices);
        raymarchMaterial.SetBuffer(CellsID, _cells);
    }

    private void ReleaseBuffers()
    {
        _bonds?.Release();
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