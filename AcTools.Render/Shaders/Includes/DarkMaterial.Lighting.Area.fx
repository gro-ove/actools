//////////////// Area lights
// thanks to https://www.shadertoy.com/view/ldfGWs

float specTrowbridgeReitz(float HoN, float a, float aP){
	float a2 = a * a;
	float aP2 = aP * aP;
	return (a2 * aP2) / pow(HoN * HoN * (a2 - 1.0) + 1.0, 2.0);
}

float visSchlickSmithMod(float NoL, float NoV, float r){
	float k = pow(r * 0.5 + 0.5, 2.0) * 0.5;
	float l = NoL * (1.0 - k) + k;
	float v = NoV * (1.0 - k) + k;
	return 1.0 / (4.0 * l * v);
}

float fresSchlickSmith(float HoV, float f0){
	return f0 + (1.0 - f0) * pow(1.0 - HoV, 5.0);
}

float AreaLight_Sphere(float3 pos, float3 N, float3 V, float3 r, float3 spherePos, float sphereRad, 
		float f0, float roughness, float NoV, out float distance, out float NoL){
	float3 L = spherePos - pos;
	float3 centerToRay = dot(L, r) * r - L;
	float3 closestPoint = L + centerToRay * clamp(sphereRad / length(centerToRay), 0.0, 1.0);
	float3 l = normalize(closestPoint);
	float3 h = normalize(V + l);

	distance = length(closestPoint - pos);

	NoL = clamp(dot(N, l), 0.0, 1.0);
	float HoN = clamp(dot(h, N), 0.0, 1.0);
	float HoV = dot(h, V);

	float distL = length(L);
	float alpha = roughness * roughness;
	float alphaPrime = clamp(sphereRad / (distL * 2.0) + alpha, 0.0, 1.0);

	float specD = specTrowbridgeReitz(HoN, alpha, alphaPrime);
	float specF = fresSchlickSmith(HoV, f0);
	float specV = visSchlickSmithMod(NoL, NoV, roughness);

	return specD * specF * specV;
}

float AreaLight_Tube(float3 pos, float3 N, float3 V, float3 r, float3 tubeStart, float3 tubeEnd, float tubeRad, 
		float f0, float roughness, float NoV, out float distance, out float NoL){
	float3 L0 = tubeStart - pos;
	float3 L1 = tubeEnd - pos;
	float distL0 = length(L0);
	float distL1 = length(L1);

	float NoL0 = dot(L0, N) / (2.0 * distL0);
	float NoL1 = dot(L1, N) / (2.0 * distL1);
	NoL = (2.0 * clamp(NoL0 + NoL1, 0.0, 1.0))
		/ (distL0 * distL1 + dot(L0, L1) + 2.0);

	float3 Ld = L1 - L0;
	float RoL0 = dot(r, L0);
	float RoLd = dot(r, Ld);
	float L0oLd = dot(L0, Ld);
	float distLd = length(Ld);
	float t = (RoL0 * RoLd - L0oLd)
		/ (distLd * distLd - RoLd * RoLd);

	float3 closestPoint = L0 + Ld * clamp(t, 0.0, 1.0);
	float3 centerToRay = dot(closestPoint, r) * r - closestPoint;
	closestPoint = closestPoint + centerToRay * clamp(tubeRad / length(centerToRay), 0.0, 1.0);
	distance = length(closestPoint - pos);

	float3 l = normalize(closestPoint);
	float3 h = normalize(V + l);

	float HoN = clamp(dot(h, N), 0.0, 1.0);
	float HoV = dot(h, V);

	float distLight = length(closestPoint);
	float alpha = roughness * roughness;
	float alphaPrime = clamp(tubeRad / (distLight * 2.0) + alpha, 0.0, 1.0);

	float specD = specTrowbridgeReitz(HoN, alpha, alphaPrime);
	float specF = fresSchlickSmith(HoV, f0);
	float specV = visSchlickSmithMod(NoL, NoV, roughness);

	return specD * specF * specV;
}


//////////// plane?

float IntegrateEdge(float3 v1, float3 v2){
	float cosTheta = dot(v1, v2);
	cosTheta = clamp(cosTheta, -0.9999, 0.9999);

	float theta = acos(cosTheta);
	float res = cross(v1, v2).z * theta / sin(theta);

	return res;
}

void ClipQuadToHorizon(inout float3 L[5], out int n){
	// detect clipping config
	int config = 0;
	if (L[0].z > 0.0) config += 1;
	if (L[1].z > 0.0) config += 2;
	if (L[2].z > 0.0) config += 4;
	if (L[3].z > 0.0) config += 8;

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

void InitRect(float3 position, float3 normal, float width, float height, out float3 points[4]){
	float3 ex = normalize(cross(normal, abs(dot(normal, float3(0, 1, 0))) > 0.9 ? float3(1, 0, 0) : float3(0, 1, 0)));
	float3 ey = normalize(cross(normal, ex));

	ex *= width;
	ey *= height;

	points[0] = position - ex - ey;
	points[1] = position + ex - ey;
	points[2] = position + ex + ey;
	points[3] = position - ex + ey;
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
    ClipQuadToHorizon(L, n);
    
    if (n == 0) return 0;

    // project onto sphere
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    L[3] = normalize(L[3]);
    L[4] = normalize(L[4]);

    // integrate
    float sum = 0.0;

    sum += IntegrateEdge(L[0], L[1]);
    sum += IntegrateEdge(L[1], L[2]);
    sum += IntegrateEdge(L[2], L[3]);
    if (n >= 4) sum += IntegrateEdge(L[3], L[4]);
    if (n == 5) sum += IntegrateEdge(L[4], L[0]);

    sum = twoSided ? abs(sum) : max(0.0, sum);
    return sum;
}