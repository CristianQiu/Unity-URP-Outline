using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The outline draw mask render pass.
/// </summary>
public sealed class OutlineDrawMaskRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public RendererListHandle rendererListHandleR;
		public RendererListHandle rendererListHandleG;
		public RendererListHandle rendererListHandleB;
		public RendererListHandle rendererListHandleA;
	}

	#endregion

	#region Private Attributes

	private static readonly ShaderTagId[] ShaderTagIds = { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };

	private static readonly uint RenderingLayerMaskR = RenderingLayerMask.GetMask("Outline_1");
	private static readonly uint RenderingLayerMaskG = RenderingLayerMask.GetMask("Outline_2");
	private static readonly uint RenderingLayerMaskB = RenderingLayerMask.GetMask("Outline_3");
	private static readonly uint RenderingLayerMaskA = RenderingLayerMask.GetMask("Outline_4");

	private Material material;

	#endregion

	#region Initialization Methods

	public OutlineDrawMaskRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler("Outline");
		renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
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
		OutlineData outlineData = frameData.GetOrCreate<OutlineData>();

		CreateRenderGraphTextures(renderGraph, resourceData, out TextureHandle renderedObjects);
		outlineData.RenderedObjects = renderedObjects;

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Draw Mask", out PassData passData, profilingSampler))
		{
			passData.rendererListHandleR = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskR, ColorWriteMask.Red);
			passData.rendererListHandleG = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskG, ColorWriteMask.Green);
			passData.rendererListHandleB = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskB, ColorWriteMask.Blue);
			passData.rendererListHandleA = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskA, ColorWriteMask.Alpha);

			builder.SetRenderAttachment(renderedObjects, 0, AccessFlags.Write);
			builder.UseRendererList(passData.rendererListHandleR);
			builder.UseRendererList(passData.rendererListHandleG);
			builder.UseRendererList(passData.rendererListHandleB);
			builder.UseRendererList(passData.rendererListHandleA);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="renderedObjects"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle renderedObjects)
	{
		TextureDesc cameraColorDescriptor = renderGraph.GetTextureDesc(resourceData.cameraColor);
		cameraColorDescriptor.name = "_OutlineMask";
		cameraColorDescriptor.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGB32, false);
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
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		context.cmd.DrawRendererList(passData.rendererListHandleR);
		context.cmd.DrawRendererList(passData.rendererListHandleG);
		context.cmd.DrawRendererList(passData.rendererListHandleB);
		context.cmd.DrawRendererList(passData.rendererListHandleA);
	}

	#endregion
}