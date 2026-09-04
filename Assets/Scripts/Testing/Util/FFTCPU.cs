using System;
using UnityEngine;
using static FFTGeneration;
public class FFTCPU : MonoBehaviour
{
    // Code is adapted from the following sources:
    // George Bolba's implementation of Jerry Tessendorf's Simulating Water paper
    // AceRolla's (Garrett Gunnell) implementation of the Tessendorf paper which refereneces gasgiant's FFT and JONSWAP implementation

    public Vector4[,,] _SpectrumTextures, _InitialSpectrumTextures, _DisplacementTextures;
    Vector2[,,] _SlopeTextures;
    // RWTexture2D<half> _BuoyancyData;

    public float _FrameTime, _Gravity, _RepeatTime, _Depth, _LowCutoff, _HighCutoff, _MeshSize;
    public uint _Seed;
    public Vector2 _Lambda;
    public uint _N, _LengthScale0, _LengthScale1, _LayerCount;

    Vector2 ComplexMult(Vector2 a, Vector2 b)
    {
        return new Vector2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
    }

    Vector2 EulerFormula(float x)
    {
        return new Vector2(Mathf.Cos(x), Mathf.Sin(x));
    }

    float hash(uint n)
    {
        n = (n << (int)13U) ^ n;
        n = (uint)(n * (n * n * 15731U + 0x789221U) + 0x1376312589U);
        return (float)(n & (uint)0x7fffffffU) / (float)0x7fffffff;
    }

    Vector2 UniformToGaussian(float u1, float u2)
    {
        u1 = Mathf.Max(u1, 0.000001f);

        float R = Mathf.Sqrt(-2.0f * Mathf.Log(u1));
        float theta = 2.0f * Mathf.PI * u2;

        return new Vector2(R * Mathf.Cos(theta), R * Mathf.Sin(theta));
    }

    public SpectrumSettings[] _Spectrums;

    float Dispersion(float kMag)
    {
        return Mathf.Sqrt(_Gravity * kMag * (float)Math.Tanh(Mathf.Min(kMag * _Depth, 20)));
    }

