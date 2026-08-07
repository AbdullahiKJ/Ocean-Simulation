using UnityEngine;
using System.Runtime.InteropServices;

public class GerstnerGeneration : MonoBehaviour, IWaveGeneration
{
    [SerializeField] ComputeShader gerstnerComputeShader;
    float simulationTime = 0.0f;
    ComputeBuffer waveBuffer;
    int kernel;

    Wave[] waves;
    // Wave generation configuration
    [SerializeField] int waveCount = 0;
    [SerializeField] float initialAmplitude = 1f;
    [SerializeField] float initialWavelength = 1f;
    [SerializeField] float amplitudeScaler = 1f;
    [SerializeField] float wavelengthScaler = 1f;
    [SerializeField] float steepnessParameter = 1f;
    [SerializeField] Vector2 directionRange = Vector2.zero;
    [SerializeField] float medianWavelength = 1f;
    [SerializeField] float medianAmplitude = 1f;
    [SerializeField] float wavelengthRange = 1f;

    struct Wave
    {
        // Configrable parameters
        public float amplitude;
        public float wavelength;
        public float steepness;
        public Vector2 direction;
        public float omega;
        public float waveNumber;
    }

    public void Initialise(int meshResolution, float meshSize)
    {
        waves = new Wave[waveCount];
        GenerateWaveParameters();

        // Upload the wave data to the buffer for use in the compute shader
        int stride = Marshal.SizeOf(typeof(Wave));

        waveBuffer = new ComputeBuffer(waveCount, stride);
        waveBuffer.SetData(waves);

        UploadToShader();
    }

    public float SampleHeight()
    {
        throw new System.NotImplementedException();
    }

    public Vector3 SampleNormal()
    {
        throw new System.NotImplementedException();
    }

    public void UpdateGenerator()
    {
        simulationTime += Time.deltaTime;
        gerstnerComputeShader.SetFloat("_Time", simulationTime);
    }

    // Upload the wave data to the compute shader for rendering
    public void UploadToShader()
    {
        kernel = gerstnerComputeShader.FindKernel("GerstnerMain");

        gerstnerComputeShader.SetBuffer(kernel, "_Waves", waveBuffer);

        gerstnerComputeShader.SetInt("_WaveCount", waveCount);
    }

    // Generate a list of wave parameters based on the configuration ranges
    void GenerateWaveParameters()
    {
        float wavelengthMin = medianWavelength / (1.0f + wavelengthRange);
        float wavelengthMax = medianWavelength * (1.0f + wavelengthRange);
        float ampOverLen = medianAmplitude / medianWavelength;

        for (int i = 0; i < waveCount; i++)
        {
            float wavelength = Random.Range(wavelengthMin, wavelengthMax);
            float amplitude = wavelength * ampOverLen;
            float directionDeg = Random.Range(directionRange.x, directionRange.y);
            float directionRad = directionDeg * Mathf.Deg2Rad;
            float waveNumber = 2 * Mathf.PI / wavelength;
            float steepness = steepnessParameter / (waveNumber * amplitude);

            waves[i] = new Wave
            {
                amplitude = amplitude,
                wavelength = wavelength,
                steepness = steepness,

                // Calculate derived parameters
                waveNumber = waveNumber,
                direction = new Vector2(Mathf.Cos(directionRad), Mathf.Sin(directionRad)),
                omega = Mathf.Sqrt(9.81f * waveNumber),
            };
        }
    }
}
