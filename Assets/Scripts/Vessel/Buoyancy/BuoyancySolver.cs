using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

public class BuoyancySolver : MonoBehaviour
{
    [SerializeField] OceanManager oceanManager;
    [SerializeField] ComputeShader fftCompute;
    [SerializeField] ComputeShader gerstnerCompute;
    [SerializeField] PointBuoyancy pointBuoyancy;
    [SerializeField] VolumeBuoyancy volumeBuoyancy;
    [SerializeField] PartitionedBuoyancy partitionedBuoyancy;
    [SerializeField] BuoyancyModel activeModel;
    [SerializeField] float vesselMass;
    [SerializeField] Transform vesselTransform;
    [SerializeField] Mesh vesselMesh;
    [SerializeField] float waterDensity = 1000f;
    [SerializeField] int patchResolution = 32;
    [SerializeField] float patchSize = 10f;
    [SerializeField] float gravity = 9.81f;
    ComputeBuffer gerstnerSampleBuffer;
    ComputeBuffer fftSampleBuffer;
    OceanSample[] oceanSampleArray;
    Rigidbody rb;
    bool readbackPending;
    int multipleReadbacksPending = 0;
    Vector3 requestedPatchCentre;
    public struct OceanSample
    {
        public float height;
        public Vector3 normal;
    };

    public struct MeshConfiguration
    {
        public int patchResolution;
        public Vector3 patchCentre;
        public float patchSize;
        public int meshResolution;
        public float meshSize;
    }
    public struct Buoyancy
    {
        public float[] submergedVolumes;
        public Vector3[] buoyancyForces;
        public Vector3[] waterDrag;
        public Vector3[] angularDrag;
        public Vector3[] submergedCentroids;
    }
    MeshConfiguration meshConfig;
    Buoyancy buoyancyValues;

    void Start()
    {
        // Get the rigid body and assign the vessel mass
        rb = GetComponent<Rigidbody>();
        rb.mass = vesselMass;

        // Initialise the buoyancy values 
        buoyancyValues = new Buoyancy();

        InitialiseMeshConfig();

        // Upload the compute buffer to the compute shader
        int stride = Marshal.SizeOf(typeof(OceanSample));
        gerstnerSampleBuffer = new ComputeBuffer(patchResolution * patchResolution, stride);
        fftSampleBuffer = new ComputeBuffer(patchResolution * patchResolution, stride);

        switch (oceanManager.activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerCompute.SetBuffer(1, "_OceanSamples", gerstnerSampleBuffer);
                break;
            case WaveGenerator.FFT:
                fftCompute.SetBuffer(6, "_OceanSamples", fftSampleBuffer);
                break;
            case WaveGenerator.Hybrid:
                // fftCompute.SetBuffer(6, "_OceanSamples", fftSampleBuffer);
                // gerstnerCompute.SetBuffer(1, "_OceanSamples", gerstnerSampleBuffer);
                break;
        }
    }

    void InitialiseMeshConfig()
    {
        Vector3 centre = vesselTransform.position;
        centre.y = 0;
        meshConfig = new MeshConfiguration
        {
            patchResolution = patchResolution,
            patchSize = patchSize,
            patchCentre = centre,
            meshResolution = oceanManager.meshResolution,
            meshSize = oceanManager.meshSize
        };
    }

    public Buoyancy CalculateBuoyancy()
    {
        if (!readbackPending)
        {
            // Get the vessel position
            Vector3 vesselPos = vesselTransform.position;
            vesselPos.y = 0f;

            // Update the compute shader parameters, dispatch the ocean sample kernel
            DispatchSampleKernel(vesselPos);

            // Fetch the ocean sample
            GetSample(vesselPos);
        }

        // Exit early if there are no samples
        if (oceanSampleArray == null)
            return buoyancyValues;

        // Calculate the submerged volume
        switch (activeModel)
        {
            case BuoyancyModel.Point:
                pointBuoyancy.CalculateForces(
                    out buoyancyValues,
                    oceanSampleArray,
                    meshConfig,
                    rb);
                break;
            case BuoyancyModel.Volume:
                // pointBuoyancy.CalculateSubmergedVolume(sumbergedVolumes, sumbergedCentroids);
                break;
            case BuoyancyModel.Partitioned:
                // pointBuoyancy.CalculateSubmergedVolume(sumbergedVolumes, sumbergedCentroids);
                break;
        }

        return buoyancyValues;
    }

