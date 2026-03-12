using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using GPUSorting.Runtime;

public class SplantController : MonoBehaviour
{
    private SplantKernels _k;
    private SplantBuffers _buf;
    private SdfLayout     _sdfLayout;

    [SerializeField] private Bounds        bounds      = new(Vector3.zero, Vector3.one * 6);
    [SerializeField] private Camera        mainCamera;
    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Transform     mainLight;
    [SerializeField] private Material      raymarchMaterial;
    [SerializeField] private uint          maxCells            = 30000;
    [SerializeField] private int           compactEveryNFrames;

    [SerializeField] private GrowthParams     growth     = new() { mode = GrowthMode.Blob, splitsPerCell = 3, splitAge = 0.5f, splitAgeJitter = 0.25f };
    [SerializeField] private SpatialHashParams spatialHash = new() { bucketSize = 0.2f, bucketTableSize = 1 << 18 };
    [SerializeField] private PhysicsParams    physics    = new() { neighborRadius = 0.1f, repulsionStrength = 3f, damping = 3f, gravity = new(0f, 0f, 0f) };
    [SerializeField] private WindParams       wind       = new() { direction = new(1f, 0f, 0f), strength = 0.1f, frequency = 0.5f, speed = 0.2f, variation = 2.6f };
    [SerializeField] private BondParams       bonds      = new() { range = 0.1f, linearStiffness = 10f, bendStiffness = 1f, maxBondsPerCell = 1 };
    [SerializeField] private RenderingParams  rendering  = new() { maxSteps = 200, cellRadius = 0.1f, minStepSize = 0.02f, maxStepSize = 0.0667f, surfaceThreshold = 0.037f, fresnelPow = 2.3f, fresnelStrength = 0.1f, baseColour = new(0f, 0.019607844f, 0.001600317f, 1f) };
    [SerializeField] private SssParams        sss        = new() { color = new(0.48707294f, 0.5647059f, 0f, 1f), strength = 0.523f, falloff = 0.37f, steps = 50, stepSize = 1f };
    [SerializeField] private SdfBrickParams   sdfBricks  = new() { maxDistance = 0.1f, maxBricks = 32768, samplesPerBucket = 8 };

    private DeviceRadixSort _deviceRadixSort;
    private int             _frameCount;

    private const int SdfBrickSize = 8;

    private static readonly uint[]   ZeroUint  = { 0u };
    private static readonly uint[]   CountBuf  = new uint[1];

    private void Awake()
    {
        _k = SplantKernels.Find(compute);
        InitBuffers();
        InitSdfBricks();
        _deviceRadixSort = new DeviceRadixSort(
            compute: sortCompute,
            allocationSize: (int)maxCells,
            ref _buf.Temp0, ref _buf.Temp1, ref _buf.Temp2, ref _buf.Temp3
        );
    }

    private void InitBuffers()
    {
        if (_buf.IsAllocated) _buf.Release(false);

        var maxBonds = (int)maxCells * (int)bonds.maxBondsPerCell;
        _buf.Alloc((int)maxCells, maxBonds, spatialHash.bucketTableSize, Marshal.SizeOf<Cell>());

        // Seed with two cells
        _buf.Cells.SetData(new[] {
            new Cell { age = 0, position = Vector3.zero,    polarity = Vector3.up },
            new Cell { age = 0, position = Vector3.one * 2, polarity = Vector3.up }
        });

        // Init entire bond buffer to invalid
        compute.SetInt(ShaderID.MaxBonds, maxBonds);
        compute.SetBuffer(_k.InitBonds, ShaderID.BondCells,    _buf.BondCells);
        compute.SetBuffer(_k.InitBonds, ShaderID.BondRest,     _buf.BondRest);
        compute.SetBuffer(_k.InitBonds, ShaderID.CellCount,    _buf.CellCount);
        compute.SetBuffer(_k.InitBonds, ShaderID.IndirectArgs, _buf.CellComputeArgs);
        compute.Dispatch(_k.InitBonds, Mathf.CeilToInt(maxBonds / 64f), 1, 1);
    }

    private void Start()
    {
        SetParams();
        _buf.BindKernels(compute, _k);
        _buf.BindMaterial(raymarchMaterial);
    }

    private void OnValidate()
    {
        spatialHash.Clamp();
        var bs = spatialHash.bucketSize;
        bonds.Clamp(bs);
        physics.Clamp(bs);
        rendering.Clamp(bs);
        wind.Clamp();
        sdfBricks.Clamp();
        InitSdfBricks();
        SetParams();
        if (!Application.isPlaying) return;
        EnsureBuffers();
        _buf.BindKernels(compute, _k);
        _buf.BindMaterial(raymarchMaterial);
    }

    private void OnDestroy() => _buf.Release(!Application.isPlaying);

    private void Update()
    {
        EnsureBuffers();
        StepSimulation();
        RenderFrame();
    }

