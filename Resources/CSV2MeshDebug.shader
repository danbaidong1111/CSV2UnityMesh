Shader "Hidden/CSV2MeshDebug"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            // Define keywords for different debug modes
            #pragma shader_feature _BASIC_LIGHTING _NORMAL_DEBUG _TANGENT_DEBUG _VERTEX_COLOR_DEBUG _TEXCOORD0_DEBUG _TEXCOORD1_DEBUG _TEXCOORD2_DEBUG _TEXCOORD3_DEBUG _TEXCOORD4_DEBUG
            #pragma shader_feature _ _OUTPUT_RED
            #pragma shader_feature _ _OUTPUT_GREEN
            #pragma shader_feature _ _OUTPUT_BLUE
            #pragma shader_feature _ _OUTPUT_ALPHA

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 color : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                float4 texcoord3 : TEXCOORD3;
                float4 texcoord4 : TEXCOORD4;
            };

            struct v2f
            {
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                float4 texcoord3 : TEXCOORD3;
                float4 texcoord4 : TEXCOORD4;
                
                float3 worldNormal : TEXCOORD5;
                float3 worldTangent : TEXCOORD6;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float3 _LightDirection;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = v.tangent.xyz;
                o.color = v.color;

                o.texcoord0 = v.texcoord0;
                o.texcoord1 = v.texcoord1;
                o.texcoord2 = v.texcoord2;
                o.texcoord3 = v.texcoord3;
                o.texcoord4 = v.texcoord4;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord0.xy);

                // Use shader feature to define behavior based on active keyword
                #ifdef _BASIC_LIGHTING
                    // Basic Lighting Debug
                    // Lambert diffuse calculation
                    float NdotL = dot(i.worldNormal, _LightDirection) * 0.5 + 0.5;
                    col.rgb = NdotL * 0.5; // Example: Reduce intensity for basic lighting
                #endif

                #ifdef _NORMAL_DEBUG
                    // Normal Debug
                    // 将法线转换为颜色进行调试显示
                    fixed3 normalColor = 0.5 * (i.worldNormal + 1);
                    col.rgb = normalColor;
                #endif

                #ifdef _TANGENT_DEBUG
                    // Tangent Debug
                    col.rgb = 0.5 * (i.worldTangent + 1);
                #endif

                #ifdef _VERTEX_COLOR_DEBUG
                    // Vertex Color Debug
                    col.rgb *= i.color.rgb; // Example: Multiply with vertex color for debug
                    col.a = i.color.a;
                #endif

                #ifdef _TEXCOORD0_DEBUG
                    col = i.texcoord0;
                #elif _TEXCOORD1_DEBUG
                    col = i.texcoord1;
                #elif _TEXCOORD2_DEBUG
                    col = i.texcoord2;
                #elif _TEXCOORD3_DEBUG
                    col = i.texcoord3;
                #elif _TEXCOORD4_DEBUG
                    col = i.texcoord4;
                #endif


                float4 result = col;
                #ifndef _OUTPUT_RED
                result.r = 0;
                #endif
                #ifndef _OUTPUT_GREEN
                result.g = 0;
                #endif
                #ifndef _OUTPUT_BLUE
                result.b = 0;
                #endif

                #ifdef _OUTPUT_ALPHA
                
                    #if defined(_OUTPUT_RED) || defined(_OUTPUT_GREEN) || defined(_OUTPUT_BLUE)
                    result.rgb *= result.a;
                    #else
                    result.rgb = result.aaa;
                    #endif

                #endif
                
                return result;
            }
            ENDCG
        }

        
    }
}
