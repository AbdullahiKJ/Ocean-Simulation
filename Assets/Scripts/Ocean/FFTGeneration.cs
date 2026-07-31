using UnityEngine;
using System;
using System.Numerics;

public class FFTGeneration : MonoBehaviour, IWaveGeneration
{
    [SerializeField] ComputeShader fftComputeShader;
    float GRAVITY = 9.81f;
    float PI = 3.14159265359f;
    Complex[,] initialSpectrum;
    Complex[,] conjugateSpectrum;
    Texture2D initialSpectrumTexture;
    int resolution;
    float size;

    // Wave generation configuration
    [SerializeField] float lowCutoff = 0.0f;
    [SerializeField] float highCutoff = 0.0f;
    [SerializeField] float depth = 0.0f;
    [SerializeField][Range(1.0f, 5.0f)] float scale; // Used to scale the Spectrum [1.0f, 5.0f] --> Value Range
    [SerializeField][Range(0.0f, 1.0f)] float spreadBlend; // Used to blend between agitated water motion, and windDirection [0.0f, 1.0f]
    [SerializeField][Range(0.0f, 1.0f)] float swell; // Influences wave choppines, the bigger the swell, the longer the wave length [0.0f, 1.0f]
    [SerializeField][Range(0.0f, 7.0f)] float gamma; // Defines the Spectrum Peak [0.0f, 7.0f]
    [SerializeField][Range(0.0f, 1.0f)] float shortWavesFade; // [0.0f, 1.0f]
    [SerializeField][Range(0.0f, 360.0f)] float windDirection; // [0.0f, 360.0f]
    [SerializeField][Range(0.0f, 10000.0f)] float fetch; // Distance over which Wind impacts Wave Formation [0.0f, 10000.0f]
    [SerializeField][Range(0.0f, 100.0f)] float windSpeed; // [0.0f, 100.0f]
    [SerializeField][Range(0.0f, 200.0f)] float repeatTime; // [0.0f, 200.0f]
    float angle;
    float alpha;
    float peakOmega;

    struct JonSwapParameters
    {
        float scale; // Used to scale the Spectrum [1.0f, 5.0f] --> Value Range
        float spreadBlend; // Used to blend between agitated water motion, and windDirection [0.0f, 1.0f]
        float swell; // Influences wave choppines, the bigger the swell, the longer the wave length [0.0f, 1.0f]
        float gamma; // Defines the Spectrum Peak [0.0f, 7.0f]
        float shortWavesFade; // [0.0f, 1.0f]

        float windDirection; // [0.0f, 360.0f]
        float fetch; // Distance over which Wind impacts Wave Formation [0.0f, 10000.0f]
        float windSpeed; // [0.0f, 100.0f]
        float repeatTime; // determines how quick the waves will animate, in relation to their displacement. [0.0f, 200.0f]
        float angle;
        float alpha;
        float peakOmega;
    }

    public void Initialise(int meshResolution, float meshSize)
    {
        resolution = meshResolution;
        size = meshSize;
        GenerateSpectrum(meshResolution, meshSize);
        UploadToShader();
    }

    public float SampleHeight()
    {
        throw new System.NotImplementedException();
    }

    public UnityEngine.Vector3 SampleNormal()
    {
        throw new System.NotImplementedException();
    }

    // todo: maybe remove this
    public void UpdateGenerator()
    {
    }

    // Upload the spectrum data to the compute shader for rendering
    public void UploadToShader()
    {
        int advanceKernel = fftComputeShader.FindKernel("Advance");

        // Assign textures and floats to the compute shader
        fftComputeShader.SetTexture(advanceKernel, "_InitialSpectrumTexture", initialSpectrumTexture);

        fftComputeShader.SetFloat("_MeshResolution", resolution);
        fftComputeShader.SetFloat("_MeshSize", size);
        fftComputeShader.SetFloat("_RepeatTime", repeatTime);
    }

    // The code below is adapted from George Bolba's implemetation of Jerry Tessendorf's Simulating Water paper
    float DispersionRelation(float kMag)
    {
        return Mathf.Sqrt(GRAVITY * kMag * (float)Math.Tanh(Mathf.Min(kMag * depth, 20)));
    }

    float DispersionDerivative(float kMag)
    {
        float th = (float)Math.Tanh(Mathf.Min(kMag * depth, 20));
        float ch = (float)Math.Cosh(kMag * depth);
        return GRAVITY * (depth * kMag / ch / ch + th) / DispersionRelation(kMag) / 2.0f;
    }

    float NormalizationFactor(float s)
    {
        float s2 = s * s;
        float s3 = s2 * s;
        float s4 = s3 * s;
        if (s < 5) return -0.000564f * s4 + 0.00776f * s3 - 0.044f * s2 + 0.192f * s + 0.163f;
        else return -4.80e-08f * s4 + 1.07e-05f * s3 - 9.53e-04f * s2 + 5.90e-02f * s + 3.93e-01f;
    }

    float Cosine2s(float theta, float s)
    {
        return NormalizationFactor(s) * Mathf.Pow(Mathf.Abs(Mathf.Cos(0.5f * theta)), 2.0f * s);
    }

