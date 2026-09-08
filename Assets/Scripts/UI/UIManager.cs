using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject startMenuPanel;
    [SerializeField] GameObject optionsMenuPanel;
    [SerializeField] GameObject controlsPanel;
    [SerializeField] GameObject freeControlsPanel;
    [SerializeField] GameObject confirmClosePanel;

    [Header("Button Groups")]
    [SerializeField] SelectableButtonGroup simulationGroup;
    [SerializeField] SelectableButtonGroup oceanGroup;
    [SerializeField] SelectableButtonGroup buoyancyGroup;

    [Header("Simulation")]
    [SerializeField] GameObject vesselGO;
    [SerializeField] OceanManager oceanManager;
    [SerializeField] GameObject spaceColliders;
    [SerializeField] GameObject freeFlyGO;
    [SerializeField] Vector3 defaultOceanPos = new Vector3(100f, 10f, 100f);
    [SerializeField] Vector3 defaultVesselPos = new Vector3(100f, 0f, 100f);
    BuoyancySolver buoyancySolver;
    Rigidbody vesselRigidbody;
    [SerializeField] WaveGenerator waveGenerator;
    [SerializeField] BuoyancyModel buoyancyModel;

    [Header("Buoyancy")]
    [SerializeField] float voxelMass = 50000f;
    [SerializeField] float volumeMass = 50000f;
    [SerializeField] float partitionedMass = 50000f;

    [Header("Cameras")]
    [SerializeField] CinemachineCamera freeCamera;
    [SerializeField] CinemachineCamera vesselCamera;
    [SerializeField] CinemachineCamera startMenuCamera;

    public bool IsMenuOpen => optionsMenuPanel.activeSelf;

    void Awake()
    {
        // Set the target frame rate for the demo to 60 frames per second
        Application.targetFrameRate = 60;

        // Disable all panels except the start menu panel
        startMenuPanel.SetActive(true);
        optionsMenuPanel.SetActive(false);
        controlsPanel.SetActive(false);
        freeControlsPanel.SetActive(false);
        confirmClosePanel.SetActive(false);

        // Lock the vessel x and z position
        vesselRigidbody = vesselGO.GetComponent<Rigidbody>();
        vesselRigidbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;

        // Get the buoyancy solver from the ocean manager
        buoyancySolver = vesselGO.GetComponent<BuoyancySolver>();

        // Set the default ocean simulation and buoyancy model
        oceanManager.activeWaveGenerator = waveGenerator;
        buoyancySolver.activeModel = buoyancyModel;

        // Set the mass for the active buoyancy model
        SetBuoyancyMass(buoyancyModel);

        // Set the selected button to the default wave generation and buoyancy models
        oceanGroup.SelectButton(oceanGroup.GetButtonByIndex((int)waveGenerator - 1));
        buoyancyGroup.SelectButton(buoyancyGroup.GetButtonByIndex((int)buoyancyModel - 1));

        // Set the default camera to the start menu camera
        CameraTransition(2);
    }

    void OnSettings(InputValue inputValue)
    {
        ToggleMenu();
    }

    void SetBuoyancyMass(BuoyancyModel model)
    {
        switch (buoyancySolver.activeModel)
        {
            case BuoyancyModel.Voxel:
                buoyancySolver.vesselMass = voxelMass;
                vesselRigidbody.mass = voxelMass;
                break;
            case BuoyancyModel.Volume:
                buoyancySolver.vesselMass = volumeMass;
                vesselRigidbody.mass = volumeMass;
                break;
            case BuoyancyModel.Partitioned:
                buoyancySolver.vesselMass = partitionedMass;
                vesselRigidbody.mass = partitionedMass;
                break;
        }
    }

    public void ToggleMenu()
    {
        // Toggle the options menu panel
        bool isActive = optionsMenuPanel.activeSelf;
        bool isNowActive = !isActive;
        optionsMenuPanel.SetActive(isNowActive);

        // Set the time scale to 0 to pause the simulation and 1 to resume the simulation
        Time.timeScale = isNowActive ? 0f : 1f;

        // Show or hide the cursor
        ToggleCursor(isNowActive);
    }

    void ToggleCursor(bool isVisible)
    {
        // Lock the cursor to the centre of the screen and hide
        Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isVisible;
    }

    void ToggleCollider(bool isEnabled)
    {
        // Enable or disable the vessel collider
        spaceColliders.SetActive(isEnabled);
    }

    void ToggleFreeControlsPanel(bool isVisible)
    {
        freeControlsPanel.SetActive(isVisible);
    }

    public void StartGame(int option)
    {
        switch (option)
        {
            case 0:
                // Start the game with only the ocean simulation
                vesselGO.SetActive(false);
                CameraTransition(0);
                ToggleCollider(false);
                break;
            case 1:
                // Start the game with the ocean and vessel simulation
                vesselGO.SetActive(true);
                CameraTransition(1);
                ToggleCollider(true);
                break;
        }

        // Assign the current simulation state to the settings panel
        simulationGroup.SelectButton(option);

        // Disable the start menu panel
        startMenuPanel.SetActive(false);

        // Enable the controls panel
        controlsPanel.SetActive(true);

        // Enable the free controls panel if the free camera is active
        ToggleFreeControlsPanel(option == 0);

        // Hide the cursor
        ToggleCursor(false);

        // Remove the vessel constraints to allow movement
        vesselRigidbody.constraints = RigidbodyConstraints.None;
    }

    public void CameraTransition(int option)
    {
        switch (option)
        {
            case 0:
                // Switch to the free camera
                startMenuCamera.Priority = 0;
                freeCamera.Priority = 10;
                vesselCamera.Priority = 0;
                break;
            case 1:
                // Switch to the vessel camera
                startMenuCamera.Priority = 0;
                freeCamera.Priority = 0;
                vesselCamera.Priority = 10;
                break;
            case 2:
                // Switch to the start menu camera
                startMenuCamera.Priority = 10;
                freeCamera.Priority = 0;
                vesselCamera.Priority = 0;
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
        // Set the mass for the active buoyancy model
        SetBuoyancyMass(buoyancySolver.activeModel);

        // Reinitialise the ocean simulation and the buoyancy solver
        oceanManager.Initialise();
        buoyancySolver.Initialise();

        // If the vessel simulation is active, reset the vessel position and rotation
        int simulationIndex = simulationGroup.ActiveButtonIndex;
        if (simulationIndex == 1)
        {
            vesselGO.SetActive(true);
            vesselGO.transform.position = defaultVesselPos;
            vesselGO.transform.rotation = Quaternion.identity;
            ToggleCollider(true);
        }
        else
        {
            // If the vessel simulation is not active, disable the vessel game object
            vesselGO.SetActive(false);
            ToggleCollider(false);

            // Reset the ocean position and rotation
            freeFlyGO.transform.position = defaultOceanPos;
            freeFlyGO.transform.rotation = Quaternion.identity;
        }

        // Exit the options menu panel
        ToggleMenu();

        // Toggle the free controls panel if the free camera/ocean simulation is active
        ToggleFreeControlsPanel(simulationIndex == 0);

        // Switch to the appropriate camera based on the active simulation
        CameraTransition(simulationIndex);
    }

    public void ExitGame()
    {
        // Close the application
        Application.Quit();
    }
}
