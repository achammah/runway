// THE ONE SHADER THE SOFT-LIGHT LANE NEEDS — UI/Default with the blend unlocked.
//
// Unity's built-in UI shader hard-codes `Blend SrcAlpha OneMinusSrcAlpha`, so a
// material made from it can never add and can never multiply: there is no
// _SrcBlend/_DstBlend property on it to set. This is that shader with the two
// blend factors exposed as material properties, which is the whole difference:
//
//   additive glow   _SrcBlend = SrcAlpha (5)  _DstBlend = One  (1)   light ADDS
//   in-the-red dim  _SrcBlend = DstColor (2)  _DstBlend = Zero (0)   room MULTIPLIES
//
// It lives under Resources/ on purpose: anything under a Resources folder is
// always included in a player build, so `Resources.Load<Shader>` can never come
// back null because a stripper decided nothing referenced it.
//
// ZTest is Always rather than [unity_GUIZTestMode]: the game's canvas is
// ScreenSpaceOverlay (where that built-in resolves to Always anyway) and the
// evidence harness renders through a ScreenSpaceCamera canvas, so pinning it
// makes both paths draw identically instead of depending on a global the
// canvas sets for us.
Shader "Runway/Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SrcBlend ("Source blend", Float) = 5
        _DstBlend ("Destination blend", Float) = 1
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
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.texcoord) * i.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
