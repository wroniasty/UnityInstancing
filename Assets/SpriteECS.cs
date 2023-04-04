using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DefaultNamespace;
using TreeEditor;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine.UI;

using MRandom = Unity.Mathematics.Random;

namespace Squad
{

    public class SpriteMaterialComponent : IComponentData, IEquatable<SpriteMaterialComponent>, IDisposable
    {
        public Material material;
        public GraphicsBuffer spriteDataBuffer;
        public GraphicsBuffer instanceDataBuffer;
        
        public int slices;
        
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
    }

    
    public class SpriteSheetInitComponent : IComponentData
    {
        public Texture2D texture;
    }

    public struct SpriteComponent : IComponentData
    {
        public Entity spriteSheet;
        public uint spriteIndex;
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
    public partial class SpriteSheetPreparationSystem : SystemBase
    {
        private EntityQuery _spriteInstancesQuery;
        private NativeParallelMultiHashMap<Entity, LocalTransform> spriteTransforms;
        protected override void OnUpdate()
        {
            Entities
                .WithAll<SpriteComponent, LocalTransform>()
                .ForEach((Entity e, SpriteComponent sc, LocalTransform t) =>
                {
                    
                })
                .Schedule();
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
                .ForEach((Entity e, in SpriteMaterialComponent sps, in DynamicBuffer<SpriteInstanceBuffer> instance,
                    in DynamicBuffer<SpriteInstanceTransformBuffer> transform) =>
                {
                    Graphics.DrawMeshInstanced(quad, 0, sps.material, 
                        transform.Reinterpret<Matrix4x4>().ToNativeArray(Allocator.Temp).ToArray());
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


                    var instanceTransforms = ecb.AddBuffer<SpriteInstanceTransformBuffer>(e);
                    var instanceBuffer = ecb.AddBuffer<SpriteInstanceBuffer>(e);

                    for (var instance = 0; instance < 10; instance++)
                    {
                        instanceBuffer.Add(new SpriteInstanceBuffer()
                        {
                            color = rnd.NextFloat4(),
                            spriteIndex = rnd.NextUInt(0, (uint) sprites.Length)
                        });
                        instanceTransforms.Add(new SpriteInstanceTransformBuffer()
                        {
                            transform = LocalTransform.FromPositionRotationScale(
                                rnd.NextFloat3(-10, 10),
                                Quaternion.Euler(0,0, rnd.NextFloat(-180, 180)),
                                rnd.NextFloat(0.25f, 1.25f)
                                ).ToMatrix()
                        });
                    }
                    
                    var instanceDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instanceBuffer.Length,
                        Marshal.SizeOf<SpriteInstanceBuffer>());
                    instanceDataBuffer.SetData(instanceBuffer.ToNativeArray(Allocator.Temp));

                    var spriteMaterial = new SpriteMaterialComponent()
                    {
                        material = new Material(Shader.Find("Squad/DrawMeshInstanced1"))
                        {
                            mainTexture = i.texture,
                            enableInstancing = true
                        },
                        slices = sprites.Length,
                        spriteDataBuffer = spriteDataBuffer,
                        instanceDataBuffer = instanceDataBuffer
                    };
                    
                    spriteMaterial.material.SetBuffer("_Sprites", spriteMaterial.spriteDataBuffer);
                    spriteMaterial.material.SetBuffer("_Instance", instanceDataBuffer);
                    
                    ecb.AddComponent(e, spriteMaterial);
                    ecb.RemoveComponent<SpriteSheetInitComponent>(e);
                })
                .WithoutBurst()
                .Run();
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
    
    
    
    
}

