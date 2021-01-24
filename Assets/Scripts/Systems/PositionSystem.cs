using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

public class PositionSpeedSystem : JobComponentSystem
{
    [BurstCompile]
    struct PositionSpeedPosition : IJobParallelFor
    {
        public float dt;
        public NativeArray<Speed> speeds;
        public NativeArray<Translation> positions;
        public int updateOrder;
        public int updateFrequency;

        public void Execute(int i)
        {
            if ((i + updateOrder) % updateFrequency == 0)
            {
                float3 prevPosition = positions[i].Value;
                float speed = speeds[i].Value;

                positions[i] = new Translation
                {
                    Value = prevPosition + new float3(0, 1, 0) * speed * dt * updateFrequency
                };
            }
        }
    }

    [BurstCompile]
    struct PositionSpeedPositionFilter : IJobParallelFor
    {
        public float dt;
        public NativeArray<Speed> speeds;
        public NativeArray<Translation> positions;
        public int updateFrequency;

        public void Execute(int i)
        {
            float3 prevPosition = positions[i].Value;
            float speed = speeds[i].Value;

            positions[i] = new Translation
            {
                Value = prevPosition + new float3(0, 1, 0) * speed * dt * updateFrequency
            };
        }
    }

    [BurstCompile]
    struct PositionSpeedPositionIJobChunk : IJobChunk
    {
        public float dt;
        public ComponentTypeHandle<Translation> positions;
        [ReadOnly] public ComponentTypeHandle<Speed> speeds;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkCount = chunk.Count;
            var chunkPositions = chunk.GetNativeArray(positions);
            var chunkSpeeds = chunk.GetNativeArray(speeds);

