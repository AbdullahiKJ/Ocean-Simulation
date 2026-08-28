using System;
using UnityEngine;

public class OceanRenderer : MonoBehaviour
{
    [SerializeField] ComputeShader gerstnerComputeShader;
    [SerializeField] ComputeShader fftComputeShader;
    [SerializeField] Material oceanMaterial;
    MeshFilter meshFilter;
    Mesh mesh;

    ComputeBuffer originalVertexBuffer;
    ComputeBuffer displacedVertexBuffer;
    ComputeBuffer normalsBuffer;

    Vector3[] originalVertices;

    WaveGenerator activeWaveGenerator;
    int resolution;
    float hybridScale;
    int gerstnerKernel;
    int sampleKernel;

    // Displacement and slope spectrum textures
    RenderTexture displacementTexture;
    RenderTexture slopeTexture;

    public void Initialise(Mesh generatedMesh, WaveGenerator activeGenerator, int resolution, float hybridScale)
    {
        this.resolution = resolution;
        this.hybridScale = hybridScale;

        // Get the mesh filter and mesh components and assign the generated mesh to the mesh filter
        meshFilter = GetComponentInChildren<MeshFilter>();
        mesh = generatedMesh;
        meshFilter.mesh = mesh;

        // Get the original vertices
        originalVertices = mesh.vertices;

        // Upload parameters for the Gerstner, FFT and Flat wave generators
        UploadGerstnerParameters();
        UploadFFTParameters();
        if (activeGenerator == WaveGenerator.Flat)
            UploadFlatParameters();

        // Assign the active wave generator
        activeWaveGenerator = activeGenerator;

        // Assign the active wave generator to the material
        int generatorInt = 0;
        if (activeGenerator == WaveGenerator.Gerstner)
            generatorInt = 0;
        else if (activeGenerator == WaveGenerator.FFT)
            generatorInt = 1;
        else if (activeGenerator == WaveGenerator.Hybrid)
            generatorInt = 2;
        else if (activeGenerator == WaveGenerator.Flat)
            generatorInt = 3;
        oceanMaterial.SetInt("_ActiveWaveGenerator", generatorInt);

        // Assign the hybrid scale to the material
        oceanMaterial.SetFloat("_HybridScale", hybridScale);
    }

    void UploadGerstnerParameters()
    {
        // Get the Gerstner main kernel and sample kernel
        gerstnerKernel = gerstnerComputeShader.FindKernel("GerstnerMain");
        sampleKernel = gerstnerComputeShader.FindKernel("CS_ExtractOceanPatch");

        // Create compute buffers for the original and displaced vertices and normals and asssign the data to them
        originalVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        displacedVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        normalsBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        originalVertexBuffer.SetData(originalVertices);
        displacedVertexBuffer.SetData(originalVertices);

        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_OriginalVertices", originalVertexBuffer);
        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_DisplacedVertices", displacedVertexBuffer);
        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_Normals", normalsBuffer);

        // Set the displacement and normal buffers for the ocean sample kernel
        gerstnerComputeShader.SetBuffer(sampleKernel, "_DisplacedVertices", displacedVertexBuffer);
        gerstnerComputeShader.SetBuffer(sampleKernel, "_Normals", normalsBuffer);

        oceanMaterial.SetBuffer("_DisplacedVertices", displacedVertexBuffer);
        oceanMaterial.SetBuffer("_Normals", normalsBuffer);
    }

    // todo: parameters are currently uploaded from the fft generation script
    void UploadFFTParameters()
    {
    }

    void UploadFlatParameters()
    {
        // Release buffers before creating new ones
        if (displacedVertexBuffer != null)
        {
            displacedVertexBuffer.Dispose();
            displacedVertexBuffer = null;
        }

        if (normalsBuffer != null)
        {
            normalsBuffer.Dispose();
            normalsBuffer = null;
        }

        // Create compute buffers for the displaced vertices and normals
        displacedVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        normalsBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);

        // Create an array filled with the up vector
        Vector3[] normalsArray = new Vector3[originalVertices.Length];
        Array.Fill(normalsArray, Vector3.up);

        // Assign the original vertices and normals to their respective buffers
        displacedVertexBuffer.SetData(originalVertices);
        normalsBuffer.SetData(normalsArray);

        oceanMaterial.SetBuffer("_DisplacedVertices", displacedVertexBuffer);
        oceanMaterial.SetBuffer("_Normals", normalsBuffer);
    }

    public void Render()
    {
        int threadGroups = Mathf.CeilToInt(originalVertices.Length / 64.0f);

        switch (activeWaveGenerator)
        {
            case WaveGenerator.Gerstner:
                gerstnerComputeShader.Dispatch(gerstnerKernel, threadGroups, 1, 1);
                break;
            case WaveGenerator.FFT:
                break;
            case WaveGenerator.Hybrid:
                gerstnerComputeShader.Dispatch(gerstnerKernel, threadGroups, 1, 1);
                break;
        }
    }
}