    float DispersionDerivative(float kMag)
    {
        float th = (float)Math.Tanh(Mathf.Min(kMag * _Depth, 20));
        float ch = (float)Math.Cosh(kMag * _Depth);
        return _Gravity * (_Depth * kMag / ch / ch + th) / Dispersion(kMag) / 2.0f;
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

    float DirectionSpectrum(float theta, float omega, SpectrumSettings spectrum)
    {
        float s = SpreadPower(omega, spectrum.peakOmega) + 16 * (float)Math.Tanh(Mathf.Min(omega / spectrum.peakOmega, 20)) * spectrum.swell * spectrum.swell;
        return Mathf.Lerp(2.0f / 3.1415f * Mathf.Cos(theta) * Mathf.Cos(theta), Cosine2s(theta - spectrum.angle, s), spectrum.spreadBlend);
    }

    float TMACorrection(float omega)
    {
        float omegaH = omega * Mathf.Sqrt(_Depth / _Gravity);
        if (omegaH <= 1.0f)
            return 0.5f * omegaH * omegaH;
        if (omegaH < 2.0f)
            return 1.0f - 0.5f * (2.0f - omegaH) * (2.0f - omegaH);

        return 1.0f;
    }

    float JONSWAP(float omega, SpectrumSettings spectrum)
    {
        if (omega <= 0.0001f)
            return 0.0f;
        float sigma = (omega <= spectrum.peakOmega) ? 0.07f : 0.09f;

        float r = Mathf.Exp(-(omega - spectrum.peakOmega) * (omega - spectrum.peakOmega) / 2.0f / sigma / sigma / spectrum.peakOmega / spectrum.peakOmega);

        float oneOverOmega = 1.0f / omega;
        float peakOmegaOverOmega = spectrum.peakOmega / omega;
        return spectrum.scale * TMACorrection(omega) * spectrum.alpha * _Gravity * _Gravity
            * oneOverOmega * oneOverOmega * oneOverOmega * oneOverOmega * oneOverOmega
            * Mathf.Exp(-1.25f * peakOmegaOverOmega * peakOmegaOverOmega * peakOmegaOverOmega * peakOmegaOverOmega)
            * Mathf.Pow(Mathf.Abs(spectrum.gamma), r);
    }

    float ShortWavesFade(float kLength, SpectrumSettings spectrum)
    {
        return Mathf.Exp(-spectrum.shortWavesFade * spectrum.shortWavesFade * kLength * kLength);
    }

    public void CS_InitializeSpectrum(Vector3Int id)
    {
        uint seed = (uint)(id.x + _N * id.y + _N);
        seed += _Seed;

        float[] lengthScales = { _LengthScale0, _LengthScale1 };

        for (uint i = 0; i < _LayerCount; ++i)
        {
            float halfN = _N / 2.0f;

            float deltaK = 2.0f * Mathf.PI / lengthScales[i];
            Vector2 K = new Vector2(id.x - halfN, id.y - halfN) * deltaK;
            float kLength = K.magnitude;

            if (kLength < 0.0001f)
            {
                _InitialSpectrumTextures[id.x, id.y, i] = Vector4.zero;
                continue;
            }

            seed += (uint)(i + hash(seed) * 10);
            Vector4 uniformRandSamples = new Vector4(hash(seed), hash(seed * 2), hash(seed * 3), hash(seed * 4));
            Vector2 gauss1 = UniformToGaussian(uniformRandSamples.x, uniformRandSamples.y);
            Vector2 gauss2 = UniformToGaussian(uniformRandSamples.z, uniformRandSamples.w);

            if (_LowCutoff <= kLength && kLength <= _HighCutoff)
            {
                float kAngle = Mathf.Atan2(K.y, K.x);
                float omega = Dispersion(kLength);

                float dOmegadk = DispersionDerivative(kLength);

                float spectrum = JONSWAP(omega, _Spectrums[i * 2]) * DirectionSpectrum(kAngle, omega, _Spectrums[i * 2]) * ShortWavesFade(kLength, _Spectrums[i * 2]);

                // todo: remove this later
                if (float.IsNaN(spectrum) || float.IsInfinity(spectrum))
                {
                    Debug.LogError(
                        $"Invalid spectrum at ({id.x}, {id.y}), layer {i}, k={kLength}"
                    );
                }

                if (_Spectrums[i * 2 + 1].scale > 0)
                    spectrum += JONSWAP(omega, _Spectrums[i * 2 + 1]) * DirectionSpectrum(kAngle, omega, _Spectrums[i * 2 + 1]) * ShortWavesFade(kLength, _Spectrums[i * 2 + 1]);

                Vector2 h0 = new Vector2(gauss2.x, gauss1.y) * Mathf.Sqrt(2 * spectrum * Mathf.Abs(dOmegadk) / kLength * deltaK * deltaK);
                _InitialSpectrumTextures[id.x, id.y, i] = new Vector4(h0.x, h0.y, 0.0f, 0.0f);
            }
            else
            {
                _InitialSpectrumTextures[id.x, id.y, i] = Vector4.zero;
            }
        }
    }

    public void CS_PackSpectrumConjugate(Vector3Int id)
    {
        for (uint i = 0; i < _LayerCount; ++i)
        {
            Vector2 h0 = new Vector2(_InitialSpectrumTextures[id.x, id.y, i].x, _InitialSpectrumTextures[id.x, id.y, i].y);
            Vector4 conj = _InitialSpectrumTextures[(_N - id.x) % _N, (_N - id.y) % _N, i];
            Vector2 h0conj = new Vector2(conj.x, conj.y);

            _InitialSpectrumTextures[id.x, id.y, i] = new Vector4(h0.x, h0.y, h0conj.x, -h0conj.y);
        }
    }

    public void CS_UpdateSpectrumForFFT(Vector3Int id)
    {
        float[] lengthScales = { _LengthScale0, _LengthScale1 };

        for (int i = 0; i < _LayerCount; ++i)
        {
            Vector4 initialSignal = _InitialSpectrumTextures[id.x, id.y, i];
            Vector2 h0 = new Vector2(initialSignal.x, initialSignal.y);
            Vector2 h0conj = new Vector2(initialSignal.z, initialSignal.w);

            float halfN = _N / 2.0f;
            Vector2 K = new Vector2(id.x - halfN, id.y - halfN) * 2.0f * Mathf.PI / lengthScales[i];
            float kMag = K.magnitude;
            float kMagRcp;

            if (kMag < 0.0001f)
            {
                kMagRcp = 1.0f;
            }
            else
            {
                kMagRcp = 1.0f / kMag;
            }

            float w_0 = 2.0f * Mathf.PI / _RepeatTime;
            float dispersion = Mathf.Floor(Mathf.Sqrt(_Gravity * kMag) / w_0) * w_0 * _FrameTime;

            Vector2 exponent = EulerFormula(dispersion);

            Vector2 htilde = ComplexMult(h0, exponent) + ComplexMult(h0conj, new Vector2(exponent.x, -exponent.y));
            // todo: remove this later
            if (float.IsNaN(htilde.x) || float.IsNaN(htilde.y))
            {
                Debug.LogError(
                    $"Invalid htilde at ({id.x}, {id.y}), layer {i}"
                );
            }

            Vector2 ih = new Vector2(-htilde.y, htilde.x);

            Vector2 displacementX = ih * K.x * kMagRcp;
            Vector2 displacementY = htilde;
            Vector2 displacementZ = ih * K.y * kMagRcp;

            Vector2 displacementX_dx = -htilde * K.x * K.x * kMagRcp;
            Vector2 displacementY_dx = ih * K.x;
            Vector2 displacementZ_dx = -htilde * K.x * K.y * kMagRcp;

            Vector2 displacementY_dz = ih * K.y;
            Vector2 displacementZ_dz = -htilde * K.y * K.y * kMagRcp;

            Vector2 htildeDisplacementX = new Vector2(displacementX.x - displacementZ.y, displacementX.y + displacementZ.x);
            Vector2 htildeDisplacementZ = new Vector2(displacementY.x - displacementZ_dx.y, displacementY.y + displacementZ_dx.x);

            Vector2 htildeSlopeX = new Vector2(displacementY_dx.x - displacementY_dz.y, displacementY_dx.y + displacementY_dz.x);
            Vector2 htildeSlopeZ = new Vector2(displacementX_dx.x - displacementZ_dz.y, displacementX_dx.y + displacementZ_dz.x);

            _SpectrumTextures[id.x, id.y, i * 2] = new Vector4(htildeDisplacementX.x, htildeDisplacementX.y, htildeDisplacementZ.x, htildeDisplacementZ.y);
            _SpectrumTextures[id.x, id.y, i * 2 + 1] = new Vector4(htildeSlopeX.x, htildeSlopeX.y, htildeSlopeZ.x, htildeSlopeZ.y);
        }
    }

    public uint SIZE = 512;
    public uint LOG_SIZE = 9;

    public Vector4[,,] _FourierTarget;
    bool _Direction;

    public Vector4[,] fftGroupBuffer;

    void ButterflyValues(uint step, uint index, out Vector2Int indices, out Vector2 twiddle)
    {
        twiddle = new Vector2(0.0f, 0.0f);
        const float twoPi = Mathf.PI * 2.0f;
        uint b = SIZE >> (int)(step + 1);
        uint w = b * (index / b);
        uint i = (w + index) % SIZE;

        float theta = -twoPi / SIZE * w;
        twiddle.x = Mathf.Cos(theta);
        twiddle.y = Mathf.Sin(theta);

        //This is what makes it the inverse FFT
        twiddle.y = -twiddle.y;
        indices = new Vector2Int((int)i, (int)(i + b));
    }

    int FFTBatch()
    {
        bool flag = false;

        for (uint step = 0; step < LOG_SIZE; step++)
        {
            for (uint index = 0; index < SIZE; index++)
            {
                Vector2Int inputsIndices;
                Vector2 twiddle;

                ButterflyValues(
                    step,
                    index,
                    out inputsIndices,
                    out twiddle
                );

                Vector4 v =
                    fftGroupBuffer[
                        flag ? 1 : 0,
                        inputsIndices.y
                    ];

                Vector2 vxy = new Vector2(v.x, v.y);
                Vector2 vzw = new Vector2(v.z, v.w);

                Vector2 mult1 = ComplexMult(twiddle, vxy);
                Vector2 mult2 = ComplexMult(twiddle, vzw);

                fftGroupBuffer[
                    flag ? 0 : 1,
                    index
                ] =
                    fftGroupBuffer[
                        flag ? 1 : 0,
                        inputsIndices.x
                    ]
                    + new Vector4(
                        mult1.x,
                        mult1.y,
                        mult2.x,
                        mult2.y
                    );
            }

            flag = !flag;
        }

        return flag ? 1 : 0;
    }

    public void CS_HorizontalFFT(int y)
    {
        for (int channel = 0; channel < 8; channel++)
        {
            // Load row into buffer 0
            for (int x = 0; x < SIZE; x++)
            {
                fftGroupBuffer[0, x] =
                    _FourierTarget[x, y, channel];
            }

            // Perform complete FFT
            int finalBuffer = FFTBatch();

            // Copy final result back
            for (int x = 0; x < SIZE; x++)
            {
                _FourierTarget[x, y, channel] =
                    fftGroupBuffer[finalBuffer, x];
            }
        }
    }

    public void CS_VerticalFFT(int x)
    {
        for (int channel = 0; channel < 8; channel++)
        {
            // Load row into buffer 0
            for (int y = 0; y < SIZE; y++)
            {
                fftGroupBuffer[0, y] =
                    _FourierTarget[x, y, channel];
            }

            // Perform complete FFT
            int finalBuffer = FFTBatch();

            // Copy final result back
            for (int y = 0; y < SIZE; y++)
            {
                _FourierTarget[x, y, channel] =
                    fftGroupBuffer[finalBuffer, y];
            }
        }
    }

    Vector4 Permute(Vector4 data, Vector3 id)
    {
        return data * (1.0f - 2.0f * ((id.x + id.y) % 2));
    }

    public void CS_AssembleMaps(Vector3Int id)
    {
        for (int i = 0; i < _LayerCount; ++i)
        {
            Vector4 htildeDisplacement = Permute(_SpectrumTextures[id.x, id.y, i * 2], id);
            Vector4 htildeSlope = Permute(_SpectrumTextures[id.x, id.y, i * 2 + 1], id);

            Vector2 dxdz = new Vector2(htildeDisplacement.x, htildeDisplacement.y);
            Vector2 dydxz = new Vector2(htildeDisplacement.z, htildeDisplacement.w);
            Vector2 dyxdyz = new Vector2(htildeSlope.x, htildeSlope.y);
            Vector2 dxxdzz = new Vector2(htildeSlope.z, htildeSlope.w);

            float jacobian = (1.0f + _Lambda.x * dxxdzz.x) * (1.0f + _Lambda.y * dxxdzz.y) - _Lambda.x * _Lambda.y * dydxz.y * dydxz.y;

            Vector3 displacement = new Vector3(_Lambda.x * dxdz.x, dydxz.x, _Lambda.y * dxdz.y);

            Vector2 slopes = new Vector2(dyxdyz.x, dyxdyz.y) / (1 + (dxxdzz * _Lambda).magnitude);
            float covariance = slopes.x * slopes.y;

            float foam = 0.0f;

            _DisplacementTextures[id.x, id.y, i] = new Vector4(displacement.x, displacement.y, displacement.z, foam);
            // _SlopeTextures[id.x, id.y, i] = slopes;
        }
    }
}