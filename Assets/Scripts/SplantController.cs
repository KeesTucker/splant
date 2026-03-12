using System.Runtime.InteropServices;
using Structs;
using UnityEngine;
using UnityEngine.Rendering;
using GPUSorting.Runtime;
using UnityEngine.Experimental.Rendering;

public class SplantController : MonoBehaviour
{
    private enum GrowthMode { Blob, String }

    private static class ShaderID
    {
        public static readonly int BaseColor           = Shader.PropertyToID("base_color");
        public static readonly int BondBendStiffness   = Shader.PropertyToID("bond_bend_stiffness");
        public static readonly int BondLinearStiffness = Shader.PropertyToID("bond_linear_stiffness");
        public static readonly int BondRangeSq         = Shader.PropertyToID("bond_range_sq");
        public static readonly int BondCells           = Shader.PropertyToID("bond_cells");
        public static readonly int BondRest            = Shader.PropertyToID("bond_rest");
        public static readonly int BoundsMax           = Shader.PropertyToID("bounds_max");
        public static readonly int BoundsMin           = Shader.PropertyToID("bounds_min");
        public static readonly int BucketCheck         = Shader.PropertyToID("bucket_check");
        public static readonly int BucketEnd           = Shader.PropertyToID("bucket_end");
        public static readonly int BucketSize          = Shader.PropertyToID("bucket_size");
        public static readonly int BucketStart         = Shader.PropertyToID("bucket_start");
        public static readonly int BucketTableSize     = Shader.PropertyToID("bucket_table_size");
        public static readonly int CameraPos           = Shader.PropertyToID("camera_pos");
        public static readonly int CellCount           = Shader.PropertyToID("cell_count");
        public static readonly int CellFullHash        = Shader.PropertyToID("cell_full_hash");
        public static readonly int CellIndices         = Shader.PropertyToID("cell_indices");
        public static readonly int CellRadius          = Shader.PropertyToID("cell_radius");
        public static readonly int CellRemap           = Shader.PropertyToID("cell_remap");
        public static readonly int CellTableKeys       = Shader.PropertyToID("cell_table_keys");
        public static readonly int Cells               = Shader.PropertyToID("cells");
        public static readonly int CellsTemp           = Shader.PropertyToID("cells_temp");
        public static readonly int CompactCount        = Shader.PropertyToID("compact_count");
        public static readonly int DeltaTime           = Shader.PropertyToID("delta_time");
        public static readonly int Damping             = Shader.PropertyToID("damping");
        public static readonly int DebugGrad           = Shader.PropertyToID("debug_grad");
        public static readonly int DebugAtlas          = Shader.PropertyToID("debug_atlas");
        public static readonly int DebugAtlasSlice     = Shader.PropertyToID("debug_atlas_slice");
        public static readonly int FresnelPow          = Shader.PropertyToID("fresnel_pow");
        public static readonly int FresnelStrength     = Shader.PropertyToID("fresnel_strength");
        public static readonly int ForceBuffer         = Shader.PropertyToID("force_buffer");
        public static readonly int Gravity             = Shader.PropertyToID("gravity");
        public static readonly int GrowthMode          = Shader.PropertyToID("growth_mode");
        public static readonly int MaxSplitsPerCell    = Shader.PropertyToID("max_splits_per_cell");
        public static readonly int SplitAge            = Shader.PropertyToID("split_age");
        public static readonly int SplitAgeJitter      = Shader.PropertyToID("split_age_jitter");
        public static readonly int IndirectArgs        = Shader.PropertyToID("indirect_args");
        public static readonly int InvViewProj         = Shader.PropertyToID("inv_view_proj");
        public static readonly int LightDir            = Shader.PropertyToID("light_dir");
        public static readonly int MaxBonds            = Shader.PropertyToID("max_bonds");
        public static readonly int MaxBondsPerCell     = Shader.PropertyToID("max_bonds_per_cell");
        public static readonly int MaxCells            = Shader.PropertyToID("max_cells");
        public static readonly int MaxStepSize         = Shader.PropertyToID("max_step_size");
        public static readonly int MaxSteps            = Shader.PropertyToID("max_steps");
        public static readonly int MinStepSize         = Shader.PropertyToID("min_step_size");
        public static readonly int NeighborRadius      = Shader.PropertyToID("neighbor_radius");
        public static readonly int NeighborRadiusSq    = Shader.PropertyToID("neighbor_radius_sq");
        public static readonly int RepulsionStrength   = Shader.PropertyToID("repulsion_strength");
        public static readonly int SSSColor            = Shader.PropertyToID("sss_color");
        public static readonly int SSSFalloff          = Shader.PropertyToID("sss_falloff");
        public static readonly int SSSStepSize         = Shader.PropertyToID("sss_step_size");
        public static readonly int SSSSteps            = Shader.PropertyToID("sss_steps");
        public static readonly int SSSStrength         = Shader.PropertyToID("sss_strength");
        public static readonly int SdfBrickAtlas       = Shader.PropertyToID("sdf_brick_atlas");
        public static readonly int SdfBrickIndex       = Shader.PropertyToID("sdf_brick_index");
        public static readonly int SdfBrickCounter     = Shader.PropertyToID("sdf_brick_counter");
        public static readonly int SdfBrickDim         = Shader.PropertyToID("sdf_brick_dim");
        public static readonly int SdfBucketCheckRo    = Shader.PropertyToID("bucket_check_ro");
        public static readonly int SdfBucketEndRo      = Shader.PropertyToID("bucket_end_ro");
        public static readonly int SdfBucketStartRo    = Shader.PropertyToID("bucket_start_ro");
        public static readonly int SdfCellsRo          = Shader.PropertyToID("cells_ro");
        public static readonly int SdfCellFullHashRo   = Shader.PropertyToID("cell_full_hash_ro");
        public static readonly int SdfCellIndicesRo    = Shader.PropertyToID("cell_indices_ro");
        public static readonly int SdfBucketDim        = Shader.PropertyToID("sdf_bucket_dim");
        public static readonly int SdfBucketMin        = Shader.PropertyToID("sdf_bucket_min");
        public static readonly int SdfAtlasBrickDim    = Shader.PropertyToID("sdf_atlas_brick_dim");
        public static readonly int SdfMaxDistance      = Shader.PropertyToID("sdf_max_distance");
        public static readonly int SdfMaxBricks        = Shader.PropertyToID("sdf_max_bricks");
        public static readonly int SdfSampleDim        = Shader.PropertyToID("sdf_sample_dim");
        public static readonly int SdfSamplesPerBucket = Shader.PropertyToID("sdf_samples_per_bucket");
        public static readonly int SurfaceThreshold    = Shader.PropertyToID("surface_threshold");
        public static readonly int Time                = Shader.PropertyToID("time");
        public static readonly int Velocities          = Shader.PropertyToID("velocities");
        public static readonly int VelocitiesTemp      = Shader.PropertyToID("velocities_temp");
        public static readonly int WindDirection       = Shader.PropertyToID("wind_direction");
        public static readonly int WindFrequency       = Shader.PropertyToID("wind_frequency");
        public static readonly int WindSpeed           = Shader.PropertyToID("wind_speed");
        public static readonly int WindStrength        = Shader.PropertyToID("wind_strength");
        public static readonly int WindVariation       = Shader.PropertyToID("wind_variation");
    }

