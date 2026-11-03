// Lit props that dance under the dome: a board's planks, a tarp's stripes, a hut that breaks apart. See ShowLit.hlsl.
Shader "GrowAGarden/ShowLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [Toggle(_VERTEX_COLOUR)] _VertexColour ("Multiply by vertex colour", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormal ("Normal map", Float) = 0
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal scale", Float) = 1
        [Toggle(_METALLICSPECGLOSSMAP)] _UseMetallicMap ("Metallic map (r metal, a smoothness)", Float) = 0
        _MetallicGlossMap ("Metallic map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        [Toggle(_OCCLUSIONMAP)] _UseOcclusion ("Occlusion map", Float) = 0
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion strength", Range(0, 1)) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [KeywordEnum(None, Planks, Tarp, Pieces)] _Show ("Dance", Float) = 0
        _Glow ("Show brightness", Range(0, 4)) = 1
        _PanelMin ("Planks: panel min (object space)", Vector) = (-1, -1, -1, 0)
        _PanelMax ("Planks: panel max (object space)", Vector) = (1, 1, 1, 0)
        _Planks ("Planks: how many", Float) = 5
        _Stripes ("Tarp: stripes per object unit along x", Float) = 2
        _Wave ("Pieces: pulse front, width, ring, strength (set by script)", Vector) = (0, 0.2, 0, 0)
        _WaveFrom ("Pieces: pulse start (world)", Vector) = (0, 0, 0, 0)
        _WaveTo ("Pieces: pulse end (world)", Vector) = (0, 1, 0, 0)
        _Reach ("Pieces: how far out, in metres", Float) = 0.35

        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _SHOW_NONE _SHOW_PLANKS _SHOW_TARP _SHOW_PIECES
            #pragma shader_feature_local _VERTEX_COLOUR
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "ShowLit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 piece : TEXCOORD3;
                float4 way : TEXCOORD4;
                half4 colour : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 positionOS : TEXCOORD4;   // w: how far apart the piece is
                half4 colour : TEXCOORD5;
                float2 fogAndHeight : TEXCOORD6;  // x: fog, y: the piece's height
                half3 vertexLight : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS, normalWS, tangentWS;
                float a;
                ShowVertex(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, input.piece, input.way, positionWS, normalWS, tangentWS, a);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = normalize(normalWS);
                o.tangentWS = float4(normalize(tangentWS), input.tangentOS.w * GetOddNegativeScale());
                o.positionOS = float4(input.positionOS.xyz, a);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.colour = input.colour;
                o.fogAndHeight = float2(ComputeFogFactor(o.positionCS.z), input.piece.w);
                o.vertexLight = VertexLighting(positionWS, o.normalWS);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_VERTEX_COLOUR)
                    albedo.rgb *= input.colour.rgb;
                #endif

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = albedo.a;
                #if defined(_METALLICSPECGLOSSMAP)
                    half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                    surface.metallic = mg.r;
                    surface.smoothness = mg.a * _Smoothness;
                #else
                    surface.metallic = _Metallic;
                    surface.smoothness = _Smoothness;
                #endif
                #if defined(_OCCLUSIONMAP)
                    surface.occlusion = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
                #else
                    surface.occlusion = 1;
                #endif
                surface.specular = 0;
                surface.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                surface.emission = _EmissionColor.rgb;
                ShowColour(input.positionOS.xyz, input.colour, input.positionOS.w, input.fogAndHeight.y, surface.albedo, surface.emission);

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                float3 normalWS = input.normalWS;
                #if defined(_NORMALMAP)
                    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    normalWS = TransformTangentToWorld(surface.normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS));
                #endif
                data.normalWS = NormalizeNormalPerPixel(normalWS);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = input.fogAndHeight.x;
                data.vertexLighting = input.vertexLight;
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1, 1, 1, 1);

                half4 colour = UniversalFragmentPBR(data, surface);
                colour.rgb = MixFog(colour.rgb, data.fogCoord);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                    colour.rgb = (colour.rgb - surface.emission) * surface.alpha + surface.emission;   // premultiplied; the glow is not faded by the glass
                    colour.a = surface.alpha;
                #else
                    colour.a = 1;
                #endif
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SHOW_NONE _SHOW_PLANKS _SHOW_TARP _SHOW_PIECES
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "ShowLit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 piece : TEXCOORD3;
                float4 way : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS, normalWS, tangentWS;
                float a;
                ShowVertex(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, input.piece, input.way, positionWS, normalWS, tangentWS, a);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalize(normalWS), lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SHOW_NONE _SHOW_PLANKS _SHOW_TARP _SHOW_PIECES
            #pragma multi_compile_instancing

            #include "ShowLit.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 piece : TEXCOORD3;
                float4 way : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS, normalWS, tangentWS;
                float a;
                ShowVertex(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, input.piece, input.way, positionWS, normalWS, tangentWS, a);
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half Frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SHOW_NONE _SHOW_PLANKS _SHOW_TARP _SHOW_PIECES
            #pragma multi_compile_instancing

            #include "ShowLit.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 piece : TEXCOORD3;
                float4 way : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS, normalWS, tangentWS;
                float a;
                ShowVertex(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, input.piece, input.way, positionWS, normalWS, tangentWS, a);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
            ENDHLSL
        }
    }
}
