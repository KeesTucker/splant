Shader "Unlit/GaussianSplats"
{
    Properties
    {
        _Colour("Colour", Color) = (1,1,1,1)
        _Size("Quad Size", Float) = 1
        _Falloff("Gaussian Falloff", Float) = 0.5
        _CamRight("Camera Right", Vector) = (1,0,0,0)
        _CamUp("Camera Up", Vector) = (0,1,0,0)
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

            struct cell
            {
                float3 position;
            };

            StructuredBuffer<cell> cells;

            float4 _Colour;
            float _Size;
            float _Falloff;
            float3 _CamRight;
            float3 _CamUp;

            struct vs_out
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
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

                float2 offset = quad[quad_vertex] * _Size;

                float3 pos = cells[cell_id].position;
                float3 world = pos + _CamRight * offset.x + _CamUp * offset.y;

                o.pos = UnityWorldToClipPos(float4(world,1));
                o.uv = quad[quad_vertex];
                return o;
            }

            float4 frag(vs_out i) : SV_Target
            {
                // Gaussian falloff
                float dist2 = dot(i.uv, i.uv);
                float alpha = exp(-dist2 * _Falloff);
                
                // Pre-multiply alpha for correct blending
                float final_alpha = alpha * _Colour.a * 0.1;
                float4 c;
                c.rgb = _Colour.rgb * final_alpha;
                c.a = final_alpha;
                return c;
            }
            ENDHLSL
        }
    }
}