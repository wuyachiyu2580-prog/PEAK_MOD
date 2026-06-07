using System;
using ImGuiNET;
using UnityEngine;
using Zorro.Core;

namespace TerrainCustomiserCN.UI.Windows
{
	public class FreeCamWindow : ImGuiWindowBase
	{
		public override string WindowName
		{
			get
			{
				return "自由相机设置";
			}
		}

		public override ImGuiWindowFlags WindowFlags
		{
			get
			{
				return ImGuiWindowFlags.NoResize;
			}
		}

		public override Vector2 InitialWindowSize
		{
			get
			{
				return new Vector2(275f, 150f);
			}
		}

		private GodCam godCam
		{
			get
			{
				MainCameraMovement instance = Singleton<MainCameraMovement>.Instance;
				return (instance != null) ? instance.godcam : null;
			}
		}

		protected override void DrawContent()
		{
			bool flag = this.godCam == null;
			if (!flag)
			{
				WidgetFactory.FieldWidgetData[] fieldWidgets = new WidgetFactory.FieldWidgetData[]
				{
					new WidgetFactory.FieldWidgetData(typeof(float), () => this.godCam.lookSens, delegate(object v)
					{
						this.godCam.lookSens = (float)v;
					}, "视角灵敏度", null, false, false),
					new WidgetFactory.FieldWidgetData(typeof(float), () => this.godCam.lookDrag, delegate(object v)
					{
						this.godCam.lookDrag = (float)v;
					}, "视角阻尼", null, false, false),
					new WidgetFactory.FieldWidgetData(typeof(float), () => this.godCam.force, delegate(object v)
					{
						this.godCam.force = (float)v;
					}, "移动力度", null, false, false),
					new WidgetFactory.FieldWidgetData(typeof(float), () => this.godCam.drag, delegate(object v)
					{
						this.godCam.drag = (float)v;
					}, "移动阻尼", null, false, false)
				};
				ImGuiHelpers.DrawPropertiesTable("godCamTable", fieldWidgets);
			}
		}
	}
}
