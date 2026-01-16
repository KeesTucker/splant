Shader "Raymarch/Plant"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Cell.hlsl"
            #include "Helpers.hlsl"
            
            #define FRESNEL_ENABLED 1

            struct vs_out
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float ambient_light;
            float bucket_size;
            float cell_radius;
            float fresnel_pow;
            float fresnel_strength;
            float max_step_size;
            float min_step_size;
            float debug_grad;
            float debug_atlas;
            float debug_atlas_slice;
            float sss_falloff;
            float sss_step_size;
            float sss_strength;
            float surface_threshold;
            float sdf_max_distance;
            float3 bounds_max;
            float3 bounds_min;
            float3 camera_pos;
            float3 light_dir;
            float3 sss_color;
            float3 sdf_sample_dim;
            float3 sdf_bucket_min;
            float3 sdf_atlas_brick_dim;
            float4 base_color;
            float4x4 inv_view_proj;
            uint bucket_table_size;
            uint max_steps;
            uint sss_steps;
            float sdf_samples_per_bucket;
            
            Texture3D<float> sdf_brick_atlas;
            Texture3D<uint> sdf_brick_index;
            
            StructuredBuffer<cell> cells;
            StructuredBuffer<uint> bucket_end;
            StructuredBuffer<uint> bucket_start;
            StructuredBuffer<uint> bucket_check;
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

            float decode_sdf(float encoded)
            {
                return encoded * (2.0 * sdf_max_distance) - sdf_max_distance;
            }

            float sample_sdf_texel(int3 sample_coord)
            {
                int3 max_coord = (int3)sdf_sample_dim - 1;
                sample_coord = clamp(sample_coord, int3(0, 0, 0), max_coord);
                
                int3 brick_coord = sample_coord / 8;
                int3 local_coord = sample_coord - brick_coord * 8;
                uint brick_index = sdf_brick_index.Load(int4(brick_coord, 0));
                if (brick_index == 0)
                {
                    return sdf_max_distance;
                }
                int3 atlas_brick_coord = index_to_coord_3d(brick_index - 1, (uint3)sdf_atlas_brick_dim);
                int3 atlas_coord = atlas_brick_coord * 8 + local_coord;
                float encoded = sdf_brick_atlas.Load(int4(atlas_coord, 0));
                return decode_sdf(encoded);
            }

            float sample_distance(float3 world_pos)
            {
                if (world_pos.x < bounds_min.x || world_pos.y < bounds_min.y || world_pos.z < bounds_min.z ||
                    world_pos.x > bounds_max.x || world_pos.y > bounds_max.y || world_pos.z > bounds_max.z)
                {
                    return sdf_max_distance;
                }

                float3 grid_pos = ((world_pos / bucket_size) - sdf_bucket_min) * sdf_samples_per_bucket;
                float3 base_f = floor(grid_pos);
                float3 t = saturate(grid_pos - base_f);

                int3 base_coord = (int3)base_f;
                int3 next_coord = min(base_coord + 1, (int3)sdf_sample_dim - 1);

                float d000 = sample_sdf_texel(int3(base_coord.x, base_coord.y, base_coord.z));
                float d100 = sample_sdf_texel(int3(next_coord.x, base_coord.y, base_coord.z));
                float d010 = sample_sdf_texel(int3(base_coord.x, next_coord.y, base_coord.z));
                float d110 = sample_sdf_texel(int3(next_coord.x, next_coord.y, base_coord.z));
                float d001 = sample_sdf_texel(int3(base_coord.x, base_coord.y, next_coord.z));
                float d101 = sample_sdf_texel(int3(next_coord.x, base_coord.y, next_coord.z));
                float d011 = sample_sdf_texel(int3(base_coord.x, next_coord.y, next_coord.z));
                float d111 = sample_sdf_texel(int3(next_coord.x, next_coord.y, next_coord.z));

                float d00 = lerp(d000, d100, t.x);
                float d10 = lerp(d010, d110, t.x);
                float d01 = lerp(d001, d101, t.x);
                float d11 = lerp(d011, d111, t.x);
                float d0 = lerp(d00, d10, t.y);
                float d1 = lerp(d01, d11, t.y);
                return lerp(d0, d1, t.z);
            }

            float sample_distance_with_grad(float3 world_pos, out float3 grad)
            {
                if (world_pos.x < bounds_min.x || world_pos.y < bounds_min.y || world_pos.z < bounds_min.z ||
                    world_pos.x > bounds_max.x || world_pos.y > bounds_max.y || world_pos.z > bounds_max.z)
                {
                    grad = float3(0, 0, 0);
                    return sdf_max_distance;
                }

                float3 grid_pos = ((world_pos / bucket_size) - sdf_bucket_min) * sdf_samples_per_bucket;
                int3 max_base = (int3)sdf_sample_dim - 2;
                int3 base_coord = clamp((int3)floor(grid_pos), int3(0, 0, 0), max_base);
                float3 t = saturate(grid_pos - (float3)base_coord);
                int3 next_coord = base_coord + 1;

                float d000 = sample_sdf_texel(int3(base_coord.x, base_coord.y, base_coord.z));
                float d100 = sample_sdf_texel(int3(next_coord.x, base_coord.y, base_coord.z));
                float d010 = sample_sdf_texel(int3(base_coord.x, next_coord.y, base_coord.z));
                float d110 = sample_sdf_texel(int3(next_coord.x, next_coord.y, base_coord.z));
                float d001 = sample_sdf_texel(int3(base_coord.x, base_coord.y, next_coord.z));
                float d101 = sample_sdf_texel(int3(next_coord.x, base_coord.y, next_coord.z));
                float d011 = sample_sdf_texel(int3(base_coord.x, next_coord.y, next_coord.z));
                float d111 = sample_sdf_texel(int3(next_coord.x, next_coord.y, next_coord.z));

                float d00 = lerp(d000, d100, t.x);
                float d10 = lerp(d010, d110, t.x);
                float d01 = lerp(d001, d101, t.x);
                float d11 = lerp(d011, d111, t.x);
                float d0 = lerp(d00, d10, t.y);
                float d1 = lerp(d01, d11, t.y);
                float d = lerp(d0, d1, t.z);

                float ddx = lerp(lerp(d100 - d000, d110 - d010, t.y),
                                 lerp(d101 - d001, d111 - d011, t.y), t.z);
                float ddy = lerp(lerp(d010 - d000, d110 - d100, t.x),
                                 lerp(d011 - d001, d111 - d101, t.x), t.z);
                float ddz = lerp(lerp(d001 - d000, d101 - d100, t.x),
                                 lerp(d011 - d010, d111 - d110, t.x), t.y);

                float sample_size = bucket_size / max(sdf_samples_per_bucket, 1.0);
                grad = float3(ddx, ddy, ddz) / max(sample_size, 1e-6);
                return d;
            }

            // SSS using fast sampling
            float optical_depth_along_ray(float3 start, float3 dir, float2 uv)
            {
                float dist = 0;
                float optical_depth = 0;

                for (int i = 0; i < sss_steps; ++i)
                {
                    float step = lerp(0, sss_step_size, float(i + 1) / float(sss_steps + 1));
                    dist += step;
                    
                    float3 pos = start + dist * dir;

                    float distance = sample_distance(pos);
                    float density = saturate(1.0 - distance / cell_radius);
                    optical_depth += density;

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
                if (debug_atlas > 0.5)
                {
                    int3 atlas_dim = (int3)sdf_atlas_brick_dim * 8;
                    int2 atlas_xy = (int2)floor(input.uv * atlas_dim.xy);
                    atlas_xy = clamp(atlas_xy, int2(0, 0), atlas_dim.xy - 1);
                    int slice = clamp((int)floor(debug_atlas_slice), 0, atlas_dim.z - 1);
                    float encoded = sdf_brick_atlas.Load(int4(atlas_xy, slice, 0));
                    return float4(encoded, encoded, encoded, 1);
                }

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

                if (debug_grad > 0.5)
                {
                    float gmax = 0.0;
                    float t = dist;
                    int debug_steps = min(max_steps, 64);
                    for (int i = 0; i < debug_steps; i++)
                    {
                        if (t > dist_max) break;
                        float3 p_debug = ray_origin + ray_direction * t;
                        float3 grad_debug;
                        float d_debug = sample_distance_with_grad(p_debug, grad_debug);
                        gmax = max(gmax, length(grad_debug));
                        t += max(min_step_size, abs(d_debug));
                    }
                    float g = saturate(gmax * 0.2);
                    return float4(g, g, g, 1);
                }
                
                for (int i = 0; i < max_steps; i++)
                {
                    // early out if we have exited bounds
                    if (dist > dist_max) break;
                    
                    float3 p = ray_origin + ray_direction * dist;
                    float distance = sample_distance(p);
                    
                    // surface hit, refine with binary search
                    if (distance <= surface_threshold)
                    {
                        // back up slightly
                        float dist_near = dist - max_step_size;
                        float dist_far = dist;
                        
                        // binary search for precise surface location
                        for (int j = 0; j < 4; j++)
                        {
                            float t_mid = (dist_near + dist_far) * 0.5;
                            float3 p_mid = ray_origin + ray_direction * t_mid;
                            float distance_mid = sample_distance(p_mid);
                            
                            if (distance_mid <= surface_threshold)
                                dist_far = t_mid;
                            else
                                dist_near = t_mid;
                        }
                        
                        // recalculate p with our refined distance
                        p = ray_origin + ray_direction * ((dist_near + dist_far) * 0.5);
                        
                        // crude approximation of sss, surprisingly, by not calculating diffuse lighting and just
                        // applying the sss on top of a dark base colour you get a really nice soft shadow with self
                        // shadowing for free since we march along light dir + it hides a lot of the imperfections
                        float optical_depth_toward_light = optical_depth_along_ray(p, light_dir, input.uv);
                        float sss_factor = exp(-optical_depth_toward_light * sss_falloff) * sss_strength;
                        sss_factor = saturate(sss_factor);
                        
                        float3 color = lerp(base_color.rgb, sss_color, sss_factor);
                        
                        // fresnel rim light
                        if (FRESNEL_ENABLED)
                        {
                            float3 grad;
                            sample_distance_with_grad(p, grad);
                            float3 normal = normalize(grad);
                            float fresnel = pow(1.0 - max(0.0, dot(normal, -ray_direction)), fresnel_pow);
                            color += fresnel * fresnel_strength;
                        }
                        
                        // naive distance fog
                        //color *= 1 - saturate(dist * 0.2);
                        
                        return float4(color, 1);
                    }
                    
                    // adaptive step with minimum and maximum bounds, 0.8 multiplier seems to be conservative enough
                    float step = clamp(abs(distance) * 0.8, min_step_size, max_step_size);
                    dist += step;
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
