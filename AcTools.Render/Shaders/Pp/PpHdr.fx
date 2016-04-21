#include "Common.fx"

	cbuffer cbPerFrame : register(b0) {
		float2 gPixel;
		float2 gCropImage;
	}

// downsampling
	float4 ps_Downsampling (PS_IN pin) : SV_Target {
		float2 uv = pin.Tex * gCropImage + 0.5 - gCropImage / 2;
		float2 delta = gPixel * uv;
		
		float4 color = tex(uv);
		color += tex(uv + float2(-delta.x, 0));
		color += tex(uv + float2(delta.x, 0));
		color += tex(uv + float2(0, -delta.y));
		color += tex(uv + float2(0, delta.y));
		return saturate(color / 5);
	}

	technique11 Downsampling {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Downsampling() ) );
		}
	}

// downsampling
	Texture2D gBrightnessMap;

	float4 ps_Adaptation (PS_IN pin) : SV_Target {
		return (tex(0.5) * 49 + tex(gBrightnessMap, 0.5)) / 50;
	}

	technique11 Adaptation {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Adaptation() ) );
		}
	}
	
// tonemap
	static const float3 LUM_CONVERT = float3(0.299f, 0.587f, 0.114f);

	Texture2D gBloomMap;

	float3 ToneReinhard(float3 vColor, float average, float exposure, float whitePoint){
		// RGB -> XYZ conversion
		const float3x3 RGB2XYZ = {  0.5141364, 0.3238786,  0.16036376,
									0.265068,  0.67023428, 0.06409157,
									0.0241188, 0.1228178,  0.84442666  };				                    
		float3 XYZ = mul(RGB2XYZ, vColor.rgb);
  
		// XYZ -> Yxy conversion
		float3 Yxy;
		Yxy.r = XYZ.g;                            // copy luminance Y
		Yxy.g = XYZ.r / (XYZ.r + XYZ.g + XYZ.b ); // x = X / (X + Y + Z)
		Yxy.b = XYZ.g / (XYZ.r + XYZ.g + XYZ.b ); // y = Y / (X + Y + Z)
    
		// (Lp) Map average luminance to the middlegrey zone by scaling pixel luminance
		float Lp = Yxy.r * exposure / average;         
                
		// (Ld) Scale all luminance within a displayable range of 0 to 1
		Yxy.r = (Lp * (1.0f + Lp/(whitePoint * whitePoint)))/(1.0f + Lp);
  
		// Yxy -> XYZ conversion
		XYZ.r = Yxy.r * Yxy.g / Yxy. b;               // X = Y * x / y
		XYZ.g = Yxy.r;                                // copy luminance Y
		XYZ.b = Yxy.r * (1 - Yxy.g - Yxy.b) / Yxy.b;  // Z = Y * (1-x-y) / y
    
		// XYZ -> RGB conversion
		const float3x3 XYZ2RGB  = {  2.5651, -1.1665, -0.3986,
									-1.0217,  1.9777,  0.0439, 
									 0.0753, -0.2543,  1.1892  };
		return saturate(mul(XYZ2RGB, XYZ));
	}

	float4 ps_Tonemap (PS_IN pin) : SV_Target {
		float currentBrightness = 0.167 + dot(tex(gBrightnessMap, 0.5).rgb, LUM_CONVERT) * 0.667;
		return float4(ToneReinhard(tex(pin.Tex).rgb, currentBrightness, 0.56, 1.2), 1) + tex(gBloomMap, pin.Tex);
	}

	technique11 Tonemap {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Tonemap() ) );
		}
	}
	
// copy
	float4 ps_Copy (PS_IN pin) : SV_Target {
		return tex(pin.Tex);
	}

	technique11 Copy {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Copy() ) );
		}
	}

// bloom
	float4 ps_Bloom (PS_IN pin) : SV_Target {
		return saturate(tex(pin.Tex) - 1.0);
	}

	technique11 Bloom {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Bloom() ) );
		}
	}