using Unity.Entities;
using UnityEngine;

public class UpdateOrderAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float Value;

    public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
    {
        var data = new UpdateOrder { Value = Value };
        manager.AddSharedComponentData(entity, data);
    }
}
