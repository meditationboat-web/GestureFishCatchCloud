Shader "GestureFistGame/Meadow"
{
  Properties
  {
    _IsGrass ("Grass Mesh", Float) = 0
    _BaseColor ("Ground Base", Color) = (0.08, 0.22, 0.06, 1)
    _MeadowColor ("Meadow Color", Color) = (0.28, 0.52, 0.12, 1)
    _WetColor ("Wet Soil Color", Color) = (0.28, 0.20, 0.10, 1)
    _RootColor ("Grass Root", Color) = (0.04, 0.18, 0.03, 1)
    _TipColor ("Grass Tip", Color) = (0.46, 0.82, 0.12, 1)
    _NoiseScale ("Color Noise Scale", Range(0.01, 2)) = 0.12
    _NoiseStrength ("Color Noise Strength", Range(0, 1)) = 0.2
    _WindStrength ("Wind Strength", Range(0, 0.2)) = 0.05
    _WindSpeed ("Wind Speed", Range(0, 4)) = 1.1
    _WindScale ("Wind Scale", Range(0.1, 4)) = 0.65
    _HeightRange ("Blade Height", Range(0.1, 3)) = 0.7
    _Smoothness ("Smoothness", Range(0, 1)) = 0.12
  }

  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
    Cull Off
    ZWrite On

    Pass
    {
      Name "Forward"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
        half _IsGrass;
        half4 _BaseColor;
        half4 _MeadowColor;
        half4 _WetColor;
        half4 _RootColor;
        half4 _TipColor;
        float _NoiseScale;
        float _NoiseStrength;
        float _WindStrength;
        float _WindSpeed;
        float _WindScale;
        float _HeightRange;
        float _Smoothness;
      CBUFFER_END

      struct Attributes
      {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
      };
      struct Varyings
      {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        float2 uv : TEXCOORD2;
        half4 color : COLOR;
        half height : TEXCOORD3;
        half fogFactor : TEXCOORD4;
      };

      float Hash21(float2 p)
      {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
      }

      float3 DeformGrass(float3 positionOS, float3 positionWS, out float height)
      {
        height = saturate((positionOS.y + 0.02) / max(0.1, _HeightRange));
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
        float3 originalWS = TransformObjectToWorld(input.positionOS.xyz);
        float height;
        float3 positionOS = input.positionOS.xyz;
        if (_IsGrass > 0.5) positionOS = DeformGrass(positionOS, originalWS, height);
        else height = 0;
        VertexPositionInputs pos = GetVertexPositionInputs(positionOS);
        output.positionCS = pos.positionCS;
        output.positionWS = pos.positionWS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = input.uv;
        output.color = input.color;
        output.height = height;
        output.fogFactor = ComputeFogFactor(pos.positionCS.z);
        return output;
      }

      half4 frag(Varyings input) : SV_Target
      {
        float noise = Hash21(floor(input.positionWS.xz * max(0.01, _NoiseScale)));
        half3 color;
        if (_IsGrass > 0.5)
        {
          color = lerp(_RootColor.rgb, _TipColor.rgb, saturate(input.height));
          color *= 1.0 + (noise - 0.5) * _NoiseStrength;
          color *= 0.78 + input.color.r * 0.30;
        }
        else
        {
          half meadowMix = 0.50;
          half3 meadow = lerp(_BaseColor.rgb, _MeadowColor.rgb, meadowMix);
          // Keep the ground pass independent from interpolated mesh colors;
          // those values are authored for edge bookkeeping and can reveal
          // the grid as diagonal wedges at overview scale.
          color = meadow;
        }

        // The meadow is a broad painted surface. Its low-frequency height
        // variation is there for the silhouette and contact shadows, not for
        // giant diagonal lighting facets across the whole field. Use a stable
        // up normal for the ground while keeping the authored normals for the
        // small grass cards.
        half3 normal = _IsGrass > 0.5 ? normalize(input.normalWS) : half3(0, 1, 0);
        half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
        Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
        half diffuse = saturate(dot(normal, mainLight.direction) * 0.5 + 0.5);
        half3 ambient = SampleSH(normal);
        // Keep the palette close to the reference's soft painted greens. The
        // previous ambient + direct-light sum pushed the meadow into neon
        // yellow and made every terrain triangle read as a separate shard.
        half3 lit = color * (_IsGrass > 0.5
          ? (ambient * 0.58 + mainLight.color * diffuse * 0.92)
          : (0.78 + diffuse * 0.22));
        // Let the meadow receive the same soft shadow shapes as the imported
        // trees and fences. Without this attenuation the props float above a
        // uniformly lit plane even when the URP light is casting shadows.
        lit *= lerp(0.58, 1.0, mainLight.shadowAttenuation);
        half rim = pow(1.0 - saturate(dot(normal, viewDir)), 3.2);
        lit += (_IsGrass > 0.5 ? half3(0.52, 0.88, 0.18) : half3(0.22, 0.40, 0.12)) * rim * 0.12;
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
      #pragma vertex shadowVert
      #pragma fragment shadowFrag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

      CBUFFER_START(UnityPerMaterial)
        half _IsGrass;
        float _WindStrength;
        float _WindSpeed;
        float _WindScale;
        float _HeightRange;
      CBUFFER_END
      struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
      struct ShadowVaryings { float4 positionCS : SV_POSITION; };
      float HashShadow(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

      ShadowVaryings shadowVert(ShadowAttributes input)
      {
        ShadowVaryings output;
        float3 positionOS = input.positionOS.xyz;
        if (_IsGrass > 0.5)
        {
          float3 positionWS = TransformObjectToWorld(positionOS);
          float height = saturate((positionOS.y + 0.02) / max(0.1, _HeightRange));
          float phase = HashShadow(floor(positionWS.xz * _WindScale)) * 6.28318;
          float bend = (sin(_Time.y * _WindSpeed + dot(positionWS.xz, float2(.72, .31)) + phase) * .65 + cos(_Time.y * (_WindSpeed * .73) + dot(positionWS.xz, float2(-.22, .91)) + phase * 1.7) * .35) * _WindStrength * height * height;
          positionOS += float3(bend, 0, bend * .55);
        }
        float3 positionWS = TransformObjectToWorld(positionOS);
        output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, TransformObjectToWorldNormal(input.normalOS), _MainLightPosition.xyz));
        return output;
      }
      half4 shadowFrag(ShadowVaryings input) : SV_Target { return 0; }
      ENDHLSL
    }
  }
  FallBack Off
}
