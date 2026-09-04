using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static BuoyancySolver;
using UnityEngine.Rendering;
using System.IO;

public class OceanFFTTest : MonoBehaviour
{
    TestManager testManager;
    OceanManager oceanManager;
    FFTGeneration fftGeneration;
    FFTCPU fftCPU;
    TextMeshProUGUI text;
    [SerializeField] ComputeShader fftCompute;
    [SerializeField] float recordTime = 30.0f;
    [SerializeField] float textUpdateInterval = 0.1f;
    [SerializeField] Vector3 patchCentre = new Vector3(100f, 0f, 100f);
    float accumulatedTime = 0f;
    float textAccumulatedTime = 0f;
    bool testing = false;
    List<SurfaceMeasurement> measurements = new List<SurfaceMeasurement>();
    ComputeBuffer fftSampleBuffer;
    int patchResolution;
    float patchSize;
    bool readbackPending = false;
    float errorSum = 0f;
    float squareErrorSum = 0f;
    public int count;
    public struct SurfaceMeasurement
    {
        public float x;
        public float z;
        public float generatedHeight;
        public float predictedHeight;

        public SurfaceMeasurement(
            float x,
            float z,
            float generatedHeight,
            float predictedHeight)
        {
            this.x = x;
            this.z = z;
            this.generatedHeight = generatedHeight;
            this.predictedHeight = predictedHeight;
        }
    }
    List<PointMeasurement> pointMeasurements = new List<PointMeasurement>();
    public struct PointMeasurement
    {
        public float time;
        public float generatedHeight;
        public float predictedHeight;
        public PointMeasurement(float time, float generatedHeight, float predictedHeight)
        {
            this.time = time;
            this.generatedHeight = generatedHeight;
            this.predictedHeight = predictedHeight;
        }

    }
    float sampleTime = 0f;

    public void Initialise()
    {
        testManager = GetComponent<TestManager>();
        oceanManager = testManager.oceanManager;
        fftGeneration = oceanManager.GetComponent<FFTGeneration>();
        fftCPU = GetComponent<FFTCPU>();
        text = testManager.text;
        text.gameObject.SetActive(false);

        // Upload the compute buffer to the compute shader
        int stride = Marshal.SizeOf(typeof(OceanSample));
        patchResolution = testManager.buoyancySolver.patchResolution;
        patchSize = testManager.buoyancySolver.patchSize;
        fftSampleBuffer = new ComputeBuffer(patchResolution * patchResolution, stride);
        fftCompute.SetBuffer(6, "_OceanSamples", fftSampleBuffer);

        // Disable the vessel game object
        testManager.buoyancySolver.gameObject.SetActive(false);
    }
    public void RunTest()
    {
        // Set the active wave generator and initialise
        oceanManager.activeWaveGenerator = WaveGenerator.FFT;
        oceanManager.Initialise();

        // Assign the FFTCPU parameters
        SetCPUUniforms();
        fftCPU._Spectrums = fftGeneration.spectrums;
        fftCPU.SIZE = (uint)patchResolution;
        fftCPU.LOG_SIZE = (uint)Mathf.Log(fftCPU.SIZE, 2);
        fftCPU.fftGroupBuffer = new Vector4[2, fftCPU.SIZE];

        // Create the initial spectrum texture, spectrum texture and displacement texture
        fftCPU._InitialSpectrumTextures = new Vector4[patchResolution, patchResolution, fftGeneration.layerCount * 2];
        fftCPU._DisplacementTextures = new Vector4[patchResolution, patchResolution, fftGeneration.layerCount * 2];
        fftCPU._SpectrumTextures = new Vector4[patchResolution, patchResolution, fftGeneration.layerCount * 4];

        // Initialize the FFT spectrum
        for (int i = 0; i < patchResolution; i++)
        {
            for (int j = 0; j < patchResolution; j++)
            {
                Vector3Int id = new Vector3Int(i, j, 0);
                fftCPU.CS_InitializeSpectrum(id);
            }
        }

        // Then pack the conjugates
        for (int i = 0; i < patchResolution; i++)
        {
            for (int j = 0; j < patchResolution; j++)
            {
                Vector3Int id = new Vector3Int(i, j, 0);
                fftCPU.CS_PackSpectrumConjugate(id);
            }
        }

        // Start the test
        testing = true;

        // Enable the text game object
        text.gameObject.SetActive(true);
        return;
    }

