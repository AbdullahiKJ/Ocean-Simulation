using UnityEngine;

public class VesselPhysics : MonoBehaviour
{
    [SerializeField] BuoyancySolver buoyancySolver;
    Rigidbody rb;
    BuoyancySolver.Buoyancy buoyancyValues;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        buoyancyValues = buoyancySolver.CalculateBuoyancy();
        ApplyBuoyancy();
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
            rb.AddForceAtPosition(buoyancyValues.buoyancyForces[i], pos, ForceMode.Acceleration);

            // Add the water drag force against velocity
            rb.AddForce(buoyancyValues.waterDrag[i], ForceMode.VelocityChange);

            // Add the water angular drag torque against angular velocity
            rb.AddTorque(buoyancyValues.angularDrag[i], ForceMode.VelocityChange);
        }
    }
}
