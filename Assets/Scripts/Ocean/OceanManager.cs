using UnityEngine;

public class OceanManager : MonoBehaviour
{
    [SerializeField] FlatGeneration flatGeneration;
    [SerializeField] GerstnerGeneration gerstnerGeneration;
    [SerializeField] FFTGeneration fftGeneration;
    public WaveGenerator activeWaveGenerator;
    [SerializeField] OceanRenderer oceanRenderer;

    // Wave generation configuration
    [SerializeField] MeshGenerator meshGenerator;
    public int meshResolution = 0;
    public float meshSize = 0;
    [SerializeField] float hybridScale = 1.0f;
    [SerializeField] WavePreset wavePreset = null;

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
            case WaveGenerator.Flat:
                flatGeneration.UpdateGenerator();
                break;
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
            case WaveGenerator.Flat:
                flatGeneration.Initialise(meshResolution, meshSize, wavePreset);
                break;
            case WaveGenerator.Gerstner:
                gerstnerGeneration.Initialise(meshResolution, meshSize, wavePreset);
                break;
            case WaveGenerator.FFT:
                fftGeneration.Initialise(meshResolution, meshSize, wavePreset);
                break;
            case WaveGenerator.Hybrid:
                gerstnerGeneration.Initialise(meshResolution, meshSize, wavePreset);
                fftGeneration.Initialise(meshResolution, meshSize, wavePreset);
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
