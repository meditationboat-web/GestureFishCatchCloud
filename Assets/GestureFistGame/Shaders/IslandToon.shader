Shader "GestureFistGame/IslandToon"
{
 Properties
 {
  _BaseColor ("Paint color", Color) = (.39,.54,.41,1)
  _ShadeColor ("Shaded face", Color) = (.23,.38,.30,1)
  _ShadowTint ("Cast shadow tint", Color) = (.76,.82,.72,1)
  _Flat ("Flat ground", Range(0,1)) = 0
  _Threshold ("Shade threshold", Range(-1,1)) = .2
  _Softness ("Shade transition", Range(.001,.4)) = .1
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  // All passes share one material layout. Lit UsePass has a different buffer,
  // which causes incorrect material constants with the SRP Batcher.
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   float4 _ShadeColor;
   float4 _ShadowTint;
   float _Flat;
   float _Threshold;
   float _Softness;
  CBUFFER_END
  struct Attributes {
   float4 positionOS : POSITION;
   float3 normalOS : NORMAL;
   UNITY_VERTEX_INPUT_INSTANCE_ID
  };
  struct Varyings {
   float4 positionCS : SV_POSITION;
   float3 positionWS : TEXCOORD0;
   half3 normalWS : TEXCOORD1;
   UNITY_VERTEX_INPUT_INSTANCE_ID
   UNITY_VERTEX_OUTPUT_STEREO
  };
  Varyings SurfaceVertex(Attributes input) {
   Varyings output = (Varyings)0;
   UNITY_SETUP_INSTANCE_ID(input);
   UNITY_TRANSFER_INSTANCE_ID(input, output);
   UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
   VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
   output.positionCS = p.positionCS;
   output.positionWS = p.positionWS;
   output.normalWS = TransformObjectToWorldNormal(input.normalOS);
   return output;
  }
  half4 DepthFragment(Varyings input) : SV_Target { return 0; }
  ENDHLSL
  Pass {
   Name "ForwardLit"
   Tags { "LightMode"="UniversalForward" }
   Cull Back ZWrite On
   HLSLPROGRAM
   #pragma vertex SurfaceVertex
   #pragma fragment ForwardFragment
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_instancing
   half4 ForwardFragment(Varyings input) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(input);
    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    half d = dot(normalize(input.normalWS), sun.direction);
    half band = smoothstep(_Threshold-_Softness, _Threshold+_Softness, d);
    half3 paint = lerp(_ShadeColor.rgb, _BaseColor.rgb, lerp(band, 1, _Flat));
    return half4(paint * lerp(_ShadowTint.rgb, half3(1,1,1), sun.shadowAttenuation), 1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0 Cull Back
   HLSLPROGRAM
   #pragma vertex ShadowVertex
   #pragma fragment DepthFragment
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection;
   float3 _LightPosition;
   Varyings ShadowVertex(Attributes input) {
    Varyings output = SurfaceVertex(input);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     float3 direction = normalize(_LightPosition-output.positionWS);
    #else
     float3 direction = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
    #if UNITY_REVERSED_Z
     output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
     output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return output;
   }
   ENDHLSL
  }
  Pass {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   ZWrite On ColorMask R Cull Back
   HLSLPROGRAM
   #pragma vertex SurfaceVertex
   #pragma fragment DepthFragment
   #pragma multi_compile_instancing
   ENDHLSL
  }
  Pass {
   Name "DepthNormals"
   Tags { "LightMode"="DepthNormals" }
   ZWrite On Cull Back
   HLSLPROGRAM
   #pragma vertex SurfaceVertex
   #pragma fragment NormalsFragment
   #pragma multi_compile_instancing
   half4 NormalsFragment(Varyings input) : SV_Target { return half4(normalize(input.normalWS), 0); }
   ENDHLSL
  }
 }
}
