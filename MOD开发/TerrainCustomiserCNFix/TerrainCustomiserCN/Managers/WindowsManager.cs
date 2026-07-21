using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ImGuiNET;
using TerrainCustomiserCN.UI.Windows;
using UnityEngine;

namespace TerrainCustomiserCN.Managers
{
	public class WindowsManager : MonoBehaviour
	{
		internal static void Setup()
		{
			bool flag = WindowsManager._instance != null;
			if (!flag)
			{
				WindowsManager._instance = new GameObject("TCCN_WindowsManager")
				{
					hideFlags = (HideFlags)61
				}.AddComponent<WindowsManager>();
			}
		}

		public static T Get<T>() where T : ImGuiWindowBase
		{
			return (T)((object)WindowsManager.windows.Find((ImGuiWindowBase w) => w is T));
		}

		public static T Register<T>() where T : ImGuiWindowBase, new()
		{
			T t = Activator.CreateInstance<T>();
			bool flag = !WindowsManager.windows.Contains(t);
			if (flag)
			{
				WindowsManager.windows.Add(t);
				t.OnCreate();
			}
			return t;
		}

		public static void Unregister<T>() where T : ImGuiWindowBase
		{
			T t = (T)((object)WindowsManager.windows.Find((ImGuiWindowBase w) => w is T));
			bool flag = t != null;
			if (flag)
			{
				t.OnDestroy();
				WindowsManager.windows.Remove(t);
			}
		}

		public static void UnregisterAll()
		{
			foreach (ImGuiWindowBase imGuiWindowBase in WindowsManager.windows)
			{
				imGuiWindowBase.OnDestroy();
			}
			WindowsManager.windows.Clear();
		}

		private void OnEnable()
		{
			ImGui.Layout += this.OnLayout;
		}

		private void OnDisable()
		{
			ImGui.Layout -= this.OnLayout;
		}

		private void OnLayout()
		{
			this.DrawAll();
		}

		private void DrawAll()
		{
			for (int i = 0; i < WindowsManager.windows.Count; i++)
			{
				WindowsManager.windows[i].DrawWindow();
			}
		}

		private static WindowsManager _instance;

		private static List<ImGuiWindowBase> windows = new List<ImGuiWindowBase>();
	}
}
