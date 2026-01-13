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
            StructuredBuffer<uint> cell_count;

            // Metaball SDF with proper smoothing
            float evaluate_metaballs(float3 p)
            {
                float density = 0.0;
                
                int3 bucket_coord = floor(p / bucket_size);
                
                // Check neighboring buckets
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int3 check_coord = bucket_coord + int3(dx, dy, dz);
                            uint hash = fast_hash_coord(check_coord);
                            uint bucket = hash & (bucket_table_size - 1);
                            uint start = bucket_start[bucket];
                            uint end = bucket_end[bucket];
                            
                            if (start != 0xffffffff && start < end)
                            {
                                for (uint i = start; i < end; i++)
                                {
                                    uint idx = cell_indices[i];
                                    if (cell_full_hash[idx] == hash && cells[idx].age >= 0)
                                    {
                                        float3 cell_pos = cells[idx].position;
                                        float3 diff = p - cell_pos;
                                        float dist_sq = dot(diff, diff);
                                        
                                        // Smooth kernel with controlled falloff
                                        // TODO: make neighbor_radius independent of physics radius
                                        // TODO: precompute this holy shit
                                        float r = neighbor_radius * 0.8;
                                        float r_sq = r * r;
                                        
                                        if (dist_sq < r_sq)
                                        {
                                            float ratio = dist_sq / r_sq;
                                            // Smooth polynomial kernel
                                            float influence = 1.0 - ratio * ratio * (3.0 - 2.0 * ratio);
                                            density += influence;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Threshold for surface (tune this)
                return 0.5 - density;
            }

            // Tetrahedral normal for better quality
            float3 calc_normal(float3 p, float eps)
            {
                float2 e = float2(1, -1) * eps;
                return normalize(
                    e.xyy * evaluate_metaballs(p + e.xyy) +
                    e.yyx * evaluate_metaballs(p + e.yyx) +
                    e.yxy * evaluate_metaballs(p + e.yxy) +
                    e.xxx * evaluate_metaballs(p + e.xxx)
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

                float t = max(t_enter, 0.0);
                float t_max = t_exit;

                // Fixed step size sphere tracing to avoid artifacts
                float min_step = neighbor_radius * 0.15;
                float max_step = neighbor_radius * 0.5;
                
                [loop]
                for (int iter = 0; iter < 128; iter++)
                {
                    if (t > t_max) break;
                    
                    float3 p = ro + rd * t;
                    float sdf = evaluate_metaballs(p);
                    
                    // Surface hit - refine with binary search
                    if (sdf < 0.0)
                    {
                        // Back up slightly
                        float t_near = t - max_step;
                        float t_far = t;
                        
                        // Binary search for precise surface location
                        for (int j = 0; j < 4; j++)
                        {
                            float t_mid = (t_near + t_far) * 0.5;
                            float3 p_mid = ro + rd * t_mid;
                            float sdf_mid = evaluate_metaballs(p_mid);
                            
                            if (sdf_mid < 0.0)
                                t_far = t_mid;
                            else
                                t_near = t_mid;
                        }
                        
                        p = ro + rd * ((t_near + t_far) * 0.5);
                        
                        // Calculate normal with adaptive epsilon
                        float eps = neighbor_radius * 0.05;
                        float3 normal = calc_normal(p, eps);
                        
                        // Improved lighting
                        float3 light_dir = normalize(float3(0.5, 1, 0.3));
                        float diffuse = max(0.0, dot(normal, light_dir));
                        
                        // Soft ambient occlusion approximation (buggy)
                        //float ao = 1.0 - saturate(abs(sdf) * 2.0);
                        
                        float ambient = 0.3;
                        
                        // Fresnel rim light
                        float fresnel = pow(1.0 - max(0.0, dot(normal, -rd)), 3.0);
                        
                        float3 base_color = float3(0.75, 0.82, 0.88);
                        float3 color = base_color * (ambient /** ao*/ + diffuse * 0.7) + fresnel * 0.15;
                        
                        return float4(color, 1);
                    }
                    
                    // Adaptive step with minimum and maximum bounds
                    float step = clamp(abs(sdf) * 0.8, min_step, max_step);
                    t += step;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}