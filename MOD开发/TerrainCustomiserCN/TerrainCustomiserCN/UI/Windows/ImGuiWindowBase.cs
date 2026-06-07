using System;
using ImGuiNET;
using UnityEngine;

namespace TerrainCustomiserCN.UI.Windows
{
	public abstract class ImGuiWindowBase
	{
		public virtual string WindowName
		{
			get
			{
				return "窗口";
			}
		}

		public virtual Vector2 InitialWindowSize
		{
			get
			{
				return Vector2.zero;
			}
		}

		public virtual ImGuiWindowFlags WindowFlags
		{
			get
			{
				return ImGuiWindowFlags.None;
			}
		}

		public virtual void OnCreate()
		{
		}

		public virtual void OnDestroy()
		{
		}

		public void ToggleOpen()
		{
			this.IsOpen = !this.IsOpen;
		}

		public void Close()
		{
			this.IsOpen = false;
		}

		public void Open()
		{
			this.IsOpen = true;
		}

		public void DrawWindow()
		{
			bool flag = !this.IsOpen;
			if (!flag)
			{
				ImGui.SetNextWindowSize(this.InitialWindowSize, ImGuiCond.Once);
				bool flag2 = ImGui.Begin(this.WindowName, this.WindowFlags);
				if (flag2)
				{
					this.DrawContent();
					ImGui.End();
				}
			}
		}

		protected abstract void DrawContent();

		public bool IsOpen = false;
	}
}
