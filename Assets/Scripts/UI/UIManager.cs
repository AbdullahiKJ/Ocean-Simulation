using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject startMenuPanel;
    [SerializeField] GameObject optionsMenuPanel;
    [SerializeField] GameObject controlsPanel;
    [SerializeField] GameObject confirmClosePanel;

    [Header("Button Groups")]
    [SerializeField] SelectableButtonGroup simulationGroup;
    [SerializeField] SelectableButtonGroup oceanGroup;
    [SerializeField] SelectableButtonGroup buoyancyGroup;


    [Header("Simulation")]
    [SerializeField] GameObject vesselGO;
    [SerializeField] Vector3 defaultPos = new Vector3(100f, 0f, 100f);
    [SerializeField] OceanManager oceanManager;
    BuoyancySolver buoyancySolver;
    [SerializeField] WaveGenerator waveGenerator;
    [SerializeField] BuoyancyModel buoyancyModel;

    void Awake()
    {
        // Disable all panels except the start menu panel
        startMenuPanel.SetActive(true);
        optionsMenuPanel.SetActive(false);
        controlsPanel.SetActive(false);
        confirmClosePanel.SetActive(false);

        // Lock the vessel x and z position
        Rigidbody vesselRigidbody = vesselGO.GetComponent<Rigidbody>();
        vesselRigidbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;

        // Get the buoyancy solver from the ocean manager
        buoyancySolver = vesselGO.GetComponent<BuoyancySolver>();

        // Set the default ocean simulation and buoyancy model
        oceanManager.activeWaveGenerator = waveGenerator;
        buoyancySolver.activeModel = buoyancyModel;
        oceanGroup.SelectButton(oceanGroup.GetButtonByIndex((int)waveGenerator - 1));
        buoyancyGroup.SelectButton(buoyancyGroup.GetButtonByIndex((int)buoyancyModel - 1));
    }

    void OnSettings(InputValue inputValue)
    {
        ToggleMenu();
    }

    void ToggleMenu()
    {
        // Toggle the options menu panel
        bool isActive = optionsMenuPanel.activeSelf;
        optionsMenuPanel.SetActive(!isActive);

        // Set the time scale to 0 to pause the simulation and 1 to resume the simulation
        Time.timeScale = isActive ? 1f : 0f;
    }

    public void StartGame(int option)
    {
        switch (option)
        {
            case 0:
                // Start the game with only the ocean simulation
                vesselGO.SetActive(false);
                CameraTransition(0);
                break;
            case 1:
                // Start the game with the ocean and vessel simulation
                vesselGO.SetActive(true);
                CameraTransition(1);
                break;
        }

        // Disable the start menu panel
        startMenuPanel.SetActive(false);

        // Enable the controls panel
        controlsPanel.SetActive(true);
    }

    public void CameraTransition(int option)
    {
        switch (option)
        {
            case 0:
                // Transition to the ocean simulation camera
                break;
            case 1:
                // Transition to the vessel camera
                break;
        }
    }

    public void ResetSimulation()
    {
        // Change the active ocean simulation
        int oceanIndex = oceanGroup.ActiveButtonIndex;
        switch (oceanIndex)
        {
            case 0:
                oceanManager.activeWaveGenerator = WaveGenerator.Gerstner;
                break;
            case 1:
                oceanManager.activeWaveGenerator = WaveGenerator.FFT;
                break;
            case 2:
                oceanManager.activeWaveGenerator = WaveGenerator.Hybrid;
                break;
        }

        // Change the active buoyancy model
        int buoyancyIndex = buoyancyGroup.ActiveButtonIndex;
        switch (buoyancyIndex)
        {
            case 0:
                buoyancySolver.activeModel = BuoyancyModel.Voxel;
                break;
            case 1:
                buoyancySolver.activeModel = BuoyancyModel.Volume;
                break;
            case 2:
                buoyancySolver.activeModel = BuoyancyModel.Partitioned;
                break;
        }

        // Reinitialise the ocean simulation
        oceanManager.Initialise();

        // If the vessel simulation is active, reset the vessel position and rotation
        int simulationIndex = simulationGroup.ActiveButtonIndex;
        if (simulationIndex == 1)
        {
            vesselGO.transform.position = defaultPos;
            vesselGO.transform.rotation = Quaternion.identity;
        }
        else
        {
            // If the vessel simulation is not active, disable the vessel game object
            vesselGO.SetActive(false);
        }

        // Exit the options menu panel
        ToggleMenu();
    }

    public void ExitGame()
    {
        // Close the application
        Application.Quit();
    }
}
