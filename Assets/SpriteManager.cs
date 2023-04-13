using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DefaultNamespace;
using Squad.SpriteECS;
using TreeEditor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;
using MRandom = Unity.Mathematics.Random;

namespace Squad.NoECS
{

    public struct SpriteData
    {
        public float4 uv;
        public float2 pivot;
    }
    
    public struct SpriteInstanceShaderData
    {
        public float4 color;
        public int spriteIndex;
    }

    [Serializable]
    public class RuntimeSpritesheetInfo
    {
        public Sprite[] sprites;

        public NativeArray<SpriteData> SpriteDataArray;
        public GraphicsBuffer SpriteDataBuffer;

        public NativeArray<Matrix4x4> SpriteInstanceTransformsArray;
        public Matrix4x4[] SpriteInstanceTransformsArrayCopy;
        public NativeArray<SpriteInstanceShaderData> SpriteInstanceShaderDataArray;
        public GraphicsBuffer SpriteInstanceDataBuffer;

        public int activeInstances;
        public int currentBufferSize;

        public Material material;
        private Mesh quad;

        public int SpritesCount
        {
            get => sprites.Length;
        }

        public RuntimeSpritesheetInfo()
        {
            quad = Helper.Quad();
        }
        
        public int SpawnInstance(int spriteIndex, Matrix4x4 transform, float4 color)
        {
            if (activeInstances > currentBufferSize / 2)
            {
                Resize(currentBufferSize * 2, activeInstances >= currentBufferSize - 1);
            }
            int newIndex = activeInstances++;
            SpriteInstanceTransformsArray[newIndex] = transform;
            SpriteInstanceShaderDataArray[newIndex] = new SpriteInstanceShaderData()
            {
                spriteIndex = spriteIndex,
                color = color
            };
            return newIndex;
        }

        public void DespawnInstance(int bufferIndex)
        {
            if (bufferIndex < activeInstances - 1)
            {
                //move last instance into the empty spot
                SpriteInstanceTransformsArray[bufferIndex] = SpriteInstanceTransformsArray[activeInstances - 1];
                SpriteInstanceShaderDataArray[bufferIndex] = SpriteInstanceShaderDataArray[activeInstances - 1];
            }
            activeInstances--;
        }

        public void Resize(int newSize, bool immediate)
        {
            SpriteInstanceTransformsArray.ResizeArray(newSize);
            SpriteInstanceTransformsArrayCopy = new Matrix4x4[newSize];
            SpriteInstanceShaderDataArray.ResizeArray(newSize);
            SpriteInstanceDataBuffer.Release(); 
            SpriteInstanceDataBuffer= new GraphicsBuffer(GraphicsBuffer.Target.Structured, newSize,
                Marshal.SizeOf<SpriteInstanceShaderData>());
            currentBufferSize = newSize;

        }

        public void Render()
        {
            if (activeInstances > 0)
            {
                //var transformsActive = SpriteInstanceTransformsArray.Slice(0, activeInstances).ToArray();
                SpriteInstanceDataBuffer.SetData(SpriteInstanceShaderDataArray);
                material.SetBuffer("_Instance", SpriteInstanceDataBuffer);
                SpriteInstanceTransformsArray.CopyTo(SpriteInstanceTransformsArrayCopy);
                Graphics.DrawMeshInstanced(quad, 0, material, SpriteInstanceTransformsArrayCopy, activeInstances);
                
            }            
        }

        public static RuntimeSpritesheetInfo FromTexture2D(Texture2D texture)
        {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>()
                    .ToArray();
                
                var rt = new RuntimeSpritesheetInfo()
                {
                    sprites = sprites,
                    activeInstances = 0,
                    material = new Material(Shader.Find("Squad/DrawMeshInstanced1"))
                    {
                        mainTexture = texture,
                        enableInstancing = true
                    },
                    currentBufferSize = 8192,
                    SpriteDataArray = new NativeArray<SpriteData>(sprites.Length, Allocator.Persistent),
                    SpriteDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sprites.Length, Marshal.SizeOf<SpriteData>()),
                    SpriteInstanceTransformsArray = new NativeArray<Matrix4x4>(8192, Allocator.Persistent),
                    SpriteInstanceTransformsArrayCopy = new Matrix4x4[8192],
                    SpriteInstanceShaderDataArray = new NativeArray<SpriteInstanceShaderData>(8192, Allocator.Persistent),
                    SpriteInstanceDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 8192, Marshal.SizeOf<SpriteInstanceShaderData>())
                };
                
                for (var sliceIndex = 0; sliceIndex < sprites.Length; sliceIndex++)
                {
                    var sprite = sprites[sliceIndex];
                    float2 uv0 = sprite.uv[1] - sprite.uv[2]; // uv[2] should contain the texcoord with largest x,y
                    float2 uv1 = sprite.uv[2];  // uv[2] should contain the texcoord with smallest x,y

                    rt.SpriteDataArray[sliceIndex] = new SpriteData()
                    {
                        uv = new float4(uv0, uv1),
                        pivot = sprite.pivot / sprite.pixelsPerUnit
                    };
                }
                
                rt.SpriteDataBuffer.SetData(rt.SpriteDataArray);
                rt.material.SetBuffer("_Sprites", rt.SpriteDataBuffer);

                return rt;
        }
        
        
    }
    
    public class SpriteManager : MonoBehaviour
    {
        public Texture2D[] textures;
        public int demoEntities = 1000;

        
        public RuntimeSpritesheetInfo[] runtimeInfo;

        public Matrix4x4[] transformsUsedLast;

        
        
        // Start is called before the first frame update
        void Start()
        {
            runtimeInfo = new RuntimeSpritesheetInfo[textures.Length];
            
            for (var i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                var rt = runtimeInfo[i] = RuntimeSpritesheetInfo.FromTexture2D(texture);
            }

        }

        public void OnGUI()
        {
            int toSpawn = 0, toDespawn = 0;
            if (GUI.Button(new Rect(20, 10, 300, 50), "Spawn 1"))
            {
                toSpawn = 1;
            }
            if (GUI.Button(new Rect(20, 60, 300, 50), "Spawn 500"))
            {
                toSpawn = 500;
            }
            if (GUI.Button(new Rect(320, 10, 300, 50), "Despawn 1"))
            {
                toDespawn = 1;
            }
            if (GUI.Button(new Rect(320, 60, 300, 50), "Despawn 500"))
            {
                toDespawn = 500;
            }

            if (toSpawn > 0)
            {
                MRandom rnd = new MRandom((uint) DateTime.Now.Ticks);
                for (var i = 0; i < toSpawn; i++)
                {
                    var rt = runtimeInfo[rnd.NextInt(0, runtimeInfo.Length)];
                    rt.SpawnInstance(rnd.NextInt(0, rt.SpritesCount),
                        Matrix4x4.Translate(new Vector3(
                            rnd.NextFloat(-20, 20),
                            rnd.NextFloat(-20, 20),
                            0
                        )), 1.0f
                    );
                }
            } else if (toDespawn > 0)
            {
                MRandom rnd = new MRandom((uint) DateTime.Now.Ticks);
                for (var i = 0; i < toDespawn; i++)
                {
                    var rt = runtimeInfo[rnd.NextInt(0, runtimeInfo.Length)];
                    if (rt.activeInstances > 0)
                        rt.DespawnInstance(rnd.NextInt(0, rt.activeInstances));
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            foreach (var rt in runtimeInfo)
            {
                rt.Render();
            }
        }
    }

}