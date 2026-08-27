using System.Collections.Generic;
using UnityEngine;

public class VolumeBuoyancy : MonoBehaviour
{
    [Header("Buoyancy Sim Settings")]
    [Range(0.1f, 1.0f)]
    public float waterDragCoefficient = 1.0f;
    [SerializeField] GameObject vesselParent;

    // General properties
    private BuoyancySolver buoyancySolver;
    List<Triangle> vesselTriangles = new List<Triangle>();
    Vector3 submergedCentroid;
    struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
    }
    List<Triangle> submergedTriangles;

    void OnEnable()
    {
        buoyancySolver = GetComponent<BuoyancySolver>();

        BuildTriangleList();
    }

    public void CalculateForces(
         out BuoyancySolver.Buoyancy buoyancyValues,
         BuoyancySolver.OceanSample[] oceanSamples,
         BuoyancySolver.MeshConfiguration meshConfig,
         Rigidbody rb
         )
    {
        // Create arrays for the buoyancy values
        // This method applies forces at a single point, so each array will only have 1 element
        buoyancyValues = new BuoyancySolver.Buoyancy
        {
            buoyancyForces = new Vector3[1],
            submergedCentroids = new Vector3[1],
            waterDrag = new Vector3[1],
            angularDrag = new Vector3[1]
        };

        Vector3 referencePoint = vesselParent.transform.position;
        referencePoint.y = oceanSamples[GetClosestVertex(meshConfig, vesselParent.transform.position)].height;

        submergedTriangles = GetSubmergedTriangles(oceanSamples, meshConfig);

        if (submergedTriangles.Count == 0)
            return;

        float submergedVolume;

        submergedCentroid =
            CalculateCentroidAndVolume(
                submergedTriangles,
                referencePoint,
                out submergedVolume
            );

        Vector3 buoyancyForce =
            buoyancySolver.waterDensity *
            -Physics.gravity *
            submergedVolume;

        // Calculate drag
        Vector3 dragForce = CalculateDrag(submergedTriangles, rb);

        // float maxDragForce = 100000f;
        // if (dragForce.magnitude > maxDragForce)
        // {
        //     dragForce = dragForce.normalized * maxDragForce;
        // }

        buoyancyValues.buoyancyForces[0] = buoyancyForce;

        buoyancyValues.submergedCentroids[0] = submergedCentroid;

        buoyancyValues.waterDrag[0] = dragForce;
        return;
    }

    private Vector3 CalculateCentroidAndVolume(
    List<Triangle> triangles,
    Vector3 referencePoint,
    out float volume
    )
    {
        float totalVolume = 0f;
        Vector3 centroid = Vector3.zero;

        foreach (Triangle triangle in triangles)
        {
            Vector3 a = triangle.a - referencePoint;
            Vector3 b = triangle.b - referencePoint;
            Vector3 c = triangle.c - referencePoint;

            float tetraVolume = Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;

            Vector3 tetraCentroid = (a + b + c) / 4f;

            centroid += tetraCentroid * tetraVolume;

            totalVolume += tetraVolume;
        }

        volume = Mathf.Abs(totalVolume);

        if (volume < 0.000001f)
            return referencePoint;

        return referencePoint + centroid / totalVolume;
    }

    private Vector3 CalculateDrag(List<Triangle> triangles, Rigidbody rb)
    {
        // float area = CalculateSubmergedArea(triangles);

        // Vector3 velocity =
        //     rb.GetPointVelocity(submergedCentroid);

        // float speed = velocity.magnitude;

        // if (speed < 0.01f)
        //     return Vector3.zero;

        // Vector3 drag =
        //     -0.5f *
        //     buoyancySolver.waterDensity *
        //     waterDragCoefficient *
        //     area *
        //     speed *
        //     velocity;

        // return drag;

        // todo: simpler method
        Vector3 velocity = rb.GetPointVelocity(submergedCentroid);
        // float damping = 5000f;
        return -velocity * buoyancySolver.vesselMass;
    }

    private float CalculateSubmergedArea(List<Triangle> triangles)
    {
        float totalArea = 0f;

        foreach (Triangle triangle in triangles)
        {
            Vector3 ab = triangle.b - triangle.a;
            Vector3 ac = triangle.c - triangle.a;

            float area = Vector3.Cross(ab, ac).magnitude * 0.5f;

            totalArea += area;
        }

        return totalArea;
    }
    private List<Vector3> ClipTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        BuoyancySolver.OceanSample[] oceanSamples,
        BuoyancySolver.MeshConfiguration meshConfig
    )
    {
        List<Vector3> input = new List<Vector3> { a, b, c };

        List<Vector3> output = new List<Vector3>();

        // Get the water height at each triangle vertex
        List<float> waterHeights = new List<float>();
        for (int i = 0; i < input.Count; i++)
        {
            float waterHeight =
                oceanSamples[
                    GetClosestVertex(
                        meshConfig,
                        input[i]
                    )
                ].height;
            waterHeights.Add(waterHeight);
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 current = input[i];
            Vector3 next = input[(i + 1) % 3];

            bool currentInside = current.y <= waterHeights[i];
            bool nextInside = next.y <= waterHeights[(i + 1) % 3];

            if (currentInside && nextInside)
            {
                // Add the next point if both points are below water 
                output.Add(next);
            }
            else if (currentInside && !nextInside)
            {
                // If a is inside and b is outside, add the point where the line ab intersects the water
                output.Add(
                    GetWaterIntersection(
                        current,
                        next,
                        oceanSamples,
                        meshConfig
                    )
                );
            }
            else if (!currentInside && nextInside)
            {
                // add the point where line ab intersects the water
                output.Add(
                    GetWaterIntersection(
                        current,
                        next,
                        oceanSamples,
                        meshConfig
                    )
                );
                // add point b as well
                output.Add(next);
            }
            // Do nothing if both points are above the water
        }

        return output;
    }

    private Vector3 GetWaterIntersection(
        Vector3 a,
        Vector3 b,
        BuoyancySolver.OceanSample[] oceanSamples,
        BuoyancySolver.MeshConfiguration meshConfig
    )
    {
        // This is an approximation of the water intersection
        Vector3 midPoint = Vector3.Lerp(a, b, 0.5f);

        // Get the water height at the midpoint
        float waterHeight = oceanSamples[
                    GetClosestVertex(
                        meshConfig,
                        midPoint
                    )
                ].height;

        // Assign the water height to the midpoint and return it
        midPoint.y = waterHeight;
        return midPoint;
    }

    private void AddClippedTriangles(
    List<Vector3> polygon,
    List<Triangle> result)
    {
        if (polygon.Count < 3)
            return;

        // Triangle
        if (polygon.Count == 3)
        {
            result.Add(
                new Triangle(
                    polygon[0],
                    polygon[1],
                    polygon[2]
                )
            );

            return;
        }

        // split the quad into two triangles
        if (polygon.Count == 4)
        {
            result.Add(
                new Triangle(
                    polygon[0],
                    polygon[1],
                    polygon[2]
                )
            );

            result.Add(
                new Triangle(
                    polygon[0],
                    polygon[2],
                    polygon[3]
                )
            );
        }
        return;
    }

    private List<Triangle> GetSubmergedTriangles(
        BuoyancySolver.OceanSample[] oceanSamples,
        BuoyancySolver.MeshConfiguration meshConfig
    )
    {
        List<Triangle> submerged = new List<Triangle>();

        foreach (Triangle triangle in vesselTriangles)
        {
            // Convert from local space to world space
            Vector3 a = vesselParent.transform.TransformPoint(triangle.a);
            Vector3 b = vesselParent.transform.TransformPoint(triangle.b);
            Vector3 c = vesselParent.transform.TransformPoint(triangle.c);

            List<Vector3> polygon = ClipTriangle(a, b, c, oceanSamples, meshConfig);

            AddClippedTriangles(polygon, submerged);
        }

        return submerged;
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

    private void BuildTriangleList()
    {
        vesselTriangles.Clear();

        MeshFilter[] meshFilters = vesselParent.GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;

            Vector3[] meshVertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Get the world positions of the triangle vertices
                Vector3 worldA = meshFilter.transform.TransformPoint(
                    meshVertices[triangles[i]]
                );

                Vector3 worldB = meshFilter.transform.TransformPoint(
                    meshVertices[triangles[i + 1]]
                );

                Vector3 worldC = meshFilter.transform.TransformPoint(
                    meshVertices[triangles[i + 2]]
                );

                // Convert world position into vessel-local space
                Vector3 localA =
                    vesselParent.transform.InverseTransformPoint(worldA);

                Vector3 localB =
                    vesselParent.transform.InverseTransformPoint(worldB);

                Vector3 localC =
                    vesselParent.transform.InverseTransformPoint(worldC);

                vesselTriangles.Add(new Triangle(localA, localB, localC));
            }
        }
    }
}
