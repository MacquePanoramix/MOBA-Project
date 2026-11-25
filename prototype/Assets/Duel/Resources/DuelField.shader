Shader "MOBA/Duel field"
{
    Properties { _Color ("Tint", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct FieldVertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };
            struct FieldVaryings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
            };

            FieldVaryings vert(FieldVertex v)
            {
                FieldVaryings o;
                o.position = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float light = dot(n, normalize(float3(-0.48, 0.85, -0.32)));
                float3 shade = lerp(float3(0.53, 0.61, 0.67), float3(1.07, 1.04, 0.98), saturate(light * 0.69 + 0.31));
                o.color = fixed4(v.color.rgb * shade * _Color.rgb, 1);
                return o;
            }

            fixed4 frag(FieldVaryings i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
