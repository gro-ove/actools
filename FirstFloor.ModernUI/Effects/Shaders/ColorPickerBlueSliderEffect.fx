float red : register(c0);
float green : register(c1);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(red, green, 1.0 - uv.y, 1.0);
}
