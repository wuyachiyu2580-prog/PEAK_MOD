using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private ConfigEntry<bool> _debugPhaseTiming;
        private ConfigEntry<int> _debugPhaseTimingSeconds;
        private Action<string> _phaseTimingOutput;
        private readonly BoundedPhaseProfiler _phaseProfiler=new BoundedPhaseProfiler(
            "Update","NetworkDiscovery","ClassificationRetries","NetworkCleanup","LabelRefresh","LabelProjection",
            "Scene00Statues","Scene01Tumbleweed","Scene02GloomBell","Scene03Spider","Scene04Bees",
            "Scene05Scoutmaster","Scene06Spikes","Scene07Antlion","Scene08Venus","Scene09Tornado",
            "Scene10Napberry","Scene11Arrows","Scene12Saw","Scene13Roller","Scene14Axe","Scene15Jellyfish",
            "Scene16Spores","Scene17RunSettings","Scene18RootItems","Scene19RootFakeItems");
        private void TickPhaseTiming()
        {
            if(_debugPhaseTiming==null)return;
            if(_phaseTimingOutput==null&&_debugPhaseTiming.Value)_phaseTimingOutput=ReportPhaseTiming;
            if(_phaseProfiler.Tick(_debugPhaseTiming.Value,_debugPhaseTimingSeconds.Value,Time.realtimeSinceStartup,_phaseTimingOutput))
                _debugPhaseTiming.Value=false;
            UpdatePhaseContext();
        }
        private void UpdatePhaseContext()
        {
            if(!_phaseProfiler.Active)return;
            _phaseProfiler.Current=new BoundedPhaseProfiler.Context{Objects=_networkObjects.Count,Labels=_labels.Count,
                Selection=_diagnosticSelectionGeneration,Scene=_sceneGeneration,SelectedItems=_selectedIds.Count,
                LocationScopes=(int)_effectiveScopes,MapScope=(int)_mapScope.Value,Frame=Time.frameCount};
        }
        private void ReportPhaseTiming(string report)
        {
            try
            {
                BoundedPhaseProfiler.SaveReport(Path.Combine(Paths.BepInExRootPath, "WhereIsThing", "diagnostics"), report);
            }
            catch (IOException ex)
            {
                _log?.LogWarning("Could not save phase timing report: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _log?.LogWarning("Could not save phase timing report: " + ex.Message);
            }
            _log?.LogInfo(report);
        }
    }
}