    float SpreadPower(float omega, float peakOmega)
    {
        if (omega > peakOmega)
            return 9.77f * Mathf.Pow(Mathf.Abs(omega / peakOmega), -2.5f);
        else
            return 6.97f * Mathf.Pow(Mathf.Abs(omega / peakOmega), 5.0f);
    }

    float DirectionSpectrum(float theta, float omega)
    {
        float s = SpreadPower(omega, peakOmega) + 16 * (float)Math.Tanh(Mathf.Min(omega / peakOmega, 20)) * swell * swell;

        return Mathf.Lerp(2.0f / 3.1415f * Mathf.Cos(theta) * Mathf.Cos(theta), Cosine2s(theta - angle, s), spreadBlend);
    }

    float TMACorrection(float omega)
    {
        float omegaH = omega * Mathf.Sqrt(depth / GRAVITY);
        if (omegaH <= 1.0f)
            return 0.5f * omegaH * omegaH;
        if (omegaH < 2.0f)
            return 1.0f - 0.5f * (2.0f - omegaH) * (2.0f - omegaH);

        return 1.0f;
    }

    float JONSWAP(float omega)
    {
        angle = windDirection / 180.0f * Mathf.PI;
        alpha = 0.076f * Mathf.Pow(GRAVITY * fetch / windSpeed / windSpeed, -0.22f);
        peakOmega = 22 * Mathf.Pow(windSpeed * fetch / 9.81f / 9.81f, -0.33f);

        float sigma = (omega <= peakOmega) ? 0.07f : 0.09f;

        float r = Mathf.Exp(-(omega - peakOmega) * (omega - peakOmega) / 2.0f / sigma / sigma / peakOmega / peakOmega);

        float oneOverOmega = 1.0f / (omega + 1e-6f);
        float peakOmegaOverOmega = peakOmega / omega;
        return scale * TMACorrection(omega) * alpha * GRAVITY * GRAVITY
            * oneOverOmega * oneOverOmega * oneOverOmega * oneOverOmega * oneOverOmega
            * Mathf.Exp(-1.25f * peakOmegaOverOmega * peakOmegaOverOmega * peakOmegaOverOmega * peakOmegaOverOmega)
            * Mathf.Pow(Mathf.Abs(gamma), r);

    }

    float ShortWavesFade(float kLength)
    {
        return Mathf.Exp(-shortWavesFade * shortWavesFade * kLength * kLength);
    }

    UnityEngine.Vector2 GaussianRandom()
    {
        float u1 = UnityEngine.Random.value;
        float u2 = UnityEngine.Random.value;

        float r = Mathf.Sqrt(-2.0f * Mathf.Log(u1));
        float theta = 2.0f * Mathf.PI * u2;

        return new UnityEngine.Vector2(
            r * Mathf.Cos(theta),
            r * Mathf.Sin(theta)
        );
    }

    void GenerateSpectrum(int meshResolution, float meshSize)
    {
        float halfN = meshResolution / 2.0f;

        float deltaK = 2.0f * PI / meshSize;

        initialSpectrum = new Complex[meshResolution, meshResolution];
        conjugateSpectrum = new Complex[meshResolution, meshResolution];
        initialSpectrumTexture = new Texture2D(meshResolution, meshResolution, TextureFormat.RGBAFloat, false);

        for (int y = 0; y < meshResolution; y++)
        {
            for (int x = 0; x < meshResolution; x++)
            {
                UnityEngine.Vector2 K = new UnityEngine.Vector2(
                    (x - halfN) * deltaK,
                    (y - halfN) * deltaK
                );

                float kLength = K.magnitude;

                if (lowCutoff <= kLength && kLength <= highCutoff)
                {
                    UnityEngine.Vector2 gauss1 = GaussianRandom();
                    UnityEngine.Vector2 gauss2 = GaussianRandom();

                    float kAngle = Mathf.Atan2(K.y, K.x);
                    float omega = DispersionRelation(kLength);
                    float dOmegadk = DispersionDerivative(kLength);

                    float S = JONSWAP(omega) * DirectionSpectrum(kAngle, omega) * ShortWavesFade(kLength);

                    float amplitude = Mathf.Sqrt(
                        2.0f *
                        S *
                        Mathf.Abs(dOmegadk) /
                        kLength *
                        deltaK *
                        deltaK);

                    initialSpectrum[x, y] =
                        new Complex(
                            gauss2.x * amplitude,
                            gauss1.y * amplitude
                        );
                }
                else
                {
                    initialSpectrum[x, y] = Complex.Zero;
                }
            }
        }

        for (int y = 0; y < meshResolution; y++)
        {
            for (int x = 0; x < meshResolution; x++)
            {
                int conjugateX = (meshResolution - x) % meshResolution;
                int conjugateY = (meshResolution - y) % meshResolution;

                conjugateSpectrum[x, y] = Complex.Conjugate(initialSpectrum[conjugateX, conjugateY]);

                initialSpectrumTexture.SetPixel(
                    x,
                    y,
                    new Color(
                        (float)initialSpectrum[x, y].Real,
                        (float)initialSpectrum[x, y].Imaginary,
                        (float)conjugateSpectrum[x, y].Real,
                        (float)conjugateSpectrum[x, y].Imaginary
                    )
                );
            }
        }

        // Apply the texture
        initialSpectrumTexture.Apply();
    }
}
