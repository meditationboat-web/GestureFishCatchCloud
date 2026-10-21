Shader "GestureFistGame/InteractiveWater"
{
  Properties
  {
    _ShallowColor ("Shallow Color", Color) = (0.20, 0.78, 0.94, 0.78)
    _DeepColor ("Deep Color", Color) = (0.008, 0.11, 0.34, 0.88)
    _FoamColor ("Foam Color", Color) = (0.86, 0.98, 1, 0.96)
    _WaveScale ("Wave Scale", Float) = 2.4
    _WaveSpeed ("Wave Speed", Float) = 0.7
    _WaveHeight ("Wave Height", Range(0, 0.2)) = 0.045
    _Alpha ("Opacity", Range(0,1)) = 0.84
    _WaterWidth ("Water Width", Float) = 8.4
    _WaterDepth ("Water Depth", Float) = 8
    _Circular ("Circular Pool", Float) = 0
    _Gloss ("Gloss", Range(0,1)) = 0.9
    _SimulationFoamStrength ("Simulation Foam Strength", Range(0,3)) = 1.25
    _SurfaceDetail ("Surface Detail", Range(0,2)) = 1.0
    _CausticStrength ("Caustic Strength", Range(0,2)) = 0.7
    _InteractionMap ("Shallow Water State (Height/Flow/Foam)", 2D) = "black" {}
    _RippleMap ("Render Texture Ripple Map", 2D) = "black" {}
    _RippleStrength ("Ripple Strength", Range(0,2)) = 1.0
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    ZWrite Off
    Cull Off
    Pass
    {
      Name "WaterForward"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.5
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

      CBUFFER_START(UnityPerMaterial)
        half4 _ShallowColor;
        half4 _DeepColor;
        half4 _FoamColor;
        float _WaveScale;
        float _WaveSpeed;
        float _WaveHeight;
        float _Alpha;
        float _WaterWidth;
        float _WaterDepth;
        float _Circular;
        float _Gloss;
        float _SimulationFoamStrength;
        float _SurfaceDetail;
        float _CausticStrength;
        float _RippleStrength;
      CBUFFER_END
      TEXTURE2D(_InteractionMap);
      SAMPLER(sampler_InteractionMap);
      TEXTURE2D(_RippleMap);
      SAMPLER(sampler_RippleMap);

      struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
      struct Varyings
      {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float2 localXZ : TEXCOORD2;
        float4 screenPos : TEXCOORD3;
      };

      float WaveHeight(float2 p)
      {
        float t = _Time.y * _WaveSpeed;
        float a = sin(dot(p, float2(1.0, 0.32)) * _WaveScale + t * 1.35);
        float b = cos(dot(p, float2(-0.42, 1.0)) * (_WaveScale * 1.42) - t * 1.05);
        float c = sin(dot(p, float2(2.2, -1.7)) * (_WaveScale * 2.2) + t * 1.8);
        return (a * .55 + b * .30 + c * .15) * _WaveHeight;
      }

      float3 GerstnerDisplacement(float2 p, float2 direction, float wavelength, float steepness, float speed)
      {
        direction = normalize(direction);
        float k = 6.2831853 / wavelength;
        float phase = k * dot(direction, p) + _Time.y * speed * _WaveSpeed;
        float amplitude = steepness / max(.001, k);
        return float3(direction.x * amplitude * cos(phase), amplitude * sin(phase), direction.y * amplitude * cos(phase));
      }

      float hash21(float2 p)
      {
        p = frac(p * float2(123.34, 345.45));
        p += dot(p, p + 34.345);
        return frac(p.x * p.y);
      }

      float valueNoise(float2 p)
      {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        float a = hash21(i);
        float b = hash21(i + float2(1, 0));
        float c = hash21(i + float2(0, 1));
        float d = hash21(i + float2(1, 1));
        return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
      }

      float fbm(float2 p)
      {
        float v = 0.0;
        float a = .5;
        for (int k = 0; k < 4; k++)
        {
          v += valueNoise(p) * a;
          p = p * 2.03 + 17.1;
          a *= .5;
        }
        return v;
      }

      Varyings vert(Attributes input)
      {
        Varyings o;
        float3 positionOS = input.positionOS.xyz;
        float2 p = positionOS.xz;
        float3 gerstner =
          GerstnerDisplacement(p, float2(1.0, .18), 2.8, .18, 1.2) * _WaveHeight * 7.0 +
          GerstnerDisplacement(p, float2(-.35, 1.0), 1.65, .10, 1.65) * _WaveHeight * 5.0 +
          GerstnerDisplacement(p, float2(.7, -.7), .82, .045, 2.25) * _WaveHeight * 3.0;
        // The CPU shallow-water solver supplies the large interaction
        // displacement. Keep procedural displacement subtle so it adds detail
        // without fighting the simulated surface and causing shimmer.
        positionOS.xz += gerstner.xz * .22;
        positionOS.y += gerstner.y * .22 + WaveHeight(p) * .28;
        float2 dx = p + float2(.035, 0);
        float2 dz = p + float2(0, .035);
        float3 tangent = float3(.07, WaveHeight(dx) - WaveHeight(p), 0);
        float3 bitangent = float3(0, WaveHeight(dz) - WaveHeight(p), .07);
        float3 normalOS = normalize(cross(bitangent, tangent));
        o.positionWS = TransformObjectToWorld(positionOS);
        o.positionCS = TransformWorldToHClip(o.positionWS);
        o.normalWS = TransformObjectToWorldNormal(normalOS);
        o.localXZ = p;
        o.screenPos = ComputeScreenPos(o.positionCS);
        return o;
      }

      half4 frag(Varyings i) : SV_Target
      {
        float2 uv = i.localXZ;
        float2 halfSize = float2(_WaterWidth, _WaterDepth) * .5;
        float edgeX = min(uv.x + halfSize.x, halfSize.x - uv.x) / max(.001, halfSize.x);
        float edgeZ = min(uv.y + halfSize.y, halfSize.y - uv.y) / max(.001, halfSize.y);
        float radial = length(uv / max(.001, min(halfSize.x, halfSize.y)));
        if (_Circular > .5 && radial > 1.0) discard;
        float edge = _Circular > .5 ? 1.0 - radial : min(edgeX, edgeZ);

        float2 interactionUv = saturate(uv / float2(_WaterWidth, _WaterDepth) + .5);
        half4 interaction = SAMPLE_TEXTURE2D(_InteractionMap, sampler_InteractionMap, interactionUv);
        half ripple = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, interactionUv).r;
        half rippleLeft = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, saturate(interactionUv - float2(.006, 0))).r;
        half rippleRight = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, saturate(interactionUv + float2(.006, 0))).r;
        half rippleBack = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, saturate(interactionUv - float2(0, .006))).r;
        half rippleFront = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, saturate(interactionUv + float2(0, .006))).r;
        float2 rippleGradient = float2(rippleRight - rippleLeft, rippleFront - rippleBack);
        float impactHeight = abs(interaction.r * 2.0 - 1.0);
        // The shallow-water solver stores flow speed directly in [0,1].
        float impactVelocity = saturate(interaction.g);
        float simulatedFoam = saturate(interaction.b * _SimulationFoamStrength);
        float2 interactionStep = float2(.018, .018);
        float interactionLeft = SAMPLE_TEXTURE2D(_InteractionMap, sampler_InteractionMap, saturate(interactionUv - float2(interactionStep.x, 0))).r;
        float interactionRight = SAMPLE_TEXTURE2D(_InteractionMap, sampler_InteractionMap, saturate(interactionUv + float2(interactionStep.x, 0))).r;
        float interactionBack = SAMPLE_TEXTURE2D(_InteractionMap, sampler_InteractionMap, saturate(interactionUv - float2(0, interactionStep.y))).r;
        float interactionFront = SAMPLE_TEXTURE2D(_InteractionMap, sampler_InteractionMap, saturate(interactionUv + float2(0, interactionStep.y))).r;
        float2 interactionGradient = float2(interactionRight - interactionLeft, interactionFront - interactionBack);

        float t = _Time.y;
        float detailNoise = fbm(uv * (1.25 + _SurfaceDetail * 1.8) + float2(t * .08, -t * .06));
        float longWave = sin((uv.x * 1.2 + uv.y * .75) * 2.0 + t * .8) * .5 + .5;
        float shortWave = cos((uv.x * 3.4 - uv.y * 2.6) * 2.8 - t * 1.35) * .5 + .5;
        float crest = saturate(longWave * .42 + shortWave * .25 + detailNoise * .48);
        float foam = smoothstep(.76, .98, crest) * .38;
        // Keep the shoreline soft and translucent; the authored grass contact
        // strip supplies the readable boundary instead of a bright cyan halo.
        float edgeFoam = (1.0 - smoothstep(.018, .30, edge));
        foam += edgeFoam * (.18 + detailNoise * .24);
        foam += simulatedFoam * .92;
        foam += saturate(impactVelocity * 1.65 + impactHeight * .45) * .68;
        float rippleFoam = smoothstep(.18, .72, ripple) * _RippleStrength;
        foam += rippleFoam * .72;

        float2 screenUv = i.screenPos.xy / max(.001, i.screenPos.w);
        float rawSceneDepth = SampleSceneDepth(screenUv);
        float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
        float waterEyeDepth = LinearEyeDepth(i.positionCS.z / max(.001, i.positionCS.w), _ZBufferParams);
        float waterThickness = saturate(max(0.0, sceneEyeDepth - waterEyeDepth) * .85);
        float depthMix = saturate(1.0 - waterThickness + longWave * .10);
        half3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthMix);
        water += _ShallowColor.rgb * crest * .12;
        water += _FoamColor.rgb * saturate(simulatedFoam * .82 + impactVelocity * .6 + impactHeight * .18) * .22;
        water = lerp(water, _FoamColor.rgb, saturate(foam));

        float shallowCaustic = pow(saturate(valueNoise(uv * 5.0 + float2(t * .24, -t * .18)) * .7 + valueNoise(uv * 11.0 - t * .31) * .3), 3.2);
        water += _FoamColor.rgb * shallowCaustic * (1.0 - waterThickness) * .08 * _CausticStrength;
        water += _FoamColor.rgb * rippleFoam * .14;

        half3 n = normalize(i.normalWS);
        half3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
        n = normalize(n + float3(-interactionGradient.x, 0, -interactionGradient.y) * 1.35);
        n = normalize(n + float3(-rippleGradient.x, 0, -rippleGradient.y) * (1.65 * _RippleStrength));
        half fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4.0);
        float shorelineDepth = max(0.0, sceneEyeDepth - waterEyeDepth);
        float depthFoam = 1.0 - smoothstep(.035, .55, shorelineDepth);
        foam += depthFoam * (.30 + detailNoise * .50);
        // Keep the water readable without duplicating the raised rope net as a
        // second bright grid inside the pool.
        half3 refractedScene = SampleSceneColor(screenUv + n.xz * (.012 + impactVelocity * .025));
        water = lerp(refractedScene * .12, water, .97 + fresnel * .02);
        half3 reflectedScene = SampleSceneColor(screenUv - n.xz * (.028 + fresnel * .035));
        water = lerp(water, reflectedScene * .88 + water * .12, .015 + fresnel * .025);
        Light mainLight = GetMainLight();
        half3 halfDir = normalize(mainLight.direction + viewDir);
        half specular = pow(saturate(dot(n, halfDir)), lerp(28, 110, _Gloss));
        water += mainLight.color * specular * (.25 + fresnel * .65);
        half alpha = saturate(_Alpha + foam * .12 + fresnel * .10);
        return half4(water, alpha);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
