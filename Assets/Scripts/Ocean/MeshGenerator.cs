using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    int resolution = 512;
    float size = 100.0f;
    Mesh mesh;
    Vector3[] vertices;
    Vector3[] normals;
    Vector2[] uvs;

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            mesh = GenerateMesh(resolution, size);
            meshFilter.mesh = mesh;
        }
    }

    // Generate a mesh based on the resolution and size and return it
    public Mesh GenerateMesh(int resolution, float size)
    {
        this.resolution = resolution;
        this.size = size;

        // Instantiate the mesh and set the format to support large meshes
        mesh = new Mesh();
        mesh.name = "Ocean Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        // Create arrays for the vertices, normals and UV
        vertices = new Vector3[(resolution * resolution)];
        normals = new Vector3[(resolution * resolution)];
        uvs = new Vector2[(resolution * resolution)];

        GenerateVertices();

        // Assign the vertices, normals and UVs to the mesh
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;

        GenerateTriangles();
        return mesh;
    }

    // Generate the vertices of the mesh based on the resolution and size
    public void GenerateVertices()
    {
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                float x = (float)i / (resolution - 1) * size;
                float z = (float)j / (resolution - 1) * size;
                vertices[i * resolution + j] = new Vector3(x, 0, z);

                // Generate the normals and UVs for each vertex
                GenerateNormals(i * resolution + j);
                GenerateUVs(i, j);
            }
        }
    }

    // Generate the triangles of the mesh
    public void GenerateTriangles()
    {
        int[] triangles = new int[((resolution - 1) * (resolution - 1) * 6)];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int bottomLeft = vert;
                int bottomRight = vert + 1;
                int topLeft = vert + resolution;
                int topRight = vert + resolution + 1;

                triangles[tris + 0] = bottomLeft;
                triangles[tris + 1] = bottomRight;
                triangles[tris + 2] = topLeft;

                triangles[tris + 3] = bottomRight;
                triangles[tris + 4] = topRight;
                triangles[tris + 5] = topLeft;

                vert++;
                tris += 6;
            }
            vert++;
        }

        // Assign the triangles to the mesh
        mesh.triangles = triangles;
    }

    // Generate the normals of the mesh (upwards facing for a flat plane)
    public void GenerateNormals(int index)
    {
        normals[index] = Vector3.up;
    }

    // Generate the UVs of the mesh, map each vertex to its realtive position to the corners of the mesh (0,0) to (1,1)
    public void GenerateUVs(int i, int j)
    {
        float x = (float)i / (resolution - 1);
        float z = (float)j / (resolution - 1);
        uvs[i * resolution + j] = new Vector2(x, z);
    }
}
