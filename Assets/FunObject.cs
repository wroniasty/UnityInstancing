using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FunObject : MonoBehaviour
{
    public float speed = 0f;
    public float rotating = 0f;
    // Start is called before the first frame update
    void Start()
    {
        speed = 0f;
        
    }

    // Update is called once per frame
    void Update()
    {
        
        rotating = 0f;
        //Matrix4x4 move = Matrix4x4.Translate(new Vector3(velocity.x, velocity.y, 0f));
        if (Input.GetKey(KeyCode.A))
        {
            rotating = 1f;
        } else if (Input.GetKey(KeyCode.D))
        {
            rotating = -1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            speed += 0.1f;
        } else if (Input.GetKey(KeyCode.S))
        {
            speed -= 0.1f;
        } else if (Input.GetKey(KeyCode.Space))
        {
            speed = 0f;
        }
        
        
        
        transform.Translate(-transform.up * speed);
        transform.Rotate(0f, 0f, rotating * 10f);
        
        
    }
}
