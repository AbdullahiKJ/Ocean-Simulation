using UnityEngine;
using System;

// Code is adapted from the following sources
// George Bolba's implementation of Jerry Tessendorf's Simulating Water paper
// AceRolla's (Garrett Gunnell) implementation of the Tessendorf paper which refereneces gasgiant's FFT and JONSWAP implementation

public class FFTGeneration : MonoBehaviour, IWaveGeneration
{
    [SerializeField] ComputeShader fftComputeShader;
    [SerializeField] Material oceanMaterial;

    // Wave generation Spectrum settings
    public struct SpectrumSettings
    {
        public float scale;
        public float angle;
        public float spreadBlend;
        public float swell;
        public float alpha;
        public float peakOmega;
        public float gamma;
        public float shortWavesFade;
    }

    public SpectrumSettings[] spectrums = new SpectrumSettings[4];

    [System.Serializable]
    public struct DisplaySpectrumSettings
    {
        [Range(0, 5)]
        [Tooltip("Scale of the spectrum. Higher values result in larger waves.")]
        public float scale;
        [Range(0.01f, 10000.0f)]
        [Tooltip("Wind speed affects the growth of waves, higher wind speeds produce larger waves.")]
        public float windSpeed;
        [Range(0.0f, 360.0f)]
        [Tooltip("Direction from which the wind is blowing.")]
        public float windDirection;
        [Tooltip("Fetch is the distance over water that the wind blows in a single direction. Longer fetches allow for larger waves to develop.")]
        public float fetch;
        [Range(0, 1)]
        [Tooltip("Dictates how directional the waves are relative to the wind direction"
        + "Lower values produce more chaotic wave patterns, while higher values produce waves that align with the wind direction")]
        public float spreadBlend;
        [Range(0, 1)]
        [Tooltip("Controls hwo much energy is focused in the dominant direction."
        + "Lower values produce more chaotic wave patterns, while higher values produce more parallel wave patterns")]
        public float swell;
        [Tooltip("Controls the sharpness of the peak in the wave spectrum. Higher values result in a more pronounced peak, leading to larger waves.")]
        public float peakEnhancement;
        [Tooltip("Controls shorwavelegth waves. Higher values remove choppiness and sharp peaks. Low vvalues produce rough surfaces with smaller waves.")]
        public float shortWavesFade;
    }

    [Header("Spectrum Settings")]
    [Range(0, 100000)]
    public int seed = 0;

    [Range(0.0f, 0.1f)]
    public float lowCutoff = 0.0001f;

    [Range(0.1f, 9000.0f)]
    public float highCutoff = 9000.0f;

    [Range(0.01f, 20.0f)]
    public float gravity = 9.81f;

    [Range(2.0f, 20.0f)]
    public float depth = 20.0f;

    [Range(0.0f, 200.0f)]
    public float repeatTime = 200.0f;

    [Range(0.0f, 5.0f)]
    public float speed = 1.0f;

    public Vector2 lambda = new Vector2(1.0f, 1.0f);

    [Range(0.0f, 10.0f)]
    public float displacementDepthFalloff = 1.0f;
    public int layerCount = 2;
    public bool updateSpectrum = false;

    [Header("Layer One")]
    [Range(0, 2048)]
    public int lengthScale1 = 256;
    [SerializeField] DisplaySpectrumSettings spectrum1;
    [SerializeField] DisplaySpectrumSettings spectrum2;

    [Header("Layer Two")]
    [Range(0, 2048)]
    public int lengthScale2 = 256;
    [SerializeField] DisplaySpectrumSettings spectrum3;
    [SerializeField] DisplaySpectrumSettings spectrum4;

    // Textures
    RenderTexture displacementTextures,
                slopeTextures,
                initialSpectrumTextures,
                spectrumTextures;

    private ComputeBuffer spectrumBuffer;

    // Mesh Configuration
    int resolution, logN, threadGroupsX, threadGroupsY;
    float size;