    private void StepSimulation()
    {
        compute.SetFloat(ShaderID.DeltaTime, Time.deltaTime);
        compute.SetFloat(ShaderID.Time,      Time.time);

        compute.DispatchIndirect(_k.Grow, _buf.CellComputeArgs);
        DispatchCompactionIfNeeded();
        compute.Dispatch(_k.PrepareIndirectArgs, 1, 1, 1);

        DispatchSpatialHash(ReadActiveCellCount());
        DispatchSdfBricks();

        compute.DispatchIndirect(_k.AddBonds,             _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.ApplyCollisionForces, _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.ApplyBondForces,      _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.ResolveForceBuffer,   _buf.CellComputeArgs);
    }

    private void RenderFrame()
    {
        raymarchMaterial.SetVector(ShaderID.LightDir, -mainLight.transform.forward);

        var view        = mainCamera.worldToCameraMatrix;
        var proj        = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
        raymarchMaterial.SetMatrix(ShaderID.InvViewProj, (proj * view).inverse);
        raymarchMaterial.SetVector(ShaderID.CameraPos,   mainCamera.transform.position);

        Graphics.DrawProcedural(
            raymarchMaterial,
            new Bounds(Vector3.zero, Vector3.one * 100000),
            MeshTopology.Triangles,
            3
        );
    }

    private void DispatchCompactionIfNeeded()
    {
        if (compactEveryNFrames == 0 || ++_frameCount < compactEveryNFrames) return;
        _frameCount = 0;

        _buf.CompactCount.SetData(ZeroUint);
        compute.DispatchIndirect(_k.CompactCells,   _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.WriteCompacted, _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.RemapBonds,     _buf.CellComputeArgs);
        compute.DispatchIndirect(_k.CopyBack,       _buf.CellComputeArgs);
        compute.Dispatch(_k.FinalizeCompact, 1, 1, 1);
    }

    private int ReadActiveCellCount()
    {
        // Sort currently needs the count on the CPU.
        _buf.CellCount.GetData(CountBuf);
        return (int)Mathf.Min(CountBuf[0], maxCells);
    }

    private void DispatchSpatialHash(int activeCount)
    {
        compute.Dispatch(_k.ClearBuckets, Mathf.CeilToInt(spatialHash.bucketTableSize / 64f), 1, 1);
        compute.DispatchIndirect(_k.BuildSpatial, _buf.CellComputeArgs);

        _deviceRadixSort.Sort(
            activeCount,
            _buf.CellTableKeys, _buf.CellIndices,
            _buf.Temp0, _buf.Temp1, _buf.Temp2, _buf.Temp3,
            typeof(uint), typeof(uint), true
        );

        compute.DispatchIndirect(_k.BuildBucket, _buf.CellComputeArgs);
    }

    private void DispatchSdfBricks()
    {
        if (!_buf.SdfBrickAtlas || _buf.SdfBrickCounter == null) return;

        _buf.SdfBrickCounter.SetData(ZeroUint);
        compute.Dispatch(
            _k.ClearSdfIndex,
            Mathf.CeilToInt(_sdfLayout.BrickDim.x / 4f),
            Mathf.CeilToInt(_sdfLayout.BrickDim.y / 4f),
            Mathf.CeilToInt(_sdfLayout.BrickDim.z / 4f)
        );
        compute.Dispatch(_k.BuildSdfBricks, _sdfLayout.BrickDim.x, _sdfLayout.BrickDim.y, _sdfLayout.BrickDim.z);
    }

    private void SetParams()
    {
        growth.Apply(compute);
        spatialHash.Apply(compute);
        physics.Apply(compute);
        wind.Apply(compute);
        bonds.Apply(compute, (int)maxCells);
        rendering.Apply(compute);
        sdfBricks.Apply(compute);
        _sdfLayout.Apply(compute);
        compute.SetInt(ShaderID.MaxCells,     (int)maxCells);
        compute.SetVector(ShaderID.BoundsMax, bounds.max);
        compute.SetVector(ShaderID.BoundsMin, bounds.min);

        spatialHash.Apply(raymarchMaterial);
        physics.Apply(raymarchMaterial);
        rendering.Apply(raymarchMaterial);
        sss.Apply(raymarchMaterial);
        sdfBricks.Apply(raymarchMaterial);
        _sdfLayout.Apply(raymarchMaterial);
        raymarchMaterial.SetVector(ShaderID.BoundsMax, bounds.max);
        raymarchMaterial.SetVector(ShaderID.BoundsMin, bounds.min);
    }

    private void InitSdfBricks()
    {
        _sdfLayout = SdfLayout.Compute(sdfBricks, bounds, spatialHash.bucketSize, SdfBrickSize);
        if (!Application.isPlaying) { _buf.ReleaseSdfResources(true); return; }
        _buf.ReallocSdfTextures(_sdfLayout);
    }

    private void EnsureBuffers()
    {
        if (!Application.isPlaying || _buf.IsAllocated) return;
        InitBuffers();
        InitSdfBricks();
        _buf.BindKernels(compute, _k);
        _buf.BindMaterial(raymarchMaterial);
    }

}
