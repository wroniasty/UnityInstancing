Shader "Squad/DrawMeshInstanced1"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct InstanceData
            {
                fixed4 color;
                uint spriteIndex;
            };

            struct SpriteData
            {
                float4 uv;
            };
            
            StructuredBuffer<InstanceData> instance;
            StructuredBuffer<SpriteData> sprites;

            v2f vert (appdata_full v,  uint instanceID : SV_InstanceID)
            {
                UNITY_SETUP_INSTANCE_ID(v);	
                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);	
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 uv = sprites[instance[instanceID].spriteIndex].uv;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uv = o.uv * uv.xy + uv.zw;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                // sample the texture
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col;
                col = tex2D(_MainTex, i.uv) * instance[instanceID].color;
                UNITY_APPLY_FOG(i.fogCoord, col);
                clip(col.a - 1.0f / 256.f);
                return col;
            }
            ENDCG
        }
    }

}
