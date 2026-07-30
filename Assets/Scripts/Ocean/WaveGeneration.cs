using UnityEngine;
interface IWaveGeneration
{
    void Initialise(int meshResolution, float meshSize);
    void UpdateGenerator();
    float SampleHeight();
    Vector3 SampleNormal();
}

public enum WaveGenerator
{
    Gerstner,
    FFT,
    Hybrid
}