    private int _addBondsKernel;
    private int _applyBondForcesKernel;
    private int _applyCollisionForcesKernel;
    private int _buildBucketKernel;
    private int _buildSdfBricksKernel;
    private int _clearSdfIndexKernel;
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

    [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one * 10);
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ComputeShader compute;
    [SerializeField] private ComputeShader sortCompute;
    [SerializeField] private Transform mainLight;
    [SerializeField] private Material raymarchMaterial;
    [SerializeField] private uint maxCells = 100000;

    [Header("Growth")]
    [SerializeField] private GrowthMode growthMode = GrowthMode.String;
    [Min(1)]
    [SerializeField] private int splitsPerCell = 1;
    [Min(0.001f)]
    [SerializeField] private float splitAge = 0.02f;
    [Min(0f)]
    [SerializeField] private float splitAgeJitter = 0.01f;

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

    [Header("Wind")]
    [SerializeField] private Vector3 windDirection = new(1f, 0f, 0f);
    [SerializeField] private float windStrength = 0.2f;
    [SerializeField] private float windFrequency = 0.15f;
    [SerializeField] private float windSpeed = 0.8f;
    [SerializeField] private float windVariation = 0.35f;

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
    [SerializeField] private bool debugSdfGrad;
    [SerializeField] private bool debugAtlasSlice;
    [SerializeField] private int debugAtlasSliceIndex;

    [Header("SDF Bricks")]
    [SerializeField] private float sdfMaxDistance = 0.2f;
    [SerializeField] private int sdfMaxBricks = 32768;
    [SerializeField] private int sdfSamplesPerBucket = 2;

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
    private ComputeBuffer _sdfBrickCounter;
    private RenderTexture _sdfBrickAtlas;
    private RenderTexture _sdfBrickIndex;

    private Vector3Int _sdfBucketDim;
    private Vector3Int _sdfBucketMin;
    private Vector3Int _sdfSampleDim;
    private Vector3Int _sdfBrickDim;
    private Vector3Int _sdfAtlasBrickDim;
    private Vector3Int _sdfAtlasTexDim;
    private int _sdfMaxBricksApplied;

    private DeviceRadixSort _deviceRadixSort;
    private int _frameCount;

    private const int SdfBrickSize = 8;

    private void Awake()
    {
        FindKernels();
        InitBuffers();
        InitSdfBricks();
        InitRadixSort();
    }

    private void FindKernels()
    {
        _addBondsKernel              = compute.FindKernel("add_bonds");
        _applyBondForcesKernel       = compute.FindKernel("apply_bond_forces");
        _applyCollisionForcesKernel  = compute.FindKernel("apply_collision_forces");
        _buildBucketKernel           = compute.FindKernel("build_bucket_ranges");
        _buildSdfBricksKernel        = compute.FindKernel("build_sdf_bricks");
        _clearSdfIndexKernel         = compute.FindKernel("clear_sdf_index");
        _buildSpatialKernel          = compute.FindKernel("build_spatial_hash");
        _clearBucketsKernel          = compute.FindKernel("clear_buckets");
        _compactCellsKernel          = compute.FindKernel("compact_cells");
        _copyBackKernel              = compute.FindKernel("copy_back");
        _finalizeCompactKernel       = compute.FindKernel("finalize_compact");
        _growKernel                  = compute.FindKernel("grow");
        _initBondsKernel             = compute.FindKernel("init_bonds");
        _prepareIndirectArgsKernel   = compute.FindKernel("prepare_indirect_args");
        _remapBondsKernel            = compute.FindKernel("remap_bonds");
        _resolveForceBufferKernel    = compute.FindKernel("resolve_force_buffer");
        _writeCompactedKernel        = compute.FindKernel("write_compacted");
    }

