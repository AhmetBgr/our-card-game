Shader "Custom/2D/RadialSweep"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Space(8)]
        [KeywordEnum(Spinner, Progress)] _Mode ("Mode", Float) = 0

        [Header(Sweep)]
        _Progress   ("Progress / Arc (0-1)", Range(0,1)) = 0.25
        _StartAngle ("Start Angle (deg)", Range(0,360)) = 90
        [Toggle] _Clockwise ("Clockwise", Float) = 1

        [Header(Spinner Motion)]
        _Speed ("Spin Speed (turns/sec)", Float) = 1
        _Fade  ("Trailing Fade", Range(0,1)) = 1

        [Header(Edges)]
        _Feather    ("Edge Feather", Range(0,0.5)) = 0.02
        _AngleSteps ("Angle Steps (0 = smooth)", Range(0,64)) = 0

        [Header(Radial Mask)]
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.0
        _OuterRadius ("Outer Radius", Range(0,1.5)) = 1.0

        // Sprite/UI blend + stencil plumbing (matches Sprites-Default)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            #pragma multi_compile_local _MODE_SPINNER _MODE_PROGRESS
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            #define TAU 6.28318530718

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _RendererColor;

            float _Progress;
            float _StartAngle;
            float _Clockwise;
            float _Speed;
            float _Fade;
            float _Feather;
            float _AngleSteps;
            float _InnerRadius;
            float _OuterRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Tint + vertex alpha, then premultiply by the FINAL alpha (which includes
                // the texture's own alpha). Premultiplying by only the vertex alpha left
                // semi-transparent texels with un-scaled rgb, so alpha edges fringed bright
                // under the One / OneMinusSrcAlpha blend. Matches Sprites-Default.
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;   // premultiply by texture * tint alpha

                // --- polar coordinates around the sprite center ---
                float2 p = i.uv - 0.5;
                float  radius = length(p) * 2.0;                 // 0 at center, ~1 at edge
                float  ang = atan2(p.y, p.x);                    // -PI..PI
                ang = ang / TAU + 0.5;                           // 0..1

                // start angle + spin direction
                float dir = (_Clockwise > 0.5) ? -1.0 : 1.0;
                float phase = _StartAngle / 360.0;

            #ifdef _MODE_SPINNER
                phase += _Time.y * _Speed * dir;                 // continuous rotation
            #endif

                // signed distance around the ring from the sweep start (0..1)
                float a = frac((ang - phase) * dir);

                // optional chunky quantization of the angle for pixel-art feel
                if (_AngleSteps >= 1.0)
                    a = floor(a * _AngleSteps) / _AngleSteps;

                float feather = max(_Feather, 1e-4);
                float mask;

            #ifdef _MODE_PROGRESS
                // wedge that fills from 0 up to _Progress
                mask = smoothstep(_Progress + feather, _Progress - feather, a);
            #else
                // arc of width _Progress that rotates; leading edge crisp, trailing fades
                float arc = max(_Progress, feather);
                float lead  = smoothstep(0.0, feather, a);               // crisp start
                float trail = smoothstep(arc + feather, arc - feather, a);
                float body  = lead * trail;
                // fade brightness along the tail (like a classic spinner)
                float tail  = lerp(1.0, saturate(1.0 - a / arc), _Fade);
                mask = body * tail;
            #endif

                // radial ring mask (lets you cut a donut / hollow center)
                float ring = smoothstep(_InnerRadius - feather, _InnerRadius + feather, radius)
                           * smoothstep(_OuterRadius + feather, _OuterRadius - feather, radius);

                c *= mask * ring;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
