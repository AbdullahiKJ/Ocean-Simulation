using TMPro;
using UnityEngine;

public class TestManager : MonoBehaviour
{
    PerformanceTest performanceTest;
    OceanTest oceanTest;
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
        Ocean,
        Vessel,
        Buoyancy,
    }

    void OnEnable()
    {
        performanceTest = GetComponent<PerformanceTest>();
        oceanTest = GetComponent<OceanTest>();
        vesselTest = GetComponent<VesselTest>();
        buoyancyTest = GetComponent<BuoyancyTest>();

        // Run tests on play
        if (runOnAwake)
            RunActiveTest();
    }
    public void RunActiveTest()
    {
        switch (activeTest)
        {
            case TestCase.Performance:
                performanceTest.RunTest(waveGenerator, buoyancyModel, testVessel);
                break;
            case TestCase.Ocean:
                oceanTest.RunTest();
                break;
            case TestCase.Vessel:
                vesselTest.RunTest();
                break;
            case TestCase.Buoyancy:
                buoyancyTest.RunTest(buoyancyModel);
                break;
        }
    }
}