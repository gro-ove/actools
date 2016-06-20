float3 HSVtoRGB(float3 HSV) {
	float3 RGB = 0;
	float C = HSV.z * HSV.y;
	float H = HSV.x * 6;
	float X = C * (1 - abs(fmod(H, 2) - 1));
	if (HSV.y != 0) {
		float I = floor(H);
		if (I == 0) { RGB = float3(C, X, 0); }
		else if (I == 1) { RGB = float3(X, C, 0); }
		else if (I == 2) { RGB = float3(0, C, X); }
		else if (I == 3) { RGB = float3(0, X, C); }
		else if (I == 4) { RGB = float3(X, 0, C); }
		else { RGB = float3(C, 0, X); }
	}
	float M = HSV.z - C;
	return RGB + M;
}