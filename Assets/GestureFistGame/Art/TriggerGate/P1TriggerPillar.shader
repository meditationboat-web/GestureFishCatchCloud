Shader "P1/Trigger Pillar"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.23, 0.43, 0.56, 1)
        _BottomColor ("Bottom Color", Color) = (0.045, 0.09, 0.14, 1)
        _HighlightColor ("Edge Highlight", Color) = (0.35, 0.72, 0.88, 1)
        _HighlightStrength ("Edge Highlight Strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _HighlightColor;
            half _HighlightStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = lerp(_BottomColor, _TopColor, saturate(input.uv.y));
                half edge = 1.0h - saturate(abs(input.uv.x - 0.5h) * 2.0h);
                color.rgb = lerp(color.rgb, _HighlightColor.rgb, edge * _HighlightStrength);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
