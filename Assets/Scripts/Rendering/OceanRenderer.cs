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

        // Get all kernels
        gerstnerKernel = gerstnerComputeShader.FindKernel("GerstnerMain");
        advanceKernel = fftComputeShader.FindKernel("Advance");
        verticalFFTKernel = fftComputeShader.FindKernel("VerticalFFT");
        horizontalFFTKernel = fftComputeShader.FindKernel("HorizontalFFT");
        fftMainKernel = fftComputeShader.FindKernel("FFTMain");

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
        UploadFFTParameters();

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
                threadGroups = resolution / 8;
                fftComputeShader.Dispatch(advanceKernel, threadGroups, threadGroups, 1);

                RenderTexture read = displacementSpectrumA;
                RenderTexture write = displacementSpectrumB;

                RenderTexture slopeRead = slopeSpectrumA;
                RenderTexture slopeWrite = slopeSpectrumB;

                int stages = (int)Mathf.Log(resolution, 2);
                for (int stage = 0; stage < stages; stage++)
                {
                    fftComputeShader.SetTexture(verticalFFTKernel, "_DisplacementSpectrumInput", read);
                    fftComputeShader.SetTexture(verticalFFTKernel, "_DisplacementSpectrumOutput", write);

                    fftComputeShader.SetTexture(verticalFFTKernel, "_SlopeSpectrumInput", slopeRead);
                    fftComputeShader.SetTexture(verticalFFTKernel, "_SlopeSpectrumOutput", slopeWrite);

                    fftComputeShader.SetInt("_Stage", stage);
                    fftComputeShader.Dispatch(verticalFFTKernel, resolution, 1, 1);

                    // swap displacement spectrum
                    var temp = read;
                    read = write;
                    write = temp;

                    // swap slope spectrum
                    temp = slopeRead;
                    slopeRead = slopeWrite;
                    slopeWrite = temp;
                }
                for (int stage = 0; stage < stages; stage++)
                {
                    fftComputeShader.SetTexture(horizontalFFTKernel, "_DisplacementSpectrumInput", read);
                    fftComputeShader.SetTexture(horizontalFFTKernel, "_DisplacementSpectrumOutput", write);

                    fftComputeShader.SetTexture(horizontalFFTKernel, "_SlopeSpectrumInput", slopeRead);
                    fftComputeShader.SetTexture(horizontalFFTKernel, "_SlopeSpectrumOutput", slopeWrite);

                    fftComputeShader.SetInt("_Stage", stage);
                    fftComputeShader.Dispatch(horizontalFFTKernel, resolution, 1, 1);

                    // swap displacement spectrum
                    var temp = read;
                    read = write;
                    write = temp;

                    // swap slope spectrum
                    temp = slopeRead;
                    slopeRead = slopeWrite;
                    slopeWrite = temp;
                }

                // Assign the final displacement and slope spectrum read textures to the compute shader for rendering
                fftComputeShader.SetTexture(fftMainKernel, "_DisplacementSpectrumInput", read);
                fftComputeShader.SetTexture(fftMainKernel, "_SlopeSpectrumInput", slopeRead);

                // Dispatch the main kernel to compute the final displacement and slope textures
                fftComputeShader.Dispatch(fftMainKernel, threadGroups, threadGroups, 1);
                break;
            case WaveGenerator.Hybrid:
                break;
        }
    }
}