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
            #pragma shader_feature _BASIC_LIGHTING _NORMAL_DEBUG _TANGENT_DEBUG _VERTEX_COLOR_DEBUG _UV_DEBUG
            #pragma shader_feature _ _OUTPUT_RED
            #pragma shader_feature _ _OUTPUT_GREEN
            #pragma shader_feature _ _OUTPUT_BLUE
            #pragma shader_feature _ _OUTPUT_ALPHA

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float3 _LightDirection;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = v.tangent.xyz;
                o.color = v.color;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

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

                #ifdef _UV_DEBUG
                    // UV Debug
                    col.rgb = float3(i.uv, 0); // Example: Display UV as color
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
