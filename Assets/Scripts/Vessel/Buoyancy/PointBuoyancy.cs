using UnityEngine;

public class PointBuoyancy : MonoBehaviour
{
    [SerializeField] Transform[] buoyancyPoints;
    [SerializeField] float buoyancyStrength = 350f;
    [Range(0.01f, 30f)]
    [SerializeField] float submersionDepth;
    [SerializeField] float waterDrag;
    [SerializeField] float waterAngularDrag;

    public void CalculateForces(
        out BuoyancySolver.Buoyancy buoyancyValues,
        BuoyancySolver.OceanSample[] oceanSamples,
        BuoyancySolver.MeshConfiguration meshConfig,
        Rigidbody rb
        )
    {
        buoyancyValues = new BuoyancySolver.Buoyancy();

        // Create arrays for the buoyancy force and submerged centroids
        int pointCount = buoyancyPoints.Length;
        buoyancyValues.buoyancyForces = new Vector3[pointCount];
        buoyancyValues.submergedCentroids = new Vector3[pointCount];
        buoyancyValues.waterDrag = new Vector3[pointCount];
        buoyancyValues.angularDrag = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            // Identify the closest point on the ocean sample
            Vector3 currentPos = buoyancyPoints[i].position;
            int sampleIndex = GetClosestVertex(meshConfig, currentPos);

            // Assign the centroid position to the buoyancy values
            buoyancyValues.submergedCentroids[i] = buoyancyPoints[i].position;

            // Get the surface height and normal
            float oceanHeight = oceanSamples[sampleIndex].height;
            Vector3 oceanNormal = oceanSamples[sampleIndex].normal;

            // Calculate the submerged depth
            float depth = oceanHeight - buoyancyPoints[i].position.y;

            // Calculate the displacement multiplier based on the submerged depth
            float displacementMultiplier = Mathf.Clamp01(depth) / submersionDepth * buoyancyStrength;

            // Calculate buoyancy forces
            buoyancyValues.buoyancyForces[i] = -Physics.gravity * displacementMultiplier / pointCount;

            // Calculate the water drag
            buoyancyValues.waterDrag[i] = displacementMultiplier * -rb.linearVelocity * waterDrag * Time.fixedDeltaTime;

            // Calculate the angular water drag
            buoyancyValues.angularDrag[i] = displacementMultiplier * -rb.angularVelocity * waterAngularDrag * Time.fixedDeltaTime;
        }
        return;
    }

    int GetClosestVertex(BuoyancySolver.MeshConfiguration meshConfig, Vector3 pos)
    {
        // Get the position relative to the centre of the patch
        Vector2 localPos = new Vector2(
            pos.x - meshConfig.patchCentre.x,
            pos.z - meshConfig.patchCentre.z
        );

        // Convert the world position to 0-1 patch coordinates
        float u = localPos.x / meshConfig.patchSize + 0.5f;
        float v = localPos.y / meshConfig.patchSize + 0.5f;

        // Convert to the nearest sample coordinate
        int x = Mathf.RoundToInt(u * meshConfig.patchResolution - 0.5f);
        int y = Mathf.RoundToInt(v * meshConfig.patchResolution - 0.5f);

        // Clamp to the patch to avoid index errors
        x = Mathf.Clamp(x, 0, meshConfig.patchResolution - 1);
        y = Mathf.Clamp(y, 0, meshConfig.patchResolution - 1);

        // Convert 2D grid coordinate to array index
        return y * meshConfig.patchResolution + x;
    }
}
