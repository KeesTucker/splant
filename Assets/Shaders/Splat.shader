Shader "Unlit/Cells"
{
    Properties
    {
        _Colour("Colour", Color) =  (1,1,1,1)
        _Size("Quad Size", Float) = 1
        _Falloff("Quad Transparency Falloff", Float) = 20
        _CamRight("Camera Right", Vector) = (1,0,0,0)
        _CamUp("Camera Up", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

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
                float dist = length(i.uv);
                float alpha = exp(-dist * dist * _Falloff);
                float4 c = _Colour;
                c.w = alpha;
                return c;
            }
            ENDHLSL
        }
    }
}