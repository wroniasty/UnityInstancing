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
using Unity.Transforms;
using UnityEditor;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

using MRandom = Unity.Mathematics.Random;

namespace Squad
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
            return material = other.material;
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

    public class SpriteInstanceSpawning : IComponentData
    {
        public Entity spriteSheet;
        public uint spriteIndex;
    }

    
    public class SpriteSheetInitComponent : IComponentData
    {
        public Texture2D texture;

        public int demoEntitiesCount = 1000;
    }

    public struct SpriteComponent : IComponentData
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


    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SpriteSheetRenderSystem))]
    public partial class SpriteSheetSpawningSystem : SystemBase
    {
        public EntityQuery query;
        protected override void OnCreate()
        {
            query = GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(), 
                ComponentType.ReadOnly<SpriteInstanceSpawning>()
            );
        }

        protected override void OnUpdate()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();
            var cntrs = GetComponentLookup<SpriteMaterialComponentInstancesCounter>();
            
            Entities
                .WithAll<SpriteInstanceSpawning>()
                .ForEach((Entity e, in SpriteInstanceSpawning i) =>
                {
                    var spc = cntrs[i.spriteSheet];
                    ecb.AddComponent<SpriteComponent>(e);
                    ecb.SetComponent(e, new SpriteComponent()
                    {
                        spriteIndex = i.spriteIndex,
                        spriteSheet = i.spriteSheet,
                        bufferIndex = spc.activeInstances
                    });
                    ecb.SetComponent(i.spriteSheet, new SpriteMaterialComponentInstancesCounter()
                    {
                        activeInstances = spc.activeInstances++
                    });
                    ecb.RemoveComponent<SpriteInstanceSpawning>(e);
                })
                .WithoutBurst()
                .Run();
        }

    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SpriteSheetRenderSystem))]
    public partial class SpriteSheetPreparationSystem : SystemBase
    {
        public EntityQuery query;
        private bool initialized = false;

        protected override void OnCreate()
        {
            query = GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(), 
                ComponentType.ReadOnly<SpriteComponent>()
                );
        }

        protected override void OnUpdate()
        {
            if (!initialized)
            {
                NativeList<LocalTransform> transforms =
                    query.ToComponentDataListAsync<LocalTransform>(Allocator.TempJob, out JobHandle transformsHandle);
                NativeList<SpriteComponent> spriteComponents =
                    query.ToComponentDataListAsync<SpriteComponent>(Allocator.TempJob, out JobHandle spcHandle);
                Dependency = JobHandle.CombineDependencies(Dependency, transformsHandle, spcHandle);

                Entities
                    .WithAll<SpriteMaterialComponent>()
                    .ForEach((Entity e, SpriteMaterialComponent sps, SpriteMaterialComponentInstancesCounter c, ref DynamicBuffer<SpriteInstanceBuffer> ib,
                        ref DynamicBuffer<SpriteInstanceTransformBuffer> tb) =>
                    {
                        if (c.activeInstances > 0)
                        {
                            if (ib.Length != c.activeInstances) ib.Resize(c.activeInstances, NativeArrayOptions.ClearMemory);
                            if (tb.Length != c.activeInstances) tb.Resize(c.activeInstances, NativeArrayOptions.ClearMemory);
                        }
                    })
                    .WithoutBurst()
                    .Run();


                Dependency = Entities
                    .WithReadOnly(transforms)
                    .WithReadOnly(spriteComponents)
                    .ForEach((Entity e, ref DynamicBuffer<SpriteInstanceTransformBuffer> tb,
                        ref DynamicBuffer<SpriteInstanceBuffer> ib) =>
                    {
                        for (var i = 0; i < transforms.Length; i++)
                        {
                            if (spriteComponents[i].spriteSheet == e)
                            {
                                tb[spriteComponents[i].bufferIndex] = new SpriteInstanceTransformBuffer()
                                    { transform = transforms[i].ToMatrix() };
                                ib[spriteComponents[i].bufferIndex] = new SpriteInstanceBuffer()
                                {
                                    spriteIndex = spriteComponents[i].spriteIndex,
                                    color = 1f
                                };
                            }
                        }
                    })
                    .WithDisposeOnCompletion(transforms)
                    .WithDisposeOnCompletion(spriteComponents)
                    .ScheduleParallel(Dependency);
//                initialized = true;
            }
        }
    }
    
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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

