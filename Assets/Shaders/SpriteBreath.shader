Shader "Custom/2D/SpriteBreath"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Breath)]
        _Speed ("Breaths Per Second", Float) = 1
        _PhaseOffset ("Start Phase (0-1)", Range(0,1)) = 0

        [Header(Scale Swell)]
        _ScaleAmountX ("Swell X", Range(0,0.5)) = 0.04
        _ScaleAmountY ("Swell Y", Range(0,0.5)) = 0.06
        _Pivot ("Pivot (object-space XY)", Vector) = (0,0,0,0)

        [Header(Alpha Pulse)]
        _AlphaAmount ("Alpha Pulse (0 = off)", Range(0,1)) = 0
        _AlphaMin ("Alpha Min", Range(0,1)) = 0
        _AlphaMax ("Alpha Max", Range(0,1)) = 1

        // Sprite/UI plumbing (matches Sprites-Default)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
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

            float  _Speed;
            float  _PhaseOffset;
            float  _ScaleAmountX;
            float  _ScaleAmountY;
            float4 _Pivot;
            float  _AlphaAmount;
            float  _AlphaMin;
            float  _AlphaMax;

            v2f vert (appdata v)
            {
                v2f o;

                // Smooth 0..1 breath wave (eased in/out by the sine itself).
                float w = sin(_Time.y * _Speed * TAU + _PhaseOffset * TAU) * 0.5 + 0.5;

                // Swell outward from the pivot: rest scale at w=0, +amount at w=1.
                float2 scale = 1.0 + float2(_ScaleAmountX, _ScaleAmountY) * w;
                float3 pos = v.vertex.xyz;
                pos.xy = (pos.xy - _Pivot.xy) * scale + _Pivot.xy;
                o.vertex = UnityObjectToClipPos(float4(pos, v.vertex.w));

                // Optional alpha pulse in sync with the swell, clamped to [min, max].
                float alpha = lerp(1.0, 1.0 - _AlphaAmount, w);
                alpha = clamp(alpha, _AlphaMin, _AlphaMax);

                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                o.color.a *= alpha;

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb *= i.color.rgb;
                c *= i.color.a;   // premultiply
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
