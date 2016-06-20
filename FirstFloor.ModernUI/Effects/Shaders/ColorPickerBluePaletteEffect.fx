float blue : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(uv.x, 1.0 - uv.y, blue, 1.0);
}