Shader "Custom/DigitalGlitchEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.1
        _ColorGlitchIntensity ("Color Glitch Intensity", Range(0, 1)) = 0.1
        _BlockSize ("Block Size", Range(1, 10)) = 3
        _NoiseScale ("Noise Scale", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
            float _GlitchIntensity;
            float _ColorGlitchIntensity;
            float _BlockSize;
            float _NoiseScale;
            
            // Simple noise function
            float noise(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // Block noise
            float blockNoise(float2 uv)
            {
                float2 blockPos = floor(uv * _BlockSize) / _BlockSize;
                return noise(blockPos);
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate time-based variation
                float time = _Time.y * 10.0;
                
                // Create horizontal glitch offset
                float glitchAmount = _GlitchIntensity * noise(float2(time, i.uv.y * 100));
                float horizontalGlitch = glitchAmount * 0.1 * step(0.95, noise(float2(time * 0.5, floor(i.uv.y * 80.0))));
                
                // Create block-based glitch
                float blockGlitchX = 0;
                float blockGlitchY = 0;
                
                if (blockNoise(i.uv + time * 0.1) > 0.95 && _GlitchIntensity > 0.2)
                {
                    blockGlitchX = blockNoise(i.uv + time) * 0.1 * _GlitchIntensity;
                    blockGlitchY = blockNoise(i.uv + time * 0.5) * 0.1 * _GlitchIntensity;
                }
                
                // Apply glitch to UV coordinates
                float2 glitchedUV = i.uv;
                glitchedUV.x += horizontalGlitch + blockGlitchX;
                glitchedUV.y += blockGlitchY;
                
                // Sample the texture with glitched UVs
                fixed4 col = tex2D(_MainTex, glitchedUV);
                
                // Apply color distortion (RGB shift)
                if (_ColorGlitchIntensity > 0.01)
                {
                    float colorGlitchThreshold = 0.8 - _ColorGlitchIntensity * 0.2;
                    if (noise(float2(time * 0.25, floor(i.uv.y * 40.0))) > colorGlitchThreshold)
                    {
                        // RGB shift
                        float rgbOffset = _ColorGlitchIntensity * 0.02;
                        float2 rUV = glitchedUV + float2(rgbOffset, 0);
                        float2 gUV = glitchedUV;
                        float2 bUV = glitchedUV - float2(rgbOffset, 0);
                        
                        col.r = tex2D(_MainTex, rUV).r;
                        col.g = tex2D(_MainTex, gUV).g;
                        col.b = tex2D(_MainTex, bUV).b;
                    }
                }
                
                // Add noise lines
                float lineNoise = 0;
                if (noise(float2(time, floor(i.uv.y * 100))) > 0.97)
                {
                    lineNoise = noise(float2(time * 10, i.uv.y * 100)) * _GlitchIntensity;
                    col.rgb += lineNoise;
                }
                
                // Add random blocks of inverted color
                if (blockNoise(i.uv + time * 0.5) > 0.97 && _GlitchIntensity > 0.5)
                {
                    col.rgb = 1 - col.rgb;
                }
                
                return col;
            }
            ENDCG
        }
    }
}