float AreaLight_GetSpecular(float3 N, float3 V, float3 l, float R, float3 L, float f0, float y, float q, float g) {
	float w = pow(g * 0.5 + 0.5, 2) * 0.5;
	float W = 1 - w;
	float j = g * g;
	float A = j * j;
	float G = saturate(R / (length(L) * 2) + j);
	float3 h = normalize(V + l);
	float M = saturate(dot(h, N));
	float H = dot(h, V);
	return A * G * G * (f0 + (1 - f0) * pow(1 - H, 5)) / (4 * (y * W + w) * (q * W + w) * pow(M * M * (A - 1) + 1, 2));
}

float AreaLight_Sphere(float3 pos, float3 N, float3 V, float3 r, float3 P, float R, float f0, float g, float q,	
		out float distance, out float y) {
	float3 L = P - pos;
	float3 c = dot(L, r) * r - L;
	float3 o = L + c * saturate(R / length(c));
	float3 l = normalize(o);
	distance = length(o);
	y = saturate(dot(N, l));
	return AreaLight_GetSpecular(N, V, l, R, L, f0, y, q, g);
}

float AreaLight_Tube(float3 pos, float3 N, float3 V, float3 r, float3 P0, float3 P1, float R, float f0, float g, float q,
		out float distance, out float y){
	float3 L0 = P0 - pos;
	float3 L1 = P1 - pos;
	float3 w = L1 - L0;
	float s = dot(r, w);
	float z = length(w);
	float t = (dot(r, L0) * s - dot(L0, w))	/ (z * z - s * s);

	float3 L = L0 + w * saturate(t);
	float3 c = dot(L, r) * r - L;
	L = L + c * saturate(R / length(c));

	float a = length(L0);
	float b = length(L1);

	y = (2 * saturate(dot(L0, N) / (2 * a) + dot(L1, N) / (2 * b))) / (a * b + dot(L0, L1) + 2);
	distance = length(L);

	return AreaLight_GetSpecular(N, V, L / distance, R, L, f0, y, q, g);
}

float AreaLight_IntegrateEdge(float3 v1, float3 v2){
	float cosTheta = dot(v1, v2);
	cosTheta = clamp(cosTheta, -0.9999, 0.9999);

	float theta = acos(cosTheta);
	float res = cross(v1, v2).z * theta / sin(theta);

	return res;
}

void AreaLight_ClipQuadToHorizon(inout float3 L[5], out int n){
	// detect clipping config
	int config = 0;
	if (L[0].z > 0) config += 1;
	if (L[1].z > 0) config += 2;
	if (L[2].z > 0) config += 4;
	if (L[3].z > 0) config += 8;

	// clip
	n = 0;

	if (config == 0){
		// clip all
	} else if (config == 1){ // V1 clip V2 V3 V4
		n = 3;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[3].z * L[0] + L[0].z * L[3];
	} else if (config == 2){ // V2 clip V1 V3 V4
		n = 3;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
	} else if (config == 3){ // V1 V2 clip V3 V4
		n = 4;
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		L[3] = -L[3].z * L[0] + L[0].z * L[3];
	} else if (config == 4){ // V3 clip V1 V2 V4
		n = 3;
		L[0] = -L[3].z * L[2] + L[2].z * L[3];
		L[1] = -L[1].z * L[2] + L[2].z * L[1];
	} else if (config == 5){ // V1 V3 clip V2 V4) impossible
		n = 0;
	} else if (config == 6){ // V2 V3 clip V1 V4
		n = 4;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];
	} else if (config == 7){ // V1 V2 V3 clip V4
		n = 5;
		L[4] = -L[3].z * L[0] + L[0].z * L[3];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];
	} else if (config == 8){ // V4 clip V1 V2 V3
		n = 3;
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
		L[1] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] = L[3];
	} else if (config == 9){ // V1 V4 clip V2 V3
		n = 4;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[2].z * L[3] + L[3].z * L[2];
	} else if (config == 10){ // V2 V4 clip V1 V3) impossible
		n = 0;
	} else if (config == 11){ // V1 V2 V4 clip V3
		n = 5;
		L[4] = L[3];
		L[3] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
	} else if (config == 12){ // V3 V4 clip V1 V2
		n = 4;
		L[1] = -L[1].z * L[2] + L[2].z * L[1];
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
	} else if (config == 13){ // V1 V3 V4 clip V2
		n = 5;
		L[4] = L[3];
		L[3] = L[2];
		L[2] = -L[1].z * L[2] + L[2].z * L[1];
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
	} else if (config == 14){ // V2 V3 V4 clip V1
		n = 5;
		L[4] = -L[0].z * L[3] + L[3].z * L[0];
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
	} else if (config == 15){ // V1 V2 V3 V4
		n = 4;
	}

	if (n == 3) {
		L[3] = L[0];
	}

	if (n == 4) {
		L[4] = L[0];
	}
}

float AreaLight_Plane(float3 N, float3 V, float3 P, float3x3 Minv, float3 points[4], bool twoSided){
    // construct orthonormal basis around N
    float3 T1, T2;
    T1 = normalize(V - N * dot(V, N));
    T2 = cross(N, T1);

	float3x3 tmp;
	tmp[0] = T1;
	tmp[1] = T2;
	tmp[2] = N;

    // rotate area light in (T1, T2, N) basis
    Minv = mul(Minv, tmp);

    // polygon (allocate 5 vertices for clipping)
    float3 L[5];
    L[0] = mul(Minv, points[0] - P);
    L[1] = mul(Minv, points[1] - P);
    L[2] = mul(Minv, points[2] - P);
    L[3] = mul(Minv, points[3] - P);
	L[4] = L[3];

    int n;
    AreaLight_ClipQuadToHorizon(L, n);
    
    if (n == 0) return 0;

    // project onto sphere
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    L[3] = normalize(L[3]);
    L[4] = normalize(L[4]);

    // integrate
    float sum = 0;

    sum += AreaLight_IntegrateEdge(L[0], L[1]);
    sum += AreaLight_IntegrateEdge(L[1], L[2]);
    sum += AreaLight_IntegrateEdge(L[2], L[3]);
    if (n >= 4) sum += AreaLight_IntegrateEdge(L[3], L[4]);
    if (n == 5) sum += AreaLight_IntegrateEdge(L[4], L[0]);

    sum = twoSided ? abs(sum) : max(0, sum);
    return sum;
}