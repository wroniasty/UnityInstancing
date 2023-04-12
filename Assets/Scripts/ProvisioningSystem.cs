using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Squad.Sprites
{
/*
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial class SpritesProvisioningGroup : ComponentSystemGroup {}
    
    public struct SpriteEntityInit : IComponentData
    {
        public Entity spriteSheet;
        public int spriteIndex;
    }

    public struct SpriteSheetInstanceComponent : ISharedComponentData
    {
        public Entity spriteSheet;
    }
    
    public struct SpriteComponent : IComponentData
    {
        public int spriteIndex;
        public int bufferIndex;
    }
    
    [UpdateInGroup(typeof(SpritesProvisioningGroup))]
    public partial struct ProvisioningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            Debug.Log($"Creating system [{this.GetType().FullName}]");    
        }

        public void OnUpdate(ref SystemState state)
        {
            var initQuery = SystemAPI.QueryBuilder().WithAll<SpriteEntityInit>().Build();
            var entitiesToInit = initQuery.ToEntityArray(Allocator.Temp);

        }
    }
*/
}