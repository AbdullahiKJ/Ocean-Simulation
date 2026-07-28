using UnityEngine;

public class OceanManager : MonoBehaviour
{
    float simulationTime = 0.0f;
    [SerializeField] GerstnerGeneration gerstnerGeneration;
    [SerializeField] FFTGeneration fftGeneration;
    [SerializeField] WaveGenerator activeWaveGenerator;
    [SerializeField] ComputeShader oceanShader;
    [SerializeField] OceanRenderer oceanRenderer;
    ComputeBuffer waveBuffer;

    // Wave generation configuration
    [SerializeField] MeshGenerator meshGenerator;
    [SerializeField] int meshResolution = 0;
    [SerializeField] float meshSize = 0;

    void Start()
    {
        Initialise();
    }

    void Initialise()
    {
        // Generate the mesh for the ocean surface
        Mesh newMesh = meshGenerator.GenerateMesh(meshResolution, meshSize);
        oceanRenderer.Initialise(newMesh);
        SetWaveGenerator();
    }

    void Update()
    {
        simulationTime += Time.deltaTime;
        switch (activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerGeneration.UpdateGenerator();
                gerstnerGeneration.UploadToShader(oceanShader);
                oceanRenderer.Render();
                break;
            case WaveGenerator.FFT:
                // fftGeneration.UpdateGenerator();
                break;
            case WaveGenerator.Hybrid:
                gerstnerGeneration.UpdateGenerator();
                // fftGeneration.UpdateGenerator();
                break;
        }

        // Calculate horizontal displacement
        // Add horizontal displacement to the height map
        // Pass the height map to the renderer
    }

    // Set the active wave generator based on the selected option
    void SetWaveGenerator()
    {
        switch (activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerGeneration.Initialise();
                break;
            case WaveGenerator.FFT:
                // fftGeneration.Initialise();
                break;
            case WaveGenerator.Hybrid:
                gerstnerGeneration.Initialise();
                // fftGeneration.Initialise();
                break;
        }
    }

    float SampleHeight()
    {
        return 0.0f;
    }

    Vector3 SampleNormal()
    {
        return Vector3.up;
    }
}
