Shader "Custom/URP/AnimMapShaderDotsInstancing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AnimMap ("AnimMap", 2D) = "white" {}
        _AnimLen ("Anim Length", Range(0, 10)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma shader_feature _ _DOTS_INSTANCING_ON

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

                float animMap_x = i.vertex.x / _ScreenParams.x;
                float animMap_y = f;

                fixed4 col = tex2D(_AnimMap, float2(animMap_x, animMap_y));

                return col;
            }
            ENDCG
        }
    }
}
