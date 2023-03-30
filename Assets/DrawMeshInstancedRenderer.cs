using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;
using MRandom = Unity.Mathematics.Random;
public class DrawMeshInstancedRenderer : MonoBehaviour
{
    public Mesh mesh;
    public Texture2D texture;
    public Material material;

    private int instances = 100;

    public Matrix4x4[] instance_transform;

    public List<Shader> shaders;
    
    // Start is called before the first frame update
    void Start()
    {
        instance_transform = new Matrix4x4[instances];
        var rnd = MRandom.CreateFromIndex(1);
        for (var i = 0; i < instance_transform.Length; i++)
        {
            var translate = rnd.NextFloat3() * 10 - 5;
            translate.z = 0;
            instance_transform[i] = Matrix4x4.identity * Matrix4x4.Translate(translate);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(5,0,1)),  material, 0);
        
        Graphics.DrawMeshInstanced(mesh, 0, material, instance_transform);
    }
}
