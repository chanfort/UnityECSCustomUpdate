using UnityEngine;

public class PrefabHolder : MonoBehaviour
{
    public static PrefabHolder active;

    public GameObject prefab;
    public int numberToSpawn = 1000000;

    public static PrefabHolder GetActive()
    {
        if (PrefabHolder.active == null)
        {
            PrefabHolder.active = UnityEngine.Object.FindObjectOfType<PrefabHolder>();
        }

        return PrefabHolder.active;
    }


    void Start()
    {

    }
}
