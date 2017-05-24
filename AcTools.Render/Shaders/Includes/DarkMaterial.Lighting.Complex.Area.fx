#define PI 3.141592653

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

float Fpo(float d, float l){
    return l/(d*(d*d + l*l)) + atan(l/d)/(d*d);
}

float Fwt(float d, float l){
    return l*l/(d*(d*d + l*l));
}

float I_diffuse_line(float3 p1, float3 p2){
    // tangent
    float3 wt = normalize(p2 - p1);

    // clamping
    if (p1.z <= 0.0 && p2.z <= 0.0) return 0.0;
    if (p1.z < 0.0) p1 = (+p1*p2.z - p2*p1.z) / (+p2.z - p1.z);
    if (p2.z < 0.0) p2 = (-p1*p2.z + p2*p1.z) / (-p2.z + p1.z);

    // parameterization
    float l1 = dot(p1, wt);
    float l2 = dot(p2, wt);

    // shading point orthonormal projection on the line
    float3 po = p1 - l1*wt;

    // distance to line
    float d = length(po);

    // integral
    float I = (Fpo(d, l2) - Fpo(d, l1)) * po.z +
              (Fwt(d, l2) - Fwt(d, l1)) * wt.z;
    return I / PI;
}

void buildOrthonormalBasis(in float3 n, out float3 b1, out float3 b2){
    if (n.z < -0.9999999)
    {
        b1 = float3( 0.0, -1.0, 0.0);
        b2 = float3(-1.0,  0.0, 0.0);
        return;
    }
    float a = 1.0 / (1.0 + n.z);
    float b = -n.x*n.y*a;
    b1 = float3(1.0 - n.x*n.x*a, b, -n.x);
    b2 = float3(b, 1.0 - n.y*n.y*a, -n.y);
}

float determinant(float3x3 m){
    return + m[0][0]*(m[1][1]*m[2][2] - m[2][1]*m[1][2])
           - m[1][0]*(m[0][1]*m[2][2] - m[2][1]*m[0][2])
           + m[2][0]*(m[0][1]*m[1][2] - m[1][1]*m[0][2]);
}

float3x3 inverse(float3x3 m){
    float a00 = m[0][0], a01 = m[0][1], a02 = m[0][2];
    float a10 = m[1][0], a11 = m[1][1], a12 = m[1][2];
    float a20 = m[2][0], a21 = m[2][1], a22 = m[2][2];

    float b01 =  a22 * a11 - a12 * a21;
    float b11 = -a22 * a10 + a12 * a20;
    float b21 =  a21 * a10 - a11 * a20;

    float det = a00 * b01 + a01 * b11 + a02 * b21;

    return float3x3(b01, (-a22 * a01 + a02 * a21), ( a12 * a01 - a02 * a11),
                b11, ( a22 * a00 - a02 * a20), (-a12 * a00 + a02 * a10),
                b21, (-a21 * a00 + a01 * a20), ( a11 * a00 - a01 * a10)) / det;
}

float I_ltc_line(float3 p1, float3 p2, float3x3 Minv){
    // transform to diffuse configuration
    float3 p1o = mul(Minv, p1);
    float3 p2o = mul(Minv, p2);

    float I_diffuse = I_diffuse_line(p1o, p2o);

    // width factor
    float3 ortho = normalize(cross(p1, p2));
    float w =  1.0 / length(mul(inverse(transpose(Minv)), ortho));

    return w * I_diffuse;
}

float AreaLight_Tube(float3 N, float3 V, float3 P, float3x3 Minv, float3 points[2], float R, bool endCaps){
    // construct orthonormal basis around N
    float3 T1, T2;
    T1 = normalize(V - N * dot(V, N));
    T2 = cross(N, T1);

	float3x3 tmp;
	tmp[0] = T1;
	tmp[1] = T2;
	tmp[2] = N;

    // rotate area light in (T1, T2, N) basis
    // Minv = mul(Minv, tmp);

    // polygon (allocate 5 vertices for clipping)
    float3 p1 = mul(tmp, points[0] - P);
    float3 p2 = mul(tmp, points[1] - P);

    float Iline = R * I_ltc_line(p1, p2, Minv);
    float Idisks = 0;//endCaps ? I_ltc_disks(p1, p2, R) : 0.0;
    return min(1.0, Iline + Idisks);
}