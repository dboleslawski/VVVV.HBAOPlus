#include "stdafx.h"
#include "Bridge.h"
#include "GFSDK_SSAO.h"
#include "assert.h"

void InitHBAO(void* device)
{
	customHeap.new_ = ::operator new;
	customHeap.delete_ = ::operator delete;

	status = GFSDK_SSAO_CreateContext_D3D11((ID3D11Device*)device, &aoContext, &customHeap);
	assert(status == GFSDK_SSAO_OK); // HBAO+ requires feature level 11_0 or above
}

void SetDepthParameters(void* depthSRV, float* proj, float sceneScale)
{
	input.DepthData.DepthTextureType = GFSDK_SSAO_HARDWARE_DEPTHS;
	input.DepthData.pFullResDepthTextureSRV = (ID3D11ShaderResourceView*)depthSRV;
	// input.DepthData.pFullResDepthTexture2ndLayerSRV = pDepthStencilTexture2ndLayerSRV; // required only if Params.DualLayerAO=true
	input.DepthData.ProjectionMatrix.Data = GFSDK_SSAO_Float4x4(proj);
	input.DepthData.ProjectionMatrix.Layout = GFSDK_SSAO_ROW_MAJOR_ORDER;
	input.DepthData.MetersToViewSpaceUnits = sceneScale;
	input.NormalData.Enable = false; // TODO: implement this sometime
}

void SetAoParameters(float radius, float bias, float powerExp, float smallScaleAo, float largeScaleAo, GFSDK_SSAO_StepCount stepCount,
	bool foregroundAo, float foregroundViewDepth, bool backgroundAo, float backgroundViewDepth,
	GFSDK_SSAO_DepthStorage depthStorage, GFSDK_SSAO_DepthClampMode depthClampMode,
	bool depthThreshold, float depthThresholdMaxViewDepth, float depthThresholdSharpness,
	bool blur, GFSDK_SSAO_BlurRadius blurRadius, float blurSharpness,
	bool blurSharpnessProfile, float blurSharpnessProfileForegroundScale, float blurSharpnessProfileForegroundViewDepth, float blurSharpnessProfileBackgroundViewDepth)
{
	params.Radius = radius;
	params.Bias = bias;
	params.PowerExponent = powerExp;
	params.SmallScaleAO = smallScaleAo;
	params.LargeScaleAO = largeScaleAo;
	params.StepCount = stepCount;

	params.ForegroundAO.Enable = foregroundAo;
	params.ForegroundAO.ForegroundViewDepth = foregroundViewDepth;
	params.BackgroundAO.Enable = backgroundAo;
	params.BackgroundAO.BackgroundViewDepth = backgroundViewDepth;

	params.DepthStorage = depthStorage;
	params.DepthClampMode = depthClampMode;

	params.DepthThreshold.Enable = depthThreshold;
	params.DepthThreshold.MaxViewDepth = depthThresholdMaxViewDepth;
	params.DepthThreshold.Sharpness = depthThresholdSharpness;

	params.Blur.Enable = blur;
	params.Blur.Radius = blurRadius;
	params.Blur.Sharpness = blurSharpness;

	params.Blur.SharpnessProfile.Enable = blurSharpnessProfile;
	params.Blur.SharpnessProfile.ForegroundSharpnessScale = blurSharpnessProfileForegroundScale;
	params.Blur.SharpnessProfile.ForegroundViewDepth = blurSharpnessProfileForegroundViewDepth;
	params.Blur.SharpnessProfile.BackgroundViewDepth = blurSharpnessProfileBackgroundViewDepth;
}

void RenderHBAO(ID3D11DeviceContext* context, ID3D11RenderTargetView* rtv)
{
	output.pRenderTargetView = rtv;
	status = aoContext->RenderAO(context, input, params, output, renderMask);
}

void SetRenderMask(GFSDK_SSAO_RenderMask mask)
{
	renderMask = mask;
}

GFSDK_SSAO_Status PollStatus()
{
	return status;
}