using UnityEngine;
interface IWaveGeneration
{
    void Initialise();
    void UpdateGenerator();
    float SampleHeight();
    Vector3 SampleNormal();
}

enum WaveGenerator
{
    Gerstner,
    FFT,
    Hybrid
}