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

            struct vs_out
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float bucket_size;
            float cell_radius;
            float cell_radius_sq;
            float max_step_size;
            float min_step_size;
            float surface_threshold;
            float3 bounds_max;
            float3 bounds_min;
            float3 camera_pos;
            float4x4 inv_view_proj;
            uint bucket_table_size;
            float3 sss_color;
            float sss_strength;
            float sss_falloff;
            float sss_max_distance;


            StructuredBuffer<cell> cells;
            StructuredBuffer<uint> bucket_end;
            StructuredBuffer<uint> bucket_start;
            StructuredBuffer<uint> cell_count;
            StructuredBuffer<uint> cell_full_hash;
            StructuredBuffer<uint> cell_indices;

            vs_out vert(uint id : SV_VertexID)
            {
                vs_out o;
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
                float3 inv_rd = 1.0 / rd;

                float3 t0 = (bmin - ro) * inv_rd;
                float3 t1 = (bmax - ro) * inv_rd;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                t_enter = max(max(tmin.x, tmin.y), tmin.z);
                t_exit  = min(min(tmax.x, tmax.y), tmax.z);

                return t_exit >= max(t_enter, 0.0);
            }

            // metaball SDF with proper smoothing
            float evaluate_metaballs(float3 p)
            {
                float density = 0.0;
                
                int3 bucket_coord = floor(p / bucket_size);
                
                // check neighboring buckets
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
                                        
                                        if (dist_sq < cell_radius_sq)
                                        {
                                            float ratio = dist_sq / cell_radius_sq;
                                            // inverted smooth polynomial kernel
                                            float influence = 1.0 - smooth_step(ratio);
                                            density += influence;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // threshold for surface (tune this)
                return surface_threshold - density;
            }

            // tetrahedral normal for better quality
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
            
            float estimate_thickness(float3 p, float3 light_dir, float step_size, float max_distance)
            {
                float thickness = 0.0;
                float3 sample_pos = p - light_dir * step_size;

                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    if (thickness >= max_distance)
                        break;

                    float sdf = evaluate_metaballs(sample_pos);
                    if (sdf > 0.0)
                        break;

                    thickness += step_size;
                    sample_pos -= light_dir * step_size;
                }

                return thickness;
            }
            
            float4 frag(vs_out i) : SV_Target
            {
                // normalized device coords, -1 -> 1 across the screen on x & y
                float2 ndc = i.uv * 2.0 - 1.0;
                // flip so -1 at bottom of screen and 1 at top
                ndc.y *= -1.0;
                
                float4 clip = float4(ndc, 1, 1);
                
                float4 world = mul(inv_view_proj, clip);
                world.xyz /= world.w;

                float3 ro = camera_pos;
                float3 rd = normalize(world.xyz - camera_pos);

                float t_enter, t_exit;
                if (!ray_aabb(ro, rd, bounds_min, bounds_max, t_enter, t_exit))
                    return float4(0,0,0,1);

                float t = max(t_enter, 0.0);
                float t_max = t_exit;
                
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
                        float t_near = t - max_step_size;
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
                        float eps = cell_radius * 0.05;
                        float3 normal = calc_normal(p, eps);
                        
                        // Improved lighting
                        float3 light_dir = normalize(float3(0.5, 1, 0.3));
                        float ndotl = dot(normal, light_dir);
                        float diffuse = max(0.0, ndotl);
                        
                        // Soft ambient occlusion approximation (buggy)
                        //float ao = 1.0 - saturate(abs(sdf) * 2.0);
                        
                        float ambient = 0.3;
                        
                        // Fresnel rim light
                        float fresnel = pow(1.0 - max(0.0, dot(normal, -rd)), 3.0);
                        
                        // SSS
                        float sss_step = cell_radius * 0.12;
                        float sss_max = cell_radius * sss_max_distance;
                        float thickness = estimate_thickness(p, light_dir, sss_step, sss_max);
                        float transmittance = 1.0 - exp(-thickness * sss_falloff);
                        float wrap = saturate((ndotl + 0.4) / 1.4);
                        float backscatter = pow(saturate(dot(-rd, light_dir)), 2.0);
                        float transmission = saturate(-ndotl);
                        float view_through = saturate(dot(-rd, -light_dir));
                        float sss = sss_strength * transmittance
                            * (0.6 + 0.4 * backscatter)
                            * (wrap + transmission * (0.35 + 0.65 * view_through));
                        
                        float3 base_color = float3(0.75, 0.82, 0.88);
                        float3 color = base_color * (ambient /** ao*/ + diffuse * 0.7) + fresnel * 0.15;
                        color += sss_color * sss;
                        return float4(sss_color * sss, 1);
                    }
                    
                    // Adaptive step with minimum and maximum bounds
                    float step = clamp(abs(sdf) * 0.8, min_step_size, max_step_size);
                    t += step;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}