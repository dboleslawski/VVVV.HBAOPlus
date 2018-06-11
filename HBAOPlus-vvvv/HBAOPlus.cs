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
        public int SampleCount = -1;
    }

    [PluginInfo(Name = "HBAO+",
                Category = "DX11",
                Help = "optimized Sceen Space Ambient Occlusion algorithm by NVIDIA",
                Tags = "dx11, post processing",
                Credits = "NVIDIA, NSYNK",
                Author = "dennis, NSYNK")]
    public class NVHBAOPlusNode : IPluginEvaluate, IDX11ResourceHost, IDX11Queryable, IDisposable
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

        [Input("Use Normal Buffer", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<bool> FNormal;
        [Input("View", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<Matrix4x4> FView;
        [Input("Normal Buffer", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<DX11Resource<DX11Texture2D>> FNormalIn;
        [Input("Normal Decode Bias", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FNormalDecodeBiasIn;
        [Input("Normal Decode Scale", DefaultValue = 1f, Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<float> FNormalDecodeScaleIn;

        [Input("RenderMask", DefaultEnumEntry = "GFSDK_SSAO_RENDER_AO", Visibility = PinVisibility.OnlyInspector)]
        protected IDiffSpread<GfsdkHbaoRenderMask> FRendermaskIn;

        [Input("Enabled", DefaultBoolean = true)]
        protected IDiffSpread<bool> FEnabled;


        [Output("Output")]
        protected Pin<DX11Resource<DX11Texture2D>> FOut;

        [Output("Status", IsSingle = true)]
        protected ISpread<String> FStatusOut;

        [Output("Query", IsSingle = true)]
        protected ISpread<IDX11Queryable> FQueryableOut;

        private List<Dictionary<DX11RenderContext, RenderTargetHbaoContextPair>> HbaoInstances = new List<Dictionary<DX11RenderContext, RenderTargetHbaoContextPair>>();

        public void Evaluate(int SpreadMax)
        {
            if (FDepthIn.SliceCount == 0 || !FOut.IsConnected)
                return; // no need to run

            for (int i = 0; i < FDepthIn.SliceCount; i++)
            {
                if (FOut[i] == null)
                    FOut[i] = new DX11Resource<DX11Texture2D>();

                FStatusOut[i] = "";
                foreach (var rthcp in HbaoInstances[i].Values)
                    FStatusOut[i] += rthcp.Hbao.PollStatus() + Environment.NewLine;
            }

            FQueryableOut[0] = this;
        }

        public void Update(DX11RenderContext context)
        {
            if(!FEnabled[0]) return;

            BeginQuery?.Invoke(context);

            if (HbaoInstances.Count != FDepthIn.SliceCount)
            {
                // TODO: dispose stuff here too? there must be a better way
                HbaoInstances.Clear();
                for (int i = 0; i < FDepthIn.SliceCount; i++)
                    HbaoInstances.Add(new Dictionary<DX11RenderContext, RenderTargetHbaoContextPair>());
            }

            for (int i = 0; i < FDepthIn.SliceCount; i++)
            {
                RenderTargetHbaoContextPair rthcp;
                GfsdkHbaoContext hbao;

                bool isNew = false;

                int w = FDepthIn[0][context].Width;
                int h = FDepthIn[0][context].Height;
                int aa = FDepthIn[0][context].Description.SampleDescription.Count;

                if (HbaoInstances.Count < i+1)
                    HbaoInstances.Add(new Dictionary<DX11RenderContext, RenderTargetHbaoContextPair>());

                if (!HbaoInstances[i].ContainsKey(context))
                {
                    rthcp = new RenderTargetHbaoContextPair
                    {
                        // TODO: is the 8 bpc format a limitation of the HBAO+ SDK?
                        // TODO: changing format of the input texture seems to kill hbao+
                        RenderTarget = new DX11RenderTarget2D(context, w, h, new SampleDescription(1, 0), Format.R8G8B8A8_UNorm),
                        Hbao = new GfsdkHbaoContext(context.Device),
                        Width = w,
                        Height = h,
                        SampleCount = aa
                    };
                    rthcp.Hbao.DepthSrv = FDepthIn[i][context].SRV;
                    rthcp.Hbao.RenderTarget = rthcp.RenderTarget.RTV;
                    rthcp.Hbao.SetDepthSrv();

                    FOut[i][context] = rthcp.RenderTarget;

                    HbaoInstances[i].Add(context, rthcp);

                    isNew = true;
                }
                else rthcp = HbaoInstances[i][context];

                hbao = rthcp.Hbao;
                hbao.DeviceContext = context.CurrentDeviceContext;

                bool resChanged = w != rthcp.Width || h != rthcp.Height || aa != rthcp.SampleCount; // TODO: do we need reset here?

                // res change
                if (resChanged)
                {
                    rthcp.Width = w;
                    rthcp.Height = h;
                    rthcp.SampleCount = aa;

                    rthcp.RenderTarget.Dispose();
                    rthcp.RenderTarget = new DX11RenderTarget2D(context, w, h, new SampleDescription(1, 0), Format.R8G8B8A8_UNorm);

                    FOut[i][context] = rthcp.RenderTarget;
                    hbao.RenderTarget = rthcp.RenderTarget.RTV;

                    // make sure it doesn't try to run on the old depth buffer
                    rthcp.Hbao.DepthSrv = FDepthIn[i][context].SRV;
                    hbao.SetDepthSrv();

                    if (FNormalIn[i] != null)
                    {
                        hbao.NormalSrv = FNormalIn[i][context].SRV;
                        hbao.SetNormalSrv();
                    }
                }

                // depth parameters
                if (FProjIn.IsChanged || FSceneScaleIn.IsChanged || resChanged || isNew)
                {
                    hbao.Projection = Array.ConvertAll(FProjIn[i].Values, x => (float)x);
                    hbao.SceneScale = FSceneScaleIn[i];

                    hbao.SetDepthParameters();
                }

                // ao parameters
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
                resChanged || isNew)
                {
                    hbao.Radius = FRadiusIn[i];
                    hbao.Bias = FBiasIn[i];
                    hbao.PowerExp = FPowerExpIn[i];
                    hbao.SmallScaleAo = FSmallScaleAoIn[i];
                    hbao.LargeScaleAo = FLargeScaleAoIn[i];
                    hbao.StepCount = FStepCountIn[i];
                    hbao.ForegroundAo = FForegroundAoIn[i];
                    hbao.ForegroundViewDepth = FForegroundViewDepthIn[i];
                    hbao.BackgroundAo = FBackgroundAoIn[i];
                    hbao.BackgroundViewDepth = FBackgroundViewDepthIn[i];
                    hbao.DepthStorage = FDepthStorageIn[i];
                    hbao.DepthClampMode = FDepthClampModeIn[i];
                    hbao.DepthThreshold = FDepthThresholdIn[i];
                    hbao.DepthThresholdMaxViewDepth = FDepthThresholdMaxViewDepthIn[i];
                    hbao.DepthThresholdSharpness = FDepthThresholdSharpnessIn[i];
                    hbao.Blur = FBlurIn[i];
                    hbao.BlurRadius = FBlurRadiusIn[i];
                    hbao.BlurSharpness = FBlurSharpnessIn[i];
                    hbao.BlurSharpnessProfile = FBlurSharpnessProfileIn[i];
                    hbao.BlurSharpnessProfileForegroundScale = FBlurSharpnessProfileForegroundScaleIn[i];
                    hbao.BlurSharpnessProfileForegroundViewDepth = FBlurSharpnessProfileForegroundViewDepthIn[i];
                    hbao.BlurSharpnessProfileBackgroundViewDepth = FBlurSharpnessProfileBackgroundViewDepthIn[i];

                    hbao.SetAoParameters();
                }

                // normal srv
                if (FNormalIn[i] != null)
                {
                    hbao.NormalSrv = FNormalIn[i][context].SRV;
                    hbao.SetNormalSrv();
                }

                // normal parameters
                if (FNormal.IsChanged || FNormalDecodeBiasIn.IsChanged || FNormalDecodeScaleIn.IsChanged || isNew)
                {
                    hbao.Normal = FNormal[i];
                    hbao.View = Array.ConvertAll(FView[i].Values, x => (float)x);
                    hbao.DecodeBias = FNormalDecodeBiasIn[i];
                    hbao.DecodeScale = FNormalDecodeScaleIn[i];
                    hbao.SetNormalParameters();
                }

                // rendermask
                if (FRendermaskIn.IsChanged || isNew)
                    hbao.SetRenderMask(FRendermaskIn[0]);

                hbao.Render();
            }

            EndQuery?.Invoke(context);
        }

        public void Destroy(DX11RenderContext context, bool force)
        {
            for (int i = 0; i < FOut.SliceCount; i++)
            {
                if (HbaoInstances[i].ContainsKey(context))
                {
                    var rthcp = HbaoInstances[i][context];
                    rthcp.RenderTarget.Dispose();
                    rthcp.Hbao.Dispose();
                    HbaoInstances[i].Remove(context);
                }
                HbaoInstances.Clear();
                FOut[i][context].Dispose();
            }
        }

        ~NVHBAOPlusNode()
        {
            Dispose(false);
        }

        public event DX11QueryableDelegate BeginQuery;
        public event DX11QueryableDelegate EndQuery;
        
        private void Dispose(bool disposing)
        {
            for (int i = 0; i < FOut.SliceCount; i++)
            {
                foreach (var rthcp in HbaoInstances[i].Values)
                {
                    rthcp.Hbao.Dispose();
                    rthcp.RenderTarget.Dispose();
                }
                HbaoInstances[i].Clear();
                FOut[i]?.Dispose();
            }
            //HbaoInstances.Flush();
            FOut.SafeDisposeAll();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}