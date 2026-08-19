using UnityEngine;
interface IBuoyancyModel
{
    void SampleOcean();
    void CalculateBuoyancy();
}

public enum BuoyancyModel
{
    Point,
    Volume,
    Partitioned
}