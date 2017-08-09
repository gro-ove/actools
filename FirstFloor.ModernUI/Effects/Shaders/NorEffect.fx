sampler2D input1 : register(S0);
sampler2D input2 : register(S1);

float4 main(float2 uv: TEXCOORD): COLOR {
   float4 color1 = tex2D(input1, uv);
   float4 color2 = tex2D(input2, uv);
   return saturate(color1 - color2) + saturate(color2 - color1);
}