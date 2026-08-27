using System.Collections.Generic;
using UnityEngine;
using static VolumeBuoyancy;

public class PartitionedBuoyancy : MonoBehaviour
{
    [SerializeField] GameObject vesselParent;
    BuoyancySolver buoyancySolver;
    List<Triangle>[] partitions;
    List<Triangle> vesselTriangles = new List<Triangle>();

    void OnEnable()
    {
        buoyancySolver = GetComponent<BuoyancySolver>();
        BuildTriangleList(vesselTriangles, vesselParent);
        BuildPartitions();
    }
    public void CalculateForces(
    out BuoyancySolver.Buoyancy buoyancyValues,
    BuoyancySolver.OceanSample[] oceanSamples,
    BuoyancySolver.MeshConfiguration meshConfig,
    Rigidbody rb)
    {
        buoyancyValues = new BuoyancySolver.Buoyancy
        {
            buoyancyForces = new Vector3[4],
            submergedCentroids = new Vector3[4],
            waterDrag = new Vector3[4],
            angularDrag = new Vector3[4]
        };

        for (int i = 0; i < 4; i++)
        {
            List<Triangle> submerged =
                GetSubmergedTriangles(
                    oceanSamples,
                    meshConfig,
                    partitions[i],
                    vesselParent
                );

            if (submerged.Count == 0)
                continue;

            float volume;

            Vector3 referencePoint = vesselParent.transform.position;
            referencePoint.y = oceanSamples[GetClosestVertex(meshConfig, vesselParent.transform.position)].height;

            Vector3 centroid =
                CalculateCentroidAndVolume(
                    submerged,
                    referencePoint,
                    out volume
                );

            Vector3 buoyancy =
                buoyancySolver.waterDensity *
                -Physics.gravity *
                volume;

            Vector3 drag = CalculateDrag(centroid, rb);

            buoyancyValues.buoyancyForces[i] = buoyancy;
            buoyancyValues.submergedCentroids[i] = centroid;
            buoyancyValues.waterDrag[i] = drag;
        }
    }

    private Vector3 CalculateDrag(Vector3 submergedCentroid, Rigidbody rb)
    {
        Vector3 velocity = rb.GetPointVelocity(submergedCentroid);
        return -velocity * buoyancySolver.vesselMass / 4;
    }

    // Split the vessel triangles into groups of 4
    private void BuildPartitions()
    {
        partitions = new List<Triangle>[4];

        for (int i = 0; i < 4; i++)
            partitions[i] = new List<Triangle>();

        foreach (Triangle triangle in vesselTriangles)
        {
            Vector3 localCentroid = (triangle.a + triangle.b + triangle.c) / 3f;

            // Identify which partition the triangle belongs to based on its centroid
            int partition = GetPartition(localCentroid);

            partitions[partition].Add(triangle);
        }
    }

    private int GetPartition(Vector3 local)
    {
        bool right = local.x >= 0f;
        bool front = local.z >= 0f;

        if (front && !right) return 0;
        if (front && right) return 1;
        if (!front && !right) return 2;
        return 3;
    }

    void OnDrawGizmos()
    {
        float size = 0.1f;
        if (partitions.Length > 0)
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (Triangle triangle in partitions[i])
                {
                    switch (i)
                    {
                        case 0:
                            Gizmos.color = Color.blue;
                            break;
                        case 1:
                            Gizmos.color = Color.red;
                            break;
                        case 2:
                            Gizmos.color = Color.green;
                            break;
                        case 3:
                            Gizmos.color = Color.yellow;
                            break;
                    }
                    Vector3 a = vesselParent.transform.TransformPoint(triangle.a);
                    Vector3 b = vesselParent.transform.TransformPoint(triangle.b);
                    Vector3 c = vesselParent.transform.TransformPoint(triangle.c);

                    Gizmos.DrawSphere(a, size);
                    Gizmos.DrawSphere(b, size);
                    Gizmos.DrawSphere(c, size);
                }
            }
        }
    }
}
