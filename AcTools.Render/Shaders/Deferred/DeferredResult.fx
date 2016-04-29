// textures
    Texture2D gBaseMap;
    Texture2D gNormalMap;
    Texture2D gMapsMap;
    Texture2D gDepthMap;
    Texture2D gLightMap;
    Texture2D gLocalReflectionMap;
    Texture2D gBottomLayerMap;
    TextureCube gReflectionCubemap;

    SamplerState samInputImage {
        Filter = MIN_MAG_LINEAR_MIP_POINT;
        AddressU = CLAMP;
        AddressV = CLAMP;
    };

    SamplerState samTest {
        Filter = MIN_MAG_MIP_POINT;
        AddressU = CLAMP;
        AddressV = CLAMP;
    };

    SamplerState samAnisotropic {
        Filter = ANISOTROPIC;
        MaxAnisotropy = 4;

        AddressU = WRAP;
        AddressV = WRAP;
    };
    
// input resources
    cbuffer cbPerFrame : register(b0) {
        matrix gWorldViewProjInv;
        float3 gEyePosW;
        float4 gScreenSize;
    }

// fn structs
    struct VS_IN {
        float3 PosL    : POSITION;
        float2 Tex     : TEXCOORD;
    };

    struct PS_IN {
        float4 PosH    : SV_POSITION;
        float2 Tex     : TEXCOORD;
    };

// functions
    float3 GetPosition(float2 uv, float depth){
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
        return position.xyz / position.w;
    }

    float CalculateReflectionPower(float3 toEyeNormalW, float3 normalW, float metalness){
		metalness = max(metalness, 0.1);

        float rid = 1 - dot(toEyeNormalW, normalW);
        float rim = metalness + pow(saturate(rid), max(1 / metalness - 2.4, 0));
        return saturate(rim);
    }

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
        return vout;
    }

// debug (g-buffer)
    float4 ps_debug(PS_IN pin) : SV_Target {
        if (pin.Tex.y < 0.5){
            if (pin.Tex.x < 0.5){
                return gBaseMap.Sample(samInputImage, pin.Tex * 2);
            } else {
                float4 normalValue = gNormalMap.Sample(samInputImage, pin.Tex * 2 - float2(1, 0));
                if (normalValue.x == 0 && normalValue.y == 0 && normalValue.z == 0){
                    return 0.0;
                }
                return 0.5 + 0.5 * normalValue;
            }
        } else {
            if (pin.Tex.x < 0.5){
                float depthValue = gDepthMap.Sample(samInputImage, pin.Tex * 2 - float2(0, 1)).x;
                return (1 - pow(saturate(depthValue), 10));
            }
        }
        
        if (pin.Tex.y < 0.75){
            if (pin.Tex.x < 0.75){
                return gMapsMap.Sample(samInputImage, pin.Tex * 4 - float2(2, 2)).x;
            } else {
                return gMapsMap.Sample(samInputImage, pin.Tex * 4 - float2(3, 2)).y;
            }
        } else {
            if (pin.Tex.x < 0.75){
                return gMapsMap.Sample(samInputImage, pin.Tex * 4 - float2(2, 3)).z;
            } else {
                return gMapsMap.Sample(samInputImage, pin.Tex * 4 - float2(3, 3)).w;
            }
        }

        return 0;
    }

    technique11 Debug {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_debug() ) );
        }
    }

