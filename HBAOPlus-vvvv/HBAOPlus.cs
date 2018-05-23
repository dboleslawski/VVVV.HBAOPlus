using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;

using VVVV.DX11;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using FeralTic.DX11.Queries;

using SlimDX.DXGI;

namespace VVVV.Nodes.DX11.NVHBAOPlus
{
    [PluginInfo(Name = "HBAO+",
                Category = "DX11",
                Help = "optimized Sceen Space Ambient Occlusion algorithm by NVIDIA",
                Tags = "dx11, post processing",
                Credits = "NVIDIA, NSYNK",
                Author = "dennis, NSYNK")]
    public class NVHBAOPlusNode : IPluginEvaluate, IDX11ResourceHost, IDX11Queryable, IPartImportsSatisfiedNotification
    {
        [DllImport("HBAOPlus-bridge-vvvv.dll")]
        private static extern void SetRenderMask(GFSDK_SSAO_RenderMask mask);

        [DllImport("HBAOPlus-bridge-vvvv.dll")]
        private static extern void RenderHBAO(IntPtr context, IntPtr rtv);

        [DllImport("HBAOPlus-bridge-vvvv.dll")]
        private static extern GFSDK_SSAO_Status PollStatus();

        [Input("Depth Buffer")]
        protected IDiffSpread<DX11Resource<DX11DepthStencil>> FDepthIn;

        [Input("Projection")]
        protected IDiffSpread<Matrix4x4> FProjIn;

        [Input("Scene Scale", DefaultValue = 1f)]
        protected IDiffSpread<float> FSceneScaleIn;

        [Input("Radius", DefaultValue = 1f)]
        protected IDiffSpread<float> FRadiusIn;

        [Input("Bias", DefaultValue = .1f, MinValue = 0f, MaxValue = 0.5f)]
        protected IDiffSpread<float> FBiasIn;

        [Input("Power Exponent", DefaultValue = 2f, MinValue = 1f, MaxValue = 4f)]
        protected IDiffSpread<float> FPowerExpIn;

        [Input("Small Scale AO", DefaultValue = 1f, MinValue = 0f, MaxValue = 2f)]
        protected IDiffSpread<float> FSmallScaleAoIn;

        [Input("Large Scale AO", DefaultValue = 1f, MinValue = 0f, MaxValue = 2f)]
        protected IDiffSpread<float> FLargeScaleAoIn;

