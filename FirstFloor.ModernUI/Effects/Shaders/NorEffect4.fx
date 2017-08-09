sampler2D input1 : register(S0);
sampler2D input2 : register(S1);
sampler2D input3 : register(S2);
sampler2D input4 : register(S3);

float4 nor(float4 a, float4 b){
    if (a.a == 0) return b;
    float3 color = a.rgb / a.a;
    float alpha = 1 - abs(1 - a.a - b.a);
    return float4(color * alpha, alpha);
}

float4 main(float2 uv: TEXCOORD): COLOR {
   float4 color1 = tex2D(input1, uv);
   float4 color2 = tex2D(input2, uv);
   float4 color3 = tex2D(input3, uv);
   float4 color4 = tex2D(input4, uv);
   float4 pair1 = nor(color1, color2);
   float4 pair2 = nor(color3, color4);
   return nor(pair1, pair2);
}