Shader "P1/Cute Toon"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _ShadowColor ("Toon Shadow Color", Color) = (0.62, 0.42, 0.58, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.48
        _ShadowSoftness ("Shadow Softness", Range(0.01, 0.5)) = 0.12
        _RimColor ("Soft Rim Color", Color) = (1, 0.72, 0.86, 1)
        _RimStrength ("Soft Rim Strength", Range(0, 1)) = 0.16
        _RimPower ("Soft Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf P1Toon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _ShadowColor;
        half _ShadowThreshold;
        half _ShadowSoftness;
        fixed4 _RimColor;
        half _RimStrength;
        half _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 baseSample = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = baseSample.rgb;
            o.Alpha = baseSample.a;
            o.Specular = 0;
            o.Gloss = 0;
        }

        inline half4 LightingP1Toon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half halfLambert = dot(s.Normal, lightDir) * 0.5h + 0.5h;
            half lightAmount = saturate(halfLambert * atten);
            half band = smoothstep(
                _ShadowThreshold - _ShadowSoftness,
                _ShadowThreshold + _ShadowSoftness,
                lightAmount);

            half3 litColor = s.Albedo * _LightColor0.rgb;
            half3 shadowColor = s.Albedo * _ShadowColor.rgb;
            half3 toonColor = lerp(shadowColor, litColor, band);
            half rim = pow(1.0h - saturate(dot(normalize(s.Normal), normalize(viewDir))), _RimPower);
            toonColor += _RimColor.rgb * rim * _RimStrength;
            return half4(toonColor, s.Alpha);
        }
        ENDCG

    }

    Fallback "Diffuse"
}
