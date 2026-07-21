using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using TerrainCustomiserCN.Managers;

namespace TerrainCustomiserCN.TerrainGeneration
{
	public static class PropGrouperHelpers
	{
		private static void AddToStepList(DeferredStepTiming key, IDeferredStep stepToAdd)
		{
			bool flag = !PropGrouperHelpers.deferredSteps.ContainsKey(key);
			if (flag)
			{
				PropGrouperHelpers.deferredSteps.Add(key, new List<IDeferredStep>());
			}
			PropGrouperHelpers.deferredSteps[key].Add(stepToAdd);
		}

		private static void ExecuteAndClearDeferredStepsFor(DeferredStepTiming key)
		{
			bool flag = PropGrouperHelpers.deferredSteps.ContainsKey(key);
			if (flag)
			{
				foreach (IDeferredStep deferredStep in PropGrouperHelpers.deferredSteps[key])
				{
					deferredStep.DeferredGo();
				}
				PropGrouperHelpers.deferredSteps[key].Clear();
			}
		}

		public static void RunRootPropGrouper(PropGrouper rootGrouper)
		{
			PropGrouperHelpers.deferredSteps.Clear();
			rootGrouper.ClearAll();
			LevelGenStep[] componentsInChildren = rootGrouper.GetComponentsInChildren<LevelGenStep>();
			List<LevelGenStep> list = new List<LevelGenStep>();
			List<LevelGenStep> list2 = new List<LevelGenStep>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				PropGrouper.PropGrouperTiming timing = componentsInChildren[i].GetComponentInParent<PropGrouper>().timing;
				if (timing == PropGrouper.PropGrouperTiming.Early)
				{
					list.Add(componentsInChildren[i]);
				}
				else if (timing == PropGrouper.PropGrouperTiming.Late)
				{
					list2.Add(componentsInChildren[i]);
				}
			}
			foreach (LevelGenStep levelGenStep in list)
			{
				levelGenStep.Execute();
				bool flag = levelGenStep.DeferredTiming > 0;
				if (flag)
				{
					PropGrouperHelpers.AddToStepList(levelGenStep.DeferredTiming, levelGenStep.ConstructDeferred(levelGenStep));
				}
			}
			PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
			bool value = ConfigManager.EnableLightMapBaking.Value;
			if (value)
			{
				LightMapBaker.RunBake();
			}
			PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
			PropGrouperHelpers.ExecuteAndClearDeferredStepsFor(DeferredStepTiming.AfterCurrentGroupTiming);
			foreach (LevelGenStep levelGenStep2 in list2)
			{
				levelGenStep2.Execute();
			}
		}

		public static Dictionary<DeferredStepTiming, List<IDeferredStep>> deferredSteps = new Dictionary<DeferredStepTiming, List<IDeferredStep>>();
	}
}
