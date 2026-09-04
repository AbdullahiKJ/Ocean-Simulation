using TMPro;
using UnityEngine;

public class TestManager : MonoBehaviour
{
    PerformanceTest performanceTest;
    OceanGerstnerTest oceanGerstnerTest;
    OceanFFTTest oceanFFTTest;
    VesselTest vesselTest;
    BuoyancyTest buoyancyTest;

    [SerializeField] TestCase activeTest;
    public OceanManager oceanManager;
    public BuoyancySolver buoyancySolver;
    [SerializeField] bool runOnAwake = true;
    public TextMeshProUGUI text;

    [Header("Performance Test Settings")]
    [SerializeField] bool testVessel;
    [SerializeField] WaveGenerator waveGenerator;
    [SerializeField] BuoyancyModel buoyancyModel;
    enum TestCase
    {
        Performance,
        OceanGerstner,
        OceanFFT,
        Vessel,
        Buoyancy,
    }

    void OnEnable()
    {
        performanceTest = GetComponent<PerformanceTest>();
        oceanGerstnerTest = GetComponent<OceanGerstnerTest>();
        oceanFFTTest = GetComponent<OceanFFTTest>();
        vesselTest = GetComponent<VesselTest>();
        buoyancyTest = GetComponent<BuoyancyTest>();

        // Initialise test parameters
        InitialiseTest();

        // Run tests on play
        if (runOnAwake)
            RunActiveTest();
    }

    void InitialiseTest()
    {
        switch (activeTest)
        {
            case TestCase.Performance:
                performanceTest.Initialise();
                break;
            case TestCase.OceanGerstner:
                oceanGerstnerTest.Initialise();
                break;
            case TestCase.OceanFFT:
                oceanFFTTest.Initialise();
                break;
            case TestCase.Vessel:
                vesselTest.Initialise();
                break;
            case TestCase.Buoyancy:
                buoyancyTest.Initialise();
                break;
        }
    }

    public void RunActiveTest()
    {
        switch (activeTest)
        {
            case TestCase.Performance:
                performanceTest.RunTest(waveGenerator, buoyancyModel, testVessel);
                break;
            case TestCase.OceanGerstner:
                oceanGerstnerTest.RunTest();
                break;
            case TestCase.OceanFFT:
                oceanFFTTest.RunTest();
                break;
            case TestCase.Vessel:
                vesselTest.RunTest(waveGenerator, buoyancyModel);
                break;
            case TestCase.Buoyancy:
                buoyancyTest.RunTest(buoyancyModel);
                break;
        }
    }
}