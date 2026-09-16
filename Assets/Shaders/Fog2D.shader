Shader "Custom/2D/Fog2D"
{
    // Fog rolling across an orthographic 2D scene along a wind direction. Noise comes from a baked tileable
    // texture (Tools/Fog2D/Bake Noise Texture) rather than per-pixel hashing, so a pixel costs 4 texture reads
    // (2 with FOG2D_LOW). Sampled in WORLD space, so the fog stays put when the quad follows the camera.
    // Values are normally driven per-instance by Fog2DController (MaterialPropertyBlock).
    Properties
    {
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        [NoScaleOffset] _NoiseTex ("Baked Noise (R detail, GB swirl, A banks)", 2D) = "gray" {}
        [Toggle(FOG2D_LOW)] _LowQuality ("Low Quality (no swirl, one layer)", Float) = 0

        [Header(Color)]
        _Color ("Fog Color (light)", Color) = (0.62,0.68,0.70,1)
        _ShadowColor ("Fog Color (dense core)", Color) = (0.30,0.35,0.37,1)

        [Header(Amount)]
        _Density ("Density", Range(0,1)) = 0.45
        _Coverage ("Coverage", Range(0,1)) = 0.5
        _Softness ("Edge Softness", Range(0.01,0.5)) = 0.3

        [Header(Wind)]
        _WindDir ("Wind Direction (world XY)", Vector) = (1,0,0,0)
        _WindSpeed ("Wind Speed (world u/s)", Float) = 0.6
        _Layer2Speed ("Layer 2 Speed Mult", Range(0,3)) = 1.5
        _Stretch ("Stretch Along Wind", Range(1,4)) = 2
        _GustStrength ("Gust Strength", Range(0,1)) = 0.3
        _GustFrequency ("Gusts Per Second", Float) = 0.15
        _Evolve ("Shape Evolve Speed", Float) = 0.03

        [Header(Shape)]
        _NoiseScale ("Puff Size (world units)", Float) = 2.5
        _Layer2Scale ("Layer 2 Scale Mult", Range(0.25,4)) = 1.8
        _Layer2Weight ("Layer 2 Weight", Range(0,1)) = 0.5
        _Warp ("Turbulence", Range(0,2)) = 0.4

        [Header(Banks)]
        _BankSize ("Bank Size (world units)", Float) = 8
        _BankAmount ("Bank Gaps", Range(0,1)) = 0.6

        [Header(Clear Zone)]
        _ClearCenter ("Clear Center (world XY)", Vector) = (1,-1,0,0)
        _ClearSize ("Clear Radii (world XY)", Vector) = (2.4,3.3,0,0)
        _ClearFeather ("Clear Feather", Range(0.01,1)) = 0.6
        _ClearStrength ("Clear Strength", Range(0,1)) = 1

        [Header(Screen Edge)]
        _EdgeBoost ("Edge Vignette", Range(0,1)) = 0.25

        [Header(Pixel Art)]
        _PixelSize ("Pixel Snap (world units, 0 = off)", Float) = 0
        _Steps ("Alpha Steps (0 = smooth)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // premultiplied alpha, same as Sprites-Default

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _ FOG2D_LOW
            #include "UnityCG.cginc"

            #define TAU 6.28318530718
            // Puff cells per texture tile in the R (detail) channel, and bank cells per tile in A.
            // Must match Fog2DNoiseBaker.
            #define DETAIL_CELLS 8.0
            #define BANK_CELLS   4.0

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _NoiseTex;
            fixed4 _Color;
            fixed4 _ShadowColor;
            float  _Density;
            float  _Coverage;
            float  _Softness;
            float4 _WindDir;
            float  _WindSpeed;
            float  _Layer2Speed;
            float  _Stretch;
            float  _GustStrength;
            float  _GustFrequency;
            float  _Evolve;
            float  _NoiseScale;
            float  _Layer2Scale;
            float  _Layer2Weight;
            float  _Warp;
            float  _BankSize;
            float  _BankAmount;
            float4 _ClearCenter;
            float4 _ClearSize;
            float  _ClearFeather;
            float  _ClearStrength;
            float  _EdgeBoost;
            float  _PixelSize;
            float  _Steps;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv       = v.uv;
                o.color    = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 wp = i.worldPos;
                if (_PixelSize > 0) wp = (floor(wp / _PixelSize) + 0.5) * _PixelSize;

                float t = _Time.y;

                // Wind frame: x runs along the wind, y across it.
                float2 dir  = normalize(_WindDir.xy + float2(1e-5, 0));
                float2 perp = float2(-dir.y, dir.x);
                float2 local = float2(dot(wp, dir), dot(wp, perp));

                // Distance travelled = integral of speed * (1 + gust * cos(wt)). Integrating (rather than
                // multiplying t by a varying speed) keeps the motion smooth instead of lurching back and forth.
                float w = TAU * _GustFrequency;
                float travel = _WindSpeed * (t + _GustStrength * sin(w * t) / max(w, 1e-4));

                float2 stretch = float2(max(_Stretch, 1.0), 1.0);
                float2 tile    = max(_NoiseScale, 1e-3) * stretch * DETAIL_CELLS;   // world units per texture tile
                float2 uv1     = (local - float2(travel, 0)) / tile;

                float n;
            #ifdef FOG2D_LOW
                n = tex2D(_NoiseTex, uv1).r;
            #else
                // Turbulence: G/B are two independent smooth fields read as one 2D offset. It rides with
                // layer 1 and slowly evolves, so the wisps curl as they travel instead of boiling in place.
                fixed2 q = tex2D(_NoiseTex, uv1 * 0.5 + float2(0.37, 0.21) * _Evolve * t).gb;
                float2 warp = (q - 0.5) * 2.0 * _Warp / DETAIL_CELLS;

                float2 uv2 = (local - float2(travel * _Layer2Speed, 0)) / tile * _Layer2Scale + float2(0.31, 0.67);
                float n1 = tex2D(_NoiseTex, uv1 + warp).r;
                float n2 = tex2D(_NoiseTex, uv2 + warp * _Layer2Scale).r;
                n = lerp(n1, n2, _Layer2Weight);
            #endif

                // Coverage 0 -> almost no fog, 1 -> almost solid. Softness widens the puff edges.
                float threshold = 1.0 - _Coverage;
                float fog = smoothstep(threshold - _Softness, threshold + _Softness, n);

                // Banks: large travelling masses with clear gaps between them, so the direction of travel reads.
                float2 uvb = (local - float2(travel, 0)) / (max(_BankSize, 1e-3) * stretch * BANK_CELLS) + 0.71;
                float bank = smoothstep(0.35, 0.65, tex2D(_NoiseTex, uvb).a);
                fog *= lerp(1.0, bank, _BankAmount);

                // Screen-edge vignette in quad UV (the quad is fitted to the camera view).
                float edge = smoothstep(0.35, 1.25, length((i.uv - 0.5) * 2.0)) * _EdgeBoost;

                float alpha = saturate(fog * _Density + edge);

                // Elliptical clear zone (e.g. over the board) so the fog never hides gameplay. With strength 1
                // the controller also cuts the fully clear core out of the mesh, so those pixels aren't drawn.
                float d = length((wp - _ClearCenter.xy) / max(_ClearSize.xy, 1e-3));
                float clearMask = smoothstep(1.0 - _ClearFeather, 1.0, d);
                alpha *= lerp(1.0, clearMask, _ClearStrength);

                if (_Steps >= 1) alpha = floor(alpha * _Steps + 0.5) / _Steps;

                fixed3 rgb = lerp(_Color.rgb, _ShadowColor.rgb, saturate(fog * n));
                fixed4 c = fixed4(rgb, alpha * _Color.a) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
