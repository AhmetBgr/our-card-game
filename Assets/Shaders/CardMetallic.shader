Shader "Custom/2D/CardMetallic"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Metallic Look)]
        _MetalColorA ("Metal Tone A (Gold)", Color) = (1, 0.84, 0.5, 1)
        _MetalColorB ("Metal Tone B (Silver)", Color) = (0.92, 0.95, 1, 1)
        _MetalMaskThreshold ("Mask Threshold (brightness)", Range(0,1)) = 0.55
        _MetalMaskSoftness ("Mask Softness", Range(0.01,1)) = 0.12
        _BaseSheen ("Ambient Sheen", Range(0,0.3)) = 0.04

        [Header(Dynamic Highlight Streak)]
        _CoreWidth ("Highlight Core Width", Range(0.005,0.3)) = 0.035
        _OuterWidth ("Highlight Glow Width", Range(0.02,1)) = 0.2
        _CoreIntensity ("Core Intensity (white hot spot)", Range(0,4)) = 2.2
        _OuterIntensity ("Glow Intensity (metal tint)", Range(0,2)) = 0.55
        _TiltSensitivity ("Tilt Sensitivity (Z axis)", Range(0,5)) = 1.5

        [Header(Flip Edge Flash)]
        _FlashIntensity ("Flip Edge Flash Intensity", Range(0,3)) = 0.8
        _FlashPower ("Flip Edge Flash Power", Range(0.5,8)) = 3

        [Header(Live Rotation Input)]
        _RotationZ ("Rotation Z (deg, fan tilt)", Float) = 0
        _RotationY ("Rotation Y (deg, flip)", Float) = 0

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

            fixed4 _MetalColorA;
            fixed4 _MetalColorB;
            float  _MetalMaskThreshold;
            float  _MetalMaskSoftness;
            float  _BaseSheen;

            float  _CoreWidth;
            float  _OuterWidth;
            float  _CoreIntensity;
            float  _OuterIntensity;
            float  _TiltSensitivity;

            float  _FlashIntensity;
            float  _FlashPower;

            float  _RotationZ;
            float  _RotationY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                // Only let bright/metal-colored parts of the art catch the shine — dark recesses
                // in the pixel art frame stay dark, like light reflecting off raised metal edges,
                // not a flat paint tint over everything. Gamma-correct before the threshold check:
                // this project renders in Linear color space, so c.rgb is linear here (e.g. a pixel
                // that looks medium-gray on screen is ~0.3 linear, not ~0.6) — comparing raw linear
                // luminance against a threshold tuned by eye would mask out almost everything.
                float luminance = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float perceptualLuminance = pow(saturate(luminance), 1.0 / 2.2);
                float metalMask = smoothstep(_MetalMaskThreshold - _MetalMaskSoftness,
                                              _MetalMaskThreshold + _MetalMaskSoftness, perceptualLuminance) * c.a;

                float zRad = radians(_RotationZ);
                float yRad = radians(_RotationY);

                // The streak's direction tilts with the card's Z rotation (fan tilt in hand), and
                // its position slides across the face as the card turns on Y (flip animation) — so
                // the highlight visibly tracks whatever rotation the card is actually doing.
                float2 dir = normalize(float2(cos(zRad * 0.5 + 0.7854), sin(zRad * 0.5 + 0.7854)));
                float coord = dot(i.uv - 0.5, dir);
                float sweepPos = sin(yRad) * 0.6 + zRad * _TiltSensitivity * 0.15;
                float d = abs(coord - sweepPos);

                // Hard, anti-aliased edges (via fwidth) instead of a soft gradient blob — reads as
                // a crisp foil streak against pixel art rather than a smooth painterly glow.
                float aa = max(fwidth(d), 0.0008);
                float core  = 1 - smoothstep(_CoreWidth  - aa, _CoreWidth  + aa, d);
                float outer = 1 - smoothstep(_OuterWidth - aa, _OuterWidth + aa, d);

                // Which metal tone is "catching the light" shifts with the flip rotation, rather
                // than smearing two colors across a static gradient (that's what read as "blue").
                fixed3 metalTint = lerp(_MetalColorA.rgb, _MetalColorB.rgb, saturate(sin(yRad) * 0.5 + 0.5));

                // Edge flash: spikes when the card is turned edge-on mid-flip (|sin(yRad)| -> 1),
                // like light catching the thin edge of a metal card as it turns away from view.
                float flash = pow(abs(sin(yRad)), _FlashPower) * _FlashIntensity;

                float glow = saturate(outer * _OuterIntensity + _BaseSheen);
                float hot  = core * _CoreIntensity + flash;

                // Dye the glow band toward the metal tone — visible even on bright/near-white art,
                // unlike a screen-blend which goes invisible once pixels are already near white —
                // then punch a bright specular pop through the tight core. The pop is allowed to
                // blow out past white, which is what reads as a sharp foil glint, not a flat tint.
                c.rgb = lerp(c.rgb, metalTint, glow * metalMask);
                c.rgb += hot * metalMask;

                // Premultiply by final alpha, matching Sprites-Default / this project's other shaders.
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
