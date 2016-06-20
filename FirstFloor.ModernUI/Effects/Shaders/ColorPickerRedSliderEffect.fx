float green : register(c0);
float blue : register(c1);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(1.0 - uv.y, green, blue, 1.0);
}
