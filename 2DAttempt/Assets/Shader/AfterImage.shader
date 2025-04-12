Shader "Custom/AfterImageShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.005
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                float4 col = tex2D(_MainTex, i.uv);
                
                // Apply the color tint
                col *= _Color;
                
                // Sample the texture at offset positions for outline effect
                float2 uvUp = i.uv + float2(0, _OutlineWidth);
                float2 uvDown = i.uv - float2(0, _OutlineWidth);
                float2 uvRight = i.uv + float2(_OutlineWidth, 0);
                float2 uvLeft = i.uv - float2(_OutlineWidth, 0);
                
                float upAlpha = tex2D(_MainTex, uvUp).a;
                float downAlpha = tex2D(_MainTex, uvDown).a;
                float rightAlpha = tex2D(_MainTex, uvRight).a;
                float leftAlpha = tex2D(_MainTex, uvLeft).a;
                
                // Create outline
                float outlineAlpha = max(max(upAlpha, downAlpha), max(rightAlpha, leftAlpha));
                
                // If there's an outline but no texture at this point
                if (outlineAlpha > 0 && col.a < 0.1)
                {
                    // Return outline color
                    return _OutlineColor;
                }
                
                return col;
            }
            ENDCG
        }
    }
}