    private void InitBuffers()
    {
        if (_cellComputeArgs != null)
        {
            ReleaseBuffers();
        }

        var cellStride = Marshal.SizeOf<Cell>();
        var maxBonds = (int)maxCells * (int)maxBondsPerCell;

        _bondCells       = new ComputeBuffer(maxBonds, sizeof(uint) * 2);
        _bondRest        = new ComputeBuffer(maxBonds, sizeof(float) * 4);
        _bucketCheck     = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketEnd       = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _bucketStart     = new ComputeBuffer(bucketTableSize, sizeof(uint));
        _cellComputeArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
        _cellCount       = new ComputeBuffer(1, sizeof(uint));
        _cellFullHashes  = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellIndices     = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellRemap       = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cellTableKeys   = new ComputeBuffer((int)maxCells, sizeof(uint));
        _cells           = new ComputeBuffer((int)maxCells, cellStride);
        _cellsTemp       = new ComputeBuffer((int)maxCells, cellStride);
        _compactCount    = new ComputeBuffer(1, sizeof(uint));
        _forceBuffer     = new ComputeBuffer((int)maxCells * 3, sizeof(uint));
        _velocities      = new ComputeBuffer((int)maxCells, sizeof(float) * 3);
        _velocitiesTemp  = new ComputeBuffer((int)maxCells, sizeof(float) * 3);

        // Init
        _cellComputeArgs.SetData(new uint[] { 1, 1, 1 });
        _cellCount.SetData(new[] { 2u });
        _compactCount.SetData(new[] { 0u });
        _forceBuffer.SetData(new uint[(int)maxCells * 3]);
        _velocities.SetData(new Vector3[(int)maxCells]);
        _velocitiesTemp.SetData(new Vector3[(int)maxCells]);

        // Seed with one cell
        _cells.SetData(new[] {
            new Cell { age = 0, position = Vector3.zero,    polarity = Vector3.up },
            new Cell { age = 0, position = Vector3.one * 2, polarity = Vector3.up }
        });

        // Init entire bond buffer to invalid
        compute.SetInt(ShaderID.MaxBonds, maxBonds);
        compute.SetBuffer(_initBondsKernel, ShaderID.BondCells,   _bondCells);
        compute.SetBuffer(_initBondsKernel, ShaderID.BondRest,    _bondRest);
        compute.SetBuffer(_initBondsKernel, ShaderID.CellCount,   _cellCount);
        compute.SetBuffer(_initBondsKernel, ShaderID.IndirectArgs, _cellComputeArgs);
        compute.Dispatch(_initBondsKernel, Mathf.CeilToInt(maxBonds / 64f), 1, 1);
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
        EnsureBuffers();
        SetParams();
        SetComputeBuffers();
        SetRaymarchBuffers();
    }

