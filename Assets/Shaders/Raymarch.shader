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
            )
            {
                float3 inv_rd = 1.0 / max(abs(rd), 1e-6) * sign(rd);

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
            StructuredBuffer<uint> bucket_end;
            StructuredBuffer<uint> cell_indices;
            StructuredBuffer<uint> cell_full_hash;
            StructuredBuffer<cell> cells;

            float sample_density(float3 p)
            {
                int3 base_cell = floor(p / bucket_size);
                float d = 0;

                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    int3 cellc = base_cell + int3(x, y, z);
                    uint hash = hash_coord(cellc);
                    uint bucket = hash & (bucket_table_size - 1);

                    uint start = bucket_start[bucket];
                    uint end   = bucket_end[bucket];

                    if (start == 0xffffffff || start >= end)
                        continue;
                    
                    float3 bucket_center = (float3(cellc) + 0.5) * bucket_size;
                    float3 delta = p - bucket_center;
                    float dist = length(delta);
                    float falloff = 1.0 - saturate(dist / (bucket_size * 1.732)); // 1.732 ≈ sqrt(3), diagonal
                    
                    for (uint i = start; i < end; i++)
                    {
                        uint idx = cell_indices[i];
                        // Comment hash check to visualize hash contention
                        if (cell_full_hash[idx] != hash || cells[idx].age < 0)
                            continue;
                        
                        // Expensive per cell distance checks
                        /*float3 delta = p - cells[idx].position;
                        float r2 = dot(delta, delta);
                        
                        if (r2 < neighbor_radius_sq)
                        {
                            float t = r2 / neighbor_radius_sq;
                            float w = (1.0 - t) * (1.0 - t);  // quadratic falloff
                            d += w;
                        }*/
                        
                        d += falloff;
                    }
                }

                return d;
            }

            float3 density_gradient(float3 p)
            {
                float eps = max(bucket_size * 0.25, neighbor_radius * 0.5);

                return float3(
                    sample_density(p + float3(eps, 0, 0)) - sample_density(p - float3(eps, 0, 0)),
                    sample_density(p + float3(0, eps, 0)) - sample_density(p - float3(0, eps, 0)),
                    sample_density(p + float3(0, 0, eps)) - sample_density(p - float3(0, 0, eps))
                );
            }

            float4 frag(VSOut i) : SV_Target
            {
                float2 ndc = i.uv * 2.0 - 1.0;
                ndc.y *= -1.0;

                float4 clip  = float4(ndc, 0, 1);
                float4 world = mul(inv_view_proj, clip);
                world.xyz /= world.w;

                float3 ro = camera_pos;
                float3 rd = normalize(world.xyz - camera_pos);

                float t_enter, t_exit;
                if (!ray_aabb(ro, rd, bounds_min, bounds_max, t_enter, t_exit))
                    return float4(0,0,0,1);

                float t0 = max(t_enter, 0.0);
                float t1 = t_exit;

                // --- DDA SETUP ---
                float3 rayOrigin = ro + rd * t0;
                float3 rayDir    = rd;

                float3 rayDirSafe = max(abs(rayDir), 1e-6) * sign(rayDir);
                int3 step = sign(rayDirSafe);

                int3 cell = floor((rayOrigin - 1e-6 * step) / bucket_size);

                float3 nextBoundary = float3(
                    (step.x > 0 ? (cell.x + 1) : cell.x) * bucket_size,
                    (step.y > 0 ? (cell.y + 1) : cell.y) * bucket_size,
                    (step.z > 0 ? (cell.z + 1) : cell.z) * bucket_size
                );

                float3 tMax   = (nextBoundary - rayOrigin) / rayDirSafe;
                float3 tDelta = abs(bucket_size / rayDirSafe);

                float t = t0;
                float density = 0;

                // --- DDA LOOP ---
                [loop]
                for (int iter = 0; iter < 64 && t < t1; iter++)
                {
                    float t_next;
                    int axis;

                    if (tMax.x < tMax.y && tMax.x < tMax.z)
                    {
                        t_next = tMax.x;
                        tMax.x += tDelta.x;
                        cell.x += step.x;
                        axis = 0;
                    }
                    else if (tMax.y < tMax.z)
                    {
                        t_next = tMax.y;
                        tMax.y += tDelta.y;
                        cell.y += step.y;
                        axis = 1;
                    }
                    else
                    {
                        t_next = tMax.z;
                        tMax.z += tDelta.z;
                        cell.z += step.z;
                        axis = 2;
                    }

                    float t_mid = 0.5 * (t + t_next);
                    float3 p = ro + rd * t_mid;

                    float prev_density = density;
                    density += sample_density(p) * (t_next - t);

                    if (density > 1.0)
                    {
                        float alpha = saturate((1.0 - prev_density) / (density - prev_density));
                        float t_hit = lerp(t, t_next, alpha);

                        float3 p_hit = ro + rd * t_hit;
                        float3 g = density_gradient(p_hit);
                        float3 normal = normalize(g + 1e-4);

                        float3 light_dir = normalize(float3(0.4, 0.8, 0.2));
                        float lighting = 0.25 + 0.75 * saturate(dot(normal, light_dir));

                        return float4(0.1, lighting, 0.1, 1);
                    }

                    t = t_next;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
