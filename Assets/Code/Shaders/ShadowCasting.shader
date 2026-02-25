Shader "Sprites/Custom/SpriteShadow"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _ShadowSoftness("Shadow Softness", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Passe principale : rendu du sprite (alpha tested)
        Pass
        {
            Name "FORWARD"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Cutoff;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv) * IN.color;
                clip(tex.a - _Cutoff);
                return tex;
            }
            ENDHLSL
        }

        // Passe ShadowCaster : alpha-tested, variantes shadowcaster pour tous les types de lights
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile_shadowcaster
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;
            float _ShadowSoftness;

            struct appdata_shadow
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f_shadow
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f_shadow vert_shadow(appdata_shadow v)
            {
                v2f_shadow o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // élargir la silhouette en clip-space selon _ShadowSoftness (optionnel)
                if (_ShadowSoftness > 0.0001)
                {
                    float4 centerClip = UnityObjectToClipPos(float4(0,0,0,1));
                    float2 dir = o.pos.xy - centerClip.xy;
                    o.pos.xy += dir * _ShadowSoftness;
                }

                return o;
            }

            // alpha test : si transparent on n'écrit pas dans la shadowmap (depth)
            float4 frag_shadow(v2f_shadow IN) : SV_Target
            {
                float4 tex = tex2D(_MainTex, IN.uv);
                clip(tex.a - _Cutoff);
                // ColorMask 0 empêche l'écriture couleur ; l'API shadow écrira la profondeur automatiquement.
                return float4(0,0,0,0);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}