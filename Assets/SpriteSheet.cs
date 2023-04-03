using Unity.Entities;
using UnityEngine;

namespace Squad
{
    public class SpriteSheet : MonoBehaviour
    {
        public Texture2D[] textures;
    }
    
    public struct SpriteSheetContainer : IComponentData {}
    
    public class SpriteSheetBaker : Baker<Squad.SpriteSheet>
    {
        public override void Bake(SpriteSheet authoring)
        {
            Debug.Log("Baking");
            //var parent = GetEntity(TransformUsageFlags.WorldSpace); 
            //AddComponent<SpriteSheetContainer>(parent);
            for (var i = 0; i < authoring.textures.Length; i++)
            {
                var ess = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponentObject(ess, new SpriteSheetInitComponent() { texture = authoring.textures[i]});
            }

        }
    }    
}