    public float SampleHeight()
    {
        throw new System.NotImplementedException();
    }

    public Vector3 SampleNormal()
    {
        throw new System.NotImplementedException();
    }

    void SetFFTUniforms()
    {
        fftComputeShader.SetInt("_LayerCount", layerCount);
        fftComputeShader.SetVector("_Lambda", lambda);
        fftComputeShader.SetFloat("_FrameTime", Time.time * speed);
        fftComputeShader.SetFloat("_Gravity", gravity);
        fftComputeShader.SetFloat("_RepeatTime", repeatTime);
        fftComputeShader.SetInt("_N", resolution);
        fftComputeShader.SetInt("_Seed", seed);
        fftComputeShader.SetInt("_LengthScale0", lengthScale1);
        fftComputeShader.SetInt("_LengthScale1", lengthScale2);
        fftComputeShader.SetFloat("_MeshSize", size);
        // fftComputeShader.SetFloat("_NormalStrength", normalStrength);
        // fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        fftComputeShader.SetFloat("_Depth", depth);
        fftComputeShader.SetFloat("_LowCutoff", lowCutoff);
        fftComputeShader.SetFloat("_HighCutoff", highCutoff);
        // fftComputeShader.SetFloat("_FoamBias", foamBias);
        // fftComputeShader.SetFloat("_FoamDecayRate", foamDecayRate);
        // fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        // fftComputeShader.SetFloat("_FoamAdd", foamAdd);
    }

    float JonswapAlpha(float fetch, float windSpeed)
    {
        return 0.076f * Mathf.Pow(gravity * fetch / windSpeed / windSpeed, -0.22f);
    }

    float JonswapPeakFrequency(float fetch, float windSpeed)
    {
        return 22 * Mathf.Pow(windSpeed * fetch / gravity / gravity, -0.33f);
    }

