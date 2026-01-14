Shader "Unlit/Raymarch Metaballs"
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

            float ambient_light;
            float bucket_size;
            float cell_radius;
            float cell_radius_sq;
            float fresnel_pow;
            float fresnel_strength;
            float max_step_size;
            float min_step_size;
            float shadow_density_scale;
            float sss_falloff;
            float sss_start_pos;
            float sss_step_size;
            float sss_steps;
            float sss_strength;
            float surface_threshold;
            float3 bounds_max;
            float3 bounds_min;
            float3 camera_pos;
            float3 light_dir;
            float3 sss_color;
            float4 base_color;
            float4x4 inv_view_proj;
            uint bucket_table_size;

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

            float sample_density(float3 p)
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
                            
                            if (start == 0xffffffff || start >= end) continue;
                            for (uint i = start; i < end; i++)
                            {
                                uint idx = cell_indices[i];
                                if (cell_full_hash[idx] != hash || cells[idx].age < 0) continue;
                                
                                float3 cell_pos = cells[idx].position;
                                float3 diff = p - cell_pos;
                                float dist_sq = dot(diff, diff);
                                
                                if (dist_sq >= cell_radius_sq) continue;
                                
                                float ratio = dist_sq / cell_radius_sq;
                                // inverted smooth polynomial kernel
                                float influence = 1.0 - smooth_step(ratio);
                                density += influence;
                            }
                        }
                    }
                }
                
                return density;
            }

            // tetrahedral normals
            float3 calc_normal(float3 p, float eps)
            {
                // Tetrahedral offsets
                float2 e = float2(1, -1) * eps;
                
                // Sample density at 4 tetrahedral points
                float d1 = sample_density(p + e.xyy);
                float d2 = sample_density(p + e.yyx);
                float d3 = sample_density(p + e.yxy);
                float d4 = sample_density(p + e.xxx);
                
                // Compute gradient and NEGATE it (density increases inward, normals point outward)
                float3 grad = -(e.xyy * d1 +
                                e.yyx * d2 +
                                e.yxy * d3 +
                                e.xxx * d4);
                
                return normalize(grad);
            }
            
            float3 get_density_distance_shadow(float3 start, float3 dir)
            {
                float optical_depth = 0;
                float dist = 0;
                float shadow = 1.0;
                bool found_void = false;
                
                for (int i = 0; i < sss_steps; ++i)
                {
                    dist += sss_step_size;
                    float3 pos = start + dist * dir; 
                    float sample_dens = sample_density(pos);
                    
                    if (!found_void)
                    {
                        optical_depth += sample_dens * sss_step_size;
                        
                        // TODO: experiment with multiplying surface threshold to avoid that gross bright area in between objects
                        if (sample_dens < surface_threshold)
                        {
                            found_void = true;
                        }
                    }
                    else
                    {
                        // Exponential falloff - each dense sample reduces shadow
                        shadow *= exp(-sample_dens * sss_step_size * shadow_density_scale);
                        
                        // Or simpler linear falloff:
                        // shadow *= saturate(1.0 - sample_dens / surface_threshold);
                        
                        // Early out if nearly fully shadowed
                        if (shadow < 0.01) break;
                    }
                }
                
                return float3(optical_depth, dist, shadow);
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
                    float density = sample_density(p);
                    
                    // Surface hit - refine with binary search
                    if (density > surface_threshold)
                    {
                        // Back up slightly
                        float t_near = t - max_step_size;
                        float t_far = t;
                        
                        // Binary search for precise surface location
                        for (int j = 0; j < 4; j++)
                        {
                            float t_mid = (t_near + t_far) * 0.5;
                            float3 p_mid = ro + rd * t_mid;
                            float density_mid = sample_density(p_mid);
                            
                            if (density_mid > surface_threshold)
                                t_far = t_mid;
                            else
                                t_near = t_mid;
                        }
                        
                        p = ro + rd * ((t_near + t_far) * 0.5);
                        
                        // Calculate normal with adaptive epsilon
                        float eps = cell_radius * 0.05;
                        float3 normal = calc_normal(p, eps);

                        // Cast ray into the surface toward the light
                        // TODO: unsure if offseting my normal actually helps.
                        float3 result = get_density_distance_shadow(p + normal * sss_start_pos, light_dir);
                        
                        float shadow = result.z;

                        // Diffuse with shadow
                        float ndotl = dot(normal, light_dir);
                        float diffuse = max(0.0, ndotl) * shadow;

                        // SSS with shadow - light can't transmit through if it's blocked!
                        float optical_depth = result.x;
                        // Use optical depth directly instead of normalizing by distance
                        float sss_factor = exp(-optical_depth * sss_falloff) * sss_strength;
                        sss_factor *= shadow;

                        float backlight = saturate(-ndotl);
                        sss_factor *= (1.0 + backlight * 2.0);

                        // Fresnel rim light
                        float fresnel = pow(1.0 - max(0.0, dot(normal, -rd)), fresnel_pow);

                        // Combine lighting with SSS
                        float3 sss_contribution = sss_color * sss_factor;
                        float3 diffuse_light = base_color.rgb * (ambient_light + diffuse * (1 - ambient_light));
                        float3 color = diffuse_light + sss_contribution + fresnel * fresnel_strength;

                        return float4(color, 1);
                    }
                    
                    // Adaptive step with minimum and maximum bounds
                    float step = clamp(abs(surface_threshold - density) * 0.8, min_step_size, max_step_size);
                    t += step;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}