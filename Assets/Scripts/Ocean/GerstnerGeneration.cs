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
    [SerializeField] Vector2 amplitudeRange = Vector2.zero;
    [SerializeField] Vector2 wavelengthRange = Vector2.zero;
    [SerializeField] Vector2 steepnessRange = Vector2.zero;
    [SerializeField] Vector2 directionRange = Vector2.zero;

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
        for (int i = 0; i < waveCount; i++)
        {
            float wavelength = Random.Range(wavelengthRange.x, wavelengthRange.y);
            float amplitude = Random.Range(amplitudeRange.x, amplitudeRange.y);
            float directionDeg = Random.Range(directionRange.x, directionRange.y);
            float directionRad = directionDeg * Mathf.Deg2Rad;
            float steepness = Random.Range(steepnessRange.x, steepnessRange.y);
            float waveNumber = 2 * Mathf.PI / wavelength;

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
