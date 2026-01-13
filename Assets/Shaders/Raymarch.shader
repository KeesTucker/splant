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
                
                // --- DDA LOOP ---
                int prev_axis = -1; // Track which axis we crossed to enter current bucket

                [loop]
                for (int iter = 0; iter < 64 && t < t1; iter++)
                {
                    // First, check density at the current bucket before moving
                    uint hash = fast_hash_coord(cell);
                    uint bucket = hash & (bucket_table_size - 1);
                    uint start = bucket_start[bucket];
                    uint end   = bucket_end[bucket];
                    
                    float bucket_density = 0;
                    if (start != 0xffffffff && start < end)
                    {
                        for (uint i = start; i < end; i++)
                        {
                            uint idx = cell_indices[i];
                            if (cell_full_hash[idx] == hash && cells[idx].age >= 0)
                            {
                                bucket_density++;
                            }
                        }
                    }
                    
                    // If this bucket has density, render it as a solid cube
                    if (bucket_density > 1.0)
                    {
                        // Calculate normal based on which axis we crossed to enter this bucket
                        float3 normal = float3(0, 0, 0);
                        if (prev_axis == 0) normal.x = -step.x;
                        else if (prev_axis == 1) normal.y = -step.y;
                        else if (prev_axis == 2) normal.z = -step.z;
                        else normal = float3(0, 1, 0); // fallback for first bucket
                        
                        // Simple directional lighting
                        float3 light_dir = normalize(float3(0.5, 1, 0.3));
                        float diffuse = max(0.0, dot(normal, light_dir));
                        float ambient = 0.2;
                        
                        float3 color = float3(0.8, 0.8, 0.9) * (ambient + diffuse * 0.8);
                        return float4(color, 1);
                    }
                    
                    // Move to next bucket and track which axis
                    if (tMax.x < tMax.y && tMax.x < tMax.z)
                    {
                        t = tMax.x;
                        tMax.x += tDelta.x;
                        cell.x += step.x;
                        prev_axis = 0;
                    }
                    else if (tMax.y < tMax.z)
                    {
                        t = tMax.y;
                        tMax.y += tDelta.y;
                        cell.y += step.y;
                        prev_axis = 1;
                    }
                    else
                    {
                        t = tMax.z;
                        tMax.z += tDelta.z;
                        cell.z += step.z;
                        prev_axis = 2;
                    }
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
