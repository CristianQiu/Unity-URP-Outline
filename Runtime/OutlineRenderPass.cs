using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The outline render pass.
/// </summary>
public sealed class OutlineRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// The subpasses the outline render pass is made of.
	/// </summary>
	private enum PassStage : byte
	{
		RenderObjectsMask,
		ResolveOutline,
	}

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public PassStage stage;

		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public RendererListHandle rendererListHandleR;
		public RendererListHandle rendererListHandleG;
		public RendererListHandle rendererListHandleB;
		public RendererListHandle rendererListHandleA;

		public TextureHandle renderedObjects;
	}

	#endregion

	#region Private Attributes

	private const float DefaultRenderScale = 1.0f;
	private const float BorderSizeScalingReferenceHeight = 1440.0f;

	private static readonly ShaderTagId[] ShaderTagIds = { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };

	private static readonly uint RenderingLayerMaskR = RenderingLayerMask.GetMask("Outline_1");
	private static readonly uint RenderingLayerMaskG = RenderingLayerMask.GetMask("Outline_2");
	private static readonly uint RenderingLayerMaskB = RenderingLayerMask.GetMask("Outline_3");
	private static readonly uint RenderingLayerMaskA = RenderingLayerMask.GetMask("Outline_4");

	private static readonly Color[] Colors = { Color.red, Color.green, Color.blue, Color.black };
	private static readonly int BorderSizeId = Shader.PropertyToID("_BorderSize");
	private static readonly int ColorsId = Shader.PropertyToID("_Colors");
	private static readonly int FillAlphasId = Shader.PropertyToID("_FillAlphas");

	private static readonly int TextureId = Shader.PropertyToID("_OutlineMaskTexture");

	private Material material;

	#endregion

	#region Initialization Methods

	public OutlineRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler("Outline");
		renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
		requiresIntermediateTexture = false;

		this.material = material;
	}

	#endregion

	#region Scriptable Render Pass Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="frameData"></param>
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		CreateRenderGraphTextures(renderGraph, resourceData, out TextureHandle renderedObjects, out TextureHandle resolveTarget);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Draw Outline Mask", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.RenderObjectsMask;
			passData.material = material;
			passData.rendererListHandleR = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskR, ColorWriteMask.Red);
			passData.rendererListHandleG = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskG, ColorWriteMask.Green);
			passData.rendererListHandleB = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskB, ColorWriteMask.Blue);
			passData.rendererListHandleA = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskA, ColorWriteMask.Alpha);
			passData.renderedObjects = renderedObjects;

			builder.SetRenderAttachment(renderedObjects, 0);
			builder.UseRendererList(passData.rendererListHandleR);
			builder.UseRendererList(passData.rendererListHandleG);
			builder.UseRendererList(passData.rendererListHandleB);
			builder.UseRendererList(passData.rendererListHandleA);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Resolve", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.ResolveOutline;
			passData.source = resourceData.cameraColor;
			passData.material = material;
			passData.materialPassIndex = 1;
			passData.renderedObjects = renderedObjects;

			builder.SetRenderAttachment(resolveTarget, 0);
			builder.UseTexture(resourceData.cameraColor);
			builder.UseTexture(renderedObjects);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		resourceData.cameraColor = resolveTarget;
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="renderedObjects"></param>
	/// <param name="resolveTarget"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle renderedObjects, out TextureHandle resolveTarget)
	{
		resolveTarget = renderGraph.CreateTexture(resourceData.cameraColor, "_OutlineResolve");

		TextureDesc cameraColorDescriptor = renderGraph.GetTextureDesc(resourceData.cameraColor);
		cameraColorDescriptor.name = "_OutlineMask";
		cameraColorDescriptor.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGB32, false);
		cameraColorDescriptor.clearBuffer = true;
		cameraColorDescriptor.clearColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
		renderedObjects = renderGraph.CreateTexture(cameraColorDescriptor);
	}

	/// <summary>
	/// Creates and returns the renderer list handle used to render the objects in the given
	/// rendering layer mask with the wanted writing color mask.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="cameraData"></param>
	/// <param name="renderingLayerMask"></param>
	/// <param name="colorMask"></param>
	/// <returns></returns>
	private RendererListHandle CreateRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData, uint renderingLayerMask, ColorWriteMask colorMask)
	{
		RendererListDesc rendererListDesc = new RendererListDesc(ShaderTagIds, renderingData.cullResults, cameraData.camera);

		rendererListDesc.layerMask = ~0;
		rendererListDesc.renderingLayerMask = renderingLayerMask;
		rendererListDesc.overrideShader = null;
		rendererListDesc.overrideShaderPassIndex = -1;
		rendererListDesc.overrideMaterial = material;
		rendererListDesc.overrideMaterialPassIndex = 0;
		rendererListDesc.renderQueueRange = RenderQueueRange.all;
		rendererListDesc.sortingCriteria = SortingCriteria.None;
		rendererListDesc.rendererConfiguration = PerObjectData.None;
		rendererListDesc.excludeObjectMotionVectors = false;
		rendererListDesc.stateBlock = GetRenderStateBlockToRenderWithColorMask(colorMask);

		return renderGraph.CreateRendererList(rendererListDesc);
	}

	/// <summary>
	/// Gets the render state block filled with the blend state information to write with the given
	/// color mask.
	/// </summary>
	/// <param name="colorMask"></param>
	/// <returns></returns>
	private RenderStateBlock GetRenderStateBlockToRenderWithColorMask(ColorWriteMask colorMask)
	{
		bool shouldOverrideBlendState = (colorMask & ColorWriteMask.Red) != ColorWriteMask.Red;
		RenderStateMask stateMask = shouldOverrideBlendState ? RenderStateMask.Blend : RenderStateMask.Nothing;
		RenderStateBlock stateBlock = new RenderStateBlock(stateMask);

		if (shouldOverrideBlendState)
		{
			BlendState blendState = new BlendState();
			blendState.blendState0 = new RenderTargetBlendState(colorMask, destinationColorBlendMode: BlendMode.One, destinationAlphaBlendMode: BlendMode.One);
			stateBlock.blendState = blendState;
		}

		return stateBlock;
	}

	/// <summary>
	/// Scales the border to try to keep the same size at different resolutions and/or render scales.
	/// </summary>
	/// <param name="borderSize"></param>
	/// <returns></returns>
	private static float ScaleBorderSizeAccordingToRenderedPixels(float borderSize)
	{
		float renderScale = (UniversalRenderPipeline.asset != null) ? UniversalRenderPipeline.asset.renderScale : DefaultRenderScale;
		float renderHeight = Screen.height * renderScale;

		float borderSizePerHeightUnit = borderSize / BorderSizeScalingReferenceHeight;
		borderSize = borderSizePerHeightUnit * renderHeight;

		borderSize = Mathf.Max(borderSize, 0.0f);
		borderSize = (float)Mathf.RoundToInt(borderSize);

		return borderSize;
	}

	/// <summary>
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	/// <param name="passData"></param>
	private static void UpdateMaterialParameters(PassData passData)
	{
		Material material = passData.material;

		if (passData.stage == PassStage.RenderObjectsMask)
		{
			OutlineVolumeComponent volume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();

			Colors[0] = volume.color1.value;
			Colors[1] = volume.color2.value;
			Colors[2] = volume.color3.value;
			Colors[3] = volume.color4.value;

			float fillAlpha1 = volume.fillAlpha1.value;
			float fillAlpha2 = volume.fillAlpha2.value;
			float fillAlpha3 = volume.fillAlpha3.value;
			float fillAlpha4 = volume.fillAlpha4.value;

			material.SetFloat(BorderSizeId, ScaleBorderSizeAccordingToRenderedPixels(volume.borderSize.value));
			material.SetColorArray(ColorsId, Colors);
			material.SetVector(FillAlphasId, new Vector4(fillAlpha1, fillAlpha2, fillAlpha3, fillAlpha4));
		}
		else
		{
			material.SetTexture(TextureId, passData.renderedObjects);
		}
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		UpdateMaterialParameters(passData);

		if (passData.stage == PassStage.RenderObjectsMask)
		{
			context.cmd.DrawRendererList(passData.rendererListHandleR);
			context.cmd.DrawRendererList(passData.rendererListHandleG);
			context.cmd.DrawRendererList(passData.rendererListHandleB);
			context.cmd.DrawRendererList(passData.rendererListHandleA);
		}
		else
		{
			Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
		}
	}

	#endregion
}