    // Tests
    // 1) Use the wave equation summation to predict the height of the wave and compare with the generated value
    // Get the height of the entire patch, predict the expected height 
    // calculate the mean average error and the root mean square error
    // 2) when the recording time has elapsed, store the position and height of both the generated and calculated surfaces in a csv
    // Plot a graph of height vs distance in excel
    // 

    void Update()
    {
        if (testing)
        {
            textAccumulatedTime += Time.unscaledDeltaTime;
            accumulatedTime += Time.unscaledDeltaTime;

            if (textAccumulatedTime >= textUpdateInterval)
            {
                // Update the text
                float roundedTime = recordTime - accumulatedTime;
                roundedTime = (float)Math.Round(roundedTime, 1);
                text.text = "Time Remaining: " + roundedTime + "s";
                textAccumulatedTime = 0f;
            }

            if (accumulatedTime >= recordTime)
            {
                // Reset trackers for the next interval
                accumulatedTime = 0.0f;
                textAccumulatedTime = 0.0f;

                // Log the findings
                WriteCSV();

                if (count > 0)
                {
                    float meanError = errorSum / count;
                    float rmse = Mathf.Sqrt(squareErrorSum / count);
                    Debug.Log("Mean Error: " + meanError
                        + "\nRMSE: " + rmse
                    );
                }
                else
                {
                    Debug.LogWarning("No samples were collected, so RMSE and mean error cannot be calculated.");
                }

                // Disable the text
                text.gameObject.SetActive(false);

                // Update the testing flag
                testing = false;
            }

            // Dispatch the fft kernel
            DispatchSampleKernel();

            // Get the sample if you are not waiting on the previous sample
            if (!readbackPending)
            {
                sampleTime = Time.time;
                GetSample();
            }
        }
    }

    void DispatchSampleKernel()
    {
        int threadGroups = Mathf.CeilToInt(patchResolution / 8.0f);

        fftCompute.SetVector("_PatchCentre", patchCentre);
        fftCompute.SetInt("_PatchResolution", patchResolution);
        fftCompute.SetFloat("_PatchSize", patchSize);
        fftCompute.Dispatch(6, threadGroups, threadGroups, 1);
    }

    void GetSample()
    {
        readbackPending = true;

        AsyncGPUReadback.Request(
            fftSampleBuffer,
            request =>
            {
                if (request.hasError)
                {
                    readbackPending = false;
                    return;
                }

                var samples = request.GetData<OceanSample>();
                OceanSample[] oceanSampleArray = samples.ToArray();
                RunFFT(oceanSampleArray);
                readbackPending = false;
            });
    }

