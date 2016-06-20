float green : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(1.0 - uv.y, green, uv.x, 1.0);
}