    void FillSpectrumStruct(DisplaySpectrumSettings displaySettings, ref SpectrumSettings computeSettings)
    {
        computeSettings.scale = displaySettings.scale;
        computeSettings.angle = displaySettings.windDirection / 180 * Mathf.PI;
        computeSettings.spreadBlend = displaySettings.spreadBlend;
        computeSettings.swell = Mathf.Clamp(displaySettings.swell, 0.01f, 1);
        computeSettings.alpha = JonswapAlpha(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.peakOmega = JonswapPeakFrequency(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.gamma = displaySettings.peakEnhancement;
        computeSettings.shortWavesFade = displaySettings.shortWavesFade;
    }

    void SetSpectrumBuffers()
    {
        FillSpectrumStruct(spectrum1, ref spectrums[0]);
        FillSpectrumStruct(spectrum2, ref spectrums[1]);
        FillSpectrumStruct(spectrum3, ref spectrums[2]);
        FillSpectrumStruct(spectrum4, ref spectrums[3]);

        spectrumBuffer.SetData(spectrums);
        fftComputeShader.SetBuffer(0, "_Spectrums", spectrumBuffer);
    }

    void InverseFFT(RenderTexture spectrumTextures)
    {
        fftComputeShader.SetTexture(3, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(3, 1, resolution, 1);
        fftComputeShader.SetTexture(4, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(4, 1, resolution, 1);
    }

    void AssignPreset(WavePreset preset)
    {
        FFTSettings fftPreset = preset.fft;
        if (preset == null)
            return;

        seed = fftPreset.seed;
        lowCutoff = fftPreset.lowCutoff;
        highCutoff = fftPreset.highCutoff;
        gravity = fftPreset.gravity;
        depth = fftPreset.depth;
        repeatTime = fftPreset.repeatTime;
        speed = fftPreset.speed;
        lambda = fftPreset.lambda;
        displacementDepthFalloff = fftPreset.displacementDepthFalloff;
        layerCount = fftPreset.layerCount;
        lengthScale1 = fftPreset.lengthScale1;
        spectrum1 = fftPreset.spectrum1;
        spectrum2 = fftPreset.spectrum2;
        lengthScale2 = fftPreset.lengthScale2;
        spectrum3 = fftPreset.spectrum3;
        spectrum4 = fftPreset.spectrum4;

    }

    public void Initialise(int meshResolution, float meshSize, WavePreset preset)
    {
        AssignPreset(preset);
        resolution = meshResolution;
        size = meshSize;

        logN = (int)Mathf.Log(resolution, 2.0f);
        threadGroupsX = Mathf.CeilToInt(resolution / 8.0f);
        threadGroupsY = Mathf.CeilToInt(resolution / 8.0f);

        // Create the render textures for the initial spectrum, displacement, slope, and spectrum data
        initialSpectrumTextures = Util.CreateRenderTex(resolution, resolution, 4, RenderTextureFormat.ARGBHalf, true);
        displacementTextures = Util.CreateRenderTex(resolution, resolution, 4, RenderTextureFormat.ARGBHalf, true);
        slopeTextures = Util.CreateRenderTex(resolution, resolution, 4, RenderTextureFormat.RGHalf, true);
        spectrumTextures = Util.CreateRenderTex(resolution, resolution, 8, RenderTextureFormat.ARGBHalf, true);

        // Create a compute buffer to hold the spectrum data
        spectrumBuffer = new ComputeBuffer(4, 8 * sizeof(float));

        SetFFTUniforms();
        SetSpectrumBuffers();

        // Compute initial JONSWAP spectrum
        fftComputeShader.SetTexture(0, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        fftComputeShader.SetTexture(1, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);

        // todo: remove later
        UploadToShader();
    }

    public void UpdateGenerator()
    {
        SetFFTUniforms();
        // debugging: this allows you to update the spectrum every frame, but it is not necessary for normal operation
        if (updateSpectrum)
        {
            SetSpectrumBuffers();
            fftComputeShader.SetTexture(0, "_InitialSpectrumTextures", initialSpectrumTextures);
            fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            fftComputeShader.SetTexture(1, "_InitialSpectrumTextures", initialSpectrumTextures);
            fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);
        }

        // Progress Spectrum For FFT
        fftComputeShader.SetTexture(2, "_InitialSpectrumTextures", initialSpectrumTextures);
        fftComputeShader.SetTexture(2, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.Dispatch(2, threadGroupsX, threadGroupsY, 1);

        // Compute FFT For Height
        InverseFFT(spectrumTextures);

        // Assemble maps
        fftComputeShader.SetTexture(5, "_DisplacementTextures", displacementTextures);
        fftComputeShader.SetTexture(5, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.SetTexture(5, "_SlopeTextures", slopeTextures);
        fftComputeShader.SetTexture(6, "_DisplacementTextures", displacementTextures);
        fftComputeShader.SetTexture(6, "_SlopeTextures", slopeTextures);
        // fftComputeShader.SetTexture(5, "_BuoyancyData", buoyancyDataTex);
        fftComputeShader.Dispatch(5, threadGroupsX, threadGroupsY, 1);

        displacementTextures.GenerateMips();
        slopeTextures.GenerateMips();

        oceanMaterial.SetTexture("_DisplacementTextures", displacementTextures);
        oceanMaterial.SetTexture("_SlopeTextures", slopeTextures);
    }

    // todo: maybe move this to the ocean renderer script
    // Upload the spectrum data to the compute shader for rendering
    public void UploadToShader()
    {
        // Find the advance kernel
        // int advanceKernel = fftComputeShader.FindKernel("Advance");

        // Assign textures and floats to the compute shader
        // fftComputeShader.SetTexture(advanceKernel, "_InitialSpectrum", initialSpectrumTexture);

        // fftComputeShader.SetFloat("_MeshResolution", resolution);
        // fftComputeShader.SetFloat("_MeshSize", size);
        // fftComputeShader.SetFloat("_RepeatTime", repeatTime);
    }
}
