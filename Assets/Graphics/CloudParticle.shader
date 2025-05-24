Shader "SebsGlobe/CloudParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0.1, 3.0)) = 1.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            StructuredBuffer<float4> _ParticleBuffer;
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Softness;
            
            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                float4 particleData = _ParticleBuffer[instanceID];
                float3 particlePos = particleData.xyz;
                float particleSize = particleData.w;
                
                float3 worldPos = particlePos;
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 right = normalize(cross(viewDir, float3(0, 1, 0)));
                float3 up = normalize(cross(right, viewDir));
                
                worldPos += right * v.vertex.x * particleSize;
                worldPos += up * v.vertex.y * particleSize;
                
                o.vertex = UnityWorldToClipPos(worldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = _Color;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float2 uv = i.uv * 2 - 1;
                float dist = length(uv);
                float alpha = 1 - smoothstep(0, 1, dist);
                
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.a *= alpha;
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Particles/Alpha Blended"
} 