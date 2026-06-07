using System;

namespace TerrainCustomiserCN.Session
{
	internal static class SessionState
	{
		public static SessionState.State Current { get; private set; }

		public static void Set(SessionState.State state)
		{
			SessionState.Current = state;
		}

		public static bool Is(SessionState.State state)
		{
			return SessionState.Current == state;
		}

		public static bool CanGenerate
		{
			get
			{
				return SessionState.Current == SessionState.State.LoadingCustomMap || SessionState.Current == SessionState.State.LoadingEditor || SessionState.Current == SessionState.State.InEditor;
			}
		}

		public enum State
		{
			InMainMenu,
			InAirport,
			SelectingSave,
			ConfiguringCustomMap,
			WaitingToStartCustomMap,
			LoadingCustomMap,
			WaitingForMapData,
			LoadingEditor,
			InCustomMap,
			InEditor
		}
	}
}
