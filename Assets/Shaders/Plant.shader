Shader "Raymarch/Plant"
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
            
            #define FRESNEL_ENABLED 0

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
            float inv_cell_radius_sq;
            float max_step_size;
            float min_step_size;
            float sss_falloff;
            float sss_step_size;
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
            uint max_steps;
            uint sss_steps;
            

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

            bool ray_intersection_aabb(
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
                
                // Instead of checking all 27 buckets unconditionally, check if you're near bucket boundaries
                int3 bucket_coord = floor(p / bucket_size);
                float3 local_pos = frac(p / bucket_size);
                float edge_threshold = cell_radius / bucket_size;

                // Only check neighbors if we're close to the edge
                int dx_range = (local_pos.x < edge_threshold || local_pos.x > 1.0 - edge_threshold) ? 1 : 0;
                int dy_range = (local_pos.y < edge_threshold || local_pos.y > 1.0 - edge_threshold) ? 1 : 0;
                int dz_range = (local_pos.z < edge_threshold || local_pos.z > 1.0 - edge_threshold) ? 1 : 0;

                for (int dx = -dx_range; dx <= dx_range; dx++)
                    for (int dy = -dy_range; dy <= dy_range; dy++)
                        for (int dz = -dz_range; dz <= dz_range; dz++)
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
                                
                                float ratio = dist_sq * inv_cell_radius_sq;
                                // inverted smooth polynomial kernel
                                float influence = 1.0 - smooth_step(ratio);
                                density += influence;
                            }
                        }
                return density;
            }

            // tetrahedral normals
            float3 calc_normal(float3 p, float eps)
            {
                // tetrahedral offsets
                float2 e = float2(1, -1) * eps;
                
                // sample density at 4 tetrahedral points
                float d1 = sample_density(p + e.xyy);
                float d2 = sample_density(p + e.yyx);
                float d3 = sample_density(p + e.yxy);
                float d4 = sample_density(p + e.xxx);
                
                // compute gradient and negate it (density increases inward, normals point outward)
                float3 grad = -(e.xyy * d1 +
                                e.yyx * d2 +
                                e.yxy * d3 +
                                e.xxx * d4);
                
                return normalize(grad);
            }
            
            // helper for calculating sss
            // TODO: anisotropic scattering using cell polarity?
            float optical_depth_along_ray(float3 start, float3 dir)
            {
                float dist = 0;
                // approximation of how much material we have moved through
                float optical_depth = 0;

                for (int i = 0; i < sss_steps; ++i)
                {
                    // progressivley coarser step size so we capture fine detail close to hit point and then sample at
                    // lower res further away
                    float step = lerp(0, sss_step_size, float(i + 1) / float(sss_steps + 1));
                    dist += step;
                    float3 pos = start + dist * dir; 
                    float density = sample_density(pos);
                    // since we are moving through progressivley more material this isn't linear with distance
                    // it does, however, let us capture fine details, like small overhang shadows while still getting 
                    // good light transmission through thicker bits
                    optical_depth += density;
                    // early exit cause there is no point calculating more than this
                    if (optical_depth * sss_falloff > 6.0) break;
                }
                
                return optical_depth;
            }
            
            float3 get_ray_dir_for_uv(float2 uv)
            {
                // -1 -> 1 across the screen on x & y
                float2 normalized_device_coordinates = uv * 2.0 - 1.0;
                // flip so -1 at bottom of screen and 1 at top
                normalized_device_coordinates.y *= -1.0;
                // set z to 1 for far clipping plane, set w to 1 for homogeneous coordinate
                float4 clip_space_pos = float4(normalized_device_coordinates, 1, 1);
                // inverse perspective projection (unprojection)
                float4 world_pos = mul(inv_view_proj, clip_space_pos);
                // divide by homogeneous coordinate to retrieve true world space pos
                world_pos.xyz /= world_pos.w;
                // find dir from camera pos to point at far clipping plane 
                return normalize(world_pos.xyz - camera_pos);
            }
            
            float4 frag(vs_out input) : SV_Target
            {
                // start ray at camera position
                float3 ray_origin = camera_pos;
                float3 ray_direction = get_ray_dir_for_uv(input.uv);

                // check if we intersect with the sim bounds
                float dist_enter, dist_exit;
                if (!ray_intersection_aabb(
                    ray_origin,
                    ray_direction,
                    bounds_min,
                    bounds_max,
                    dist_enter,
                    dist_exit
                    )
                )
                    return float4(0,0,0,1);

                float dist = max(dist_enter, 0.0);
                float dist_max = dist_exit;
                
                [loop]
                for (int i = 0; i < max_steps; i++)
                {
                    // early out if we have exited bounds
                    if (dist > dist_max) break;
                    
                    float3 p = ray_origin + ray_direction * dist;
                    float density = sample_density(p);
                    
                    // surface hit, refine with binary search
                    if (density > surface_threshold)
                    {
                        // back up slightly
                        float dist_near = dist - max_step_size;
                        float dist_far = dist;
                        
                        // binary search for precise surface location
                        for (int j = 0; j < 4; j++)
                        {
                            float t_mid = (dist_near + dist_far) * 0.5;
                            float3 p_mid = ray_origin + ray_direction * t_mid;
                            float density_mid = sample_density(p_mid);
                            
                            if (density_mid > surface_threshold)
                                dist_far = t_mid;
                            else
                                dist_near = t_mid;
                        }
                        
                        // recalculate p with our refined distance
                        p = ray_origin + ray_direction * ((dist_near + dist_far) * 0.5);
                        
                        // crude approximation of sss, surprisingly, by not calculating diffuse lighting and just
                        // applying the sss on top of a dark base colour you get a really nice soft shadow with self
                        // shadowing for free since we march along light dir + it hides a lot of the imperfections
                        float optical_depth_toward_light = optical_depth_along_ray(p, light_dir);
                        float sss_factor = exp(-optical_depth_toward_light * sss_falloff) * sss_strength;
                        sss_factor = saturate(sss_factor);
                        
                        float3 color = lerp(base_color.rgb, sss_color, sss_factor);
                        
                        // fresnel rim light
                        if (FRESNEL_ENABLED)
                        {
                            float epsilon = cell_radius * 0.05;
                            float3 normal = calc_normal(p, epsilon);
                            float fresnel = pow(1.0 - max(0.0, dot(normal, -ray_direction)), fresnel_pow);
                            color += fresnel * fresnel_strength;
                        }
                        
                        return float4(color, 1);
                    }
                    
                    // adaptive step with minimum and maximum bounds, 0.8 multiplier seems to be conservative enough
                    float step = clamp(abs(surface_threshold - density) * 0.8, min_step_size, max_step_size);
                    dist += step;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}