        [Input("Step Count", DefaultEnumEntry = "GFSDK_SSAO_BLUR_RADIUS_4", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GFSDK_SSAO_StepCount> FStepCountIn;

        [Input("Foreground AO", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FForegroundAoIn;
        [Input("Foreground View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FForegroundViewDepthIn;

        [Input("Background AO", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FBackgroundAoIn;
        [Input("Background View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FBackgroundViewDepthIn;

        [Input("Depth Storage", DefaultEnumEntry = "GFSDK_SSAO_FP16_VIEW_DEPTHS", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GFSDK_SSAO_DepthStorage> FDepthStorageIn;

        [Input("Depth Clamp Mode", DefaultEnumEntry = "GFSDK_SSAO_CLAMP_TO_EDGE", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GFSDK_SSAO_DepthClampMode> FDepthClampModeIn;

        [Input("Depth Threshold", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FDepthThresholdIn;
        [Input("Depth Threshold Max View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FDepthThresholdMaxViewDepthIn;
        [Input("Depth Threshold Sharpness", DefaultValue = 100f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FDepthThresholdSharpnessIn;

        [Input("Blur", IsToggle = true, DefaultBoolean = true)]
        protected IDiffSpread<bool> FBlurIn;

        [Input("Blur Radius", DefaultEnumEntry = "GFSDK_SSAO_BLUR_RADIUS_4")]
        protected IDiffSpread<GFSDK_SSAO_BlurRadius> FBlurRadiusIn;

        [Input("Blur Sharpness", DefaultValue = 16f, MinValue = 0f, MaxValue = 16f)]
        protected IDiffSpread<float> FBlurSharpnessIn;

        [Input("Blur Sharpness Profile", IsToggle = true, DefaultBoolean = false, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FBlurSharpnessProfileIn;
        [Input("Blur Sharpness Profile Foreground Scale", DefaultValue = 4f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FBlurSharpnessProfileForegroundScaleIn;
        [Input("Blur Sharpness Profile Foreground View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FBlurSharpnessProfileForegroundViewDepthIn;
        [Input("Blur Sharpness Profile Background View Depth", DefaultValue = 1f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FBlurSharpnessProfileBackgroundViewDepthIn;
        // f those field names

        [Input("RenderMask", DefaultEnumEntry = "GFSDK_SSAO_RENDER_AO", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GFSDK_SSAO_RenderMask> FRendermaskIn;

        [Output("Output")]
        protected Pin<DX11Resource<DX11Texture2D>> FOut;

        [Output("Status", IsSingle = true)]
        protected ISpread<String> FStatusOut;

        [Output("Query", IsSingle = true)]
        protected ISpread<IDX11Queryable> FQueryableOut;

        [Import()]
        public ILogger FLogger;

        private bool init = false;
        private bool reset = false;

        private int w;
        private int h;
        private int aa;
        private int lastW;
        private int lastH;
        private int lastAa;

        private DX11RenderTarget2D rt;

        public void OnImportsSatisfied()
        {
            FOut.Connected += OnConnected;
        }

        public void Evaluate(int SpreadMax)
        {
            if (FDepthIn.SliceCount == 0 || !FOut.IsConnected)
                return; // no need to run

            if (FOut[0] == null)
               FOut[0] = new DX11Resource<DX11Texture2D>();

            if (FQueryableOut == null)
                FQueryableOut[0] = this;

            if (FRendermaskIn.IsChanged)
                SetRenderMask(FRendermaskIn[0]);

            FStatusOut[0] = PollStatus().ToString();
        }

        public void Update(DX11RenderContext context)
        {
            if (!init)
            {
                PluginEntry.InitHBAO(context.Device.ComPointer);
                PluginEntry.SetDepthParameters(FDepthIn[0][context].SRV.ComPointer, Array.ConvertAll(FProjIn[0].Values, x => (float)x), FSceneScaleIn[0]);
                PluginEntry.SetAoParameters(FRadiusIn[0], FBiasIn[0], FPowerExpIn[0], FSmallScaleAoIn[0], FLargeScaleAoIn[0], FStepCountIn[0], FForegroundAoIn[0], FForegroundViewDepthIn[0], FBackgroundAoIn[0], FBackgroundViewDepthIn[0], FDepthStorageIn[0], FDepthClampModeIn[0], FDepthThresholdIn[0], FDepthThresholdMaxViewDepthIn[0], FDepthThresholdSharpnessIn[0], FBlurIn[0], FBlurRadiusIn[0], FBlurSharpnessIn[0], FBlurSharpnessProfileIn[0], FBlurSharpnessProfileForegroundScaleIn[0], FBlurSharpnessProfileForegroundViewDepthIn[0], FBlurSharpnessProfileBackgroundViewDepthIn[0]);

                init = true;
            }

            w = FDepthIn[0][context].Width;
            h = FDepthIn[0][context].Height;
            aa = FDepthIn[0][context].Description.SampleDescription.Count;

            if (w != lastW || h != lastH || aa != lastAa || reset)
            {
                lastW = w;
                lastH = h;
                lastAa = aa;

                rt?.Dispose();
      
                rt = new DX11RenderTarget2D(context, w, h, new SampleDescription(1, 0), Format.R8G8B8A8_UNorm);

                FOut[0][context] = rt;

                // make sure it doesn't try to run on the old depth buffer
                PluginEntry.SetDepthParameters(FDepthIn[0][context].SRV.ComPointer, Array.ConvertAll(FProjIn[0].Values, x => (float)x), FSceneScaleIn[0]);

                reset = false;
            }

            if (FProjIn.IsChanged || FSceneScaleIn.IsChanged)
                PluginEntry.SetDepthParameters(FDepthIn[0][context].SRV.ComPointer, Array.ConvertAll(FProjIn[0].Values, x => (float)x), FSceneScaleIn[0]);

            // uff
            if (FRadiusIn.IsChanged || FBiasIn.IsChanged || FPowerExpIn.IsChanged || FSmallScaleAoIn.IsChanged || FLargeScaleAoIn.IsChanged || FStepCountIn.IsChanged || FForegroundAoIn.IsChanged || FForegroundViewDepthIn.IsChanged || FBackgroundAoIn.IsChanged || FBackgroundViewDepthIn.IsChanged || FDepthStorageIn.IsChanged || FDepthClampModeIn.IsChanged || FDepthThresholdIn.IsChanged || FDepthThresholdMaxViewDepthIn.IsChanged || FDepthThresholdSharpnessIn.IsChanged || FBlurIn.IsChanged || FBlurRadiusIn.IsChanged || FBlurSharpnessIn.IsChanged || FBlurSharpnessProfileIn.IsChanged || FBlurSharpnessProfileForegroundScaleIn.IsChanged || FBlurSharpnessProfileForegroundViewDepthIn.IsChanged || FBlurSharpnessProfileBackgroundViewDepthIn.IsChanged)
                PluginEntry.SetAoParameters(FRadiusIn[0], FBiasIn[0], FPowerExpIn[0], FSmallScaleAoIn[0], FLargeScaleAoIn[0], FStepCountIn[0], FForegroundAoIn[0], FForegroundViewDepthIn[0], FBackgroundAoIn[0], FBackgroundViewDepthIn[0], FDepthStorageIn[0], FDepthClampModeIn[0], FDepthThresholdIn[0], FDepthThresholdMaxViewDepthIn[0], FDepthThresholdSharpnessIn[0], FBlurIn[0], FBlurRadiusIn[0], FBlurSharpnessIn[0], FBlurSharpnessProfileIn[0], FBlurSharpnessProfileForegroundScaleIn[0], FBlurSharpnessProfileForegroundViewDepthIn[0], FBlurSharpnessProfileBackgroundViewDepthIn[0]);

            BeginQuery?.Invoke(context);

            RenderHBAO(context.CurrentDeviceContext.ComPointer, rt.RTV.ComPointer);

            EndQuery?.Invoke(context);
        }

        public void Destroy(DX11RenderContext context, bool force)
        {
            rt.Dispose();
            FOut[0][context].Dispose();
        }

        private void OnConnected(object sender, PinConnectionEventArgs args)
        {
            reset = true;
        }

        public event DX11QueryableDelegate BeginQuery;
        public event DX11QueryableDelegate EndQuery;
    }
}