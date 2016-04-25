// textures
    Texture2D gBaseMap;
    Texture2D gLightMap;
    Texture2D gNormalMap;
    Texture2D gDepthMap;

    SamplerState samInputImage {
        Filter = MIN_MAG_LINEAR_MIP_POINT;
        AddressU = CLAMP;
        AddressV = CLAMP;
    };
    
// input resources
    cbuffer cbPerFrame : register(b0) {
        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        float3 gEyePosW;
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
    float3 GetColor(float2 uv){
        return gBaseMap.Sample(samInputImage, uv).rgb + 
                gLightMap.Sample(samInputImage, uv).rgb;
    }

    float3 GetPosition(float2 uv, float depth){
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
        return position.xyz / position.w;
    }

    float GetDepth(float2 uv){
        return gDepthMap.Sample(samInputImage, uv).x;
    }

    float3 GetUv(float3 position){
        float4 pVP = mul(float4(position, 1.0f), gWorldViewProj);
        pVP.xy = float2(0.5f, 0.5f) + float2(0.5f, -0.5f) * pVP.xy / pVP.w;
        return float3(pVP.xy, pVP.z / pVP.w);
    }

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
        return vout;
    }

// debug
    #define MAX_L 0.72
    #define FADING_FROM 0.5
    #define MIN_L 0.0
    #define ITERATIONS 20
    #define START_L 0.01

    float4 ps_HabrahabrVersion(PS_IN pin) : SV_Target {
        float depth = GetDepth(pin.Tex);
        float3 position = GetPosition(pin.Tex, depth);
        float3 normal = gNormalMap.Sample(samInputImage, pin.Tex).xyz;
        float3 viewDir = normalize(position - gEyePosW);
        float3 reflectDir = normalize(reflect(viewDir, normal));

        float3 newUv = 0;
        float L = START_L;
        float quality = 0;

        [flatten]
        for(int i = 0; i < ITERATIONS; i++){
            float3 calculatedPosition = position + reflectDir * L;

            newUv = GetUv(calculatedPosition);
            float3 newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy));
            quality = length(calculatedPosition - newPosition);
            L = (L + length(position - newPosition)) / 2;
        }

        float fresnel = saturate(3.2 * pow(1 + dot(viewDir, normal), 2));
        quality = 1 - saturate(abs(quality) / 0.1);

        float alpha = fresnel * quality * saturate(
            (1 - saturate((length(newUv - pin.Tex) - FADING_FROM) / (MAX_L - FADING_FROM)))
                - min(L - MIN_L, 0) * -10000
        ) * saturate(min(newUv.x, 1 - newUv.x) / 0.1) * saturate(min(newUv.y, 1 - newUv.y) / 0.1);

        return float4(GetColor(newUv.xy).rgb * min(alpha * 4, 1), alpha);
    }

    technique11 HabrahabrVersion {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_HabrahabrVersion() ) );
        }
    }