// debug (post effects)
    float4 ps_DebugPost(PS_IN pin) : SV_Target {
        if (pin.Tex.y < 0.5){
            if (pin.Tex.x < 0.5){
                return gBaseMap.Sample(samInputImage, pin.Tex * 2).a;
            } else {
                float2 uv = pin.Tex * 2 - float2(1.0, 0.0);

                float4 normalValue = gNormalMap.Sample(samInputImage, uv);
                float3 normal = normalValue.xyz;
        
                float depth = gDepthMap.Sample(samInputImage, uv).x;
                float3 position = GetPosition(uv, depth);
        
                float3 toEyeW = normalize(gEyePosW - position);
                float4 reflectionColor = gReflectionCubemap.Sample(samAnisotropic, reflect(-toEyeW, normal));
                return reflectionColor;
                
                float4 mapsValue = gMapsMap.Sample(samInputImage, uv);
                float glossiness = mapsValue.g;
                float reflectiveness = mapsValue.z;
                float metalness = mapsValue.w;

                return reflectionColor * reflectiveness * CalculateReflectionPower(toEyeW, normal, metalness);
            }
        } else {
            if (pin.Tex.x < 0.5){
                return gLocalReflectionMap.Sample(samInputImage, pin.Tex * 2 - float2(0.0, 1.0));
            } else {
                return gLightMap.Sample(samInputImage, pin.Tex * 2 - float2(1.0, 1.0));
            }
        }
    }

    technique11 DebugPost {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_DebugPost() ) );
        }
    }

// debug (lighting)
    float4 ps_DebugLighting(PS_IN pin) : SV_Target {
        return gLightMap.Sample(samInputImage, pin.Tex);
    }

    technique11 DebugLighting {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_DebugLighting() ) );
        }
    }

// debug (local reflections)
    float4 ps_DebugLocalReflections(PS_IN pin) : SV_Target {
        float4 base = gBaseMap.Sample(samInputImage, pin.Tex);
        float4 light = gLightMap.Sample(samInputImage, pin.Tex);
        float4 reflection = gLocalReflectionMap.Sample(samInputImage, pin.Tex);

        float x = saturate((pin.Tex.x * (gScreenSize.x / 16) % 2 - 1.0) * 1e6);
        float y = saturate((pin.Tex.y * (gScreenSize.y / 16) % 2 - 1.0) * 1e6);
        float background = ((x + y) % 2) * 0.2 + 0.7;

        return background * (1 - reflection.a) + reflection * reflection.a;
    }

    technique11 DebugLocalReflections {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_DebugLocalReflections() ) );
        }
    }

// ps0
    float3 ReflectionColor(float3 toEyeW, float3 normal, float glossiness) {
        return gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal), 1 - glossiness).rgb;
    }

    float4 ps_0(PS_IN pin) : SV_Target {
        // normal and position
        float4 normalValue = gNormalMap.Sample(samTest, pin.Tex);

        float3 normal = normalValue.xyz;
        float3 position = GetPosition(pin.Tex, gDepthMap.Sample(samInputImage, pin.Tex).x);

        // albedo and lightness
        float4 baseValue = gBaseMap.Sample(samInputImage, pin.Tex);
        float4 lightValue = gLightMap.Sample(samInputImage, pin.Tex);
        float3 lighted = baseValue.rgb + lightValue.rgb;

        // spec/reflection params
        float4 mapsValue = gMapsMap.Sample(samInputImage, pin.Tex);
        float glossiness = mapsValue.g;
        float reflectiveness = mapsValue.z;
        float metalness = mapsValue.w;
        
        // reflection
        float3 toEyeW = normalize(gEyePosW - position);
        float3 reflectionColor = ReflectionColor(toEyeW, normal, glossiness);

        float4 localReflectionColor = gLocalReflectionMap.Sample(samInputImage, pin.Tex);
        float reflectionAlpha = saturate((localReflectionColor.a - 0.5) * 2.0 + 0.5);
        reflectionColor = reflectionColor * (1.0 - reflectionAlpha) + localReflectionColor.rgb * reflectionAlpha;

        float reflectionPower = saturate(reflectiveness * CalculateReflectionPower(toEyeW, normal, metalness));
        float3 reflection = reflectionColor * (1.0 - metalness * 0.1) * reflectionPower;

        float alpha = min(normalValue.a + reflectionPower * metalness, 1.0);

        float metalnessFine = metalness * reflectiveness * 0.5;
        lighted = max(lighted - metalnessFine, 0.0);

		return gBottomLayerMap.Sample(samInputImage, pin.Tex) * (1.0 - alpha) +
			float4(lighted + reflection, 1.0) * alpha;
    }

    technique11 Combine0 {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_0() ) );
        }
    }