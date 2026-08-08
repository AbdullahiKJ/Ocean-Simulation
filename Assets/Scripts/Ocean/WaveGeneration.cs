using UnityEngine;
interface IWaveGeneration
{
    void Initialise(int meshResolution, float meshSize, WavePreset preset);
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