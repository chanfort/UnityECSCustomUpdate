using System;
using Unity.Entities;

[Serializable]
public struct UpdateOrder : ISharedComponentData
{
    public float Value;
}

// public class UpdateOrderComponent : SharedComponentDataProxy<UpdateOrder> { }
