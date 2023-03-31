using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Transactions;
using DefaultNamespace;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using AnimationState = DefaultNamespace.AnimationState;
using URandom = UnityEngine.Random;
using MRandom = Unity.Mathematics.Random;

public class DrawMeshInstancedRenderer : MonoBehaviour
{
    protected Mesh mesh;
    protected GraphicsBuffer instanceBuffer;
    protected GraphicsBuffer spriteBuffer;
    
    public Texture2D texture;
    public Material material;

    public int instances = 100;
    [ReadOnly] public int spriteSheetSize = 0;
    
    protected NativeArray<InstanceData> instanceData;
    protected NativeArray<EntityData> entityData;
    protected NativeArray<Matrix4x4> instanceTransform;
    protected AnimationData[] animations;
    protected MRandom rnd;

    public int globalSpriteIndex = -1;

    protected Sprite[] sprites;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 15;
        rnd = new MRandom((uint) DateTime.Now.Ticks);
        //instance_transform = new Matrix4x4[instances];
        instanceTransform = new NativeArray<Matrix4x4>(instances, Allocator.Persistent);
        instanceData = new NativeArray<InstanceData>(instances, Allocator.Persistent);
        entityData = new NativeArray<EntityData>(instances, Allocator.Persistent);

        sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>()
            .ToArray();
        spriteSheetSize = sprites.Length;

        animations =  
            new[]
            {
                new AnimationData()
                {
                    frames =  
                        new[]
                        {
                            new AnimationFrame() { frameTime = 0.6f, spriteIndex = 0 },
                            new AnimationFrame() { frameTime = 0.6f, spriteIndex = 1 },
                            new AnimationFrame() { frameTime = 0.6f, spriteIndex = 2 }
                        }, 
                    loop = AnimationLoopType.Restart
                },
                new AnimationData()
                {
                    frames =  
                        new[]
                        {
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 3 },
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 4 },
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 5 }
                        }, 
                    loop = AnimationLoopType.Restart
                },
                new AnimationData()
                {
                    frames =  
                        new[]
                        {
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 6 },
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 7 },
                            new AnimationFrame() { frameTime = 0.4f, spriteIndex = 8 }
                        }, 
                    loop = AnimationLoopType.Restart
                },
            };
        
        for (var i = 0; i < instances; i++)
        {
            var translate = rnd.NextFloat3() * 10 - 5;
            translate.z = 0;
            // instance_transform[i] = Matrix4x4.identity * Matrix4x4.Translate(translate)
            //     * Matrix4x4.Rotate(Quaternion.Euler(0,0, rnd.NextFloat(0, 360)))
            //     * Matrix4x4.Scale(rnd.NextFloat3())
            //     ;
            // instanceTransform[i] = instance_transform[i];
            InstanceData idata = new InstanceData()
            {
                color = rnd.NextFloat4()
            };
            EntityData edata = new EntityData()
            {
                position = rnd.NextFloat2() * 10 - 5,
                heading = rnd.NextFloat(0, 360),
                //heading = 0,
                scale = rnd.NextFloat(0.25f, 0.75f),
                omega = rnd.NextFloat(-180, 180),
                spriteIndex = rnd.NextUInt(0, (uint) spriteSheetSize),
                animation = new AnimationState()
                {
                    animationIndex = rnd.NextInt(0, animations.Length),
                    currentFrameIndex = 0,
                    currentFrameTime = 0
                }
            };
            instanceData[i] = idata;
            entityData[i] = edata;
        }

        instanceBuffer =
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, instances, Marshal.SizeOf<InstanceData>());

        NativeArray<SpriteData> spriteData = new NativeArray<SpriteData>(spriteSheetSize, Allocator.Persistent);
        for (var i = 0; i < sprites.Length; i++)
        {
            /* Warning: this only works with vertex order defined in MeshExt.Quad() */
            /* Assuming Unity defines Sprite UV in a consistent manner */
            float2 uv0 = sprites[i].uv[1] - sprites[i].uv[2]; // uv[2] should contain the texcoord with largest x,y
            float2 uv1 = sprites[i].uv[2];  // uv[2] should contain the texcoord with smallest x,y
            
            /*
             * in the shader we assume texcoords passed to the vertex program are defined by a unit quad: 0,0 1,0 0,1 1,1
             * so we can multiply them by uv0 and add uv1
             */
            spriteData[i] = new SpriteData()
            {
                uv = new float4(uv0, uv1)
            };
        }

        spriteBuffer =
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, spriteSheetSize, Marshal.SizeOf<SpriteData>());
        spriteBuffer.SetData(spriteData);
        material.SetBuffer("sprites", spriteBuffer);
        material.mainTexture = texture;
        
        spriteData.Dispose();


        
        //instanceBuffer.SetData(n_instanceData);
        //material.SetBuffer("instance", instanceBuffer);
        mesh = Helper.Quad();
    }


    
    // Update is called once per frame
    void Update()
    {
        //Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(5,0,1)),  material, 0);
        for (int i = 0; i < instances; i++)
        {
            var edata = entityData[i];
            
            var anim = animations[edata.animation.animationIndex];
            edata.animation.currentFrameTime += Time.deltaTime;
            if (edata.animation.currentFrameTime > anim.frames[edata.animation.currentFrameIndex].frameTime)
            {
                edata.animation.currentFrameIndex = (edata.animation.currentFrameIndex + 1) % anim.frames.Length;
                edata.animation.currentFrameTime = anim.frames[edata.animation.currentFrameIndex].frameTime; //!!! 
            }

            edata.spriteIndex = anim.frames[edata.animation.currentFrameIndex].spriteIndex;
            entityData[i] = edata;
        }
        var job = new InstanceUpdateJob()
        {
            deltaTime = Time.deltaTime,
            instance_data = instanceData,
            entity_data = entityData,
            transforms = instanceTransform,
            spriteIndex = globalSpriteIndex,
            color = rnd.NextFloat4()
        };
        JobHandle jobHandle = job.Schedule(instances, 64);
        jobHandle.Complete();
        
        instanceBuffer.SetData(instanceData);
        material.SetBuffer("instance", instanceBuffer);
        
        Graphics.DrawMeshInstanced(mesh, 0, material, instanceTransform.ToArray());
    }
    
    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 150, 80), $"Change sprite [{globalSpriteIndex}]"))
        {
            globalSpriteIndex = (globalSpriteIndex + 1) % spriteSheetSize;
            GetComponent<SpriteRenderer>().sprite = sprites[globalSpriteIndex];
        }

        if (globalSpriteIndex >= 0)
        {
            var s = sprites[globalSpriteIndex];
            float2 uv0 = s.uv[1] - s.uv[2];
            float2 uv1 = s.uv[1];
            
            GUI.Label(new Rect(10, 100, 150, 40), $"{s.uv[0]}, {s.uv[1]}, {s.uv[2]}, {s.uv[3]}");
            GUI.Label(new Rect(10, 150, 350, 60), $"{uv0}, {uv1}");
        }
    }
    
}
