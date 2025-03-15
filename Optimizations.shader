Shader "Unlit/Optimizations"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle] _UseMipMap("Use Mip Maps", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Cull Back        // Only render back faces (standard culling)
        ZWrite On        // Write to depth buffer
        ZTest LEqual     // Render pixels when they are less than or equal to the existing depth
        
        // Note: We'll set up the shader to disable Z clipping using a shader feature

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma shader_feature _ZCLIP_OFF

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                
                #ifdef _ZCLIP_OFF
                // Disable Z clip by playing with the projection matrix
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                // Force w to be large enough that z/w is always in valid range
                // This effectively disables z clipping for this vertex
                if (clipPos.w < 0.01)
                    clipPos.w = 0.01;
                o.vertex = clipPos;
                #else
                // Standard vertex transformation
                o.vertex = UnityObjectToClipPos(v.vertex);
                #endif
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                #ifdef _USEMIPMAP_ON
                // Use regular texture sampling with mipmaps
                fixed4 col = tex2D(_MainTex, i.uv);
                #else
                // Force level 0 mipmap for crisp pixel art
                fixed4 col = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                #endif
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    // Define a shader variant for transparent blocks with different culling and blending
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Cull Off        // Render both sides for transparent blocks
        ZWrite On       // Still write to depth buffer
        ZTest LEqual    // Same z test as the opaque version
        Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature _ZCLIP_OFF

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                
                #ifdef _ZCLIP_OFF
                // Disable Z clip
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                if (clipPos.w < 0.01)
                    clipPos.w = 0.01;
                o.vertex = clipPos;
                #else
                o.vertex = UnityObjectToClipPos(v.vertex);
                #endif
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                #ifdef _USEMIPMAP_ON
                // Use regular texture sampling with mipmaps
                fixed4 col = tex2D(_MainTex, i.uv);
                #else
                // Force level 0 mipmap for crisp pixel art
                fixed4 col = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                #endif
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback shader
    FallBack "Unlit/Texture"
    
    CustomEditor "OptimizationsShaderGUI"
}
