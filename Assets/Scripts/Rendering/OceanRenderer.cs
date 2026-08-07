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
    int gerstnerKernel;
    int advanceKernel;
    int verticalFFTKernel;
    int horizontalFFTKernel;
    int fftMainKernel;

    // Displacement and slope spectrum textures
    RenderTexture displacementSpectrumA;
    RenderTexture displacementSpectrumB;
    RenderTexture slopeSpectrumA;
    RenderTexture slopeSpectrumB;
    RenderTexture displacementTexture;
    RenderTexture slopeTexture;

    public void Initialise(Mesh generatedMesh, WaveGenerator activeGenerator, int resolution)
    {
        this.resolution = resolution;

        // Get the mesh filter and mesh components and assign the generated mesh to the mesh filter
        meshFilter = GetComponentInChildren<MeshFilter>();
        mesh = generatedMesh;
        meshFilter.mesh = mesh;

        // Get the original vertices
        originalVertices = mesh.vertices;

        // todo: uncomment later
        // Get all kernels
        gerstnerKernel = gerstnerComputeShader.FindKernel("GerstnerMain");
        // advanceKernel = fftComputeShader.FindKernel("Advance");
        // verticalFFTKernel = fftComputeShader.FindKernel("VerticalFFT");
        // horizontalFFTKernel = fftComputeShader.FindKernel("HorizontalFFT");
        // fftMainKernel = fftComputeShader.FindKernel("FFTMain");

        // switch (activeGenerator)
        // {
        //     case WaveGenerator.Gerstner:
        //         UploadGerstnerParameters();
        //         break;
        //     case WaveGenerator.FFT:
        //         UploadFFTParameters();
        //         break;
        // todo: implement hybrid
        //     case WaveGenerator.Hybrid:
        //         break;
        // }
        UploadGerstnerParameters();
        // todo: uncomment later
        // UploadFFTParameters();

        // Assign the active wave generator
        activeWaveGenerator = activeGenerator;

        // Assign the active wave generator to the material
        oceanMaterial.SetInt("_ActiveWaveGenerator", activeGenerator == WaveGenerator.Gerstner ? 0 : activeGenerator == WaveGenerator.FFT ? 1 : 2);
    }

    void UploadGerstnerParameters()
    {
        // Create compute buffers for the original and displaced vertices and normals and asssign the data to them
        originalVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        displacedVertexBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        normalsBuffer = new ComputeBuffer(originalVertices.Length, sizeof(float) * 3);
        originalVertexBuffer.SetData(originalVertices);
        displacedVertexBuffer.SetData(originalVertices);

        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_OriginalVertices", originalVertexBuffer);
        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_DisplacedVertices", displacedVertexBuffer);
        gerstnerComputeShader.SetBuffer(gerstnerKernel, "_Normals", normalsBuffer);

        oceanMaterial.SetBuffer("_DisplacedVertices", displacedVertexBuffer);
        oceanMaterial.SetBuffer("_Normals", normalsBuffer);
    }

    void UploadFFTParameters()
    {
        // Create textures for the displacement and slope spectrums and textures
        displacementSpectrumA = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        displacementSpectrumA.Create();
        displacementSpectrumB = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        displacementSpectrumB.Create();

        slopeSpectrumA = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        slopeSpectrumA.Create();
        slopeSpectrumB = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        slopeSpectrumB.Create();

        displacementTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        displacementTexture.Create();
        slopeTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        slopeTexture.Create();

        fftComputeShader.SetTexture(advanceKernel, "_DisplacementSpectrumInput", displacementSpectrumA);
        fftComputeShader.SetTexture(advanceKernel, "_SlopeSpectrumInput", slopeSpectrumA);

        fftComputeShader.SetTexture(advanceKernel, "_DisplacementSpectrumOutput", displacementSpectrumA);
        fftComputeShader.SetTexture(advanceKernel, "_SlopeSpectrumOutput", slopeSpectrumA);

        fftComputeShader.SetTexture(fftMainKernel, "_DisplacementTexture", displacementTexture);
        fftComputeShader.SetTexture(fftMainKernel, "_SlopeTexture", slopeTexture);

        oceanMaterial.SetTexture("_DisplacementTexture", displacementTexture);
        oceanMaterial.SetTexture("_SlopeTexture", slopeTexture);
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
                break;
        }
    }
}