using Unity.Entities;
using UnityEngine;

public class SpeedAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float Value;

    public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
    {
        var data = new Speed { Value = Value };
        manager.AddComponentData(entity, data);
    }
}
