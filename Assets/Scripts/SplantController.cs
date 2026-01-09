using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting;
using GPUSorting.Runtime;

public class SplantController : MonoBehaviour
{
    private const int ThreadsPerGroup = 1024;

    // { vertexCountPerInstance, instanceCount, startVertex, startInstance }
    private static readonly uint[] Args = { 6, 0, 0, 0 };

    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellsInID    = Shader.PropertyToID("cells_in");
    private static readonly int CellsOutID   = Shader.PropertyToID("cells_out");
    private static readonly int CellCountID   = Shader.PropertyToID("cell_count");
    private static readonly int TimeID       = Shader.PropertyToID("_Time");
    private static readonly int DeltaTimeID       = Shader.PropertyToID("_DeltaTime");
    private static readonly int MaxCellsID   = Shader.PropertyToID("_MaxCells");

    private static readonly int NewColourID  = Shader.PropertyToID("_NewColour");
    private static readonly int DeathColourID  = Shader.PropertyToID("_DeathColour");
    private static readonly int FalloffID = Shader.PropertyToID("_Falloff");
    private static readonly int SizeID    = Shader.PropertyToID("_Size");
    private static readonly int CameraPosID = Shader.PropertyToID("_CameraPos");
    private static readonly int CamRightID = Shader.PropertyToID("_CamRight");
    private static readonly int CamUpID    = Shader.PropertyToID("_CamUp");
    private static readonly int CameraForwardID = Shader.PropertyToID("_CameraForward");


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

    private ComputeBuffer _cellsA;
    private ComputeBuffer _cellsB;
    private ComputeBuffer _cellCountBuffer;
    private ComputeBuffer _argsBuffer;
    
    ComputeBuffer _depthKeys;    // float
    ComputeBuffer _indices;      // uint
    
    ComputeBuffer temp0;
    ComputeBuffer temp1;
    ComputeBuffer temp2;
    ComputeBuffer temp3;

    private bool _swap;
    private Camera _mainCamera;
    
    DeviceRadixSort _deviceRadixSort;

    private void Awake()
    {
        _mainCamera = Camera.main;

        var stride = Marshal.SizeOf<Cell>();

        _cellsA = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellsB = new ComputeBuffer((int)maxCells, stride, ComputeBufferType.Append);
        _cellCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        _argsBuffer = new ComputeBuffer(1, Args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        _depthKeys = new ComputeBuffer((int)maxCells, sizeof(float));
        _indices   = new ComputeBuffer((int)maxCells, sizeof(int));

        _deviceRadixSort = new DeviceRadixSort(
            compute: sortCompute,
            allocationSize: (int)maxCells,
            ref temp0,
            ref temp1,
            ref temp2,
            ref temp3
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

        _argsBuffer.SetData(Args);
        
        _cellCountBuffer.SetData(new[] { 1 });
        compute.SetBuffer(0, CellCountID, _cellCountBuffer);
        compute.SetBuffer(1, CellCountID, _cellCountBuffer);
        compute.SetInt(MaxCellsID, (int)maxCells);
    }

    private void OnDestroy()
    {
        _cellsA?.Release();
        _cellsB?.Release();
        _cellCountBuffer.Release();
        _argsBuffer?.Release();
        _depthKeys?.Release();
        _indices?.Release();
        temp0?.Release();
        temp1?.Release();
        temp2?.Release();
        temp3?.Release();
    }

    private void Update()
    {
        var input  = _swap ? _cellsB : _cellsA;
        var output = _swap ? _cellsA : _cellsB;

        output.SetCounterValue(0);

        compute.SetBuffer(0, CellsInID, input);
        compute.SetBuffer(0, CellsOutID, output);
        compute.SetFloat(TimeID, Time.time);
        compute.SetFloat(DeltaTimeID, Time.deltaTime);
        compute.SetVector(NewColourID, newColour);
        compute.SetVector(DeathColourID, deathColour);
        
        uint[] count = { 0 };
        _cellCountBuffer.GetData(count);
        
        var groups = Mathf.CeilToInt(count[0] / (float)ThreadsPerGroup);
        compute.Dispatch(0, Mathf.Max(groups, 1), 1, 1);
        
        // Copy append count for rendering
        ComputeBuffer.CopyCount(output, _argsBuffer, sizeof(uint));
        // Copy append count into a readable buffer
        ComputeBuffer.CopyCount(output, _cellCountBuffer, 0);
        
        _cellCountBuffer.GetData(count);
        var sortSize = (int)count[0];
        
        compute.SetBuffer(1, CellsID, output);
        compute.SetBuffer(1, "depthKeys", _depthKeys);
        compute.SetBuffer(1, "indices", _indices);

        compute.SetVector(CameraPosID, _mainCamera.transform.position);
        compute.SetVector(CameraForwardID, _mainCamera.transform.forward);

        int depthGroups = Mathf.CeilToInt(sortSize / 256f);
        compute.Dispatch(1, depthGroups, 1, 1);

        System.Type typeOfKey = typeof(float);
        System.Type typeOfPayload = typeof(uint);
        
        _deviceRadixSort.Sort(
            sortSize,
            _depthKeys,
            _indices,
            temp0,
            temp1,
            temp2,
            temp3,
            typeOfKey,
            typeOfPayload,
            false // back-to-front
        );

        
        // Material setup
        material.SetBuffer(CellsID, output);
        material.SetBuffer("_SortedIndices", _indices);
        material.SetFloat(SizeID, size);
        material.SetFloat(FalloffID, falloff);
        material.SetVector(CamRightID, _mainCamera.transform.right);
        material.SetVector(CamUpID, _mainCamera.transform.up);

        Graphics.DrawProceduralIndirect(
            material,
            bounds,
            MeshTopology.Triangles,
            _argsBuffer
        );

        _swap = !_swap;
    }
}
