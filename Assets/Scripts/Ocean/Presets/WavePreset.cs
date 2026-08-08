using UnityEngine;

[CreateAssetMenu(fileName = "WavePreset", menuName = "Scriptable Objects/WavePreset")]
public class WavePreset : ScriptableObject
{
    public GerstnerSettings gerstner;
    public FFTSettings fft;
}

[System.Serializable]
public class GerstnerSettings
{
    public int waveCount = 0;
    public float steepnessParameter = 1f;
    public Vector2 directionRange = Vector2.zero;
    public float medianWavelength = 1f;
    public float medianAmplitude = 1f;
    public float wavelengthRange = 1f;
}

[System.Serializable]
public class FFTSettings
{
    [Header("Spectrum Settings")]
    [Range(0, 100000)]
    public int seed = 0;

    [Range(0.0f, 0.1f)]
    public float lowCutoff = 0.0001f;

    [Range(0.1f, 9000.0f)]
    public float highCutoff = 9000.0f;

    [Range(0.01f, 20.0f)]
    public float gravity = 9.81f;

    [Range(2.0f, 20.0f)]
    public float depth = 20.0f;

    [Range(0.0f, 200.0f)]
    public float repeatTime = 200.0f;

    [Range(0.0f, 5.0f)]
    public float speed = 1.0f;

    public Vector2 lambda = new Vector2(1.0f, 1.0f);

    [Range(0.0f, 10.0f)]
    public float displacementDepthFalloff = 1.0f;
    public int layerCount = 2;

    [Header("Layer One")]
    [Range(0, 2048)]
    public int lengthScale1 = 256;
    public FFTGeneration.DisplaySpectrumSettings spectrum1;
    public FFTGeneration.DisplaySpectrumSettings spectrum2;

    [Header("Layer Two")]
    [Range(0, 2048)]
    public int lengthScale2 = 256;
    public FFTGeneration.DisplaySpectrumSettings spectrum3;
    public FFTGeneration.DisplaySpectrumSettings spectrum4;

}
