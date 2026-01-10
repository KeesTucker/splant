using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting.Runtime;

public class SplantController : MonoBehaviour
{
    private const float ThreadsPerGroup = 256f;

    // { vertexCountPerInstance, instanceCount, startVertex, startInstance }
    private static readonly uint[] Args = { 6, 0, 0, 0 };

    private int _growKernel;
    private int _buildDepthKernel;
    private int _buildSpatialKernel;
    private int _buildBucketKernel;
    private int _applyForcesKernel;
    
    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellsInID    = Shader.PropertyToID("cells_in");
    private static readonly int CellsOutID   = Shader.PropertyToID("cells_out");
    private static readonly int CellCountID   = Shader.PropertyToID("cell_count");
    private static readonly int TimeID       = Shader.PropertyToID("time");
    private static readonly int DeltaTimeID       = Shader.PropertyToID("delta_time");
    private static readonly int MaxCellsID   = Shader.PropertyToID("max_cells");
    private static readonly int NewColourID  = Shader.PropertyToID("new_colour");
    private static readonly int DeathColourID  = Shader.PropertyToID("death_colour");
    private static readonly int FalloffID = Shader.PropertyToID("falloff");
    private static readonly int SizeID    = Shader.PropertyToID("size");
    private static readonly int CameraPosID = Shader.PropertyToID("camera_pos");
    private static readonly int CamRightID = Shader.PropertyToID("cam_right");
    private static readonly int CamUpID    = Shader.PropertyToID("cam_up");
    private static readonly int CameraForwardID = Shader.PropertyToID("camera_forward");
    private static readonly int DepthKeysID = Shader.PropertyToID("depth_keys");
    private static readonly int DepthIndicesID = Shader.PropertyToID("depth_indices");
    private static readonly int SortedIndicesID = Shader.PropertyToID("sorted_indices");
    private static readonly int CellTableKeysID = Shader.PropertyToID("cell_table_keys");
    private static readonly int CellFullHashID = Shader.PropertyToID("cell_full_hash");
    private static readonly int CellIndicesID = Shader.PropertyToID("cell_indices");
    private static readonly int BucketSizeID = Shader.PropertyToID("bucket_size");
    private static readonly int BucketStartID = Shader.PropertyToID("bucket_start");
    private static readonly int BucketCountID = Shader.PropertyToID("bucket_count");
    private static readonly int BucketTableSizeID = Shader.PropertyToID("bucket_table_size");
    private static readonly int AirPressureStrengthID = Shader.PropertyToID("air_pressure_strength");
    private static readonly int NeighborRadiusID = Shader.PropertyToID("neighbor_radius");
    private static readonly int NeighborRadiusSqID = Shader.PropertyToID("neighbor_radius_sq");
    private static readonly int RepulsionStrengthID = Shader.PropertyToID("repulsion_strength");
    private static readonly int MinPosID = Shader.PropertyToID("min_pos");
    private static readonly int MaxPosID = Shader.PropertyToID("max_pos");


    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Material material;
    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 1000);
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
    
    private ComputeBuffer _cellsA;
    private ComputeBuffer _cellsB;
    private ComputeBuffer _cellCount;
    private ComputeBuffer _args;

    private ComputeBuffer _depthKeys;
    private ComputeBuffer _depthIndices;
    
    private ComputeBuffer _cellTableKeys;
    private ComputeBuffer _cellFullHashes;
    private ComputeBuffer _cellIndices;

    private ComputeBuffer _bucketStart;
    private ComputeBuffer _bucketCount;
    
    private ComputeBuffer _temp0;
    private ComputeBuffer _temp1;
    private ComputeBuffer _temp2;
    private ComputeBuffer _temp3;

    private bool _swap;
    private Camera _mainCamera;

    private DeviceRadixSort _deviceRadixSort;

    private void Awake()
    {
        _applyForcesKernel = compute.FindKernel("apply_forces");
        _buildBucketKernel = compute.FindKernel("build_bucket_ranges");
        _buildDepthKernel = compute.FindKernel("build_depth_keys");
        _buildSpatialKernel = compute.FindKernel("build_spatial_hash");
        _growKernel       = compute.FindKernel("grow");
        
        var stride = Marshal.SizeOf<Cell>();   
        
        _args = new ComputeBuffer(1, Args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _bucketCount = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketStart = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _cellCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        _cellFullHashes = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellIndices = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellTableKeys = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellsA = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellsB = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _depthIndices = new ComputeBuffer((int)maxCells, sizeof(uint));
        _depthKeys = new ComputeBuffer((int)maxCells, sizeof(float));
        
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
        _mainCamera = Camera.main;
        
        _cellsA.SetCounterValue(0);
        _cellsB.SetCounterValue(0);

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

        _args.SetData(Args);
        _cellCount.SetData(new[] { 1 });
        
        compute.SetBuffer(0, CellCountID, _cellCount);
        compute.SetBuffer(1, CellCountID, _cellCount);
        compute.SetInt(MaxCellsID, (int)maxCells);
        compute.SetInt(BucketTableSizeID, bucketTableSize);
    }

    private void OnDestroy()
    {
        _args?.Release();
        _bucketCount.Release();
        _bucketStart.Release();
        _cellCount.Release();
        _cellFullHashes?.Release();
        _cellIndices.Release();
        _cellTableKeys?.Release();
        _cellsA?.Release();
        _cellsB?.Release();
        _depthIndices?.Release();
        _depthKeys?.Release();
        _temp0?.Release();
        _temp1?.Release();
        _temp2?.Release();
        _temp3?.Release();
    }

    private void Update()
    {
        var input  = _swap ? _cellsB : _cellsA;
        var output = _swap ? _cellsA : _cellsB;

        output.SetCounterValue(0);

        compute.SetBuffer(_growKernel, CellsInID, input);
        compute.SetBuffer(_growKernel, CellsOutID, output);
        compute.SetFloat(DeltaTimeID, Time.deltaTime);
        compute.SetFloat(TimeID, Time.time);
        compute.SetVector(DeathColourID, deathColour);
        compute.SetVector(NewColourID, newColour);
        
        // Get cell count so we can dispatch an appropriate amount of thread groups.
        uint[] count = { 0 };
        _cellCount.GetData(count);
        var cellCount = (int)count[0];
        
        var threadGroups = Mathf.CeilToInt(cellCount / ThreadsPerGroup);
        compute.Dispatch(_growKernel, Mathf.Max(threadGroups, 1), 1, 1);
        
        // Copy append count for rendering
        ComputeBuffer.CopyCount(output, _args, sizeof(uint));
        // Copy append count into a readable buffer
        ComputeBuffer.CopyCount(output, _cellCount, 0);
        
        // Grab count again cause we may have had cells divide in the grow kernel
        _cellCount.GetData(count); 
        cellCount = (int)count[0];
        threadGroups = Mathf.CeilToInt(cellCount / ThreadsPerGroup);
        
        // Depth sorting for splatting
        compute.SetBuffer(_buildDepthKernel, CellsID, output);
        compute.SetBuffer(_buildDepthKernel, DepthIndicesID, _depthIndices);
        compute.SetBuffer(_buildDepthKernel, DepthKeysID, _depthKeys);
        compute.SetVector(CameraForwardID, _mainCamera.transform.forward);
        compute.SetVector(CameraPosID, _mainCamera.transform.position);
        
        compute.Dispatch(_buildDepthKernel, threadGroups, 1, 1);

        var typeOfKey = typeof(float);
        var typeOfPayload = typeof(uint);
        
        _deviceRadixSort.Sort(
            cellCount,
            _depthKeys,
            _depthIndices,
            _temp0,
            _temp1,
            _temp2,
            _temp3,
            typeOfKey,
            typeOfPayload,
            false // back-to-front
        );

        // Spatial Hashing
        compute.SetBuffer(_buildSpatialKernel, CellCountID, _cellCount);
        compute.SetBuffer(_buildSpatialKernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(_buildSpatialKernel, CellFullHashID, _cellFullHashes);
        compute.SetBuffer(_buildSpatialKernel, CellTableKeysID, _cellTableKeys);
        compute.SetBuffer(_buildSpatialKernel, CellsID, output);
        compute.SetFloat(BucketSizeID, bucketSize);
        
        compute.Dispatch(_buildSpatialKernel, threadGroups, 1, 1);

        typeOfKey = typeof(uint);
        typeOfPayload = typeof(uint);
        
        // Sort by hash
        _deviceRadixSort.Sort(
            cellCount,
            _cellTableKeys,
            _cellIndices,
            _temp0,
            _temp1,
            _temp2,
            _temp3,
            typeOfKey,
            typeOfPayload,
            true
        );
        
        // Generate bucket ranges
        var starts = new uint[bucketTableSize];
        for (var i = 0; i < starts.Length; i++)
            starts[i] = 0xffffffff;
        _bucketStart.SetData(starts);
        var zeros = new uint[bucketTableSize];
        _bucketCount.SetData(zeros);

        compute.SetBuffer(_buildBucketKernel, CellCountID, _cellCount);
        compute.SetBuffer(_buildBucketKernel, CellTableKeysID, _cellTableKeys);
        compute.SetBuffer(_buildBucketKernel, BucketStartID, _bucketStart);
        compute.SetBuffer(_buildBucketKernel, BucketCountID, _bucketCount);
        
        compute.Dispatch(_buildBucketKernel, threadGroups, 1, 1);
        
        // Physics
        compute.SetFloat(AirPressureStrengthID, airPressureStrength);
        compute.SetVector(MinPosID,bounds.min);
        compute.SetVector(MaxPosID,bounds.max);
        compute.SetBuffer(_applyForcesKernel, CellsID, output);
        compute.SetBuffer(_applyForcesKernel, CellCountID, _cellCount);
        compute.SetBuffer(_applyForcesKernel, CellIndicesID, _cellIndices);
        compute.SetBuffer(_applyForcesKernel, BucketStartID, _bucketStart);
        compute.SetBuffer(_applyForcesKernel, BucketCountID, _bucketCount);
        compute.SetBuffer(_applyForcesKernel, CellFullHashID, _cellFullHashes);
        
        compute.SetFloat(NeighborRadiusID, neighborRadius);
        compute.SetFloat(NeighborRadiusSqID, neighborRadius * neighborRadius);
        compute.SetFloat(RepulsionStrengthID, repulsionStrength);
        
        compute.Dispatch(_applyForcesKernel, threadGroups, 1, 1);
        
        // Material setup
        material.SetBuffer(CellsID, output);
        material.SetBuffer(SortedIndicesID, _depthIndices);
        material.SetFloat(SizeID, size);
        material.SetFloat(FalloffID, falloff);
        material.SetVector(CamRightID, _mainCamera.transform.right);
        material.SetVector(CamUpID, _mainCamera.transform.up);

        Graphics.DrawProceduralIndirect(
            material,
            bounds,
            MeshTopology.Triangles,
            _args
        );

        _swap = !_swap;
    }
}
