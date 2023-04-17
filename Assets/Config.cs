using System;
using System.Collections;
using System.Collections.Generic;
using Squad.SpriteECS;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

public class Config : MonoBehaviour
{
    private MRandom _rnd;
    // Start is called before the first frame update
    public void Start()
    {
        Application.targetFrameRate = 15;
        _rnd = new MRandom((uint) DateTime.Now.Ticks);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnGUI()
    {
        //GUI.Button(Rect.)
        
        //
        // if (GUI.Button(new Rect(10, 10, 300, 60), "Add"))
        // {
        //     var s = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SpriteSheetBakingSystem>();
        //     
        //     var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        //     var ec = em.CreateArchetype(
        //         typeof(LocalTransform), typeof(SpriteInstanceSpawning));
        //     
        //     var e = em.CreateEntity(ec);
        //     em.SetComponentData(e, new SpriteInstanceSpawning()
        //     {
        //         spriteSheet = s.spritesheets[0].spriteSheet,
        //         spriteIndex = _rnd.NextUInt(0, (uint)s.spritesheets[0].spritesCount) 
        //     });
        //     em.SetComponentData(e, LocalTransform.FromPosition(_rnd.NextFloat3(-10, 10)));
        // }

    }
}
