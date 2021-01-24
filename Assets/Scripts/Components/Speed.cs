using System;
using Unity.Entities;

[Serializable]
public struct Speed : IComponentData
{
    public float Value;
}

// public class SpeedComponent : ComponentDataProxy<Speed> { }
