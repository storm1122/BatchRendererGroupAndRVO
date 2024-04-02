Shader "chenjd/URP/AnimMapWithShadowShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}

        [Header(AnimMap)]
        _AnimMap("AnimMap", 2D) = "white" {}
    }   

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_t
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            struct UnityPerMaterial
            {
                float4 _AnimLen;
            };

            float _AnimLen;
            sampler2D _AnimMap;

            v2f_t vert(appdata_t v)
            {
                v2f_t o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f_t i) : SV_Target
            {
                float f = _Time.y / _AnimLen;
                f = frac(f);

                float animMap_x = (i.vertex.x + 0.5) / _ScreenParams.x;
                float animMap_y = f;

                fixed4 col = tex2D(_AnimMap, float2(animMap_x, animMap_y));

                return col;
            }
            ENDCG
        }
    }
}
