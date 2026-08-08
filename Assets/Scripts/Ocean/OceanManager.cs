using UnityEngine;

public class OceanManager : MonoBehaviour
{
    [SerializeField] GerstnerGeneration gerstnerGeneration;
    [SerializeField] FFTGeneration fftGeneration;
    [SerializeField] WaveGenerator activeWaveGenerator;
    [SerializeField] OceanRenderer oceanRenderer;

    // Wave generation configuration
    [SerializeField] MeshGenerator meshGenerator;
    [SerializeField] int meshResolution = 0;
    [SerializeField] float meshSize = 0;
    [SerializeField] float hybridScale = 1.0f;

    void Start()
    {
        Initialise();
    }

    public void Initialise()
    {
        // Generate the mesh for the ocean surface
        Mesh newMesh = meshGenerator.GenerateMesh(meshResolution, meshSize);
        oceanRenderer.Initialise(newMesh, activeWaveGenerator, meshResolution, hybridScale);
        SetWaveGenerator();
    }

    void Update()
    {
        switch (activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerGeneration.UpdateGenerator();
                break;
            case WaveGenerator.FFT:
                fftGeneration.UpdateGenerator();
                break;
            case WaveGenerator.Hybrid:
                gerstnerGeneration.UpdateGenerator();
                fftGeneration.UpdateGenerator();
                break;
        }

        oceanRenderer.Render();
    }

    // Set the active wave generator based on the selected option
    void SetWaveGenerator()
    {
        switch (activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerGeneration.Initialise(meshResolution, meshSize);
                break;
            case WaveGenerator.FFT:
                fftGeneration.Initialise(meshResolution, meshSize);
                break;
            case WaveGenerator.Hybrid:
                gerstnerGeneration.Initialise(meshResolution, meshSize);
                fftGeneration.Initialise(meshResolution, meshSize);
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
