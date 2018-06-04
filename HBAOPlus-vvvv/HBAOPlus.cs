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

using VVVV.HBAOPlus.Bridge;

namespace VVVV.Nodes.DX11.NVHBAOPlus
{
    public class RenderTargetHbaoContextPair
    {
        public GfsdkHbaoContext Hbao;
        public DX11RenderTarget2D RenderTarget;
        public int Width = -1;
        public int Height = -1;
        public int SampleCount = 1;
    }

    [PluginInfo(Name = "HBAO+",
                Category = "DX11",
                Help = "optimized Sceen Space Ambient Occlusion algorithm by NVIDIA",
                Tags = "dx11, post processing",
                Credits = "NVIDIA, NSYNK",
                Author = "dennis, NSYNK")]
    public class NVHBAOPlusNode : IPluginEvaluate, IDX11ResourceHost, IDX11Queryable, IPartImportsSatisfiedNotification, IDisposable
    {

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
        protected IDiffSpread<GfsdkHbaoStepCount> FStepCountIn;

        [Input("Foreground AO", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FForegroundAoIn;
        [Input("Foreground View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FForegroundViewDepthIn;

        [Input("Background AO", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FBackgroundAoIn;
        [Input("Background View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FBackgroundViewDepthIn;

        [Input("Depth Storage", DefaultEnumEntry = "GFSDK_SSAO_FP16_VIEW_DEPTHS", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GfsdkHbaoDepthStorage> FDepthStorageIn;

        [Input("Depth Clamp Mode", DefaultEnumEntry = "GFSDK_SSAO_CLAMP_TO_EDGE", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GfsdkHbaoDepthClampMode> FDepthClampModeIn;

        [Input("Depth Threshold", IsToggle = true, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FDepthThresholdIn;
        [Input("Depth Threshold Max View Depth", DefaultValue = 0f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FDepthThresholdMaxViewDepthIn;
        [Input("Depth Threshold Sharpness", DefaultValue = 100f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FDepthThresholdSharpnessIn;

        [Input("Blur", IsToggle = true, DefaultBoolean = true)]
        protected IDiffSpread<bool> FBlurIn;

        [Input("Blur Radius", DefaultEnumEntry = "GFSDK_SSAO_BLUR_RADIUS_4")]
        protected IDiffSpread<GfsdkHbaoBlurRadius> FBlurRadiusIn;

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

        [Input("Normal Enable")]
        protected IDiffSpread<bool> FNormalEnable;
        [Input("Normal World Proj")]
        protected IDiffSpread<Matrix4x4> FNormalProj;
        [Input("Normal Buffer")]
        protected IDiffSpread<DX11Resource<DX11Texture2D>> FNormalIn;
        [Input("Normal Decode Bias")]
        protected IDiffSpread<float> FNormalDecodeBiasIn;
        [Input("Normal Decode Scale", DefaultValue = 1f)]
        protected IDiffSpread<float> FNormalDecodeScaleIn;
 

        [Input("Enabled", DefaultBoolean = true)]
        protected IDiffSpread<bool> FEnabled;

        [Input("RenderMask", DefaultEnumEntry = "GFSDK_SSAO_RENDER_AO", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GfsdkHbaoRenderMask> FRendermaskIn;

        [Output("Output")]
        protected Pin<DX11Resource<DX11Texture2D>> FOut;

        [Output("Status", IsSingle = true)]
        protected ISpread<String> FStatusOut;

        [Output("Query", IsSingle = true)]
        protected ISpread<IDX11Queryable> FQueryableOut;

        [Import()]
        public ILogger FLogger;

        private Dictionary<DX11RenderContext, RenderTargetHbaoContextPair> HbaoInstances = new Dictionary<DX11RenderContext, RenderTargetHbaoContextPair>();

        private bool init = false;
        private bool reset = false;

        private int w;
        private int h;
        private int aa;
        private int lastW;
        private int lastH;
        private int lastAa;

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

            FQueryableOut[0] = this;

            FStatusOut[0] = "";
            foreach (var rthcp in HbaoInstances.Values)
            {
                FStatusOut[0] += rthcp.Hbao.PollStatus() + Environment.NewLine;
            }
        }

        public void Update(DX11RenderContext context)
        {
            if(!FEnabled[0]) return;

            BeginQuery?.Invoke(context);

            w = FDepthIn[0][context].Width;
            h = FDepthIn[0][context].Height;
            aa = FDepthIn[0][context].Description.SampleDescription.Count;

            RenderTargetHbaoContextPair rthcp;
            var isnew = false;
            if (!HbaoInstances.ContainsKey(context))
            {
                rthcp = new RenderTargetHbaoContextPair
                {
                    //TODO: is the 8 bpc format a limitation of the HBAO+ SDK?
                    RenderTarget = new DX11RenderTarget2D(context, w, h, new SampleDescription(1, 0), Format.R8G8B8A8_UNorm),
                    Hbao = new GfsdkHbaoContext(context.Device),
                    Width = w,
                    Height = h,
                    SampleCount = aa
                };
                rthcp.Hbao.DepthSrv = FDepthIn[0][context].SRV;
                rthcp.Hbao.RenderTarget = rthcp.RenderTarget.RTV;
                rthcp.Hbao.SetDepthSrv();


                FOut[0][context] = rthcp.RenderTarget;

                HbaoInstances.Add(context, rthcp);
                isnew = true;
            }
            else rthcp = HbaoInstances[context];
            var hbao = rthcp.Hbao;

            hbao.DeviceContext = context.CurrentDeviceContext;

            var reschanged = w != rthcp.Width || h != rthcp.Height || aa != rthcp.SampleCount || reset;
            if(FNormalEnable[0])
            {
                hbao.View = Array.ConvertAll(FNormalProj[0].Values, x => (float)x);
                hbao.Normal = true;
                hbao.NormalSrv = FNormalIn[0][context].SRV;
                hbao.DecodeBias = FNormalDecodeBiasIn[0];
                hbao.DecodeScale = FNormalDecodeScaleIn[0];
                hbao.SetNormalsParameters();
            }
            else
            {
                hbao.Normal = false;
                hbao.SetNormalsParameters();
            }
            if (FProjIn.IsChanged || FSceneScaleIn.IsChanged || reschanged || isnew)
            {

                hbao.Projection = Array.ConvertAll(FProjIn[0].Values, x => (float)x);
                hbao.SceneScale = FSceneScaleIn[0];

                if (FNormalEnable[0])
                {

                    FLogger.Log(LogType.Debug, "ayy we normal");
                }

                hbao.SetDepthParameters();
            }

            if (FRadiusIn.IsChanged || FBiasIn.IsChanged || FPowerExpIn.IsChanged ||
                FSmallScaleAoIn.IsChanged || FLargeScaleAoIn.IsChanged || FStepCountIn.IsChanged ||
                FForegroundAoIn.IsChanged || FForegroundViewDepthIn.IsChanged ||
                FBackgroundAoIn.IsChanged || FBackgroundViewDepthIn.IsChanged ||
                FDepthStorageIn.IsChanged || FDepthClampModeIn.IsChanged || FDepthThresholdIn.IsChanged ||
                FDepthThresholdMaxViewDepthIn.IsChanged || FDepthThresholdSharpnessIn.IsChanged ||
                FBlurIn.IsChanged || FBlurRadiusIn.IsChanged || FBlurSharpnessIn.IsChanged ||
                FBlurSharpnessProfileIn.IsChanged ||
                FBlurSharpnessProfileForegroundScaleIn.IsChanged ||
                FBlurSharpnessProfileForegroundViewDepthIn.IsChanged ||
                FBlurSharpnessProfileBackgroundViewDepthIn.IsChanged ||
                reschanged || isnew
            )
            {
                hbao.Radius = FRadiusIn[0];
                hbao.Bias = FBiasIn[0];
                hbao.PowerExp = FPowerExpIn[0];
                hbao.SmallScaleAo = FSmallScaleAoIn[0];
                hbao.LargeScaleAo = FLargeScaleAoIn[0];
                hbao.StepCount = FStepCountIn[0];
                hbao.ForegroundAo = FForegroundAoIn[0];
                hbao.ForegroundViewDepth = FForegroundViewDepthIn[0];
                hbao.BackgroundAo = FBackgroundAoIn[0];
                hbao.BackgroundViewDepth = FBackgroundViewDepthIn[0];
                hbao.DepthStorage = FDepthStorageIn[0];
                hbao.DepthClampMode = FDepthClampModeIn[0];
                hbao.DepthThreshold = FDepthThresholdIn[0];
                hbao.DepthThresholdMaxViewDepth = FDepthThresholdMaxViewDepthIn[0];
                hbao.DepthThresholdSharpness = FDepthThresholdSharpnessIn[0];
                hbao.Blur = FBlurIn[0];
                hbao.BlurRadius = FBlurRadiusIn[0];
                hbao.BlurSharpness = FBlurSharpnessIn[0];
                hbao.BlurSharpnessProfile = FBlurSharpnessProfileIn[0];
                hbao.BlurSharpnessProfileForegroundScale = FBlurSharpnessProfileForegroundScaleIn[0];
                hbao.BlurSharpnessProfileForegroundViewDepth = FBlurSharpnessProfileForegroundViewDepthIn[0];
                hbao.BlurSharpnessProfileBackgroundViewDepth = FBlurSharpnessProfileBackgroundViewDepthIn[0];

                hbao.SetAoParameters();
            }

            if (FRendermaskIn.IsChanged || reschanged || isnew)
                hbao.SetRenderMask(FRendermaskIn[0]);

            if (reschanged)
            {
                rthcp.Width = w;
                rthcp.Height = h;
                rthcp.SampleCount = aa;

                rthcp.RenderTarget.Dispose();
                rthcp.RenderTarget = new DX11RenderTarget2D(context, w, h, new SampleDescription(1, 0), Format.R8G8B8A8_UNorm);

                FOut[0][context] = rthcp.RenderTarget;

                hbao.RenderTarget = rthcp.RenderTarget.RTV;

                // make sure it doesn't try to run on the old depth buffer
                rthcp.Hbao.DepthSrv = FDepthIn[0][context].SRV;
                hbao.SetDepthSrv();

                reset = false;
            }


            hbao.Render();

            EndQuery?.Invoke(context);
        }

        public void Destroy(DX11RenderContext context, bool force)
        {
            if (HbaoInstances.ContainsKey(context))
            {
                var rthcp = HbaoInstances[context];
                rthcp.RenderTarget.Dispose();
                rthcp.Hbao.Dispose();
                HbaoInstances.Remove(context);
            }
            FOut[0][context].Dispose();
        }

        private void OnConnected(object sender, PinConnectionEventArgs args)
        {
            reset = true;
        }

        ~NVHBAOPlusNode()
        {
            Dispose(false);
        }

        public event DX11QueryableDelegate BeginQuery;
        public event DX11QueryableDelegate EndQuery;
        
        private void Dispose(bool disposing)
        {
            foreach (var rthcp in HbaoInstances.Values)
            {
                rthcp.Hbao.Dispose();
                rthcp.RenderTarget.Dispose();
            }
            HbaoInstances.Clear();
            FOut[0]?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}