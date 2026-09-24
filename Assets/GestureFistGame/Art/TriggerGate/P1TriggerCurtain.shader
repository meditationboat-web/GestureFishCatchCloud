Shader "P1/Trigger Curtain"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.31, 0.82, 1, 0.80)
        _BottomColor ("Bottom Color", Color) = (0.025, 0.27, 0.68, 0.84)
        _EdgeColor ("Edge Highlight", Color) = (0.70, 0.96, 1, 0.55)
        _EdgeStrength ("Edge Highlight Strength", Range(0, 1)) = 0.34
        _Opacity ("Opacity", Range(0, 1)) = 0.88
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _EdgeColor;
            half _EdgeStrength;
            half _Opacity;

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
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * _EdgeStrength);
                color.a *= _Opacity;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
