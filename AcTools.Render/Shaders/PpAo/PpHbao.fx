// params
#define _HBAO_RAYS 9
#define _HBAO_S0 (_HBAO_RAYS * 0.07)
#define _HBAO_S1 (0.020 / 0.07 / 0.07)

// textures
Texture2D gDepthMap;
Texture2D gNoiseMap;
// Texture2D gNormalMap;

SamplerState samLinear {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

SamplerState samNoise {
    Filter = MIN_MAG_MIP_POINT;
    AddressU = WRAP;
    AddressV = WRAP;
};

SamplerState samDepth {
    Filter = MIN_MAG_MIP_POINT;
    AddressU = Border;
    AddressV = Border;
    BorderColor = (float4)0;
};

// input resources
cbuffer cbPerFrame : register(b0) {
    float4 gNoiseSize;
    float4 gValues;
    float2 gNearFar;
    float gAoPower;
    // matrix gNormalsToViewSpace;
}

cbuffer OFFSETS {
    float2 offsets[8] = {
        float2(1, 0),
        float2(0.7071f, 0.7071f),
        float2(0, 1),
        float2(-0.7071f, 0.7071f),
        float2(-1, 0),
        float2(-0.7071f, -0.7071f),
        float2(0, -1),
        float2(0.7071f, -0.7071f)
    };
};

// fn structs
struct VS_IN {
    float3 PosL : POSITION;
    float2 Tex  : TEXCOORD;
};

struct PS_IN {
    float4 PosH : SV_POSITION;
    float2 Tex  : TEXCOORD0;
};

// one vertex shader for everything
PS_IN vs_main(VS_IN vin, uint vertexID : SV_VertexID) {
    PS_IN vout;
    vout.PosH = float4(vin.PosL, 1.0f);
    vout.Tex = vin.Tex;
    return vout;
}

// helper functions
float DepthToLinearized(float depth){
    return 1 / (depth * gNearFar.x + gNearFar.y);
}

float GetLinearDepth(float2 texcoord){
    return DepthToLinearized(gDepthMap.SampleLevel(samDepth, texcoord, 0).x);
}

float3 GetEyePosition(float2 uv, float depth) {
    return float3((uv * float2(2, -2) - float2(1, -1)) * gValues.xy * depth, depth);
}

float3 GetEyePosition(float2 uv) {
    return GetEyePosition(uv, GetLinearDepth(uv));
}

// trying to use proper normals?
/*float3 GetNormal(float2 coords) {
    return gNormalMap.Sample(samLinear, coords).xyz;
}*/

// hbao itself
float4 ps_Hbao(PS_IN pin) : SV_Target {
    float depth = GetLinearDepth(pin.Tex),
        occlusion = 0;

    float3 pointPos = GetEyePosition(pin.Tex, depth),
        pointNormal = normalize(cross(ddx(pointPos), ddy(pointPos)));

    float2 random = normalize(gNoiseMap.Sample(samNoise, pin.Tex * gNoiseSize.xy + gNoiseSize.zw).xy),
        multiplier = gValues.zw * depth / _HBAO_S0;

    for (uint i = 0; i < 8; i++) {
        float2 sampleCoords = reflect(offsets[i], random) * multiplier;

        float theta = 0,
            currentOcclusion = 0;
        for (uint k = 1; k <= _HBAO_RAYS; k++) {
            float3 foundPos = GetEyePosition(pin.Tex + sampleCoords * (k - 0.5 * (i % 2))),
                occlusionVector = foundPos - pointPos;

            float tempTheta = dot(pointNormal, normalize(occlusionVector));
            if (tempTheta > theta) {
                theta = tempTheta;
                float tempOcclusion = 1 - sqrt(1 - theta * theta);
                occlusion += 1 / (1 + _HBAO_S1 * dot(occlusionVector, occlusionVector)) * (tempOcclusion - currentOcclusion);
                currentOcclusion = tempOcclusion;
            }
        }
    }

    // trying to use normals buffer instead?
    /*if (pin.Tex.x > 0.25 && pin.Tex.x < 0.3) return float4(pointNormal, 1.0);
    if (pin.Tex.x >= 0.3 && pin.Tex.x < 0.35) return float4(normalize(mul(float4(GetNormal(pin.Tex), 0.0), gNormalsToViewSpace).xyz), 1.0);
    if (pin.Tex.x >= 0.35 && pin.Tex.x < 0.4) return float4((float3)0.5 + pointNormal - normalize(mul(float4(GetNormal(pin.Tex), 0.0), gNormalsToViewSpace).xyz), 1.0);*/
    return 1.0 - saturate(occlusion * (3.4 / 8)) * gAoPower;
}

technique10 Hbao {
    pass P0 {
        SetVertexShader(CompileShader(vs_4_0, vs_main()));
        SetGeometryShader(NULL);
        SetPixelShader(CompileShader(ps_4_0, ps_Hbao()));
    }
}
