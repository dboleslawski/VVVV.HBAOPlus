#pragma once

#include "d3d11.h"
#include "GFSDK_SSAO.h"

#ifdef BRIDGE_EXPORTS
#define BRIDGE_API __declspec(dllexport)
#else
#define BRIDGE_API __declspec(dllimport)
#endif

using namespace SlimDX;
using namespace SlimDX::Direct3D11;

namespace VVVV {
	namespace HBAOPlus {
		namespace Bridge {

			public enum class GfsdkHbaoStepCount
			{
				GFSDK_SSAO_STEP_COUNT_4,                                // Use 4 steps per sampled direction (same as in HBAO+ 3.x)
				GFSDK_SSAO_STEP_COUNT_8,                                // Use 8 steps per sampled direction (slower, to reduce banding artifacts)
			};

			public enum class GfsdkHbaoDepthStorage
			{
				GFSDK_SSAO_FP16_VIEW_DEPTHS,                            // Store the internal view depths in FP16 (recommended)
				GFSDK_SSAO_FP32_VIEW_DEPTHS,                            // Store the internal view depths in FP32 (slower)
			};

			public enum class GfsdkHbaoDepthClampMode
			{
				GFSDK_SSAO_CLAMP_TO_EDGE,                               // Use clamp-to-edge when sampling depth (may cause false occlusion near screen borders)
				GFSDK_SSAO_CLAMP_TO_BORDER,                             // Use clamp-to-border when sampling depth (may cause halos near screen borders)
			};
			public enum class GfsdkHbaoBlurRadius
			{
				GFSDK_SSAO_BLUR_RADIUS_2,                               // Kernel radius = 2 pixels
				GFSDK_SSAO_BLUR_RADIUS_4,                               // Kernel radius = 4 pixels (recommended)
			};

			public enum class GfsdkHbaoRenderMask
			{
				GFSDK_SSAO_DRAW_Z = (1 << 0),  // Linearize the input depths
				GFSDK_SSAO_DRAW_AO = (1 << 1),  // Render AO based on pre-linearized depths
				GFSDK_SSAO_DRAW_DEBUG_N = (1 << 2),  // Render the internal view normals (for debugging)
				GFSDK_SSAO_DRAW_DEBUG_X = (1 << 3),  // Render the X component as grayscale
				GFSDK_SSAO_DRAW_DEBUG_Y = (1 << 4),  // Render the Y component as grayscale
				GFSDK_SSAO_DRAW_DEBUG_Z = (1 << 5),  // Render the Z component as grayscale
				GFSDK_SSAO_RENDER_AO = GFSDK_SSAO_DRAW_Z | GFSDK_SSAO_DRAW_AO,
				GFSDK_SSAO_RENDER_DEBUG_NORMAL = GFSDK_SSAO_DRAW_Z | GFSDK_SSAO_DRAW_DEBUG_N,
				GFSDK_SSAO_RENDER_DEBUG_NORMAL_X = GFSDK_SSAO_DRAW_Z | GFSDK_SSAO_DRAW_DEBUG_N | GFSDK_SSAO_DRAW_DEBUG_X,
				GFSDK_SSAO_RENDER_DEBUG_NORMAL_Y = GFSDK_SSAO_DRAW_Z | GFSDK_SSAO_DRAW_DEBUG_N | GFSDK_SSAO_DRAW_DEBUG_Y,
				GFSDK_SSAO_RENDER_DEBUG_NORMAL_Z = GFSDK_SSAO_DRAW_Z | GFSDK_SSAO_DRAW_DEBUG_N | GFSDK_SSAO_DRAW_DEBUG_Z,
			};

