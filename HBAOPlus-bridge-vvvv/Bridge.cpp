#include "stdafx.h"
#include "Bridge.h"
#include "GFSDK_SSAO.h"
#include "assert.h"

using namespace VVVV::HBAOPlus::Bridge;

GfsdkHbaoContext::GfsdkHbaoContext(SlimDX::Direct3D11::Device^ device)
{
	Unmanaged = new GfsdkHbaoUnmanagedData();
	Unmanaged->customHeap.new_ = ::operator new;
	Unmanaged->customHeap.delete_ = ::operator delete;
	Device = device;
	Unmanaged->device = (ID3D11Device*)(void*)Device->ComPointer;

	Unmanaged->status = ManagedDLLImport::GFSDK_SSAO_CreateContext_D3D11(Unmanaged->device, &(Unmanaged->aoContext), &(Unmanaged->customHeap), GFSDK_SSAO_VERSION);

	//void* context = (void*)(Unmanaged->aoContext);

	assert(Unmanaged->status == GFSDK_SSAO_OK); // HBAO+ requires feature level 11_0 or above
}

GfsdkHbaoContext::~GfsdkHbaoContext()
{
	Unmanaged->aoContext->Release();
	delete RenderTarget;
	delete Unmanaged;
}

void GfsdkHbaoContext::SetDepthSrv()
{
	Unmanaged->input.DepthData.DepthTextureType = GFSDK_SSAO_HARDWARE_DEPTHS;
	Unmanaged->input.DepthData.pFullResDepthTextureSRV = (ID3D11ShaderResourceView*)(void*)DepthSrv->ComPointer;
}

void GfsdkHbaoContext::SetDepthParameters()
{
	GCHandle handle = GCHandle::Alloc(Projection, GCHandleType::Pinned);
	try
	{
		float* proj = (float*)(void*)handle.AddrOfPinnedObject();
		// input.DepthData.pFullResDepthTexture2ndLayerSRV = pDepthStencilTexture2ndLayerSRV; // required only if Params.DualLayerAO=true
		Unmanaged->input.DepthData.ProjectionMatrix.Data = GFSDK_SSAO_Float4x4(proj);
		Unmanaged->input.DepthData.ProjectionMatrix.Layout = GFSDK_SSAO_ROW_MAJOR_ORDER;
		Unmanaged->input.DepthData.MetersToViewSpaceUnits = SceneScale;
		// TODO: implement this sometime
		//Unmanaged->input.NormalData.Enable = false;
	}
	finally
	{
		handle.Free();
	}
}

void GfsdkHbaoContext::SetNormalsParameters()
{
	GCHandle handle = GCHandle::Alloc(View, GCHandleType::Pinned);
	try
	{
		float* proj = (float*)(void*)handle.AddrOfPinnedObject();
		Unmanaged->input.NormalData.Enable = Normal;
		Unmanaged->input.NormalData.pFullResNormalTextureSRV = (ID3D11ShaderResourceView*)(void*)NormalSrv->ComPointer;
		Unmanaged->input.NormalData.WorldToViewMatrix.Data = GFSDK_SSAO_Float4x4(proj);
		Unmanaged->input.NormalData.WorldToViewMatrix.Layout = GFSDK_SSAO_ROW_MAJOR_ORDER;
		Unmanaged->input.NormalData.DecodeBias = DecodeBias;
		Unmanaged->input.NormalData.DecodeScale = DecodeScale;
	}
	finally
	{
		handle.Free();
	}

}

void GfsdkHbaoContext::SetAoParameters()
{
	Unmanaged->params.Radius = Radius;
	Unmanaged->params.Bias = Bias;
	Unmanaged->params.PowerExponent = PowerExp;
	Unmanaged->params.SmallScaleAO = SmallScaleAo;
	Unmanaged->params.LargeScaleAO = LargeScaleAo;
	Unmanaged->params.StepCount = (GFSDK_SSAO_StepCount)((unsigned int)StepCount);

	Unmanaged->params.ForegroundAO.Enable = ForegroundAo;
	Unmanaged->params.ForegroundAO.ForegroundViewDepth = ForegroundViewDepth;
	Unmanaged->params.BackgroundAO.Enable = BackgroundAo;
	Unmanaged->params.BackgroundAO.BackgroundViewDepth = BackgroundViewDepth;

	Unmanaged->params.DepthStorage = (GFSDK_SSAO_DepthStorage)((unsigned int)DepthStorage);
	Unmanaged->params.DepthClampMode = (GFSDK_SSAO_DepthClampMode)((unsigned int)DepthClampMode);

	Unmanaged->params.DepthThreshold.Enable = DepthThreshold;
	Unmanaged->params.DepthThreshold.MaxViewDepth = DepthThresholdMaxViewDepth;
	Unmanaged->params.DepthThreshold.Sharpness = DepthThresholdSharpness;

	Unmanaged->params.Blur.Enable = Blur;
	Unmanaged->params.Blur.Radius = (GFSDK_SSAO_BlurRadius)((unsigned int)BlurRadius);
	Unmanaged->params.Blur.Sharpness = BlurSharpness;

	Unmanaged->params.Blur.SharpnessProfile.Enable = BlurSharpnessProfile;
	Unmanaged->params.Blur.SharpnessProfile.ForegroundSharpnessScale = BlurSharpnessProfileForegroundScale;
	Unmanaged->params.Blur.SharpnessProfile.ForegroundViewDepth = BlurSharpnessProfileForegroundViewDepth;
	Unmanaged->params.Blur.SharpnessProfile.BackgroundViewDepth = BlurSharpnessProfileBackgroundViewDepth;
}

void GfsdkHbaoContext::Render()
{

	Unmanaged->output.pRenderTargetView = (ID3D11RenderTargetView*)(void*)(RenderTarget->ComPointer);
	ID3D11DeviceContext* context = (ID3D11DeviceContext*)(void*)(DeviceContext->ComPointer);
	//GFSDK_SSAO_Output_D3D11* outstruct = &(Unmanaged->output);
	Unmanaged->status = Unmanaged->aoContext->RenderAO(context, Unmanaged->input, Unmanaged->params, Unmanaged->output, Unmanaged->renderMask);
}

void GfsdkHbaoContext::SetRenderMask(GfsdkHbaoRenderMask mask)
{
	Unmanaged->renderMask = (GFSDK_SSAO_RenderMask)((unsigned int)mask);
}

GfsdkHbaoStatus GfsdkHbaoContext::PollStatus()
{
	return (GfsdkHbaoStatus)((unsigned int)(Unmanaged->status));
}