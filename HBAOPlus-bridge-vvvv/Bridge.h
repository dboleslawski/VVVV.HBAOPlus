#pragma once

#include "d3d11.h"
#include "GFSDK_SSAO.h"

#ifdef BRIDGE_EXPORTS
#define BRIDGE_API __declspec(dllexport)
#else
#define BRIDGE_API __declspec(dllimport)
#endif

static ID3D11Device* device;
static GFSDK_SSAO_InputData_D3D11 input;
static GFSDK_SSAO_CustomHeap customHeap;
static GFSDK_SSAO_Status status;
static GFSDK_SSAO_Context_D3D11* aoContext;
static GFSDK_SSAO_Parameters params;
static GFSDK_SSAO_Output_D3D11 output;
static GFSDK_SSAO_RenderMask renderMask;

extern "C" BRIDGE_API void InitHBAO(void* device);

extern "C" BRIDGE_API void SetDepthParameters(void* depthSRV, float* proj, float sceneScale);

extern "C" BRIDGE_API void SetAoParameters(float radius, float bias, float powerExp, float smallScaleAo, float largeScaleAo, GFSDK_SSAO_StepCount stepCount,
	bool foregroundAo, float foregroundViewDepth, bool backgroundAo, float backgroundViewDepth,
	GFSDK_SSAO_DepthStorage depthStorage, GFSDK_SSAO_DepthClampMode depthClampMode,
	bool depthThreshold, float depthThresholdMaxViewDepth, float depthThresholdSharpness,
	bool blur, GFSDK_SSAO_BlurRadius blurRadius, float blurSharpness,
	bool blurSharpnessProfile, float blurSharpnessProfileForegroundScale, float blurSharpnessProfileForegroundViewDepth, float blurSharpnessProfileBackgroundViewDepth);

extern "C" BRIDGE_API void RenderHBAO(ID3D11DeviceContext* context, ID3D11RenderTargetView* rtv);

extern "C" BRIDGE_API void SetRenderMask(GFSDK_SSAO_RenderMask mask);

extern "C" BRIDGE_API GFSDK_SSAO_Status PollStatus();