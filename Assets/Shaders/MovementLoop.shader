Shader "Custom/2D/MovementLoop"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Movement)]
        _Target ("Destination Offset (XY units)", Vector) = (3,0,0,0)
        _Speed  ("Loops Per Second", Float) = 0.5
        _PhaseOffset ("Start Phase (0-1)", Range(0,1)) = 0

        [Header(End Fade)]
        [Toggle] _UseFade ("Fade At Ends", Float) = 1
        _FadeZone ("Fade Zone", Range(0,0.5)) = 0.15

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

            float4 _Target;
            float  _Speed;
            float  _PhaseOffset;
            float  _UseFade;
            float  _FadeZone;

            v2f vert (appdata v)
            {
                v2f o;

                // 0 -> 1 sawtooth: 0 at origin, 1 at the destination, then wraps.
                float t = frac(_Time.y * _Speed + _PhaseOffset);

                // Translate the whole sprite (object space, so it respects rotation/scale).
                float3 disp = float3(_Target.xy * t, 0.0);
                o.vertex = UnityObjectToClipPos(v.vertex + float4(disp, 0.0));

                // Fade in as it leaves the origin, fade out as it nears the target,
                // so the snap-back reset is invisible. Toggle off = full opacity throughout.
                float fz = max(_FadeZone, 1e-4);
                float fade = smoothstep(0.0, fz, t) * smoothstep(1.0, 1.0 - fz, t);
                fade = lerp(1.0, fade, _UseFade);

                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                o.color.a *= fade;

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Tint + vertex/fade alpha, then premultiply by the FINAL alpha (which
                // includes the texture's own alpha). Premultiplying by only the vertex
                // alpha left semi-transparent texels with un-scaled rgb, so alpha edges
                // fringed bright under the One / OneMinusSrcAlpha blend. Matches Sprites-Default.
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