    private void OnValidate()
    {
        // enforce bucket size constrained vars
        bondRange               = Mathf.Clamp(bondRange, 0, bucketSize * 0.5f);
        cellRadius              = Mathf.Clamp(cellRadius, 0, bucketSize * 0.5f);
        maxStepSize             = Mathf.Clamp(maxStepSize, 0, bucketSize * 0.5f);
        minStepSize             = Mathf.Clamp(minStepSize, 0, bucketSize * 0.5f);
        neighborCollisionRadius = Mathf.Clamp(neighborCollisionRadius, 0, bucketSize * 0.5f);
        damping                 = Mathf.Max(0f, damping);
        windStrength            = Mathf.Max(0f, windStrength);
        windFrequency           = Mathf.Max(0f, windFrequency);
        windSpeed               = Mathf.Max(0f, windSpeed);
        windVariation           = Mathf.Max(0f, windVariation);

        // ensure bucket table size is a power of 2 (has exactly 1 bit)
        if (!(bucketTableSize > 0 && (bucketTableSize & (bucketTableSize - 1)) == 0))
        {
            bucketTableSize = 1 << 18;
        }

        sdfMaxDistance      = Mathf.Max(0f, sdfMaxDistance);
        sdfMaxBricks        = Mathf.Max(1, sdfMaxBricks);
        sdfSamplesPerBucket = Mathf.Max(1, sdfSamplesPerBucket);
        InitSdfBricks();
        SetParams();
        if (Application.isPlaying)
        {
            EnsureBuffers();
            SetComputeBuffers();
            SetRaymarchBuffers();
        }
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void Update()
    {
        EnsureBuffers();
        StepSimulation();
        RenderFrame();
    }

    private void StepSimulation()
    {
        UpdateTimeParams();
        DispatchGrow();
        DispatchCompactionIfNeeded();
        DispatchPrepareIndirectArgs();

        var activeCount = ReadActiveCellCount();
        DispatchSpatialHash(activeCount);
        DispatchSdfBricks();
        DispatchBonds();
        DispatchPhysics();
    }

    private void RenderFrame()
    {
        UpdateLightParams();

        var view = mainCamera.worldToCameraMatrix;
        var proj = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
        var invViewProj = (proj * view).inverse;

        raymarchMaterial.SetMatrix(ShaderID.InvViewProj, invViewProj);
        raymarchMaterial.SetVector(ShaderID.CameraPos, mainCamera.transform.position);

        Graphics.DrawProcedural(
            raymarchMaterial,
            new Bounds(Vector3.zero, Vector3.one * 100000),
            MeshTopology.Triangles,
            3
        );
    }

    private void UpdateTimeParams()
    {
        compute.SetFloat(ShaderID.DeltaTime, UnityEngine.Time.deltaTime);
        compute.SetFloat(ShaderID.Time,      UnityEngine.Time.time);
    }

    private void UpdateLightParams()
    {
        raymarchMaterial.SetVector(ShaderID.LightDir, -mainLight.transform.forward);
    }

    private void DispatchGrow()
    {
        compute.DispatchIndirect(_growKernel, _cellComputeArgs);
    }

    private void DispatchCompactionIfNeeded()
    {
        _frameCount++;
        if (compactEveryNFrames == 0 || _frameCount < compactEveryNFrames)
        {
            return;
        }

        _frameCount = 0;
        CompactCells();
    }

    private void DispatchPrepareIndirectArgs()
    {
        compute.Dispatch(_prepareIndirectArgsKernel, 1, 1, 1);
    }

    private int ReadActiveCellCount()
    {
        // Sort currently needs the count on the CPU.
        var count = new uint[1];
        _cellCount.GetData(count);
        return (int)Mathf.Min(count[0], maxCells);
    }

    private void DispatchSpatialHash(int activeCount)
    {
        var bucketGroups = Mathf.CeilToInt(bucketTableSize / 64f);
        compute.Dispatch(_clearBucketsKernel, bucketGroups, 1, 1);
        compute.DispatchIndirect(_buildSpatialKernel, _cellComputeArgs);

        _deviceRadixSort.Sort(
            activeCount,
            _cellTableKeys,
            _cellIndices,
            _temp0, _temp1, _temp2, _temp3,
            typeof(uint), typeof(uint),
            true
        );

        compute.DispatchIndirect(_buildBucketKernel, _cellComputeArgs);
    }

    private void DispatchSdfBricks()
    {
        ClearSdfIndex();
        DispatchSdfBrickBuild();
    }

    private void DispatchSdfBrickBuild()
    {
        if (_sdfBrickAtlas == null || _sdfBrickIndex == null || _sdfBrickCounter == null)
        {
            return;
        }

        compute.Dispatch(_buildSdfBricksKernel, _sdfBrickDim.x, _sdfBrickDim.y, _sdfBrickDim.z);
    }

    private void DispatchBonds()
    {
        compute.DispatchIndirect(_addBondsKernel, _cellComputeArgs);
    }

    private void DispatchPhysics()
    {
        compute.DispatchIndirect(_applyCollisionForcesKernel, _cellComputeArgs);
        compute.DispatchIndirect(_applyBondForcesKernel,      _cellComputeArgs);
        compute.DispatchIndirect(_resolveForceBufferKernel,   _cellComputeArgs);
    }

    private void SetParams()
    {
        SetComputeParams();
        SetRaymarchParams();
    }

    private void SetComputeParams()
    {
        compute.SetFloat(ShaderID.BondBendStiffness,   bondBendStiffness);
        compute.SetFloat(ShaderID.BondLinearStiffness, bondLinearStiffness);
        compute.SetFloat(ShaderID.BondRangeSq,         bondRange * bondRange);
        compute.SetFloat(ShaderID.BucketSize,          bucketSize);
        compute.SetFloat(ShaderID.CellRadius,          cellRadius);
        compute.SetFloat(ShaderID.Damping,             damping);
        compute.SetVector(ShaderID.Gravity,            gravity);
        compute.SetInt(ShaderID.GrowthMode,            (int)growthMode);
        compute.SetInt(ShaderID.MaxSplitsPerCell,      Mathf.Max(1, splitsPerCell));
        compute.SetFloat(ShaderID.SplitAge,            Mathf.Max(0.001f, splitAge));
        compute.SetFloat(ShaderID.SplitAgeJitter,      Mathf.Max(0f, splitAgeJitter));
        compute.SetFloat(ShaderID.NeighborRadius,      neighborCollisionRadius);
        compute.SetFloat(ShaderID.NeighborRadiusSq,    neighborCollisionRadius * neighborCollisionRadius);
        compute.SetFloat(ShaderID.RepulsionStrength,   repulsionStrength);
        compute.SetVector(ShaderID.WindDirection,      windDirection);
        compute.SetFloat(ShaderID.WindStrength,        windStrength);
        compute.SetFloat(ShaderID.WindFrequency,       windFrequency);
        compute.SetFloat(ShaderID.WindSpeed,           windSpeed);
        compute.SetFloat(ShaderID.WindVariation,       windVariation);
        compute.SetInt(ShaderID.BucketTableSize,       bucketTableSize);
        compute.SetInt(ShaderID.MaxBonds,              (int)maxCells * (int)maxBondsPerCell);
        compute.SetInt(ShaderID.MaxBondsPerCell,       (int)maxBondsPerCell);
        compute.SetInt(ShaderID.MaxCells,              (int)maxCells);
        compute.SetVector(ShaderID.BoundsMax,          bounds.max);
        compute.SetVector(ShaderID.BoundsMin,          bounds.min);
        compute.SetFloat(ShaderID.SdfMaxDistance,      sdfMaxDistance);
        compute.SetInt(ShaderID.SdfMaxBricks,          Mathf.Max(1, _sdfMaxBricksApplied));
        compute.SetInts(ShaderID.SdfBucketDim,         _sdfBucketDim.x, _sdfBucketDim.y, _sdfBucketDim.z);
        compute.SetInts(ShaderID.SdfBucketMin,         _sdfBucketMin.x, _sdfBucketMin.y, _sdfBucketMin.z);
        compute.SetInts(ShaderID.SdfSampleDim,         _sdfSampleDim.x, _sdfSampleDim.y, _sdfSampleDim.z);
        compute.SetInts(ShaderID.SdfBrickDim,          _sdfBrickDim.x,  _sdfBrickDim.y,  _sdfBrickDim.z);
        compute.SetInts(ShaderID.SdfAtlasBrickDim,     _sdfAtlasBrickDim.x, _sdfAtlasBrickDim.y, _sdfAtlasBrickDim.z);
        compute.SetInt(ShaderID.SdfSamplesPerBucket,   Mathf.Max(1, sdfSamplesPerBucket));
    }

    private void SetRaymarchParams()
    {
        raymarchMaterial.SetFloat(ShaderID.BucketSize,          bucketSize);
        raymarchMaterial.SetFloat(ShaderID.CellRadius,          cellRadius);
        raymarchMaterial.SetFloat(ShaderID.DebugGrad,           debugSdfGrad ? 1f : 0f);
        raymarchMaterial.SetFloat(ShaderID.DebugAtlas,          debugAtlasSlice ? 1f : 0f);
        raymarchMaterial.SetFloat(ShaderID.DebugAtlasSlice,     debugAtlasSliceIndex);
        raymarchMaterial.SetFloat(ShaderID.FresnelPow,          fresnelPow);
        raymarchMaterial.SetFloat(ShaderID.FresnelStrength,     fresnelStrength);
        raymarchMaterial.SetFloat(ShaderID.MaxStepSize,         maxStepSize);
        raymarchMaterial.SetFloat(ShaderID.MinStepSize,         minStepSize);
        raymarchMaterial.SetFloat(ShaderID.NeighborRadiusSq,    neighborCollisionRadius * neighborCollisionRadius);
        raymarchMaterial.SetFloat(ShaderID.SSSFalloff,          sssFalloff);
        raymarchMaterial.SetFloat(ShaderID.SSSStepSize,         sssStepSize);
        raymarchMaterial.SetFloat(ShaderID.SSSStrength,         sssStrength);
        raymarchMaterial.SetFloat(ShaderID.SurfaceThreshold,    surfaceThreshold);
        raymarchMaterial.SetInt(ShaderID.BucketTableSize,       bucketTableSize);
        raymarchMaterial.SetInt(ShaderID.MaxSteps,              maxSteps);
        raymarchMaterial.SetInt(ShaderID.SSSSteps,              sssSteps);
        raymarchMaterial.SetVector(ShaderID.BaseColor,          baseColour);
        raymarchMaterial.SetVector(ShaderID.BoundsMax,          bounds.max);
        raymarchMaterial.SetVector(ShaderID.BoundsMin,          bounds.min);
        raymarchMaterial.SetVector(ShaderID.SSSColor,           sssColor);
        raymarchMaterial.SetFloat(ShaderID.SdfMaxDistance,      sdfMaxDistance);
        raymarchMaterial.SetVector(ShaderID.SdfBucketMin,       (Vector3)_sdfBucketMin);
        raymarchMaterial.SetVector(ShaderID.SdfSampleDim,       (Vector3)_sdfSampleDim);
        raymarchMaterial.SetVector(ShaderID.SdfAtlasBrickDim,   (Vector3)_sdfAtlasBrickDim);
        raymarchMaterial.SetFloat(ShaderID.SdfSamplesPerBucket, Mathf.Max(1, sdfSamplesPerBucket));
    }

    private void SetComputeBuffers()
    {
        if (_cellComputeArgs == null)
        {
            return;
        }

        compute.SetBuffer(_prepareIndirectArgsKernel, ShaderID.IndirectArgs, _cellComputeArgs);
        compute.SetBuffer(_prepareIndirectArgsKernel, ShaderID.CellCount,    _cellCount);

        SetAddBondsKernelBuffers();
        SetApplyBondForcesKernelBuffers();
        SetApplyCollisionForcesKernelBuffers();
        SetBuildBucketKernelBuffers();
        SetBuildSdfKernelBuffers();
        SetBuildSpatialKernelBuffers();
        SetClearBucketsKernelBuffers();
        SetGrowKernelBuffers();
        SetResolveForceKernelBuffers();

        // Compaction kernels
        compute.SetBuffer(_compactCellsKernel, ShaderID.CellCount,    _cellCount);
        compute.SetBuffer(_compactCellsKernel, ShaderID.CellRemap,    _cellRemap);
        compute.SetBuffer(_compactCellsKernel, ShaderID.Cells,        _cells);
        compute.SetBuffer(_compactCellsKernel, ShaderID.CompactCount, _compactCount);

        compute.SetBuffer(_writeCompactedKernel, ShaderID.CellCount,      _cellCount);
        compute.SetBuffer(_writeCompactedKernel, ShaderID.CellRemap,      _cellRemap);
        compute.SetBuffer(_writeCompactedKernel, ShaderID.Cells,          _cells);
        compute.SetBuffer(_writeCompactedKernel, ShaderID.CellsTemp,      _cellsTemp);
        compute.SetBuffer(_writeCompactedKernel, ShaderID.Velocities,     _velocities);
        compute.SetBuffer(_writeCompactedKernel, ShaderID.VelocitiesTemp, _velocitiesTemp);

        compute.SetBuffer(_remapBondsKernel, ShaderID.BondCells,    _bondCells);
        compute.SetBuffer(_remapBondsKernel, ShaderID.BondRest,     _bondRest);
        compute.SetBuffer(_remapBondsKernel, ShaderID.CellCount,    _cellCount);
        compute.SetBuffer(_remapBondsKernel, ShaderID.CellRemap,    _cellRemap);
        compute.SetBuffer(_remapBondsKernel, ShaderID.Cells,        _cells);
        compute.SetBuffer(_remapBondsKernel, ShaderID.CompactCount, _compactCount);

        compute.SetBuffer(_copyBackKernel, ShaderID.Cells,          _cells);
        compute.SetBuffer(_copyBackKernel, ShaderID.CellsTemp,      _cellsTemp);
        compute.SetBuffer(_copyBackKernel, ShaderID.Velocities,     _velocities);
        compute.SetBuffer(_copyBackKernel, ShaderID.VelocitiesTemp, _velocitiesTemp);
        compute.SetBuffer(_copyBackKernel, ShaderID.CompactCount,   _compactCount);

        compute.SetBuffer(_finalizeCompactKernel, ShaderID.CellCount,    _cellCount);
        compute.SetBuffer(_finalizeCompactKernel, ShaderID.CompactCount, _compactCount);
    }

    private void SetAddBondsKernelBuffers()
    {
        compute.SetBuffer(_addBondsKernel, ShaderID.BondCells,    _bondCells);
        compute.SetBuffer(_addBondsKernel, ShaderID.BondRest,     _bondRest);
        compute.SetBuffer(_addBondsKernel, ShaderID.BucketEnd,    _bucketEnd);
        compute.SetBuffer(_addBondsKernel, ShaderID.BucketStart,  _bucketStart);
        compute.SetBuffer(_addBondsKernel, ShaderID.CellCount,    _cellCount);
        compute.SetBuffer(_addBondsKernel, ShaderID.CellFullHash, _cellFullHashes);
        compute.SetBuffer(_addBondsKernel, ShaderID.CellIndices,  _cellIndices);
        compute.SetBuffer(_addBondsKernel, ShaderID.Cells,        _cells);
    }

    private void SetApplyBondForcesKernelBuffers()
    {
        compute.SetBuffer(_applyBondForcesKernel, ShaderID.BondCells,   _bondCells);
        compute.SetBuffer(_applyBondForcesKernel, ShaderID.BondRest,    _bondRest);
        compute.SetBuffer(_applyBondForcesKernel, ShaderID.CellCount,   _cellCount);
        compute.SetBuffer(_applyBondForcesKernel, ShaderID.Cells,       _cells);
        compute.SetBuffer(_applyBondForcesKernel, ShaderID.ForceBuffer, _forceBuffer);
    }

    private void SetApplyCollisionForcesKernelBuffers()
    {
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.BucketEnd,    _bucketEnd);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.BucketStart,  _bucketStart);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.CellCount,    _cellCount);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.CellFullHash, _cellFullHashes);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.CellIndices,  _cellIndices);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.Cells,        _cells);
        compute.SetBuffer(_applyCollisionForcesKernel, ShaderID.ForceBuffer,  _forceBuffer);
    }

    private void SetBuildBucketKernelBuffers()
    {
        compute.SetBuffer(_buildBucketKernel, ShaderID.BucketEnd,      _bucketEnd);
        compute.SetBuffer(_buildBucketKernel, ShaderID.BucketStart,    _bucketStart);
        compute.SetBuffer(_buildBucketKernel, ShaderID.CellCount,      _cellCount);
        compute.SetBuffer(_buildBucketKernel, ShaderID.CellTableKeys,  _cellTableKeys);
    }

    private void SetBuildSpatialKernelBuffers()
    {
        compute.SetBuffer(_buildSpatialKernel, ShaderID.BucketCheck,   _bucketCheck);
        compute.SetBuffer(_buildSpatialKernel, ShaderID.CellCount,     _cellCount);
        compute.SetBuffer(_buildSpatialKernel, ShaderID.CellFullHash,  _cellFullHashes);
        compute.SetBuffer(_buildSpatialKernel, ShaderID.CellIndices,   _cellIndices);
        compute.SetBuffer(_buildSpatialKernel, ShaderID.CellTableKeys, _cellTableKeys);
        compute.SetBuffer(_buildSpatialKernel, ShaderID.Cells,         _cells);
    }

    private void SetBuildSdfKernelBuffers()
    {
        if (_sdfBrickAtlas == null || _sdfBrickIndex == null || _sdfBrickCounter == null)
        {
            return;
        }

        compute.SetTexture(_buildSdfBricksKernel, ShaderID.SdfBrickAtlas,      _sdfBrickAtlas);
        compute.SetTexture(_buildSdfBricksKernel, ShaderID.SdfBrickIndex,      _sdfBrickIndex);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfBrickCounter,    _sdfBrickCounter);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfBucketCheckRo,   _bucketCheck);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfBucketEndRo,     _bucketEnd);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfBucketStartRo,   _bucketStart);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfCellFullHashRo,  _cellFullHashes);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfCellIndicesRo,   _cellIndices);
        compute.SetBuffer(_buildSdfBricksKernel,  ShaderID.SdfCellsRo,         _cells);

        compute.SetTexture(_clearSdfIndexKernel, ShaderID.SdfBrickIndex, _sdfBrickIndex);
    }

    private void SetClearBucketsKernelBuffers()
    {
        compute.SetBuffer(_clearBucketsKernel, ShaderID.BucketCheck, _bucketCheck);
        compute.SetBuffer(_clearBucketsKernel, ShaderID.BucketEnd,   _bucketEnd);
        compute.SetBuffer(_clearBucketsKernel, ShaderID.BucketStart, _bucketStart);
    }

    private void SetGrowKernelBuffers()
    {
        compute.SetBuffer(_growKernel, ShaderID.CellCount,  _cellCount);
        compute.SetBuffer(_growKernel, ShaderID.Cells,      _cells);
        compute.SetBuffer(_growKernel, ShaderID.Velocities, _velocities);
    }

    private void SetResolveForceKernelBuffers()
    {
        compute.SetBuffer(_resolveForceBufferKernel, ShaderID.CellCount,  _cellCount);
        compute.SetBuffer(_resolveForceBufferKernel, ShaderID.Cells,      _cells);
        compute.SetBuffer(_resolveForceBufferKernel, ShaderID.ForceBuffer, _forceBuffer);
        compute.SetBuffer(_resolveForceBufferKernel, ShaderID.Velocities, _velocities);
    }

    private void SetRaymarchBuffers()
    {
        raymarchMaterial.SetBuffer(ShaderID.BucketCheck,  _bucketCheck);
        raymarchMaterial.SetBuffer(ShaderID.BucketEnd,    _bucketEnd);
        raymarchMaterial.SetBuffer(ShaderID.BucketStart,  _bucketStart);
        raymarchMaterial.SetBuffer(ShaderID.CellFullHash, _cellFullHashes);
        raymarchMaterial.SetBuffer(ShaderID.CellIndices,  _cellIndices);
        raymarchMaterial.SetBuffer(ShaderID.Cells,        _cells);
        if (_sdfBrickAtlas != null)
        {
            raymarchMaterial.SetTexture(ShaderID.SdfBrickAtlas, _sdfBrickAtlas);
            if (_sdfBrickIndex != null)
            {
                raymarchMaterial.SetTexture(ShaderID.SdfBrickIndex, _sdfBrickIndex);
            }
        }
    }

    private void ReleaseBuffers()
    {
        Release(ref _bondCells);
        Release(ref _bondRest);
        Release(ref _bucketCheck);
        Release(ref _bucketEnd);
        Release(ref _bucketStart);
        Release(ref _cellComputeArgs);
        Release(ref _cellCount);
        Release(ref _cellFullHashes);
        Release(ref _cellIndices);
        Release(ref _cellRemap);
        Release(ref _cellTableKeys);
        Release(ref _cells);
        Release(ref _cellsTemp);
        Release(ref _compactCount);
        Release(ref _forceBuffer);
        Release(ref _velocities);
        Release(ref _velocitiesTemp);
        Release(ref _temp0);
        Release(ref _temp1);
        Release(ref _temp2);
        Release(ref _temp3);
        Release(ref _sdfBrickCounter);
        ReleaseSdfTextures(!Application.isPlaying);
    }

    private static void Release(ref ComputeBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
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

    private void InitSdfBricks()
    {
        if (bucketSize <= 1e-6f)
        {
            _sdfBucketMin = Vector3Int.zero;
            _sdfBucketDim = Vector3Int.one;
            _sdfSampleDim = Vector3Int.one;
            _sdfBrickDim  = Vector3Int.one;
            _sdfAtlasBrickDim = Vector3Int.one;
            _sdfAtlasTexDim   = Vector3Int.one;
            _sdfMaxBricksApplied = 1;
            return;
        }

        var min = bounds.min;
        var max = bounds.max;
        _sdfBucketMin = new Vector3Int(
            Mathf.FloorToInt(min.x / bucketSize),
            Mathf.FloorToInt(min.y / bucketSize),
            Mathf.FloorToInt(min.z / bucketSize)
        );
        var bucketMax = new Vector3Int(
            Mathf.FloorToInt(max.x / bucketSize),
            Mathf.FloorToInt(max.y / bucketSize),
            Mathf.FloorToInt(max.z / bucketSize)
        );
        _sdfBucketDim = new Vector3Int(
            Mathf.Max(1, bucketMax.x - _sdfBucketMin.x + 1),
            Mathf.Max(1, bucketMax.y - _sdfBucketMin.y + 1),
            Mathf.Max(1, bucketMax.z - _sdfBucketMin.z + 1)
        );
        var samples = Mathf.Max(1, sdfSamplesPerBucket);
        var samplesPow2 = Mathf.NextPowerOfTwo(samples);
        sdfSamplesPerBucket = samplesPow2;
        _sdfSampleDim = _sdfBucketDim * samplesPow2 + Vector3Int.one;
        _sdfBrickDim = new Vector3Int(
            Mathf.CeilToInt(_sdfSampleDim.x / (float)SdfBrickSize),
            Mathf.CeilToInt(_sdfSampleDim.y / (float)SdfBrickSize),
            Mathf.CeilToInt(_sdfSampleDim.z / (float)SdfBrickSize)
        );

        var maxPossibleBricks = _sdfBrickDim.x * _sdfBrickDim.y * _sdfBrickDim.z;
        var targetBricks = Mathf.Clamp(sdfMaxBricks, 1, maxPossibleBricks);
        _sdfMaxBricksApplied = targetBricks;
        var side = Mathf.CeilToInt(Mathf.Pow(targetBricks, 1f / 3f));
        var depth = Mathf.CeilToInt(targetBricks / (float)(side * side));
        // Pack the atlas into a near-cubic grid of bricks for predictable layout.
        _sdfAtlasBrickDim = new Vector3Int(side, side, Mathf.Max(1, depth));
        _sdfAtlasTexDim   = _sdfAtlasBrickDim * SdfBrickSize;

        if (!Application.isPlaying)
        {
            ReleaseSdfTextures(true);
            _sdfBrickCounter?.Release();
            _sdfBrickCounter = null;
            return;
        }

        if (_sdfBrickAtlas != null &&
            _sdfBrickAtlas.width == _sdfAtlasTexDim.x &&
            _sdfBrickAtlas.height == _sdfAtlasTexDim.y &&
            _sdfBrickAtlas.volumeDepth == _sdfAtlasTexDim.z &&
            _sdfBrickIndex != null &&
            _sdfBrickIndex.width == _sdfBrickDim.x &&
            _sdfBrickIndex.height == _sdfBrickDim.y &&
            _sdfBrickIndex.volumeDepth == _sdfBrickDim.z &&
            _sdfBrickCounter != null)
        {
            return;
        }

        ReleaseSdfTextures(false);
        _sdfBrickCounter?.Release();
        _sdfBrickCounter = null;

        var atlasDesc = new RenderTextureDescriptor(_sdfAtlasTexDim.x, _sdfAtlasTexDim.y, GraphicsFormat.R8_UNorm, 0)
        {
            dimension     = TextureDimension.Tex3D,
            volumeDepth   = _sdfAtlasTexDim.z,
            enableRandomWrite = true
        };
        _sdfBrickAtlas = new RenderTexture(atlasDesc)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };
        _sdfBrickAtlas.Create();

        var indexDesc = new RenderTextureDescriptor(_sdfBrickDim.x, _sdfBrickDim.y, GraphicsFormat.R32_UInt, 0)
        {
            dimension     = TextureDimension.Tex3D,
            volumeDepth   = _sdfBrickDim.z,
            enableRandomWrite = true
        };
        _sdfBrickIndex = new RenderTexture(indexDesc)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };
        _sdfBrickIndex.Create();

        _sdfBrickCounter = new ComputeBuffer(1, sizeof(uint));
    }

    private void ClearSdfIndex()
    {
        if (_sdfBrickIndex == null || _sdfBrickCounter == null)
        {
            return;
        }

        _sdfBrickCounter.SetData(new[] { 0u });
        compute.Dispatch(
            _clearSdfIndexKernel,
            Mathf.CeilToInt(_sdfBrickDim.x / 4f),
            Mathf.CeilToInt(_sdfBrickDim.y / 4f),
            Mathf.CeilToInt(_sdfBrickDim.z / 4f)
        );
    }

    private void ReleaseSdfTextures(bool immediate)
    {
        if (_sdfBrickAtlas != null)
        {
            _sdfBrickAtlas.Release();
            if (immediate) DestroyImmediate(_sdfBrickAtlas); else Destroy(_sdfBrickAtlas);
            _sdfBrickAtlas = null;
        }
        if (_sdfBrickIndex != null)
        {
            _sdfBrickIndex.Release();
            if (immediate) DestroyImmediate(_sdfBrickIndex); else Destroy(_sdfBrickIndex);
            _sdfBrickIndex = null;
        }
    }

    private void EnsureBuffers()
    {
        if (!Application.isPlaying || _cellComputeArgs != null)
        {
            return;
        }

        InitBuffers();
        InitSdfBricks();
        SetComputeBuffers();
        SetRaymarchBuffers();
    }

}
