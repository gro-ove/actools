// Various functions
float2 BarycentricInterpolate(float2 v0, float2 v1, float2 v2, float3 barycentric) {
    return barycentric.z * v0 + barycentric.x * v1 + barycentric.y * v2;
}

float2 BarycentricInterpolate(float2 v[3], float3 barycentric) {
    return BarycentricInterpolate(v[0], v[1], v[2], barycentric);
}

float3 BarycentricInterpolate(float3 v0, float3 v1, float3 v2, float3 barycentric) {
    return barycentric.z * v0 + barycentric.x * v1 + barycentric.y * v2;
}

float3 BarycentricInterpolate(float3 v[3], float3 barycentric) {
    return BarycentricInterpolate(v[0], v[1], v[2], barycentric);
}

float4 BarycentricInterpolate(float4 v0, float4 v1, float4 v2, float3 barycentric) {
    return barycentric.z * v0 + barycentric.x * v1 + barycentric.y * v2;
}

float4 BarycentricInterpolate(float4 v[3], float3 barycentric) {
    return BarycentricInterpolate(v[0], v[1], v[2], barycentric);
}

float3 ProjectOntoPlane(float3 planeNormal, float3 planePoint, float3 pointToProject) {
    return pointToProject - dot(pointToProject-planePoint, planeNormal) * planeNormal;
}

// Shader
#define VertexIn VS_IN
#define DomainOut PS_IN

struct VertexOut {
    float3 PosW : POSITION;
    float3 NormalW : NORMAL;
	float3 TangentW : TANGENT;
	float2 Tex : TEXCOORD;
	float TessFactor : TESS;
};

VertexOut VS(VertexIn vin) {
	VertexOut vout;

	// Transform to world space space.
	vout.PosW     = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	vout.NormalW  = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
	vout.TangentW = mul(vin.TangentL, (float3x3)gWorld);

	// Output vertex attributes for interpolation across triangle.
	vout.Tex = vin.Tex;

	// float d = distance(vout.PosW, gEyePosW);

	// Normalized tessellation factor.
	// The tessellation is
	//   0 if d >= gMinTessDistance and
	//   1 if d <= gMaxTessDistance.
    // float tess = saturate( (gMinTessDistance - d) / (gMinTessDistance - gMaxTessDistance) );

	// Rescale [0,1] --> [gMinTessFactor, gMaxTessFactor].
	vout.TessFactor = 5.0; // gMinTessFactor + tess*(gMaxTessFactor-gMinTessFactor);

	return vout;
}

struct PatchTess {
	float EdgeTess[3] : SV_TessFactor;
	float InsideTess  : SV_InsideTessFactor;
};

PatchTess PatchHS(InputPatch<VertexOut,3> patch, uint patchId : SV_PrimitiveID) {
	PatchTess pt;
	pt.EdgeTess[0] = 0.5f*(patch[1].TessFactor + patch[2].TessFactor);
	pt.EdgeTess[1] = 0.5f*(patch[2].TessFactor + patch[0].TessFactor);
	pt.EdgeTess[2] = 0.5f*(patch[0].TessFactor + patch[1].TessFactor);
	pt.InsideTess  = pt.EdgeTess[0];
	return pt;
}

struct HullOut {
	float3 PosW     : POSITION;
    float3 NormalW  : NORMAL;
	float3 TangentW : TANGENT;
	float2 Tex      : TEXCOORD;
};

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("PatchHS")]
HullOut HS(InputPatch<VertexOut,3> p, uint i : SV_OutputControlPointID, uint patchId : SV_PrimitiveID) {
	HullOut hout;
	hout.PosW     = p[i].PosW;
	hout.NormalW  = p[i].NormalW;
	hout.TangentW = p[i].TangentW;
	hout.Tex      = p[i].Tex;
	return hout;
}

[domain("tri")]
DomainOut DS(PatchTess patchTess, float3 bary : SV_DomainLocation, const OutputPatch<HullOut,3> tri) {
	DomainOut dout;
	dout.PosW = BarycentricInterpolate(tri[0].PosW, tri[1].PosW, tri[2].PosW, bary);
	dout.NormalW = BarycentricInterpolate(tri[0].NormalW, tri[1].NormalW, tri[2].NormalW, bary);
	dout.TangentW = BarycentricInterpolate(tri[0].TangentW, tri[1].TangentW, tri[2].TangentW, bary);
	dout.Tex = BarycentricInterpolate(tri[0].Tex, tri[1].Tex, tri[2].Tex, bary);
	dout.NormalW = normalize(dout.NormalW);
	dout.PosH = mul(float4(dout.PosW, 1.0f), gViewProj);
	return dout;
}

