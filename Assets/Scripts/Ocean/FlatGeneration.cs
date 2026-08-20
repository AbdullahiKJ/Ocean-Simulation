using UnityEngine;

public class FlatGeneration : MonoBehaviour, IWaveGeneration
{
    public void Initialise(int meshResolution, float meshSize, WavePreset preset)
    {
        return;
    }
    public void UpdateGenerator()
    {
        // This generator does not update
        return;
    }
    public float SampleHeight()
    {
        throw new System.NotImplementedException();
    }
    public Vector3 SampleNormal()
    {
        throw new System.NotImplementedException();
    }
}