Shader "GestureFistGame/StylizedGrass"
{
  Properties
  {
    _BaseColor ("Grass Base", Color) = (0.08, 0.30, 0.10, 1)
    _TipColor ("Grass Tip", Color) = (0.55, 0.86, 0.16, 1)
    _WindStrength ("Wind Strength", Range(0, 0.18)) = 0.045
    _WindSpeed ("Wind Speed", Range(0, 4)) = 1.1
    _WindScale ("Wind Scale", Range(0.1, 4)) = 0.65
    _HeightRange ("Height Range", Range(0.1, 3)) = 1.0
    _VariationStrength ("Color Variation", Range(0, 0.35)) = 0.14
    _RimColor ("Rim Light", Color) = (0.64, 0.95, 0.28, 1)
    _RimPower ("Rim Power", Range(0.5, 8)) = 3.2
    _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.25
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
    Cull Off
    ZWrite On
    AlphaToMask On

    Pass
    {
      Name "Forward"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _TipColor;
        half4 _RimColor;
        float _WindStrength;
        float _WindSpeed;
        float _WindScale;
        float _HeightRange;
        float _VariationStrength;
        float _RimPower;
        float _Cutoff;
      CBUFFER_END

      struct Attributes
      {
        UNITY_VERTEX_INPUT_INSTANCE_ID
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
      };
      struct Varyings
      {
        UNITY_VERTEX_INPUT_INSTANCE_ID
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        float2 uv : TEXCOORD2;
        half height : TEXCOORD3;
        half fogFactor : TEXCOORD4;
      };

      float Hash21(float2 p)
      {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
      }

      float3 BendGrass(float3 positionOS, float3 positionWS, out float height)
      {
        height = saturate((positionOS.y + 0.08) / max(0.1, _HeightRange));
        float2 cell = floor(positionWS.xz * _WindScale);
        float phase = Hash21(cell) * 6.28318;
        float gust = sin(_Time.y * _WindSpeed + dot(positionWS.xz, float2(0.72, 0.31)) + phase);
        float gust2 = cos(_Time.y * (_WindSpeed * 0.73) + dot(positionWS.xz, float2(-0.22, 0.91)) + phase * 1.7);
        float bend = (gust * 0.65 + gust2 * 0.35) * _WindStrength * height * height;
        positionOS.x += bend;
        positionOS.z += bend * 0.55;
        return positionOS;
      }

      Varyings vert(Attributes input)
      {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        float3 positionWS0 = TransformObjectToWorld(input.positionOS.xyz);
        float height;
        float3 positionOS = BendGrass(input.positionOS.xyz, positionWS0, height);
        VertexPositionInputs pos = GetVertexPositionInputs(positionOS);
        output.positionCS = pos.positionCS;
        output.positionWS = pos.positionWS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = input.uv;
        output.height = height;
        output.fogFactor = ComputeFogFactor(pos.positionCS.z);
        return output;
      }

      half4 frag(Varyings input) : SV_Target
      {
        float variation = (Hash21(floor(input.positionWS.xz * 1.7)) - 0.5) * 2.0;
        half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, input.height);
        color *= 1.0 + variation * _VariationStrength;
        half3 normal = normalize(input.normalWS);
        half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
        Light mainLight = GetMainLight();
        half diffuse = saturate(dot(normal, mainLight.direction) * 0.5 + 0.5);
        half3 ambient = SampleSH(normal);
        half3 lit = color * (ambient * 0.55 + mainLight.color * diffuse * 0.85);
        half rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
        lit += _RimColor.rgb * rim * (0.10 + input.height * 0.18);
        lit = MixFog(lit, input.fogFactor);
        return half4(lit, 1);
      }
      ENDHLSL
    }

    Pass
    {
      Name "ShadowCaster"
      Tags { "LightMode"="ShadowCaster" }
      ZWrite On
      ZTest LEqual
      ColorMask 0
      HLSLPROGRAM
      #pragma vertex ShadowVert
      #pragma fragment ShadowFrag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
      CBUFFER_START(UnityPerMaterial)
        float _WindStrength;
        float _WindSpeed;
        float _WindScale;
        float _HeightRange;
      CBUFFER_END
      struct ShadowAttributes { UNITY_VERTEX_INPUT_INSTANCE_ID float4 positionOS : POSITION; float3 normalOS : NORMAL; };
      struct ShadowVaryings { UNITY_VERTEX_INPUT_INSTANCE_ID float4 positionCS : SV_POSITION; };
      float HashGrass(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
      ShadowVaryings ShadowVert(ShadowAttributes input)
      {
        ShadowVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float h = saturate((input.positionOS.y + 0.08) / max(0.1, _HeightRange));
        float phase = HashGrass(floor(positionWS.xz * _WindScale)) * 6.28318;
        float bend = (sin(_Time.y * _WindSpeed + dot(positionWS.xz, float2(.72, .31)) + phase) * .65 + cos(_Time.y * (_WindSpeed * .73) + dot(positionWS.xz, float2(-.22, .91)) + phase * 1.7) * .35) * _WindStrength * h * h;
        float3 deformed = input.positionOS.xyz + float3(bend, 0, bend * .55);
        float3 ws = TransformObjectToWorld(deformed);
        output.positionCS = TransformWorldToHClip(ApplyShadowBias(ws, TransformObjectToWorldNormal(input.normalOS), _MainLightPosition.xyz));
        return output;
      }
      half4 ShadowFrag(ShadowVaryings input) : SV_Target { return 0; }
      ENDHLSL
    }
  }
  FallBack Off
}