// Phong tessellation
[domain("tri")]
DomainOut DS_phong(PatchTess patchTess, float3 bary : SV_DomainLocation, const OutputPatch<HullOut,3> tri) {
	DomainOut dout;
	dout.PosW = BarycentricInterpolate(tri[0].PosW, tri[1].PosW, tri[2].PosW, bary);
	dout.NormalW = BarycentricInterpolate(tri[0].NormalW, tri[1].NormalW, tri[2].NormalW, bary);
	dout.TangentW = BarycentricInterpolate(tri[0].TangentW, tri[1].TangentW, tri[2].TangentW, bary);
	dout.Tex = BarycentricInterpolate(tri[0].Tex, tri[1].Tex, tri[2].Tex, bary);

    float3 posProjectedU = ProjectOntoPlane(tri[0].NormalW, tri[0].PosW, dout.PosW);
    float3 posProjectedV = ProjectOntoPlane(tri[1].NormalW, tri[1].PosW, dout.PosW);
    float3 posProjectedW = ProjectOntoPlane(tri[2].NormalW, tri[2].PosW, dout.PosW);
    dout.PosW = BarycentricInterpolate(posProjectedU, posProjectedV, posProjectedW, bary);

	dout.NormalW = normalize(dout.NormalW);
	dout.PosH = mul(float4(dout.PosW, 1.0f), gViewProj);
	return dout;
}

// PN tesselation
[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("HS_PNTrianglesConstant")]
HullOut HS_pn(InputPatch<VertexOut, 3> p, uint i : SV_OutputControlPointID, uint patchId : SV_PrimitiveID){
    HullOut hout = (HullOut)0;
	hout.PosW     = p[i].PosW;
	hout.NormalW  = p[i].NormalW;
	hout.TangentW = p[i].TangentW;
	hout.Tex      = p[i].Tex;
    return hout;
}

struct HS_PNTrianglePatchConstant {
    float EdgeTessFactor[3] : SV_TessFactor;
    float InsideTessFactor : SV_InsideTessFactor;

    float3 B210: POSITION3;
    float3 B120: POSITION4;
    float3 B021: POSITION5;
    float3 B012: POSITION6;
    float3 B102: POSITION7;
    float3 B201: POSITION8;
    float3 B111: CENTER;

    float3 N200: NORMAL0;
    float3 N020: NORMAL1;
    float3 N002: NORMAL2;

    float3 N110: NORMAL3;
    float3 N011: NORMAL4;
    float3 N101: NORMAL5;
};

#define TessellationFactor 5.0