    void RunFFT(OceanSample[] samples)
    {
        UpdateFFT();

        float generatedHeight;
        float predictedHeight = 0f;
        float x;
        float z;
        // Clear the measurements array first
        measurements.Clear();

        for (int i = 0; i < samples.Length; i++)
        {
            // Get the x and z grid positions based off the index
            float iX = (float)(i % patchResolution) / (patchResolution - 1);
            float iZ = (float)(i / patchResolution) / (patchResolution - 1);

            // Get the local x and z positions relative to the patch centre
            float localX = (iX - 0.5f) * patchSize;
            float localZ = (iZ - 0.5f) * patchSize;

            // Get the world x and y positions
            x = localX + patchCentre.x;
            z = localZ + patchCentre.z;

            generatedHeight = samples[i].height;
            // Get the predicted height
            int xIndex = i % patchResolution;
            int zIndex = i / patchResolution;
            predictedHeight = 0f;
            for (int layer = 0; layer < fftGeneration.layerCount; layer++)
            {
                predictedHeight += fftCPU._DisplacementTextures[xIndex, zIndex, layer].y;
            }
            measurements.Add(new SurfaceMeasurement(x, z, generatedHeight, predictedHeight));
        }

        CalculateError();

        // Store the position at the bottom left corner of the patch
        generatedHeight = samples[0].height;
        x = -0.5f + patchCentre.x;
        z = -0.5f + patchCentre.z;
        predictedHeight = 0f;
        for (int layer = 0; layer < fftGeneration.layerCount; layer++)
        {
            predictedHeight += fftCPU._DisplacementTextures[0, 0, layer].y;
        }
        pointMeasurements.Add(new PointMeasurement(sampleTime, generatedHeight, predictedHeight));
    }

    void UpdateFFT()
    {
        // Update the FFTCPU uniforms
        SetCPUUniforms();

        // 1. Generate the time-dependent spectrum for every point
        for (int x = 0; x < patchResolution; x++)
        {
            for (int y = 0; y < patchResolution; y++)
            {
                Vector3Int id = new Vector3Int(x, y, 0);
                fftCPU.CS_UpdateSpectrumForFFT(id);
            }
        }

        fftCPU._FourierTarget = fftCPU._SpectrumTextures;

        // 2. Perform horizontal FFT for every row
        for (int y = 0; y < patchResolution; y++)
        {
            fftCPU.CS_HorizontalFFT(y);
        }

        // 3. Perform vertical FFT for every column
        for (int x = 0; x < patchResolution; x++)
        {
            fftCPU.CS_VerticalFFT(x);
        }

        // 4. Convert Fourier data into displacement maps
        for (int x = 0; x < patchResolution; x++)
        {
            for (int y = 0; y < patchResolution; y++)
            {
                Vector3Int id = new Vector3Int(x, y, 0);
                fftCPU.CS_AssembleMaps(id);
            }
        }
    }

    void CalculateError()
    {
        foreach (SurfaceMeasurement point in measurements)
        {
            count++;
            float error = point.predictedHeight - point.generatedHeight;
            errorSum += error;
            squareErrorSum += error * error;
        }
    }

    // Write to a csv
    void WriteCSV()
    {
        string directory = Path.Combine(
            Application.dataPath,
            "Scripts/Testing/TestResults"
        );
        string fileName = "ocean_test_fft.csv";
        string filePath = Path.Combine(directory, fileName);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            // CSV header
            writer.WriteLine(
                "time,PredictedHeight,GeneratedHeight"
            );

            // Data
            foreach (PointMeasurement point in pointMeasurements)
            {
                writer.WriteLine(
                    $"{point.time:F3}," +
                    $"{point.predictedHeight:F4}," +
                    $"{point.generatedHeight:F4},"
                );
            }
        }

        Debug.Log("Ocean test CSV saved to: " + filePath);
    }

    void SetCPUUniforms()
    {
        fftCPU._LayerCount = (uint)fftGeneration.layerCount;
        fftCPU._Lambda = fftGeneration.lambda;
        fftCPU._FrameTime = sampleTime * fftGeneration.speed;
        fftCPU._Gravity = fftGeneration.gravity;
        fftCPU._RepeatTime = fftGeneration.repeatTime;
        fftCPU._N = (uint)patchResolution;
        fftCPU._Seed = (uint)fftGeneration.seed;
        fftCPU._LengthScale0 = (uint)fftGeneration.lengthScale1;
        fftCPU._LengthScale1 = (uint)fftGeneration.lengthScale2;
        fftCPU._MeshSize = patchSize;
        fftCPU._Depth = fftGeneration.depth;
        fftCPU._LowCutoff = fftGeneration.lowCutoff;
        fftCPU._HighCutoff = fftGeneration.highCutoff;
    }
}