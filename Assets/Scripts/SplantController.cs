using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting.Runtime;

public class SplantController : MonoBehaviour
{
    private int _applyForcesKernel;
    private int _buildBucketKernel;
    private int _buildSpatialKernel;
    private int _clearBucketsKernel;
    private int _growKernel;
    private int _prepareIndirectArgsKernel;
    
    private static readonly int AirPressureStrengthID = Shader.PropertyToID("air_pressure_strength");
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
    private static readonly int CellTableKeysID = Shader.PropertyToID("cell_table_keys");
    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellsInID    = Shader.PropertyToID("cells_in");
    private static readonly int CellsOutID   = Shader.PropertyToID("cells_out");
    private static readonly int DeathColourID  = Shader.PropertyToID("death_colour");
    private static readonly int DeltaTimeID       = Shader.PropertyToID("delta_time");
    private static readonly int IndirectArgsId = Shader.PropertyToID("indirect_args");
    private static readonly int InvViewProjID = Shader.PropertyToID("inv_view_proj");
    private static readonly int MaxCellsID   = Shader.PropertyToID("max_cells");
    private static readonly int MaxPosID = Shader.PropertyToID("max_pos"); // replace with bounds max
    private static readonly int MinPosID = Shader.PropertyToID("min_pos"); // replace with bounds min 
    private static readonly int NeighborRadiusID = Shader.PropertyToID("neighbor_radius");
    private static readonly int NeighborRadiusSqID = Shader.PropertyToID("neighbor_radius_sq");
    private static readonly int NewColourID  = Shader.PropertyToID("new_colour");
    private static readonly int RepulsionStrengthID = Shader.PropertyToID("repulsion_strength");
    private static readonly int TimeID       = Shader.PropertyToID("time");

    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Material raymarchMaterial;
    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 10);
    [SerializeField] private uint maxCells = 100000;

    [SerializeField] private float size = 1f;
    [SerializeField] private float falloff = 20f;
    [ColorUsage(true, true)]
    [SerializeField] private Color newColour = Color.white;
    [ColorUsage(true, true)]
    [SerializeField] private Color deathColour = Color.green;
    
    [Header("Spatial Hashing")]
    [Tooltip("Must be >= neighborRadius, should be 1.5x or more")]
    [SerializeField] private float bucketSize = 0.1f;
    [Tooltip("Must be a power of 2")]
    [SerializeField] private int bucketTableSize = 1 << 18;
    
    [Header("Physics")] 
    [Tooltip("Must be <= bucketSize, should be 0.667x or more")]
    [SerializeField] private float neighborRadius = 0.1f;
    [SerializeField] private float airPressureStrength = 1f;
    [SerializeField] private float repulsionStrength = 1f;
    
    private ComputeBuffer _bucketEnd;
    private ComputeBuffer _bucketStart;
    private ComputeBuffer _cellComputeArgs;
    private ComputeBuffer _cellCount;
    private ComputeBuffer _cellFullHashes;
    private ComputeBuffer _cellIndices;
    private ComputeBuffer _cellTableKeys;
    private ComputeBuffer _cells;
    private ComputeBuffer _cellsA;
    private ComputeBuffer _cellsB;
    private ComputeBuffer _renderArgs;
    private ComputeBuffer _temp0;
    private ComputeBuffer _temp1;
    private ComputeBuffer _temp2;
    private ComputeBuffer _temp3;

    private bool _swap;
    private Camera _mainCamera;

    private DeviceRadixSort _deviceRadixSort;

    private void Awake()
    {
        _mainCamera = Camera.main;
        
        FindKernels();
        InitBuffers();
        InitRadixSort();
    }

    private void FindKernels()
    {
        _applyForcesKernel = compute.FindKernel("apply_forces");
        _buildBucketKernel = compute.FindKernel("build_bucket_ranges");
        _buildSpatialKernel = compute.FindKernel("build_spatial_hash");
        _clearBucketsKernel = compute.FindKernel("clear_buckets");
        _growKernel       = compute.FindKernel("grow");
        _prepareIndirectArgsKernel = compute.FindKernel("prepare_indirect_args");
    }

    private void InitBuffers()
    {
        var stride = Marshal.SizeOf<Cell>();   
        
        _bucketEnd = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketStart = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _cellComputeArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
        _cellCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        _cellFullHashes = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellIndices = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellTableKeys = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellsA = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellsB = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _renderArgs = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

        // Init args
        _cellComputeArgs.SetData(new uint[] { 1, 1, 1 });
        _renderArgs.SetData(new[] { 6, 0, 0, 0 });
        
        // Seed with one cell
        _cellsA.SetData(new[] {
            new Cell
            {
                age = 0,
                color = newColour,
                position = Vector3.zero
            }
        });
        _cellsA.SetCounterValue(1);
        _cellsB.SetCounterValue(0);
        _cellCount.SetData(new[] { 1 });
        
        // Start with cells A
        _cells = _cellsA;
    }

    private void InitRadixSort()
    {
        _deviceRadixSort = new DeviceRadixSort(
            compute: sortCompute,
            allocationSize: (int)maxCells,
            ref _temp0,
            ref _temp1,
            ref _temp2,
            ref _temp3
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
        _cells = DispatchGrowKernel();
    
        // Copy count from append buffer to cell_count buffer
        ComputeBuffer.CopyCount(_cells, _cellCount, 0);
    
        // Prepare indirect args
        compute.Dispatch(_prepareIndirectArgsKernel, 1, 1, 1);
    
        // Copy count for rendering args
        ComputeBuffer.CopyCount(_cells, _renderArgs, sizeof(uint));
    
        // Sort needs CPU count. TODO: modify to allow DispatchIndirect
        uint[] count = { 0 };
        _cellCount.GetData(count);
        var cellCount = (int)count[0];
        
        // Build spatial hash
        compute.SetBuffer(_buildSpatialKernel, CellsID, _cells);
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
        
        // Clear buckets
        var bucketGroups = Mathf.CeilToInt(bucketTableSize / 256f);
        compute.Dispatch(_clearBucketsKernel, bucketGroups, 1, 1);
        
        // Build bucket ranges
        compute.DispatchIndirect(_buildBucketKernel, _cellComputeArgs);
        
        // Physics
        compute.SetBuffer(_applyForcesKernel, CellsID, _cells);
        compute.DispatchIndirect(_applyForcesKernel, _cellComputeArgs);

        // Raymarch
        var view = _mainCamera.worldToCameraMatrix;
        var proj = GL.GetGPUProjectionMatrix(_mainCamera.projectionMatrix, false);
        var invViewProj = (proj * view).inverse;

        raymarchMaterial.SetMatrix(InvViewProjID, invViewProj);
        raymarchMaterial.SetVector(CameraPosID, _mainCamera.transform.position);
        raymarchMaterial.SetBuffer(CellsID, _cells);
        
        Graphics.DrawProcedural(
            raymarchMaterial,
            new Bounds(Vector3.zero, Vector3.one * 100000),
            MeshTopology.Triangles,
            3
        );
    }
    
    private void SetParams()
    {
        compute.SetFloat(AirPressureStrengthID, airPressureStrength);
        compute.SetFloat(BucketSizeID, bucketSize);
        compute.SetFloat(NeighborRadiusID, neighborRadius);
        compute.SetFloat(NeighborRadiusSqID, neighborRadius * neighborRadius);
        compute.SetFloat(RepulsionStrengthID, repulsionStrength);
        compute.SetInt(BucketTableSizeID, bucketTableSize);
        compute.SetInt(MaxCellsID, (int)maxCells);
        compute.SetVector(DeathColourID, deathColour);
        compute.SetVector(MaxPosID, bounds.max);
        compute.SetVector(MinPosID, bounds.min);
        compute.SetVector(NewColourID, newColour);
        
        raymarchMaterial.SetFloat(BucketSizeID, bucketSize);
        raymarchMaterial.SetFloat(NeighborRadiusID, neighborRadius);
        raymarchMaterial.SetFloat(NeighborRadiusSqID, neighborRadius * neighborRadius);
        raymarchMaterial.SetInt(BucketTableSizeID, bucketTableSize);
        raymarchMaterial.SetVector(BoundsMaxId, bounds.max);
        raymarchMaterial.SetVector(BoundsMinId, bounds.min);
    }

    private void SetComputeBuffers()
    {
        compute.SetBuffer(_prepareIndirectArgsKernel, IndirectArgsId, _cellComputeArgs);
        compute.SetBuffer(_prepareIndirectArgsKernel, CellCountID, _cellCount);

        SetCellKernalBuffers(_applyForcesKernel);
        SetCellKernalBuffers(_clearBucketsKernel);
        SetCellKernalBuffers(_buildBucketKernel);
        SetCellKernalBuffers(_buildSpatialKernel);
        SetCellKernalBuffers(_growKernel);
    }

    private void SetCellKernalBuffers(int kernel)
    {
        compute.SetBuffer(kernel, BucketEndID, _bucketEnd);
        compute.SetBuffer(kernel, BucketStartID, _bucketStart);
        compute.SetBuffer(kernel, CellCountID, _cellCount);
        compute.SetBuffer(kernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(kernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(kernel, CellTableKeysID, _cellTableKeys);
    }

    private void SetRaymarchBuffers()
    {
        raymarchMaterial.SetBuffer(BucketEndID, _bucketEnd);
        raymarchMaterial.SetBuffer(BucketStartID, _bucketStart);
        raymarchMaterial.SetBuffer(CellFullHashID, _cellFullHashes);
        raymarchMaterial.SetBuffer(CellIndicesID, _cellIndices);
    }

    private void ReleaseBuffers()
    {
        _renderArgs?.Release();
        _bucketEnd?.Release();
        _bucketStart?.Release();
        _cellCount?.Release();
        _cellFullHashes?.Release();
        _cellIndices?.Release();
        _cellTableKeys?.Release();
        _cellsA?.Release();
        _cellsB?.Release();
        _cellComputeArgs?.Release();
        _temp0?.Release();
        _temp1?.Release();
        _temp2?.Release();
        _temp3?.Release();
    }

    private ComputeBuffer DispatchGrowKernel()
    {
        var input  = _swap ? _cellsB : _cellsA;
        var output = _swap ? _cellsA : _cellsB;
        // Clear output buffer
        output.SetCounterValue(0);
        
        compute.SetBuffer(_growKernel, CellsInID, input);
        compute.SetBuffer(_growKernel, CellsOutID, output);
        compute.DispatchIndirect(_growKernel, _cellComputeArgs);
        
        _swap = !_swap;
        return output;
    }
}