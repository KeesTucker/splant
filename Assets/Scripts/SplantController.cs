using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using UnityEngine.Serialization;

public class SplantController : MonoBehaviour
{
    // { vertexCountPerInstance, instanceCount, startVertex, startInstance }
    private static readonly uint[] Args = { 6, 0, 0, 0 };

    private static readonly int CellsID = Shader.PropertyToID("cells");
    private static readonly int CellCounterID = Shader.PropertyToID("cell_counter");
    private static readonly int ColourID = Shader.PropertyToID("_Colour");
    private static readonly int FalloffID = Shader.PropertyToID("_Falloff");
    private static readonly int SizeID     = Shader.PropertyToID("_Size");
    private static readonly int CamRightID = Shader.PropertyToID("_CamRight");
    private static readonly int CamUpID    = Shader.PropertyToID("_CamUp");
    private static readonly int TimeID = Shader.PropertyToID("_Time");
    private static readonly int MaxCellsID = Shader.PropertyToID("_MaxCells");


    [SerializeField] private ComputeShader compute;
    [SerializeField] private Material material;
    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 1000);
    [SerializeField] private uint maxCells = 100000;
    [SerializeField] private float size = 1f;
    [SerializeField] private float falloff = 20f;
    [ColorUsage(true, true)]
    [SerializeField] private Color colour = Color.white;
    
    private ComputeBuffer _cellBuffer;
    private ComputeBuffer _counterBuffer;
    private ComputeBuffer _argsBuffer;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        
        _cellBuffer = new ComputeBuffer(
            (int)maxCells,
            Marshal.SizeOf<Cell>(),
            ComputeBufferType.Append
        );
        
        _counterBuffer = new ComputeBuffer(
            1,
            sizeof(uint), 
            ComputeBufferType.Raw
        );

        _argsBuffer = new ComputeBuffer(
            1,
            Args.Length * sizeof(uint),
            ComputeBufferType.IndirectArguments
        );
    }

    private void OnDestroy()
    {
        _cellBuffer?.Release();
        _counterBuffer?.Release();
        _argsBuffer?.Release();
    }

    
    private void Start()
    {
        _cellBuffer.SetCounterValue(0);
        uint[] counterReset = { 0 };
        _counterBuffer.SetData(counterReset);
        _argsBuffer.SetData(Args);
        
        compute.SetBuffer(0, CellsID, _cellBuffer);
        compute.SetBuffer(0, CellCounterID, _counterBuffer);
        material.SetBuffer(CellsID, _cellBuffer);
    }

    private void Update()
    {
        material.SetColor(ColourID, colour);
        material.SetFloat(SizeID, size);
        material.SetFloat(FalloffID, falloff);
        material.SetVector(CamRightID, _mainCamera.transform.right);
        material.SetVector(CamUpID, _mainCamera.transform.up);
        
        compute.SetFloat(TimeID, Time.time);
        compute.SetInt(MaxCellsID, (int)maxCells);
        compute.Dispatch(0, 1, 1, 1);
        
        ComputeBuffer.CopyCount(_cellBuffer, _argsBuffer, sizeof(uint));
        
        Graphics.DrawProceduralIndirect(
            material,
            bounds,
            MeshTopology.Triangles,
            _argsBuffer
        );
    }
}
