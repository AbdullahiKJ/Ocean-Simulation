using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class VesselTest : MonoBehaviour
{
    TestManager testManager;
    OceanManager oceanManager;
    BuoyancySolver buoyancySolver;
    Rigidbody rb;
    TextMeshProUGUI text;
    [SerializeField] float recordTime = 30.0f;
    [SerializeField] float recordInterval = 0.1f;
    [SerializeField] float textUpdateInterval = 0.1f;
    float accumulatedTime = 0f;
    float textAccumulatedTime = 0f;
    float recordAccumulatedTime = 0f;
    bool testing = false;
    Transform vesselParent;
    List<WaveMeasurement> measurements = new List<WaveMeasurement>();
    int index;
    string fileName;

    public struct WaveMeasurement
    {
        public float time;
        public float vesselHeight;
        public float waterHeight;

        public WaveMeasurement(
            float time,
            float vesselHeight,
            float waterHeight)
        {
            this.time = time;
            this.vesselHeight = vesselHeight;
            this.waterHeight = waterHeight;
        }
    }
    public void Initialise()
    {
        testManager = GetComponent<TestManager>();
        oceanManager = testManager.oceanManager;
        buoyancySolver = testManager.buoyancySolver;
        vesselParent = buoyancySolver.transform;
        rb = buoyancySolver.GetComponent<Rigidbody>();
        text = testManager.text;
    }
    public void RunTest(WaveGenerator waveGenerator, BuoyancyModel buoyancyModel)
    {
        StartCoroutine(StartTestAfterInitialisation(waveGenerator, buoyancyModel));
    }

    private IEnumerator StartTestAfterInitialisation(
    WaveGenerator waveGenerator,
    BuoyancyModel buoyancyModel)
    {
        // Set the wave generator and initialise
        oceanManager.activeWaveGenerator = waveGenerator;
        oceanManager.Initialise();

        // Initialise the buoyancy model
        buoyancySolver.activeModel = buoyancyModel;

        // Set the file name for csv writing
        fileName = $"vessel_test_{waveGenerator}_{buoyancyModel}.csv";

        // Wait until the ocean sample array exists and contains data
        yield return new WaitUntil(() =>
            buoyancySolver.oceanSampleArray != null &&
            buoyancySolver.oceanSampleArray.Length > 0);

        // Get the index to sample the ocean
        index = VolumeBuoyancy.GetClosestVertex(buoyancySolver.meshConfig, Vector3.zero);

        // Restrict the movement and rotation of the test vessel to vertical movement only
        rb.constraints =
            RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationZ |
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY;

        // Start the test
        testing = true;

        // Enable the text game object
        text.gameObject.SetActive(true);
    }

    void Update()
    {
        if (testing)
        {
            accumulatedTime += Time.unscaledDeltaTime;
            textAccumulatedTime += Time.unscaledDeltaTime;
            recordAccumulatedTime += Time.unscaledDeltaTime;

            if (textAccumulatedTime >= textUpdateInterval)
            {
                // Update the text
                float roundedTime = recordTime - accumulatedTime;
                roundedTime = (float)Math.Round(roundedTime, 1);
                text.text = "Time Remaining: " + roundedTime + "s";
                textAccumulatedTime = 0f;
            }

            if (recordAccumulatedTime >= recordInterval)
            {
                // Record the vessel and ocean heights
                float vesselHeight = vesselParent.position.y;
                float waterHeight = buoyancySolver.oceanSampleArray[index].height;
                measurements.Add(new WaveMeasurement(
                    accumulatedTime,
                    vesselHeight,
                    waterHeight
                ));

                // Reset the accumulated timer
                recordAccumulatedTime = 0f;
            }

            if (accumulatedTime >= recordTime)
            {
                // Reset trackers for the next interval
                accumulatedTime = 0.0f;
                textAccumulatedTime = 0.0f;

                // Store the recorded data in a csv
                WriteCSV();

                // Disable the text
                text.gameObject.SetActive(false);

                // Update the testing flag
                testing = false;
            }
        }
    }

    // Write to a csv
    void WriteCSV()
    {
        string directory = Path.Combine(
            Application.dataPath,
            "Scripts/Testing/TestResults"
        );
        string filePath = Path.Combine(directory, fileName);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            // CSV header
            writer.WriteLine(
                "Time,VesselHeight,WaterHeight"
            );

            // Data
            foreach (WaveMeasurement measurement in measurements)
            {
                writer.WriteLine(
                    $"{measurement.time:F3}," +
                    $"{measurement.vesselHeight:F4}," +
                    $"{measurement.waterHeight:F4},"
                );
            }
        }

        Debug.Log("Vessel test CSV saved to: " + filePath);
    }
}