			public enum class GfsdkHbaoStatus
			{
				GFSDK_SSAO_OK,                                          // Success
				GFSDK_SSAO_VERSION_MISMATCH,                            // The header version number does not match the DLL version number
				GFSDK_SSAO_NULL_ARGUMENT,                               // One of the required argument pointers is NULL
				GFSDK_SSAO_INVALID_PROJECTION_MATRIX,                   // The projection matrix is not valid
				GFSDK_SSAO_INVALID_WORLD_TO_VIEW_MATRIX,                // The world-to-view matrix is not valid (transposing it may help)
				GFSDK_SSAO_INVALID_NORMAL_TEXTURE_RESOLUTION,           // The normal-texture resolution does not match the depth-texture resolution
				GFSDK_SSAO_INVALID_NORMAL_TEXTURE_SAMPLE_COUNT,         // The normal-texture sample count does not match the depth-texture sample count
				GFSDK_SSAO_INVALID_VIEWPORT_DIMENSIONS,                 // One of the viewport dimensions (width or height) is 0
				GFSDK_SSAO_INVALID_VIEWPORT_DEPTH_RANGE,                // The viewport depth range is not a sub-range of [0.f,1.f]
				GFSDK_SSAO_INVALID_SECOND_DEPTH_TEXTURE_RESOLUTION,     // The resolution of the second depth texture does not match the one of the first depth texture
				GFSDK_SSAO_INVALID_SECOND_DEPTH_TEXTURE_SAMPLE_COUNT,   // The sample count of the second depth texture does not match the one of the first depth texture
				GFSDK_SSAO_MEMORY_ALLOCATION_FAILED,                    // Failed to allocate memory on the heap
				GFSDK_SSAO_INVALID_DEPTH_STENCIL_RESOLUTION,            // The depth-stencil resolution does not match the output render-target resolution
				GFSDK_SSAO_INVALID_DEPTH_STENCIL_SAMPLE_COUNT,          // The depth-stencil sample count does not match the output render-target sample count
				GFSDK_SSAO_D3D_FEATURE_LEVEL_NOT_SUPPORTED,             // The current D3D11 feature level is lower than 11_0
				GFSDK_SSAO_D3D_RESOURCE_CREATION_FAILED,                // A resource-creation call has failed (running out of memory?)
				GFSDK_SSAO_D3D12_UNSUPPORTED_DEPTH_CLAMP_MODE,          // CLAMP_TO_BORDER is used (implemented on D3D11 & GL, but not on D3D12)
				GFSDK_SSAO_D3D12_INVALID_HEAP_TYPE,                     // One of the heaps provided to GFSDK_SSAO_CreateContext_D3D12 has an unexpected type
				GFSDK_SSAO_D3D12_INSUFFICIENT_DESCRIPTORS,              // One of the heaps provided to GFSDK_SSAO_CreateContext_D3D12 has an insufficient number of descriptors
				GFSDK_SSAO_D3D12_INVALID_NODE_MASK,                     // NodeMask has more than one bit set. HBAO+ only supports operation on one D3D12 device node.
				GFSDK_SSAO_NO_SECOND_LAYER_PROVIDED                     // FullResDepthTexture2ndLayerSRV is not set, but DualLayerAO is enabled.
			};

			public class GfsdkHbaoUnmanagedData
			{
			public:
				ID3D11Device* device;
				GFSDK_SSAO_InputData_D3D11 input;
				GFSDK_SSAO_CustomHeap customHeap;
				GFSDK_SSAO_Status status;
				GFSDK_SSAO_Context_D3D11* aoContext;
				GFSDK_SSAO_Parameters params;
				GFSDK_SSAO_Output_D3D11 output;
				GFSDK_SSAO_RenderMask renderMask;
			};

			public ref class GfsdkHbaoContext
			{
			public:
				SlimDX::Direct3D11::Device^ Device;
				SlimDX::Direct3D11::DeviceContext^ DeviceContext;
				SlimDX::Direct3D11::RenderTargetView^ RenderTarget;
				SlimDX::Direct3D11::ShaderResourceView^ DepthSrv;
				SlimDX::Direct3D11::ShaderResourceView^ NormalSrv;

				array<float>^ Projection = gcnew array<float>(16);

				float SceneScale;
				float Radius;
				float Bias;
				float PowerExp;
				float SmallScaleAo;
				float LargeScaleAo;
				GfsdkHbaoStepCount StepCount;
				bool ForegroundAo;
				float ForegroundViewDepth;
				bool BackgroundAo;
				float BackgroundViewDepth;
				GfsdkHbaoDepthStorage DepthStorage;
				GfsdkHbaoDepthClampMode DepthClampMode;
				bool DepthThreshold;
				float DepthThresholdMaxViewDepth;
				float DepthThresholdSharpness;
				bool Blur;
				GfsdkHbaoBlurRadius BlurRadius;
				float BlurSharpness;
				bool BlurSharpnessProfile;
				float BlurSharpnessProfileForegroundScale;
				float BlurSharpnessProfileForegroundViewDepth;
				float BlurSharpnessProfileBackgroundViewDepth;
				bool Normal;
				array<float>^ View = gcnew array<float>(16);
				float DecodeBias;
				float DecodeScale;

				GfsdkHbaoContext(SlimDX::Direct3D11::Device^ device);
				~GfsdkHbaoContext();

				void SetDepthSrv();
				void SetDepthParameters();
				void SetNormalsParameters();
				void SetAoParameters();
				void SetRenderMask(GfsdkHbaoRenderMask mask);
				void Render();
				GfsdkHbaoStatus PollStatus();

			private:
				GfsdkHbaoUnmanagedData* Unmanaged;
			};
		}
	}
}