    void DispatchSampleKernel(Vector3 pos)
    {
        int threadGroups = Mathf.CeilToInt(patchResolution / 8.0f);
        switch (oceanManager.activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerCompute.SetVector("_PatchCentre", pos);
                gerstnerCompute.SetInt("_PatchResolution", patchResolution);
                gerstnerCompute.SetFloat("_PatchSize", patchSize);
                gerstnerCompute.Dispatch(1, threadGroups, threadGroups, 1);
                break;
            case WaveGenerator.FFT:
                fftCompute.SetVector("_PatchCentre", pos);
                fftCompute.SetInt("_PatchResolution", patchResolution);
                fftCompute.SetFloat("_PatchSize", patchSize);
                fftCompute.Dispatch(6, threadGroups, threadGroups, 1);
                break;
            case WaveGenerator.Hybrid:
                fftCompute.SetVector("_PatchCentre", pos);
                fftCompute.SetInt("_PatchResolution", patchResolution);
                fftCompute.SetFloat("_PatchSize", patchSize);
                fftCompute.Dispatch(6, threadGroups, threadGroups, 1);

                gerstnerCompute.SetVector("_PatchCentre", pos);
                gerstnerCompute.SetInt("_PatchResolution", patchResolution);
                gerstnerCompute.SetFloat("_PatchSize", patchSize);
                gerstnerCompute.Dispatch(1, threadGroups, threadGroups, 1);
                break;
        }
    }

    void GetSample(Vector3 patchCentre)
    {
        readbackPending = true;

        requestedPatchCentre = patchCentre;

        switch (oceanManager.activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                AsyncGPUReadback.Request(gerstnerSampleBuffer, request => OnGPUReadback(request));
                break;
            case WaveGenerator.FFT:
                AsyncGPUReadback.Request(fftSampleBuffer, request => OnGPUReadback(request));
                break;
            case WaveGenerator.Hybrid:
                // Get the Gerstner sample
                AsyncGPUReadback.Request(gerstnerSampleBuffer, request => OnMultipleGPUReadback(request));
                // Get the FFT sample
                AsyncGPUReadback.Request(fftSampleBuffer, request => OnMultipleGPUReadback(request));
                break;
        }
    }

    void OnGPUReadback(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;

        if (request.hasError)
            return;

        var samples = request.GetData<OceanSample>();
        oceanSampleArray = samples.ToArray();
        // Update the mesh config patch centre
        meshConfig.patchCentre = requestedPatchCentre;
    }

    void OnMultipleGPUReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
            return;

        var samples = request.GetData<OceanSample>();

        // Create a new array for the first readback and assign the sample values
        if (multipleReadbacksPending == 0)
            oceanSampleArray = samples.ToArray();
        // For the second readback, add the new sample values to the existing array
        else
        {
            OceanSample[] sampleArray = samples.ToArray();
            for (int i = 0; i < oceanSampleArray.Length; i++)
            {
                oceanSampleArray[i].height += sampleArray[i].height;
                oceanSampleArray[i].normal = (oceanSampleArray[i].normal + sampleArray[i].normal).normalized;
            }
        }

        // Update the mesh config patch centre
        meshConfig.patchCentre = requestedPatchCentre;

        // Don't change the readback pending flag unless both have been returned
        if (multipleReadbacksPending < 2)
        {
            multipleReadbacksPending++;
            readbackPending = true;
        }
        else
        {
            multipleReadbacksPending = 0;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (oceanSampleArray != null && oceanSampleArray.Length > 0)
        {
            for (int i = 0; i < oceanSampleArray.Length; i++)
            {
                Vector3 pos = GetVertexPos(i);
                pos.y = oceanSampleArray[i].height;
                Gizmos.DrawSphere(pos, 0.1f);
            }
        }
    }

    Vector3 GetVertexPos(int index)
    {
        int yCoord = index / patchResolution;
        int xCoord = index % patchResolution;

        // Change from 0 to (Resolution-1) space to -0.5 to 0.5 space
        float y = (yCoord + 0.5f) / patchResolution - 0.5f;
        float x = (xCoord + 0.5f) / patchResolution - 0.5f;

        Vector3 relativePos = new Vector3(x * meshConfig.patchSize, 0f, y * meshConfig.patchSize);
        Vector3 pos = relativePos + meshConfig.patchCentre;
        return pos;
    }
}
