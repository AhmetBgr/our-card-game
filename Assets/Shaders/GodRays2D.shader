Shader "Custom/2D/GodRays2D"
{
    // Soft light shafts for an orthographic 2D scene. Each shaft is one quad whose geometry is built in the vertex
    // shader from per-ray data (origin, angle, length, widths, phase), so sway and flicker animate with no CPU work
    // and the mesh is only rebuilt when a setting changes. Per pixel: a few dot products and smoothsteps, plus one
    // baked-noise read for the dust streaks (none with GODRAYS_LOW). Values are driven by GodRays2DController.
    Properties
    {
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        [NoScaleOffset] _NoiseTex ("Baked Noise (R channel)", 2D) = "gray" {}
        [Toggle(GODRAYS_LOW)] _LowQuality ("Low Quality (no dust streaks)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (One = additive, OneMinusDstColor = screen)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One)", Float) = 1

        [Header(Light)]
        _Color ("Light Color", Color) = (1,0.9,0.65,1)
        _Intensity ("Intensity", Range(0,2)) = 0.3

        [Header(Shape)]
        _Softness ("Edge Softness", Range(0.01,1)) = 0.8
        _SourceFade ("Source Fade", Range(0.01,1)) = 0.15
        _TailFade ("Tail Fade", Range(0.01,1)) = 0.6

        [Header(Motion)]
        _SwayAmount ("Sway (radians)", Float) = 0.03
        _SwaySpeed ("Sway Speed", Float) = 0.3
        _Flicker ("Flicker", Range(0,1)) = 0.5
        _FlickerSpeed ("Flicker Speed", Float) = 0.4

        [Header(Dust)]
        _LightDir ("Light Direction (world XY)", Vector) = (0.5,-0.87,0,0)
        _DustAmount ("Dust Amount", Range(0,1)) = 0.5
        _DustScale ("Dust Size (world units)", Float) = 0.6
        _DustStretch ("Dust Stretch Along Light", Range(1,8)) = 4
        _DustSpeed ("Dust Drift (world u/s)", Float) = 0.15

        [Header(Clear Zone)]
        _ClearCenter ("Clear Center (world XY)", Vector) = (1,-1,0,0)
        _ClearSize ("Clear Radii (world XY)", Vector) = (4,6,0,0)
        _ClearFeather ("Clear Feather", Range(0.01,1)) = 0.5
        _ClearStrength ("Clear Strength", Range(0,1)) = 0.5

        [Header(Pixel Art)]
        _PixelSize ("Pixel Snap (world units, 0 = off)", Float) = 0
        _Steps ("Brightness Steps (0 = smooth)", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _ GODRAYS_LOW
            #include "UnityCG.cginc"

            // Puff cells per texture tile in the R channel. Must match Fog2DNoiseBaker.
            #define DETAIL_CELLS 8.0

            struct appdata
            {
                float4 vertex : POSITION;   // ray origin (object space), identical for all 4 corners
                float2 uv     : TEXCOORD0;  // x = side (-1/+1), y = end (0 = source, 1 = tail)
                float4 ray    : TEXCOORD1;  // angle (rad), length, width at source, width at tail
                float4 anim   : TEXCOORD2;  // phase, brightness, flicker speed mult, sway speed mult
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 posOrigin : TEXCOORD0;  // world pos xy, ray origin xy
                float4 dirLen    : TEXCOORD1;  // ray direction xy, length, brightness
                float2 widths    : TEXCOORD2;
            };

            sampler2D _NoiseTex;
            fixed4 _Color;
            float  _Intensity;
            float  _Softness;
            float  _SourceFade;
            float  _TailFade;
            float  _SwayAmount;
            float  _SwaySpeed;
            float  _Flicker;
            float  _FlickerSpeed;
            float4 _LightDir;
            float  _DustAmount;
            float  _DustScale;
            float  _DustStretch;
            float  _DustSpeed;
            float4 _ClearCenter;
            float4 _ClearSize;
            float  _ClearFeather;
            float  _ClearStrength;
            float  _PixelSize;
            float  _Steps;

            v2f vert (appdata v)
            {
                v2f o;
                float t = _Time.y;
                float phase = v.anim.x;

                // Sway pivots the whole shaft around its source, so the far end moves the most.
                float angle = v.ray.x + _SwayAmount * sin(t * _SwaySpeed * v.anim.w + phase);
                float2 dir  = float2(cos(angle), sin(angle));
                float2 perp = float2(-dir.y, dir.x);

                float3 origin = mul(unity_ObjectToWorld, v.vertex).xyz;
                float halfWidth = lerp(v.ray.z, v.ray.w, v.uv.y) * 0.5;
                float2 wp = origin.xy + dir * (v.uv.y * v.ray.y) + perp * (v.uv.x * halfWidth);
                o.vertex = UnityWorldToClipPos(float3(wp, origin.z));

                // Two detuned sines so the flicker never visibly repeats. 0 flicker = steady brightness.
                float ft = t * _FlickerSpeed * v.anim.z + phase * 7.13;
                float wave = sin(ft) * 0.6 + sin(ft * 2.37 + 1.3) * 0.4;   // -1..1
                float brightness = v.anim.y * _Intensity * (1.0 - _Flicker * (0.5 - 0.5 * wave));

                o.posOrigin = float4(wp, origin.xy);
                o.dirLen    = float4(dir, v.ray.y, brightness);
                o.widths    = v.ray.zw;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 wp = i.posOrigin.xy;
                if (_PixelSize > 0) wp = (floor(wp / _PixelSize) + 0.5) * _PixelSize;

                // Shaft coordinates are rebuilt from world position rather than interpolated UVs, which would
                // warp across a trapezoid and wouldn't respect pixel snapping.
                float2 dir  = i.dirLen.xy;
                float2 perp = float2(-dir.y, dir.x);
                float2 d    = wp - i.posOrigin.zw;
                float along = dot(d, dir) / i.dirLen.z;
                float halfWidth = lerp(i.widths.x, i.widths.y, saturate(along)) * 0.5;
                float across = abs(dot(d, perp)) / max(halfWidth, 1e-4);

                float shape = smoothstep(0.0, _Softness, 1.0 - across);
                shape *= smoothstep(0.0, _SourceFade, along) * (1.0 - smoothstep(1.0 - _TailFade, 1.0, along));

            #ifndef GODRAYS_LOW
                // Dust streaks: noise stretched along the light and drifting across it, sampled in world space
                // with the global light direction so the pattern stays put while individual shafts sway.
                float2 L  = _LightDir.xy;
                float2 Lp = float2(-L.y, L.x);
                float scale = max(_DustScale, 1e-3);
                float2 uvd = float2(dot(wp, L) / (scale * _DustStretch), (dot(wp, Lp) - _Time.y * _DustSpeed) / scale) / DETAIL_CELLS;
                float n = smoothstep(0.25, 0.75, tex2D(_NoiseTex, uvd).r);
                shape *= lerp(1.0, n * 2.0, _DustAmount);
            #endif

                // Elliptical clear zone (e.g. the board) so the light never washes out gameplay.
                float dc = length((wp - _ClearCenter.xy) / max(_ClearSize.xy, 1e-3));
                shape *= lerp(1.0, smoothstep(1.0 - _ClearFeather, 1.0, dc), _ClearStrength);

                float a = saturate(shape * i.dirLen.w);
                if (_Steps >= 1) a = floor(a * _Steps + 0.5) / _Steps;

                return fixed4(_Color.rgb * a, a);
            }
            ENDCG
        }
    }
}
