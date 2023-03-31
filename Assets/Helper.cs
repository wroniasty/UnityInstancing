using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public struct InstanceData
    {
        public float4 color;
        public uint spriteIndex;
    }


    public enum AnimationLoopType
    {
        Restart,
        PingPong,
        Stop
    }

    public struct AnimationFrame
    {
        public uint spriteIndex;
        public float frameTime;
    }
    
    public struct AnimationData
    {
        public AnimationFrame[] frames;
        public AnimationLoopType loop;
    }

    public struct AnimationState
    {
        public int animationIndex;
        public int currentFrameIndex;
        public float currentFrameTime;
    }
    
    public struct EntityData
    {
        public float heading;
        public float2 position;
        public float2 scale;
        public float omega;
        public uint spriteIndex;
        public AnimationState animation;
    }

    public struct SpriteData
    {
        public float4 uv;
    }
    
    
    [BurstCompile]
    struct InstanceUpdateJob : IJobParallelFor
    {
        public NativeArray<InstanceData> instance_data;
        public NativeArray<EntityData> entity_data;
        public NativeArray<Matrix4x4> transforms;
        public float deltaTime;
        public float4 color;
        public int spriteIndex;

        public void Execute(int index)
        {
            var idata = instance_data[index];
            var edata = entity_data[index];
            var t = transforms[index];
            //idata.color = color;

            edata.heading += edata.omega * deltaTime;

            t = Matrix4x4.Translate(new float3(edata.position, 0)) 
                * Matrix4x4.Rotate(Quaternion.Euler(0,0,edata.heading)) 
                * Matrix4x4.Scale(new float3(edata.scale, 1f));
            if (spriteIndex >= 0)
            {
                idata.spriteIndex = (uint) spriteIndex;
            } else 
                idata.spriteIndex = edata.spriteIndex;
            transforms[index] = t;
            instance_data[index] = idata;
            entity_data[index] = edata;
        }
    }    
    public class Helper
    {
        public static Mesh Quad()
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-0.5f, -0.5f, 0);
            vertices[1] = new Vector3(0.5f, -0.5f, 0);
            vertices[2] = new Vector3(-0.5f, 0.5f, 0);
            vertices[3] = new Vector3(0.5f, 0.5f, 0);
            mesh.vertices = vertices;
            int[] tri = new int[6];
            tri[0] = 0;
            tri[1] = 2;
            tri[2] = 1;
            tri[3] = 2;
            tri[4] = 3;
            tri[5] = 1;
            mesh.triangles = tri;
            Vector3[] normals = new Vector3[4];
            normals[0] = -Vector3.forward;
            normals[1] = -Vector3.forward;
            normals[2] = -Vector3.forward;
            normals[3] = -Vector3.forward;
            mesh.normals = normals;
            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);
            mesh.uv = uv;
            return mesh;
        }        
    }

    public class AnimatedSpriteSheet
    {
        public Material material;
        
    }
}