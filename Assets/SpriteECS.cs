using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DefaultNamespace;
using TreeEditor;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

using MRandom = Unity.Mathematics.Random;


namespace Squad.SpriteECS
{

    public class SpriteMaterialComponent : IComponentData, IEquatable<SpriteMaterialComponent>, IDisposable
    {
        public Entity spriteSheet;
        public Material material;
        public GraphicsBuffer spriteDataBuffer;
        public GraphicsBuffer instanceDataBuffer;
        
        public int slices;

        //public int activeInstances = 0;
        
        public bool Equals(SpriteMaterialComponent other)
        {
            return material == other.material;
        }

        public override int GetHashCode()
        {
            return material ? material.GetHashCode() : 1371622046;
        }

        public void Dispose()
        {
            spriteDataBuffer?.Release();
        }

        // public SpriteComponent getInstanceComponent(uint spriteIndex)
        // {
        //     var instance = this.activeInstances++;
        //     //Interlocked.Increment(ref this.activeInstances);
        //     //Debug.Log($"Adding instance[{this.activeInstances}] for {this.material.name}[{spriteIndex}]");
        //     return new SpriteComponent()
        //     {
        //         bufferIndex = instance, 
        //         spriteIndex = spriteIndex,
        //         spriteSheet = spriteSheet
        //     };
        //     
        // }
    }

    public struct SpriteMaterialComponentInstancesCounter : IComponentData
    {
        public int activeInstances;
    }

    public struct SpriteInstanceSpawning : IComponentData
    {
        public Entity spriteSheet;
        public uint spriteIndex;
    }

    public struct SpriteInstanceDespawning : ICleanupComponentData
    {
    }
    
    public class SpriteSheetInitComponent : IComponentData
    {
        public Texture2D texture;
        public int demoEntitiesCount = 1000;
    }

    public struct SpriteComponent : ICleanupComponentData
    {
        public Entity spriteSheet;
        public uint spriteIndex;
        public int bufferIndex;
    }

    [InternalBufferCapacity(16)]
    public struct SpriteSliceComponent : IBufferElementData
    {
        public float4 uv;
        public float2 pivot;
    }

    [InternalBufferCapacity(64)]
    public struct SpriteInstanceBuffer : IBufferElementData
    {
        public float4 color;
        public uint spriteIndex;
    }

    [InternalBufferCapacity(64)]
    public struct SpriteInstanceTransformBuffer : IBufferElementData
    {
        public Matrix4x4 transform;
    }
    

    [InternalBufferCapacity(64)]
    public struct BufferIndexToEntityMap : IBufferElementData
    {
        public Entity entity;
    }

