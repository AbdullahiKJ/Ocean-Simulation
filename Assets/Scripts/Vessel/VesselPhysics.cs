using UnityEngine;

public class VesselPhysics : MonoBehaviour
{
    [SerializeField] BuoyancySolver buoyancySolver;
    [SerializeField] VesselController vesselController;
    [SerializeField] float maxSpeed = 10f;
    Rigidbody rb;
    BuoyancySolver.Buoyancy buoyancyValues;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        vesselController = GetComponent<VesselController>();
    }

    void FixedUpdate()
    {
        buoyancyValues = buoyancySolver.CalculateBuoyancy();
        ApplyBuoyancy();
        ApplyPropulsion();
        LimitSpeed();
    }

    void ApplyPropulsion()
    {
        // Exit early if the vessel is not submerged
        if (buoyancyValues.buoyancyForces == null)
            return;

        // Get the buoyancy force counts and exit early if the vessel is not submerged
        int pointCount = buoyancyValues.buoyancyForces.Length;
        if (pointCount == 0)
            return;


        // Get the propulsion force
        Vector3 propulsionForce = vesselController.PropulsionForce;

        // Apply the propulsion force to the vessel's rigidbody
        rb.AddForce(propulsionForce, ForceMode.Acceleration);

    }

    void ApplyBuoyancy()
    {
        if (buoyancyValues.buoyancyForces == null)
            return;

        int pointCount = buoyancyValues.buoyancyForces.Length;
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = buoyancyValues.submergedCentroids[i];

            // Add the buoyancy force
            rb.AddForceAtPosition(buoyancyValues.buoyancyForces[i], pos, ForceMode.Force);

            // Add the water drag force against velocity
            rb.AddForce(buoyancyValues.waterDrag[i], ForceMode.Force);

            // Add the water angular drag torque against angular velocity
            rb.AddTorque(buoyancyValues.angularDrag[i], ForceMode.Force);
        }
    }

    void LimitSpeed()
    {
        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                rb.linearVelocity.y,
                horizontalVelocity.z
            );
        }
    }
}
