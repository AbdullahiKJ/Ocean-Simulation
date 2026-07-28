using UnityEngine;

public class OceanRenderer : MonoBehaviour
{
    [SerializeField] ComputeShader oceanShader;
    [SerializeField] Material oceanMaterial;
    MeshFilter meshFilter;
    Mesh mesh;

    ComputeBuffer originalVertexBuffer;
    ComputeBuffer displacedVertexBuffer;
    ComputeBuffer normalsBuffer;

    Vector3[] originalVertices;
    Vector3[] displacedVertices;

    int kernel;

    public void Initialise(Mesh generatedMesh)
    {
        // Get the mesh filter and mesh components and assign the generated mesh to the mesh filter
        meshFilter = GetComponentInChildren<MeshFilter>();
        mesh = generatedMesh;
        meshFilter.mesh = mesh;

        // Get the original vertices and createa a new array for the displaced vertices
        originalVertices = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];

        kernel = oceanShader.FindKernel("CSMain");

        // Create compute buffers for the original and displaced vertices and normals and asssign the data to them
        originalVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        displacedVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        normalsBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        originalVertexBuffer.SetData(originalVertices);
        displacedVertexBuffer.SetData(originalVertices);

        oceanShader.SetBuffer(kernel, "_OriginalVertices", originalVertexBuffer);
        oceanShader.SetBuffer(kernel, "_DisplacedVertices", displacedVertexBuffer);
        oceanShader.SetBuffer(kernel, "_Normals", normalsBuffer);

        oceanMaterial.SetBuffer("_DisplacedVertices", displacedVertexBuffer);
        oceanMaterial.SetBuffer("_Normals", normalsBuffer);
    }

    public void Render()
    {
        int threadGroups = Mathf.CeilToInt(originalVertices.Length / 64.0f);

        oceanShader.Dispatch(kernel, threadGroups, 1, 1);
    }
}