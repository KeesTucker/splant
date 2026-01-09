Shader "Unlit/GaussianSplats"
{
    Properties
    {
        size("Quad Size", Float) = 1
        falloff("Gaussian Falloff", Float) = 0.5
        cam_right("Camera Right", Vector) = (1,0,0,0)
        cam_up("Camera Up", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        Pass
        {
            // Proper alpha blending for accumulation
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            
            #include "Cell.hlsl"

            StructuredBuffer<cell> cells;
            StructuredBuffer<uint> sorted_indices;
            
            float size;
            float falloff;
            float3 cam_right;
            float3 cam_up;

            struct vs_out
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
            };

            vs_out vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                vs_out o;

                uint quad_vertex = vid;
                uint cell_id = iid;

                float2 quad[6] = {
                    float2( 0.5, 0.5),
                    float2( 0.5,-0.5),
                    float2(-0.5,-0.5),
                    float2(-0.5, 0.5),
                    float2( 0.5, 0.5),
                    float2(-0.5,-0.5)
                };

                float2 offset = quad[quad_vertex] * size;

                cell c = cells[sorted_indices[cell_id]];
                
                float3 pos = c.position;
                float3 world = pos + cam_right * offset.x + cam_up * offset.y;

                o.pos = UnityWorldToClipPos(float4(world,1));
                o.uv = quad[quad_vertex];
                o.color = c.color;
                return o;
            }

            float4 frag(vs_out i) : SV_Target
            {
                // Gaussian falloff
                float dist2 = dot(i.uv, i.uv);
                float alpha = exp(-dist2 * falloff);
                
                // Pre-multiply alpha for correct blending
                float final_alpha = alpha * i.color.a * 0.1;
                float4 c;
                c.rgb = i.color.rgb * final_alpha;
                c.a = final_alpha;
                return c;
            }
            ENDHLSL
        }
    }
}