            for (int i = 0; i < chunkCount; i++)
            {
                float3 prevPosition = chunkPositions[i].Value;
                float3 speed = chunkSpeeds[i].Value;

                chunkPositions[i] = new Translation
                {
                    Value = prevPosition + new float3(0, 1, 0) * speed * dt * 1
                };
            }
        }
    }

    [BurstCompile]
    struct PositionSpeedPositionIJobChunkShared : IJobChunk
    {
        public float dt;
        public ComponentTypeHandle<Translation> positions;
        [ReadOnly] public ComponentTypeHandle<Speed> speeds;
        [ReadOnly] public SharedComponentTypeHandle<UpdateOrder> updateOrderACSCT;
        [ReadOnly] public NativeArray<UpdateOrder> updateOrdersNativeArray;
        [ReadOnly] public int updateOrder;
        [ReadOnly] public int updateFrequency;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkCount = chunk.Count;
            var chunkPositions = chunk.GetNativeArray(positions);
            var chunkSpeeds = chunk.GetNativeArray(speeds);
            var chunkUpdateOrderSharedIndex = chunk.GetSharedComponentIndex(updateOrderACSCT);

            var updateOrderData = updateOrdersNativeArray[chunkUpdateOrderSharedIndex];

            if (updateOrderData.Value == updateOrder)
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    float3 prevPosition = chunkPositions[i].Value;
                    float3 speed = chunkSpeeds[i].Value;

                    chunkPositions[i] = new Translation
                    {
                        Value = prevPosition + new float3(0, 1, 0) * speed * dt * updateFrequency
                    };
                }
            }
        }
    }

    private EntityQuery m_Group;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Translation), typeof(Speed), typeof(UpdateOrder));

        GameObject prefab = PrefabHolder.GetActive().prefab;
        int numberToSpawn = PrefabHolder.GetActive().numberToSpawn;

        Entity prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab, GameObjectConversionSettings.FromWorld(this.World, null));

        for (int i = 0; i < numberToSpawn; i++)
        {
            Entity entity = EntityManager.Instantiate(prefabEntity);
            UpdateOrder updateOrder = EntityManager.GetSharedComponentData<UpdateOrder>(entity);

            updateOrder.Value = i % updateFrequency;

            EntityManager.SetSharedComponentData(entity, updateOrder);
        }
    }

    int updateOrder = 0;
    int updateFrequency = 10;
    int mode = 0;
    static NativeArray<ArchetypeChunk> chunksMode5;
    static NativeArray<UpdateOrder> updateOrdersNativeArray;
    static List<UpdateOrder> updateOrdersList = new List<UpdateOrder>();

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            mode = 0;
            m_Group.ResetFilter();
            DisposeNativeArrays();
            Debug.Log("Using mode: " + mode + ". Uses IJobParalelFor with updateOrder and updateFrequency on every entity. < 0.05 ms");
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            mode = 1;
            m_Group.ResetFilter();
            DisposeNativeArrays();
            Debug.Log("Using mode: " + mode + ". Uses IJobParalelFor with filtered SCD. Filtering gives bad performance. 1.3 ms");
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            mode = 2;
            m_Group.ResetFilter();
            m_Group.SetSharedComponentFilter(new UpdateOrder { Value = 0 });
            DisposeNativeArrays();
            Debug.Log("Using mode: " + mode + ". Same as mode 1 but without setting filter every update. Shows bad perfromance, caused by calling GetComponentDataArray when filter is set. 1.3 ms");
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            mode = 3;
            m_Group.ResetFilter();
            DisposeNativeArrays();
            Debug.Log("Using mode: " + mode + ". IJobChunk with processing all entities. Good performance. < 0.05 ms");
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            mode = 4;
            m_Group.ResetFilter();
            DisposeNativeArrays();
            Debug.Log("Using mode: " + mode + ". IJobChunk with processing only requested SCD chunks. SCD are refreshed on every update. Good performance. 0.13 ms");
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            mode = 5;
            m_Group.ResetFilter();
            RefreshUpdateOrdersLists();
            Debug.Log("Using mode: " + mode + ". IJobChunk with processing only requested SCD chunks and under assumption that SCD never change. If SCD change, RefreshUpdateOrdersLists needs to be called manually. Best filtered queries performance. 0.06 ms");
        }

        updateOrder++;
        if (updateOrder >= updateFrequency)
        {
            updateOrder = 0;
        }

        JobHandle jobHandle = inputDeps;

        if (mode == 0)
        {
            NativeArray<Translation> positions = m_Group.ToComponentDataArray<Translation>(Allocator.Persistent);
            NativeArray<Speed> speeds = m_Group.ToComponentDataArray<Speed>(Allocator.Persistent);

            jobHandle = new PositionSpeedPosition
			{
				speeds = speeds,
				positions = positions,
				dt = Time.DeltaTime,
				updateOrder = updateOrder,
				updateFrequency = updateFrequency
			}.Schedule(positions.Length, 64, inputDeps);

            positions.Dispose(jobHandle);
            speeds.Dispose(jobHandle);
        }
        else if (mode == 1)
        {
            m_Group.ResetFilter();
            m_Group.SetSharedComponentFilter(new UpdateOrder { Value = updateOrder % updateFrequency });
            NativeArray<Translation> positions = m_Group.ToComponentDataArray<Translation>(Allocator.Persistent);
            NativeArray<Speed> speeds = m_Group.ToComponentDataArray<Speed>(Allocator.Persistent);

            jobHandle = new PositionSpeedPositionFilter
			{
				speeds = speeds,
				positions = positions,
				dt = Time.DeltaTime,
				updateFrequency = updateFrequency
			}.Schedule(positions.Length, 64, inputDeps);

            positions.Dispose(jobHandle);
            speeds.Dispose(jobHandle);
        }
        else if (mode == 2)
        {
            NativeArray<Translation> positions = m_Group.ToComponentDataArray<Translation>(Allocator.Persistent);
            NativeArray<Speed> speeds = m_Group.ToComponentDataArray<Speed>(Allocator.Persistent);

            jobHandle = new PositionSpeedPositionFilter
			{
				speeds = speeds,
				positions = positions,
				dt = Time.DeltaTime,
				updateFrequency = updateFrequency
			}.Schedule(positions.Length, 64, inputDeps);

            positions.Dispose(jobHandle);
            speeds.Dispose(jobHandle);
        }
        else if (mode == 3)
        {
            EntityQuery query = GetEntityQuery(
				new EntityQueryDesc
            	{
                	Any = Array.Empty<ComponentType>(),
                	None = Array.Empty<ComponentType>(),
                	All = new ComponentType[] { ComponentType.ReadWrite<Translation>(), ComponentType.ReadOnly<Speed>() }
            	}
			);

            jobHandle = new PositionSpeedPositionIJobChunk
            {
                dt = Time.DeltaTime,
                positions = GetComponentTypeHandle<Translation>(false),
                speeds = GetComponentTypeHandle<Speed>(true),
            }.Schedule(query, inputDeps);
        }
        else if (mode == 4)
        {
            EntityQuery query = GetEntityQuery(
				new EntityQueryDesc
            	{
                	Any = Array.Empty<ComponentType>(),
                	None = Array.Empty<ComponentType>(),
                	All = new ComponentType[] { ComponentType.ReadWrite<Translation>(), ComponentType.ReadOnly<Speed>() }
            	}
			);

            RefreshUpdateOrdersLists();

            jobHandle = new PositionSpeedPositionIJobChunkShared
            {
                dt = Time.DeltaTime,
                positions = GetComponentTypeHandle<Translation>(false),
                speeds = GetComponentTypeHandle<Speed>(true),
                updateOrderACSCT = GetSharedComponentTypeHandle<UpdateOrder>(),
                updateOrdersNativeArray = updateOrdersNativeArray,
                updateOrder = updateOrder,
                updateFrequency = updateFrequency
            }.Schedule(query, inputDeps);
        }
        else if (mode == 5)
        {
            EntityQuery query = GetEntityQuery(
				new EntityQueryDesc
            	{
                	Any = Array.Empty<ComponentType>(),
                	None = Array.Empty<ComponentType>(),
                	All = new ComponentType[] { ComponentType.ReadWrite<Translation>(), ComponentType.ReadOnly<Speed>() }
            	}
			);

            jobHandle = new PositionSpeedPositionIJobChunkShared
            {
                dt = Time.DeltaTime,
                positions = GetComponentTypeHandle<Translation>(false),
                speeds = GetComponentTypeHandle<Speed>(true),
                updateOrderACSCT = GetSharedComponentTypeHandle<UpdateOrder>(),
                updateOrdersNativeArray = updateOrdersNativeArray,
                updateOrder = updateOrder,
                updateFrequency = updateFrequency
            }.Schedule(query, inputDeps);
        }

        return jobHandle;
    }

    public void RefreshUpdateOrdersLists()
    {
        DisposeNativeArrays();

        updateOrdersList.Clear();
        EntityManager.GetAllUniqueSharedComponentData<UpdateOrder>(updateOrdersList);

        updateOrdersNativeArray = new NativeArray<UpdateOrder>(updateOrdersList.Count, Allocator.Persistent);

        for (int i = 0; i < updateOrdersList.Count; i++)
        {
            updateOrdersNativeArray[i] = updateOrdersList[i];
        }
    }

    protected override void OnDestroy()
    {
        DisposeNativeArrays();
    }

    public static void DisposeNativeArrays()
    {
        if (chunksMode5.IsCreated)
        {
            chunksMode5.Dispose();
        }

        if (updateOrdersNativeArray.IsCreated)
        {
            updateOrdersNativeArray.Dispose();
        }
    }
}
