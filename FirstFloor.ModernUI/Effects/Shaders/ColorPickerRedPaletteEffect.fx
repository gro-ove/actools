float red : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(red, 1.0 - uv.y, uv.x, 1.0);
}