HS_PNTrianglePatchConstant HS_PNTrianglesConstant(InputPatch<VertexOut, 3> patch) {
    HS_PNTrianglePatchConstant result = (HS_PNTrianglePatchConstant)0;

    //// Backface culling - using face normal
    //// Calculate face normal
    //float3 edge0 = patch[1].WorldPosition - patch[0].WorldPosition;
    //float3 edge2 = patch[2].WorldPosition - patch[0].WorldPosition;
    //float3 faceNormal = normalize(cross(edge2, edge0));
    //float3 view = normalize(patch[0].WorldPosition - CameraPosition);

    //if (dot(view, faceNormal) < -0.25) {
    //    result.EdgeTessFactor[0] = 0;
    //    result.EdgeTessFactor[1] = 0;
    //    result.EdgeTessFactor[2] = 0;
    //    result.InsideTessFactor = 0;
    //    return result; // culled, so no further processing
    //}
    //// end: backface culling

    //// Backface culling - using Vertex normals
    //bool backFacing = true;
    ////float insideMultiplier = 0.125; // default inside multiplier
    //[unroll]
    //for (uint j = 0; j < 3; j++)
    //{
    //    float3 view = normalize(CameraPosition - patch[j].WorldPosition);
    //    float a = dot(view, patch[j].WorldNormal);
    //    if (a >= -0.125) {
    //        backFacing = false;
    //        //if (a <= 0.125)
    //        //{
    //        //    // Is near to silhouette so keep full tessellation
    //        //    insideMultiplier = 1.0;
    //        //}
    //    }
    //}
    //if (backFacing) {
    //    result.EdgeTessFactor[0] = 0;
    //    result.EdgeTessFactor[1] = 0;
    //    result.EdgeTessFactor[2] = 0;
    //    result.InsideTessFactor = 0;
    //    return result; // culled, so no further processing
    //}
    //// end: backface culling

    float3 roundedEdgeTessFactor; float roundedInsideTessFactor, insideTessFactor;
	ProcessTriTessFactorsMax((float3)TessellationFactor, 1.0, roundedEdgeTessFactor, roundedInsideTessFactor, insideTessFactor);

    // Apply the edge and inside tessellation factors
    result.EdgeTessFactor[0] = roundedEdgeTessFactor.x;
    result.EdgeTessFactor[1] = roundedEdgeTessFactor.y;
    result.EdgeTessFactor[2] = roundedEdgeTessFactor.z;
    result.InsideTessFactor = roundedInsideTessFactor;
    //result.InsideTessFactor = roundedInsideTessFactor * insideMultiplier;

    //************************************************************
    // Calculate PN-Triangle coefficients
    // Refer to Vlachos 2001 for the original formula
    float3 p1 = patch[0].PosW;
    float3 p2 = patch[1].PosW;
    float3 p3 = patch[2].PosW;

    //B300 = p1;
    //B030 = p2;
    //float3 b003 = p3;

    float3 n1 = patch[0].NormalW;
    float3 n2 = patch[1].NormalW;
    float3 n3 = patch[2].NormalW;

    //N200 = n1;
    //N020 = n2;
    //N002 = n3;

    // Calculate control points
    float w12 = dot ((p2 - p1), n1);
    result.B210 = (2.0f * p1 + p2 - w12 * n1) / 3.0f;

    float w21 = dot ((p1 - p2), n2);
    result.B120 = (2.0f * p2 + p1 - w21 * n2) / 3.0f;

    float w23 = dot ((p3 - p2), n2);
    result.B021 = (2.0f * p2 + p3 - w23 * n2) / 3.0f;

    float w32 = dot ((p2 - p3), n3);
    result.B012 = (2.0f * p3 + p2 - w32 * n3) / 3.0f;

    float w31 = dot ((p1 - p3), n3);
    result.B102 = (2.0f * p3 + p1 - w31 * n3) / 3.0f;

    float w13 = dot ((p3 - p1), n1);
    result.B201 = (2.0f * p1 + p3 - w13 * n1) / 3.0f;

    float3 e = (result.B210 + result.B120 + result.B021 +
                result.B012 + result.B102 + result.B201) / 6.0f;
    float3 v = (p1 + p2 + p3) / 3.0f;
    result.B111 = e + ((e - v) / 2.0f);

    // Calculate normals
    float v12 = 2.0f * dot ((p2 - p1), (n1 + n2)) /
                          dot ((p2 - p1), (p2 - p1));
    result.N110 = normalize ((n1 + n2 - v12 * (p2 - p1)));

    float v23 = 2.0f * dot ((p3 - p2), (n2 + n3)) /
                          dot ((p3 - p2), (p3 - p2));
    result.N011 = normalize ((n2 + n3 - v23 * (p3 - p2)));

    float v31 = 2.0f * dot ((p1 - p3), (n3 + n1)) /
                          dot ((p1 - p3), (p1 - p3));
    result.N101 = normalize ((n3 + n1 - v31 * (p1 - p3)));

    return result;
}

[domain("tri")]
DomainOut DS_pn(HS_PNTrianglePatchConstant constantData, float3 bary : SV_DomainLocation, const OutputPatch<HullOut,3> tri) {
	DomainOut dout;

    // Prepare barycentric ops (xyz=uvw,   w=1-u-v,   u,v,w>=0)
    float u = bary.x;
    float v = bary.y;
    float w = bary.z;
    float uu = u * u;
    float vv = v * v;
    float ww = w * w;
    float uu3 = 3.0f * uu;
    float vv3 = 3.0f * vv;
    float ww3 = 3.0f * ww;

    // Interpolate using barycentric coordinates and PN Triangle control points
    dout.PosW =
        tri[0].PosW * w * ww + //B300
        tri[1].PosW * u * uu + //B030
        tri[2].PosW * v * vv + //B003
        constantData.B210 * ww3 * u +
        constantData.B120 * uu3 * w +
        constantData.B201 * ww3 * v +
        constantData.B021 * uu3 * v +
        constantData.B102 * vv3 * w +
        constantData.B012 * vv3 * u +
        constantData.B111 * 6.0f * w * u * v;
    dout.NormalW =
        tri[0].NormalW * ww + //N200
        tri[1].NormalW * uu + //N020
        tri[2].NormalW * vv + //N002
        constantData.N110 * w * u +
        constantData.N011 * u * v +
        constantData.N101 * w * v;

	dout.TangentW = BarycentricInterpolate(tri[0].TangentW, tri[1].TangentW, tri[2].TangentW, bary);
	dout.Tex = BarycentricInterpolate(tri[0].Tex, tri[1].Tex, tri[2].Tex, bary);
	dout.NormalW = normalize(dout.NormalW);
	dout.PosH = mul(float4(dout.PosW, 1.0f), gViewProj);
	return dout;
}