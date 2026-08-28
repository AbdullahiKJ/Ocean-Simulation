using System;
using TMPro;
using UnityEngine;

public class PerformanceTest : MonoBehaviour
{
    TestManager testManager;
    OceanManager oceanManager;
    BuoyancySolver buoyancySolver;
    TextMeshProUGUI text;
    [SerializeField] float recordTime = 30.0f;
    [SerializeField] float textUpdateInterval = 0.1f;
    int frameCount = 0;
    float accumulatedTime = 0f;
    float textAccumulatedTime = 0f;
    bool testStarted = false;

    public void Initialise()
    {
        testManager = GetComponent<TestManager>();
        oceanManager = testManager.oceanManager;
        buoyancySolver = testManager.buoyancySolver;
        text = testManager.text;
        text.gameObject.SetActive(false);
    }
    public void RunTest(WaveGenerator waveGenerator, BuoyancyModel buoyancyModel, bool testVessel)
    {
        // Set the wave generator and initialise
        oceanManager.activeWaveGenerator = waveGenerator;
        oceanManager.Initialise();

        // Initialise the buoyancy model
        if (testVessel)
        {
            buoyancySolver.activeModel = buoyancyModel;
        }
        else
        {
            // Disable the vessel game object
            GameObject vessel = buoyancySolver.gameObject;
            vessel.SetActive(false);
        }

        // Start the test
        testStarted = true;

        // Enable the text game object
        text.gameObject.SetActive(true);
        return;
    }

    void Update()
    {
        if (testStarted)
        {
            textAccumulatedTime += Time.unscaledDeltaTime;
            accumulatedTime += Time.unscaledDeltaTime;
            frameCount++;

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
                // Calculate the actual average FPS over this interval
                float averageFps = frameCount / accumulatedTime;

                // Average time per frame in seconds
                float averageFrameTimeSeconds = accumulatedTime / frameCount;

                // Convert to milliseconds
                float averageFrameTimeMs = averageFrameTimeSeconds * 1000f;

                // Reset trackers for the next interval
                accumulatedTime = 0.0f;
                textAccumulatedTime = 0.0f;
                frameCount = 0;

                // Log the frame rate
                Debug.Log("Average FPS: " + averageFps);
                Debug.Log("Average frame time: " + averageFrameTimeMs);

                // Disable the text
                text.gameObject.SetActive(false);

                // Update the testing flag
                testStarted = false;
            }
        }
    }
}