    public struct TimeToLive : IComponentData
    {
        public float ttl;
    }
    

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial class SpritesProvisioningGroup : ComponentSystemGroup {}    
    
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdatePresentationSystemGroup))]
    public partial class SpritesRenderingGroup : ComponentSystemGroup {}    
    

    [UpdateInGroup(typeof(SpritesProvisioningGroup))]
    [UpdateBefore(typeof(SpriteSheetPreparationSystem))]
    public partial struct SpriteSheetSpawningSystem : ISystem
    {
        private ComponentLookup<SpriteMaterialComponentInstancesCounter> _instancesCounter;
        private EntityCommandBuffer _ecb; 
        private MRandom _rnd;

        public void OnCreate(ref SystemState state)
        {
            _instancesCounter = state.GetComponentLookup<SpriteMaterialComponentInstancesCounter>();
            _rnd = new MRandom((uint) DateTime.Now.Ticks);
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _ecb = ecbSystem.CreateCommandBuffer();
            _instancesCounter.Update(ref state);

            var count = new NativeHashMap<Entity, int>(16, Allocator.Temp);
            
            foreach (var (inst, e) in SystemAPI.Query<RefRO<SpriteInstanceSpawning>>().WithEntityAccess())
            {
                var spriteSheetEntity = inst.ValueRO.spriteSheet; 
                if (!count.ContainsKey(inst.ValueRO.spriteSheet))
                {
                    count.Add(spriteSheetEntity, _instancesCounter[spriteSheetEntity].activeInstances);
                }
                _ecb.AddComponent<SpriteComponent>(e);
                _ecb.SetComponent(e, new SpriteComponent()
                {
                    spriteIndex = inst.ValueRO.spriteIndex,
                    spriteSheet = inst.ValueRO.spriteSheet,
                    bufferIndex = count[spriteSheetEntity]++ 
                });
                
                _ecb.AddComponent<TimeToLive>(e);
                _ecb.SetComponent(e, new TimeToLive()
                {
                    ttl = _rnd.NextFloat(1f, 3f)
                });
                _ecb.RemoveComponent<SpriteInstanceSpawning>(e);
            }

            foreach (var ssCntr in count)
            {
                _ecb.SetComponent(ssCntr.Key, new SpriteMaterialComponentInstancesCounter()
                {
                    activeInstances = ssCntr.Value
                });
            }

            count.Dispose();
        }

    }

    [UpdateInGroup(typeof(SpritesProvisioningGroup))]
    [UpdateAfter(typeof(SpriteSheetPreparationSystem))]
    public partial struct SpriteSheetDespawningSystem : ISystem
    {
        private ComponentLookup<SpriteMaterialComponentInstancesCounter> _instancesCounter;
        private BufferLookup<BufferIndexToEntityMap> _i2eBuffer;
        private EntityCommandBuffer _ecb;
        private EntityQuery _query;
        private ComponentLookup<SpriteComponent> _spriteComponent;

        public void OnCreate(ref SystemState state)
        {
            _instancesCounter = state.GetComponentLookup<SpriteMaterialComponentInstancesCounter>();
            _i2eBuffer = state.GetBufferLookup<BufferIndexToEntityMap>();
            _spriteComponent = state.GetComponentLookup<SpriteComponent>();
            _query = state.GetEntityQuery(
                ComponentType.ReadOnly<SpriteInstanceDespawning>(), 
                ComponentType.ReadOnly<SpriteComponent>());
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _ecb = ecbSystem.CreateCommandBuffer();
            
            _instancesCounter.Update(ref state);
            _i2eBuffer.Update(ref state);
            _spriteComponent.Update(ref state);
            //
            state.Dependency.Complete();
            var count = new NativeHashMap<Entity, int>(16, Allocator.Temp);
            //
            foreach (var (desp,inst, e) in SystemAPI.Query<RefRO<SpriteInstanceDespawning>, RefRO<SpriteComponent>>().WithEntityAccess())
            {
                var spriteSheetEntity = inst.ValueRO.spriteSheet;
                var i2e = _i2eBuffer[spriteSheetEntity];

                _ecb.RemoveComponent<SpriteInstanceDespawning>(e);
                //_ecb.RemoveComponent<SpriteComponent>(e);
                
                if (!count.ContainsKey(inst.ValueRO.spriteSheet))
                {
                    count.Add(spriteSheetEntity, _instancesCounter[spriteSheetEntity].activeInstances);
                }

                if (inst.ValueRO.bufferIndex < count[spriteSheetEntity] - 1)
                {
                    var lastEntity = i2e[count[spriteSheetEntity] - 1].entity;
                    var lastEntitySprite = _spriteComponent[lastEntity];
                    _ecb.SetComponent(lastEntity, new SpriteComponent()
                    {
                        spriteIndex = lastEntitySprite.spriteIndex,
                        spriteSheet = lastEntitySprite.spriteSheet,
                        bufferIndex = inst.ValueRO.bufferIndex
                    });
                    i2e[inst.ValueRO.bufferIndex] = new BufferIndexToEntityMap()
                    {
                         entity = lastEntity
                    };
                }

                count[spriteSheetEntity]--;
            }
            
            foreach (var ssCntr in count)
            {
                _ecb.SetComponent(ssCntr.Key, new SpriteMaterialComponentInstancesCounter()
                {
                    activeInstances = ssCntr.Value
                });
            }
            
            count.Dispose();
        }

    }    
    
    [UpdateInGroup(typeof(SpritesProvisioningGroup))]
    [UpdateAfter(typeof(SpriteSheetSpawningSystem))]
    [UpdateBefore(typeof(SpriteSheetDespawningSystem))]
    public partial class SpriteSheetPreparationSystem : SystemBase
    {
        public EntityQuery query;
        private bool initialized = false;

        protected override void OnCreate()
        {
            query = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, SpriteComponent>()
                .Build();
            // query = GetEntityQuery(
            //     ComponentType.ReadOnly<LocalTransform>(), 
            //     ComponentType.ReadOnly<SpriteComponent>()
            //     );
        }

        protected override void OnUpdate()
        {

                NativeList<LocalTransform> transforms =
                    query.ToComponentDataListAsync<LocalTransform>(Allocator.TempJob, out JobHandle transformsHandle);
                NativeList<SpriteComponent> spriteComponents =
                    query.ToComponentDataListAsync<SpriteComponent>(Allocator.TempJob, out JobHandle spcHandle);
                NativeList<Entity> entities = query.ToEntityListAsync(Allocator.TempJob, out JobHandle entHandle);
                Dependency = JobHandle.CombineDependencies(Dependency, transformsHandle, spcHandle);
                Dependency = JobHandle.CombineDependencies(Dependency, entHandle);
                Dependency.Complete();

                Entities
                    .WithAll<SpriteMaterialComponent>()
                    .ForEach((SpriteMaterialComponentInstancesCounter c, 
                        ref DynamicBuffer<SpriteInstanceBuffer> ib,
                        ref DynamicBuffer<BufferIndexToEntityMap> i2e,
                        ref DynamicBuffer<SpriteInstanceTransformBuffer> tb) =>
                    {
                        if (c.activeInstances > 0)
                        {
                            if (ib.Length <= c.activeInstances) ib.Resize(c.activeInstances, NativeArrayOptions.ClearMemory);
                            if (tb.Length <= c.activeInstances) tb.Resize(c.activeInstances, NativeArrayOptions.ClearMemory);
                            if (i2e.Length <= c.activeInstances) i2e.Resize(c.activeInstances, NativeArrayOptions.ClearMemory);
                        }
                    })
                    .WithoutBurst()
                    .Run();


                Dependency = Entities
                    .WithReadOnly(transforms)
                    .WithReadOnly(spriteComponents)
                    .WithReadOnly(entities)
                    .ForEach((Entity e, ref DynamicBuffer<SpriteInstanceTransformBuffer> tb,
                        ref DynamicBuffer<BufferIndexToEntityMap> i2e,
                        ref DynamicBuffer<SpriteInstanceBuffer> ib) =>
                    {
                        for (var i = 0; i < transforms.Length; i++)
                        {
                            if (spriteComponents[i].spriteSheet == e)
                            {
                                var bufferIndex = spriteComponents[i].bufferIndex;
                                tb[bufferIndex] = new SpriteInstanceTransformBuffer()
                                    { transform = transforms[i].ToMatrix() };
                                ib[bufferIndex] = new SpriteInstanceBuffer()
                                {
                                    spriteIndex = spriteComponents[i].spriteIndex,
                                    color = 1f
                                }; 
                                i2e[bufferIndex] = new BufferIndexToEntityMap()
                                {
                                     entity = entities[i]
                                };
                            }
                        }
                    })
                    .WithDisposeOnCompletion(transforms)
                    .WithDisposeOnCompletion(spriteComponents)
                    .WithDisposeOnCompletion(entities)
                    .ScheduleParallel(Dependency);

        }
    }

    [UpdateInGroup(typeof(SpritesProvisioningGroup))]
    [UpdateBefore(typeof(SpriteSheetPreparationSystem))]
    public partial struct TimeToLiveSystem : ISystem
    {
        EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(ComponentType.ReadWrite<TimeToLive>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var _ecb = ecbSystem.CreateCommandBuffer();
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (t, e) in SystemAPI.Query<RefRW<TimeToLive>>().WithEntityAccess())
            {
                t.ValueRW.ttl -= deltaTime;
                if (t.ValueRW.ttl <= 0f)
                {
                    _ecb.RemoveComponent<TimeToLive>(e);
                    _ecb.AddComponent<SpriteInstanceDespawning>(e);
                    //_ecb.DestroyEntity(e);
                }
            }
        }

        
    }

    
    [UpdateInGroup(typeof(SpritesRenderingGroup))]
    public partial class SpriteSheetRenderSystem : SystemBase {
        protected Mesh quad;

        protected override void OnCreate()
        {
            quad = Helper.Quad();
        }

        
        protected override void OnUpdate()
        {
            Entities
                .WithAll<SpriteMaterialComponent>()
                .ForEach((Entity e, in SpriteMaterialComponent sps, in SpriteMaterialComponentInstancesCounter c, in DynamicBuffer<SpriteInstanceBuffer> ib,
                    in DynamicBuffer<SpriteInstanceTransformBuffer> tb) =>
                {
                    if (c.activeInstances > 0)
                    {
                        sps.instanceDataBuffer.SetData(ib.ToNativeArray(Allocator.Temp));
                        sps.material.SetBuffer("_Instance", sps.instanceDataBuffer);
                        Graphics.DrawMeshInstanced(quad, 0, sps.material,
                            tb.Reinterpret<Matrix4x4>().ToNativeArray(Allocator.Temp).ToArray());
                    }
                })
                .WithoutBurst()
                .Run();
            
        }
    }
    
    public partial class SpriteSheetBakingSystem : SystemBase
    {
        private MRandom rnd;
        protected override void OnCreate()
        {
            rnd = new MRandom((uint) DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();
            Entities
                .WithAll<SpriteSheetInitComponent>()
                .ForEach((Entity e, in SpriteSheetInitComponent i) =>
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(i.texture)).OfType<Sprite>()
                        .ToArray();
                    Debug.Log($"Init sprites {i.texture.name} ({sprites.Length})");
                    var sliceBufer = ecb.AddBuffer<SpriteSliceComponent>(e);
                    var bufferIndexToEntity = ecb.AddBuffer<BufferIndexToEntityMap>(e);
                    ecb.SetName(e, new FixedString64Bytes($"SpriteSheet({i.texture.name})"));

                    var spriteDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sprites.Length,
                        Marshal.SizeOf<SpriteSliceComponent>());
                    
                    for (var sliceIndex = 0; sliceIndex < sprites.Length; sliceIndex++)
                    {
                        var sprite = sprites[sliceIndex];
                        float2 uv0 = sprite.uv[1] - sprite.uv[2]; // uv[2] should contain the texcoord with largest x,y
                        float2 uv1 = sprite.uv[2];  // uv[2] should contain the texcoord with smallest x,y
                        
                        sliceBufer.Add(new SpriteSliceComponent()
                        {
                            uv = new float4(uv0, uv1),
                            pivot = sprite.pivot / sprite.pixelsPerUnit
                        });
                    }
                    
                    spriteDataBuffer.SetData(sliceBufer.ToNativeArray(Allocator.Temp));
                    
                    var spriteMaterial = new SpriteMaterialComponent()
                    {
                        material = new Material(Shader.Find("Squad/DrawMeshInstanced1"))
                        {
                            mainTexture = i.texture,
                            enableInstancing = true
                        },
                        slices = sprites.Length,
                        spriteDataBuffer = spriteDataBuffer,
                        instanceDataBuffer =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, 
                            8192*2,
                            Marshal.SizeOf<SpriteInstanceBuffer>()),
                        spriteSheet = e
                    };

                    spriteMaterial.material.SetBuffer("_Sprites", spriteMaterial.spriteDataBuffer);
                    
                    var instanceTransforms = ecb.AddBuffer<SpriteInstanceTransformBuffer>(e);
                    var instanceBuffer = ecb.AddBuffer<SpriteInstanceBuffer>(e);

                    //
                    EntityArchetype ea = EntityManager.CreateArchetype(
                        typeof(LocalTransform),
                        //typeof(SpriteComponent)
                        typeof(SpriteInstanceSpawning)
                    );
                    //
                    for (var instance = 0; instance < i.demoEntitiesCount; instance++)
                    {
                        var ei = ecb.CreateEntity(ea);
                        ecb.SetComponent(ei, LocalTransform.FromPositionRotationScale(
                            rnd.NextFloat3(-10, 10),
                            Quaternion.Euler(0,0, rnd.NextFloat(-180, 180)),
                            rnd.NextFloat(0.25f, 1.25f)
                        ));
                        //ecb.SetComponent(ei, spriteMaterial.getInstanceComponent(rnd.NextUInt(0, (uint) sprites.Length)));
                        ecb.SetComponent(ei, new SpriteInstanceSpawning()
                        {
                            spriteIndex = rnd.NextUInt(0, (uint) sprites.Length),
                            spriteSheet = e
                        });
                    }
                    //
                    //
  
                    
                    //instanceBuffer.Resize(spriteMaterial.activeInstances > 0 ? spriteMaterial.activeInstances : 1, NativeArrayOptions.ClearMemory);
                    //instanceTransforms.Resize(spriteMaterial.activeInstances > 0 ? spriteMaterial.activeInstances : 1, NativeArrayOptions.ClearMemory);
                    
                    ecb.AddComponent(e, spriteMaterial);
                    ecb.AddComponent(e, new SpriteMaterialComponentInstancesCounter()
                    {
                        activeInstances = 0
                    });
                    ecb.RemoveComponent<SpriteSheetInitComponent>(e);
                })
                .WithoutBurst()
                .Run();
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
    
    
    
    
}

