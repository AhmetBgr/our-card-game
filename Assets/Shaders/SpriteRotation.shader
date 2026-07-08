Shader "Custom/SpriteRotation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rotation ("Rotation (Degrees)", Float) = 0
        _RotationSpeed ("Auto Spin Speed (Deg/Sec)", Float) = 0
        _Pivot ("Pivot (UV)", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
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
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Rotation;
            float _RotationSpeed;
            float4 _Pivot;

            // Rotate a UV coordinate around a pivot by the given angle (radians).
            float2 RotateUV(float2 uv, float2 pivot, float angleRad)
            {
                float s = sin(angleRad);
                float c = cos(angleRad);
                float2 centered = uv - pivot;
                float2 rotated = float2(
                    centered.x * c - centered.y * s,
                    centered.x * s + centered.y * c
                );
                return rotated + pivot;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;

                float angle = radians(_Rotation + _RotationSpeed * _Time.y);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv = RotateUV(uv, _Pivot.xy, angle);

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.rgb *= col.a; // premultiply for the One / OneMinusSrcAlpha blend
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
