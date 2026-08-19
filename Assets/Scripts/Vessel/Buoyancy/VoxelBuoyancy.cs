using System;
using UnityEngine;

public class VoxelBuoyancy : MonoBehaviour
{
    [Header("Buoyancy Sim Settings")]
    [Range(0.1f, 1.0f)]
    public float normalizedVoxelSize = 0.1f;
    public float waterDragCoefficient = 1.0f;

    // General properties
    [SerializeField] Collider cachedCollider;
    private BuoyancySolver buoyancySolver;
    private Voxel[,,] voxels;
    private Vector3 voxelSize;
    private int voxelsPerAxis = 0;

    // Voxel Struct
    private struct Voxel
    {
        public Vector3 position { get; }

        public Voxel(Vector3 position)
        {
            this.position = position;
        }
    };

    // Most of the code is referenced from Garrett Gunnell's water and dbrizov's buoyancy repos
    // Largely referenced from https://github.com/dbrizov/NaughtyWaterBuoyancy/blob/master/Assets/NaughtyWaterBuoyancy/Scripts/Core/FloatingObject.cs
    private void CreateVoxels()
    {
        transform.rotation = Quaternion.identity;

        Bounds bounds = cachedCollider.bounds;
        voxelSize = bounds.size * normalizedVoxelSize;
        voxelsPerAxis = Mathf.RoundToInt(1.0f / normalizedVoxelSize);
        voxels = new Voxel[voxelsPerAxis, voxelsPerAxis, voxelsPerAxis];

        for (int x = 0; x < voxelsPerAxis; ++x)
        {
            for (int y = 0; y < voxelsPerAxis; ++y)
            {
                for (int z = 0; z < voxelsPerAxis; ++z)
                {
                    Vector3 point = voxelSize;
                    point.Scale(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                    point += bounds.min;

                    voxels[x, y, z] = new Voxel(transform.InverseTransformPoint(point));
                }
            }
        }
    }

    void OnEnable()
    {
        buoyancySolver = GetComponent<BuoyancySolver>();
        CreateVoxels();
    }

    public void CalculateForces(
         out BuoyancySolver.Buoyancy buoyancyValues,
         BuoyancySolver.OceanSample[] oceanSamples,
         BuoyancySolver.MeshConfiguration meshConfig,
         Rigidbody rb
         )
    {
        buoyancyValues = new BuoyancySolver.Buoyancy();

        // Create arrays for the buoyancy force and submerged centroids
        int voxelCount = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;
        buoyancyValues.buoyancyForces = new Vector3[voxelCount];
        buoyancyValues.submergedCentroids = new Vector3[voxelCount];
        buoyancyValues.waterDrag = new Vector3[voxelCount];
        buoyancyValues.angularDrag = new Vector3[voxelCount];

        if (voxels == null) return;


        float voxelVolume = voxelSize.x * voxelSize.y * voxelSize.z;
        float voxelForce = buoyancySolver.waterDensity * Physics.gravity.magnitude * voxelVolume;

        float totalSubmergedFactor = 0f;

        for (int x = 0; x < voxelsPerAxis; ++x)
        {
            for (int y = 0; y < voxelsPerAxis; ++y)
            {
                for (int z = 0; z < voxelsPerAxis; ++z)
                {
                    Vector3 worldPos = this.transform.TransformPoint(voxels[x, y, z].position);
                    int sampleIndex = GetClosestVertex(meshConfig, worldPos);

                    float waterLevel = oceanSamples[sampleIndex].height;
                    float bottom = worldPos.y - voxelSize.y * 0.5f;

                    float submergedHeight =
                        Mathf.Clamp(
                            waterLevel - bottom,
                            0f,
                            voxelSize.y
                        );

                    float submergedFactor = submergedHeight / voxelSize.y;
                    totalSubmergedFactor += submergedFactor;

                    Vector3 F = -Physics.gravity.normalized * voxelForce * submergedFactor;

                    // Assign the force value and voxel position to the buoyancy values
                    int index = x + (y * voxelsPerAxis * voxelsPerAxis) + (z * voxelsPerAxis);
                    buoyancyValues.buoyancyForces[index] = F;
                    buoyancyValues.submergedCentroids[index] = worldPos;

                    // Calculate the water drag
                    Vector3 velocity = rb.GetPointVelocity(worldPos);

                    float area =
                        Mathf.Max(
                            voxelSize.x * voxelSize.z,
                            voxelSize.x * voxelSize.y,
                            voxelSize.y * voxelSize.z
                        );

                    float submergedArea = area * submergedFactor;

                    Vector3 dragForce =
                        -0.5f *
                        buoyancySolver.waterDensity *
                        waterDragCoefficient *
                        submergedArea *
                        velocity.magnitude *
                        velocity;
                    buoyancyValues.waterDrag[index] = dragForce;
                }
            }
        }
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

    private void OnDrawGizmos()
    {
        if (this.voxels != null)
        {
            for (int x = 0; x < voxelsPerAxis; ++x)
            {
                for (int y = 0; y < voxelsPerAxis; ++y)
                {
                    for (int z = 0; z < voxelsPerAxis; ++z)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawCube(this.transform.TransformPoint(this.voxels[x, y, z].position), this.voxelSize * 0.8f);
                    }
                }
            }
        }
    }
}
