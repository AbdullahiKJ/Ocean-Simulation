using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuoyancyTest : MonoBehaviour
{
    TestManager testManager;
    OceanManager oceanManager;
    BuoyancySolver buoyancySolver;
    TextMeshProUGUI text;
    [SerializeField] float massRecordTime = 10.0f;
    [SerializeField] float rotRecordTime = 30.0f;
    [SerializeField] float textUpdateInterval = 0.1f;
    [SerializeField] Vector2 massRange;
    [SerializeField] float massTests = 5f;
    [SerializeField] float initialRotation = 30f;
    [SerializeField] float maxRotCheck = 5f;
    float maxAngle;
    float prevAngle1;
    float prevAngle2;
    float accumulatedTime = 0f;
    float textAccumulatedTime = 0f;
    bool massTesting = false;
    bool rotTesting = false;
    GameObject boatGO;
    GameObject cubeGO;
    List<float> masses;
    Rigidbody rb;
    Transform vessel;

    public void Initialise()
    {
        // Get the test manager, ocean manager and buoyancy solver
        testManager = GetComponent<TestManager>();
        oceanManager = testManager.oceanManager;
        buoyancySolver = testManager.buoyancySolver;
        // Get the text component for displaying test timers
        text = testManager.text;
        text.gameObject.SetActive(false);
        // Get the boat game object and cube game object and set their active status
        boatGO = GameObject.Find("boat");
        if (boatGO != null)
            boatGO.SetActive(false);
        cubeGO = GameObject.Find("Cube");
        if (cubeGO != null)
            cubeGO.SetActive(true);
        rb = buoyancySolver.gameObject.GetComponent<Rigidbody>();
        vessel = buoyancySolver.transform;
    }
    public void RunTest(BuoyancyModel buoyancyModel)
    {
        // Set the wave generator to flat and initialise
        oceanManager.activeWaveGenerator = WaveGenerator.Flat;
        oceanManager.Initialise();

        // Initialise the buoyancy model
        buoyancySolver.activeModel = buoyancyModel;

        // Initilaise the masses array
        masses = new List<float>();
        masses.Add(massRange[0]);
        float interval = (massRange[1] - massRange[0]) / (massTests - 1);
        for (int i = 1; i < massTests; i++)
        {
            masses.Add(masses[0] + interval * i);
        }

        // Set the mass of the cube
        buoyancySolver.vesselMass = masses[0];
        rb.mass = masses[0];

        // Volume = 1000m3
        // For 20% submersion
        // mass * g(9.81) = ro(1000) * g(9.81) * v(1000*0.2)
        // mass = 2*10^5 

        // Start the test
        massTesting = true;

        // Enable the text game object
        text.gameObject.SetActive(true);
        return;
    }

    void Update()
    {
        if (massTesting)
        {
            textAccumulatedTime += Time.unscaledDeltaTime;
            accumulatedTime += Time.unscaledDeltaTime;

            if (masses.Count > 0)
            {
                if (textAccumulatedTime >= textUpdateInterval)
                {
                    // Update the text
                    float roundedTime = massRecordTime - accumulatedTime;
                    roundedTime = (float)Math.Round(roundedTime, 1);
                    text.text = "Time Remaining: " + roundedTime + "s";
                    textAccumulatedTime = 0f;
                }

                if (accumulatedTime >= massRecordTime)
                {
                    // Reset trackers for the next interval
                    accumulatedTime = 0.0f;
                    textAccumulatedTime = 0.0f;

                    // Log the submerged depth
                    float currentHeight = cubeGO.transform.position.y;
                    float submergedDepth = 0.5f * cubeGO.transform.localScale.y - currentHeight;
                    submergedDepth = Mathf.Clamp(submergedDepth, 0.0f, 10.0f);
                    Debug.Log("Mass: " + masses[0] + "\nSubmerged depth: " + submergedDepth);

                    // Remove the mass at index 0 from the list
                    masses.RemoveAt(0);

                    // Assign the new mass
                    if (masses.Count > 0)
                        rb.mass = masses[0];
                }
            }
            else
            {
                // Update the testing flags
                massTesting = false;
                rotTesting = true;
                StartRotationTest();
            }
        }
        if (rotTesting)
        {
            textAccumulatedTime += Time.unscaledDeltaTime;
            accumulatedTime += Time.unscaledDeltaTime;

            if (textAccumulatedTime >= textUpdateInterval)
            {
                // Update the text
                float roundedTime = (float)Math.Round(accumulatedTime, 1);
                text.text = "Time Taken: " + roundedTime + "s";
                textAccumulatedTime = 0f;
            }

            // Get the angle between the upward facing cube face and the world up vector
            float currentAbsAngle = Vector3.Angle(vessel.up, Vector3.up);
            if (currentAbsAngle > initialRotation)
                currentAbsAngle = Vector3.Angle(-vessel.forward, Vector3.up);


            // Exit if the angle is repeated
            if (currentAbsAngle == prevAngle1)
                return;

            // Only update the max angle if it lies at a peak i.e, it is greater than the values before and after it
            if (prevAngle1 > prevAngle2 && prevAngle1 > currentAbsAngle)
            {
                maxAngle = prevAngle1;
            }

            if (maxAngle <= maxRotCheck)
            {
                // Disable the text
                text.gameObject.SetActive(false);

                // Update the testing flags
                massTesting = false;
                rotTesting = false;

                // Log the time taken
                Debug.Log("Time taken to settle: " + accumulatedTime);
            }

            // Update the previous angles
            prevAngle2 = prevAngle1;
            prevAngle1 = currentAbsAngle;
        }
    }

    void StartRotationTest()
    {
        // Reset the mass
        rb.mass = massRange[0];
        // Move the cube up and rotate along the x axis
        Vector3 currentPos = vessel.position;
        currentPos.y = 10f;
        vessel.position = currentPos;
        Vector3 newRotation = new Vector3(initialRotation, 0f, 0f);
        vessel.localEulerAngles = newRotation;

        // Assign initial values for the max angle and previous angles
        prevAngle1 = initialRotation;
        prevAngle2 = prevAngle1;
        maxAngle = initialRotation;
    }
}