using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UI;

namespace Squad
{

    public class SpriteMaterialComponent : IComponentData, IEquatable<SpriteMaterialComponent>
    {
        public Material material;
        public int slices;

        public bool Equals(SpriteMaterialComponent other)
        {
            return material = other.material;
        }

        public override int GetHashCode()
        {
            return material ? material.GetHashCode() : 1371622046;
        }
    }



    public class SpriteSheetInitComponent : IComponentData
    {
        public Texture2D texture;
    }

    [InternalBufferCapacity(16)]
    public struct SpriteSliceComponent : IBufferElementData
    {
        public float4 uv;
        public float2 pivot;
    }

    public partial class SpriteSheetBakingSystem : SystemBase
    {
        public void PrepareSpriteSheetEntity(Texture2D texture)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>()
                .ToArray();

            var ssEntity = EntityManager.CreateEntity();
            var spriteMaterial = new SpriteMaterialComponent()
            {
                material = new Material(Shader.Find("Squad/DrawMeshInstanced1"))
                {
                    mainTexture = texture
                },
                slices = sprites.Length
            };
            
            EntityManager.AddComponent(ssEntity, typeof(SpriteMaterialComponent));
            EntityManager.SetComponentData(ssEntity, spriteMaterial);
            var sliceBuffer = EntityManager.AddBuffer<SpriteSliceComponent>(ssEntity);
        }

        protected override void OnUpdate()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            Entities
                .WithAll<SpriteSheetInitComponent>()
                .ForEach((Entity e, in SpriteSheetInitComponent i) =>
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(i.texture)).OfType<Sprite>()
                        .ToArray();
                    Debug.Log($"Init sprites {i.texture.name} ({sprites.Length})");
                    var sliceBufer = ecb.AddBuffer<SpriteSliceComponent>(1, e);
                    ecb.SetName(1, e, new FixedString64Bytes($"SpriteSheet({i.texture.name})"));
                    
                    for (var sliceIndex = 0; sliceIndex < sprites.Length; sliceIndex++)
                    {
                        var sprite = sprites[sliceIndex];
                        sliceBufer.Add(new SpriteSliceComponent()
                        {
                            uv = new float4(sprite.uv[0], sprite.uv[1]),
                            pivot = sprite.pivot / sprite.pixelsPerUnit
                        });
                    }
                    
                    ecb.RemoveComponent<SpriteSheetInitComponent>(2, e);
                })
                .WithoutBurst()
                .Run();
            
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
    
    
    
    
}

