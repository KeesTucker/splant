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
    private static readonly int CellHashesID = Shader.PropertyToID("cell_hashes");
    private static readonly int CellIndicesID = Shader.PropertyToID("cell_indices");
    private static readonly int BucketSizeID = Shader.PropertyToID("bucket_size");


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
    [SerializeField] private float bucketSize = 0.1f;

    private ComputeBuffer _cellsA;
    private ComputeBuffer _cellsB;
    private ComputeBuffer _cellCount;
    private ComputeBuffer _args;

    private ComputeBuffer _depthKeys;
    private ComputeBuffer _depthIndices;
    
    private ComputeBuffer _cellHashes;
    private ComputeBuffer _cellIndices;

    private ComputeBuffer _temp0;
    private ComputeBuffer _temp1;
    private ComputeBuffer _temp2;
    private ComputeBuffer _temp3;

    private bool _swap;
    private Camera _mainCamera;

    private DeviceRadixSort _deviceRadixSort;

    private void Awake()
    {
        _growKernel       = compute.FindKernel("grow");
        _buildDepthKernel = compute.FindKernel("build_depth_keys");
        _buildSpatialKernel = compute.FindKernel("build_spatial_hash");
        
        _mainCamera = Camera.main;

        var stride = Marshal.SizeOf<Cell>();

        _cellsA = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellsB = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        _args = new ComputeBuffer(1, Args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        _depthKeys = new ComputeBuffer((int)maxCells, sizeof(float));
        _depthIndices = new ComputeBuffer((int)maxCells, sizeof(uint));
        
        _cellHashes = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellIndices = new ComputeBuffer((int)maxCells, sizeof(uint));

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
        // Seed with one cell
        _cellsA.SetCounterValue(0);
        _cellsB.SetCounterValue(0);

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
    }

    private void OnDestroy()
    {
        _args?.Release();
        _cellCount.Release();
        _cellHashes?.Release();
        _cellIndices.Release();
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
        compute.SetFloat(TimeID, Time.time);
        compute.SetFloat(DeltaTimeID, Time.deltaTime);
        compute.SetVector(NewColourID, newColour);
        compute.SetVector(DeathColourID, deathColour);
        
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
        
        compute.SetBuffer(_buildDepthKernel, CellsID, output);
        compute.SetBuffer(_buildDepthKernel, DepthKeysID, _depthKeys);
        compute.SetBuffer(_buildDepthKernel, DepthIndicesID, _depthIndices);

        compute.SetVector(CameraPosID, _mainCamera.transform.position);
        compute.SetVector(CameraForwardID, _mainCamera.transform.forward);

        threadGroups = Mathf.CeilToInt(cellCount / ThreadsPerGroup);
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
        compute.SetBuffer(_buildSpatialKernel, CellsID, output);
        compute.SetBuffer(_buildSpatialKernel, CellHashesID, _cellHashes);
        compute.SetBuffer(_buildSpatialKernel, CellIndicesID, _cellIndices);
        
        compute.SetFloat(BucketSizeID, bucketSize);
        
        compute.Dispatch(_buildSpatialKernel, threadGroups, 1, 1);

        typeOfKey = typeof(uint);
        typeOfPayload = typeof(uint);
        
        // Sort by hash
        _deviceRadixSort.Sort(
            cellCount,
            _cellHashes,
            _cellIndices,
            _temp0,
            _temp1,
            _temp2,
            _temp3,
            typeOfKey,
            typeOfPayload,
            true
        );
        
        // TODO: generate bucket ranges 
        
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
