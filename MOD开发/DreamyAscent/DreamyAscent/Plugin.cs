using System.IO;
using BepInEx;
using DreamyAscent.Helpers;
using DreamyAscent.Services;

namespace DreamyAscent
{
    [BepInPlugin("com.wuyachiyu.dreamyascent", "DreamyAscent", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            DaLog.Log = Logger;

            string pluginDirectory = Path.GetDirectoryName(Info.Location) ?? string.Empty;
            DaLocalization.Initialize(pluginDirectory);
            DaSaveService.Initialize(pluginDirectory);
            DaDiagnosticService.Initialize(pluginDirectory);
            DaImportExportService.Initialize(pluginDirectory);
            DaPreviewPoseService.Initialize(pluginDirectory);
            DaTemplateSnapshotService.Initialize(pluginDirectory);
            DaObjectRegistryService.Initialize(pluginDirectory);
            DaParentChildRegistryService.Initialize(pluginDirectory);
            DaCompatibilityPatchService.Initialize();
            DaSaveService.EnsureDefaultSaveExists();

            gameObject.AddComponent<DaRuntimeController>();

            DaLog.Info("DreamyAscent started.");
            DaLog.Info("Original terrain customiser implementation is treated as a reference only.");
        }
    }
}


