using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static BuoyancySolver;
using UnityEngine.Rendering;
using System.IO;
using static GerstnerGeneration;

public class OceanGerstnerTest : MonoBehaviour
{
    TestManager testManager;
    OceanManager oceanManager;
    GerstnerGeneration gerstnerGeneration;
    TextMeshProUGUI text;
    [SerializeField] ComputeShader gerstnerCompute;
    [SerializeField] float recordTime = 30.0f;
    [SerializeField] float textUpdateInterval = 0.1f;
    [SerializeField] Vector3 patchCentre = new Vector3(100f, 0f, 100f);
    float accumulatedTime = 0f;
    float textAccumulatedTime = 0f;
    bool testing = false;
    List<SurfaceMeasurement> measurements = new List<SurfaceMeasurement>();
    ComputeBuffer gerstnerSampleBuffer;
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

    public void Initialise()
    {
        testManager = GetComponent<TestManager>();
        oceanManager = testManager.oceanManager;
        gerstnerGeneration = oceanManager.GetComponent<GerstnerGeneration>();
        text = testManager.text;
        text.gameObject.SetActive(false);

        // Upload the compute buffer to the compute shader
        int stride = Marshal.SizeOf(typeof(OceanSample));
        patchResolution = testManager.buoyancySolver.patchResolution;
        patchSize = testManager.buoyancySolver.patchSize;
        gerstnerSampleBuffer = new ComputeBuffer(patchResolution * patchResolution, stride);
        gerstnerCompute.SetBuffer(1, "_OceanSamples", gerstnerSampleBuffer);

        // Disable the vessel game object
        testManager.buoyancySolver.gameObject.SetActive(false);
    }
    public void RunTest()
    {
        // Set the active wave generator and initialise
        oceanManager.activeWaveGenerator = WaveGenerator.Gerstner;
        oceanManager.Initialise();

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

            // Dispatch the gerstner kernel
            DispatchSampleKernel();

            // Get the sample if you are not waiting on the previous sample
            if (!readbackPending)
                GetSample();
        }
    }

    void DispatchSampleKernel()
    {
        int threadGroups = Mathf.CeilToInt(patchResolution / 8.0f);

        gerstnerCompute.SetVector("_PatchCentre", patchCentre);
        gerstnerCompute.SetInt("_PatchResolution", patchResolution);
        gerstnerCompute.SetFloat("_PatchSize", patchSize);
        gerstnerCompute.Dispatch(1, threadGroups, threadGroups, 1);
    }

    void GetSample()
    {
        readbackPending = true;

        AsyncGPUReadback.Request(
            gerstnerSampleBuffer,
            request =>
            {
                if (request.hasError)
                {
                    readbackPending = false;
                    return;
                }

                var samples = request.GetData<OceanSample>();
                OceanSample[] oceanSampleArray = samples.ToArray();
                FillArray(oceanSampleArray);
                readbackPending = false;
            });
    }

    void FillArray(OceanSample[] samples)
    {
        float generatedHeight;
        float predictedHeight;
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
            predictedHeight = CalculateGerstnerHeight(new Vector2(x, z));
            measurements.Add(new SurfaceMeasurement(x, z, generatedHeight, predictedHeight));
        }

        CalculateError();

        // Store the position at the bottom left corner of the patch
        generatedHeight = samples[0].height;
        x = -0.5f + patchCentre.x;
        z = -0.5f + patchCentre.z;
        predictedHeight = CalculateGerstnerHeight(new Vector2(x, z));
        pointMeasurements.Add(new PointMeasurement(Time.time, generatedHeight, predictedHeight));
    }

    float CalculateGerstnerHeight(Vector2 pos)
    {
        Vector3 displacement = new Vector3(0f, 0f, 0f);

        for (int i = 0; i < gerstnerGeneration.waveCount; i++)
        {
            Wave wave = gerstnerGeneration.waves[i];

            float phase =
                wave.waveNumber * Vector2.Dot(wave.direction, pos)
                - wave.omega * gerstnerGeneration.simulationTime;

            displacement.x +=
                wave.steepness *
                wave.amplitude *
                wave.direction.x *
                Mathf.Cos(phase);

            displacement.y +=
                wave.amplitude *
                Mathf.Sin(phase);

            displacement.z +=
                wave.steepness *
                wave.amplitude *
                wave.direction.y *
                Mathf.Cos(phase);
        }
        return displacement.y;
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
        string fileName = "ocean_test_gerstner.csv";
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
}