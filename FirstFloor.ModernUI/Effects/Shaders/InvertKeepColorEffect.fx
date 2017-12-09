sampler2D implicitInput : register(s0);

float4 main(float2 uv : TEXCOORD) : COLOR {
    float4 i = tex2D(implicitInput, uv);
    float3 v = i.rgb / (i.a + 1e-10);

    float4 P = v.g < v.b ? float4(v.bg, -1, 2 / 3) : float4(v.gb, 0, -1 / 3);
    float4 Q = v.r < P.x ? float4(P.xyw, v.r) : float4(v.r, P.yzx);
    Q.x += 1e-10;

    float m = min(Q.w, Q.y);
    float C = Q.x - m;
    float H = abs((Q.w - Q.y) / C + 6 * Q.z);
    float X = C * (1 - abs(fmod(H, 2) - 1));
    float I = floor(H);    
    if (I == 0) v = float3(C, X, 0);
    else if (I == 1) v = float3(X, C, 0);
    else if (I == 2) v = float3(0, C, X);
    else if (I == 3) v = float3(0, X, C);
    else if (I == 4) v = float3(X, 0, C);
    else v = float3(C, 0, X);

    return float4((v + m / Q.x - m) * i.a, i.a);
}

// approximately 59 instruction slots used (1 texture, 58 arithmetic)
