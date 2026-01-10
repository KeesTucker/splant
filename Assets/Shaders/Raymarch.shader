Shader "Unlit/Raymarch"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Cell.hlsl"
            #include "Helpers.hlsl"

            struct VSOut
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float3 camera_pos;
            float4x4 inv_view_proj;
            float3 bounds_min;
            float3 bounds_max;

            VSOut vert(uint id : SV_VertexID)
            {
                VSOut o;

                float2 verts[3] = {
                    float2(-1, -1),
                    float2(-1,  3),
                    float2( 3, -1)
                };

                o.pos = float4(verts[id], 0, 1);
                o.uv  = (o.pos.xy + 1.0) * 0.5;
                return o;
            }

            bool ray_aabb(
                float3 ro,
                float3 rd,
                float3 bmin,
                float3 bmax,
                out float t_enter,
                out float t_exit
            ) {
                float3 inv_rd = 1.0 / rd;

                float3 t0 = (bmin - ro) * inv_rd;
                float3 t1 = (bmax - ro) * inv_rd;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                t_enter = max(max(tmin.x, tmin.y), tmin.z);
                t_exit  = min(min(tmax.x, tmax.y), tmax.z);

                return t_exit >= max(t_enter, 0.0);
            }
            
            float bucket_size;
            uint bucket_table_size;
            float neighbor_radius;
            float neighbor_radius_sq;
            
            StructuredBuffer<uint> bucket_start;
            StructuredBuffer<uint> bucket_count;
            StructuredBuffer<uint> cell_indices;
            StructuredBuffer<uint> cell_full_hash;
            StructuredBuffer<cell> cells;
            
            float sample_density(float3 p)
            {
                int3 base_cell = floor(p / bucket_size);
                float d = 0;

                // Iterate neighboring buckets
                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    int3 cell = base_cell + int3(x, y, z);
                    uint hash = hash_coord(cell);
                    uint bucket = hash & (bucket_table_size - 1);

                    uint start = bucket_start[bucket];
                    uint count = bucket_count[bucket];

                    if (start == 0xffffffff)
                        continue;

                    for (uint i = 0; i < count; i++)
                    {
                        uint idx = cell_indices[start + i];

                        if (cell_full_hash[idx] != hash)
                            continue;

                        float3 q = cells[idx].position;
                        float r2 = dot(p - q, p - q);

                        if (r2 < neighbor_radius_sq)
                        {
                            float w = 1.0 - sqrt(r2) / neighbor_radius;
                            d += w;
                        }
                    }
                }

                return d;
            }
            
            float3 density_gradient(float3 p)
            {
                float eps = max(bucket_size * 0.25, neighbor_radius * 0.5);

                float dx = sample_density(p + float3(eps, 0, 0)) -
                           sample_density(p - float3(eps, 0, 0));

                float dy = sample_density(p + float3(0, eps, 0)) -
                           sample_density(p - float3(0, eps, 0));

                float dz = sample_density(p + float3(0, 0, eps)) -
                           sample_density(p - float3(0, 0, eps));

                return float3(dx, dy, dz);
            }
            
            float4 frag(VSOut i) : SV_Target
            {
                float2 ndc = i.uv * 2.0 - 1.0;
                ndc.y *= -1.0;

                float4 clip = float4(ndc, 0, 1);
                float4 world = mul(inv_view_proj, clip);
                world.xyz /= world.w;

                float3 ray_dir = normalize(world.xyz - camera_pos);
                
                float3 ro = camera_pos;
                float3 rd = ray_dir;

                float t_enter, t_exit;
                if (!ray_aabb(ro, rd, bounds_min, bounds_max, t_enter, t_exit))
                {
                    return float4(0,0,0,1); // background
                }

                float t0 = max(t_enter, 0.0);
                float t1 = t_exit;

                float t = t0;
                float dt = bucket_size * 0.25;

                float density = 0;

                for (int i = 0; i < 128 && t < t1; i++)
                {
                    float3 p = ro + rd * t;

                    float prev_density = density;
                    density += sample_density(p) * dt;
                    
                    if (density > 1.0)
                    {
                        float alpha = saturate((1.0 - prev_density) / (density - prev_density));
                        float t_hit = lerp(t - dt, t, alpha);

                        float3 p_hit = ro + rd * t_hit;
                        float3 g = density_gradient(p_hit);
                        float3 normal = normalize(g + 1e-4);
                        
                        float3 light_dir = normalize(float3(0.4, 0.8, 0.2));
                        float ndot_l = saturate(dot(normal, light_dir));

                        float lighting = 0.25 + 0.75 * ndot_l;

                        return float4(0.1, lighting.x, 0.1, 1);
                    };

                    t += dt;
                }
                
                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
