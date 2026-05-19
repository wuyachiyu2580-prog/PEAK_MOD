using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DreamyAscent.Helpers
{
    internal static class DaLocalization
    {
        private const string LocalizationFileName = "localization.zh-CN.json";

        private static readonly Dictionary<string, string> s_translations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Beach"] = "\u6d77\u6ee9",
            ["Jungle"] = "\u4e1b\u6797",
            ["Snow"] = "\u96ea\u5c71",
            ["Alpine"] = "\u96ea\u5c71",
            ["Desert"] = "\u6c99\u6f20",
            ["Lava"] = "\u7194\u5ca9",
            ["Caldera"] = "\u706b\u5c71\u53e3",
            ["Volcano"] = "\u706b\u5c71",
            ["Kiln"] = "\u706b\u5c71",
            ["Settings"] = "\u8bbe\u7f6e",
            ["Save"] = "\u4fdd\u5b58",
            ["Generate"] = "\u751f\u6210",
            ["FileNames"] = "\u5b58\u6863\u6587\u4ef6",
            ["SaveName"] = "\u5b58\u6863\u540d",
            ["New"] = "\u65b0\u5efa",
            ["Delete"] = "\u5220\u9664",
            ["UseRandomSeed"] = "\u4f7f\u7528\u968f\u673a\u79cd\u5b50",
            ["Seed"] = "\u79cd\u5b50",
            ["EnableBlizzard"] = "\u542f\u7528\u66b4\u98ce\u96ea",
            ["EnableRain"] = "\u542f\u7528\u964d\u96e8",
            ["Reset"] = "\u91cd\u7f6e",
            ["Close"] = "\u5173\u95ed",
            ["CurrentMap"] = "\u5f53\u524d\u5730\u56fe",
            ["Unknown"] = "\u672a\u77e5",
            ["NoRuntimeData"] = "\u5c1a\u672a\u626b\u63cf\u5230\u5730\u5f62\u6570\u636e\uff0c\u8bf7\u8fdb\u5165\u5173\u5361\u540e\u7b49\u5f85\u6570\u79d2\u3002",
            ["Rescan"] = "\u91cd\u65b0\u626b\u63cf",
            ["WriteDiagnostics"] = "\u5199\u51fa\u8bca\u65ad",
            ["ExportCurrent"] = "\u5bfc\u51fa\u5f53\u524d",
            ["ImportFile"] = "\u5bfc\u5165\u6587\u4ef6",
            ["OpenFileDirectory"] = "\u6253\u5f00\u76ee\u5f55",
            ["OpenFileDirectoryFailed"] = "\u6253\u5f00\u5730\u5f62\u6587\u4ef6\u76ee\u5f55\u5931\u8d25",
            ["UseLatestImport"] = "\u9009\u6700\u65b0\u5bfc\u5165",
            ["ImportApply"] = "\u5bfc\u5165\u5e76\u5e94\u7528",
            ["Structure"] = "\u7ed3\u6784",
            ["LevelNameRow"] = "\u8981\u4fee\u6539\u7684\u5173\u5361",
            ["AreaNameRow"] = "\u533a\u57df\u540d\u79f0",
            ["GeneratedObjects"] = "\u5f53\u524d\u751f\u6210\u6b65\u9aa4",
            ["SpecialSceneObjects"] = "\u7279\u6b8a\u573a\u666f\u7269\u4ef6",
            ["SpecialSceneObject"] = "\u7279\u6b8a\u573a\u666f\u7269\u4ef6",
            ["NoSpecialSceneObjects"] = "\u672a\u626b\u63cf\u5230\u7279\u6b8a\u573a\u666f\u7269\u4ef6\u3002",
            ["SpecialSceneObjectReadOnly"] = "\u8be5\u5bf9\u8c61\u4e0d\u662f\u5b98\u65b9\u751f\u6210\u6b65\u9aa4\uff0c\u6682\u65f6\u53ea\u63d0\u4f9b\u8fd0\u884c\u65f6\u67e5\u770b\u548c\u573a\u666f\u9ad8\u4eae\u3002",
            ["SpecialSceneObjectManageHint"] = "\u8be5\u5bf9\u8c61\u4e0d\u662f\u5b98\u65b9\u751f\u6210\u6b65\u9aa4\uff1b\u53ef\u5728\u8fd0\u884c\u65f6\u9690\u85cf/\u663e\u793a\uff0c\u975e\u4fdd\u62a4\u5bf9\u8c61\u53ef\u5220\u9664\u3002",
            ["RefreshSpecialObjects"] = "\u5237\u65b0\u7279\u6b8a\u7269\u4ef6",
            ["SpecialObjectsRefreshed"] = "\u5df2\u5237\u65b0\u7279\u6b8a\u7269\u4ef6",
            ["HideSpecialObject"] = "\u9690\u85cf",
            ["ShowSpecialObject"] = "\u663e\u793a",
            ["DeleteSpecialObject"] = "\u5220\u9664",
            ["SpecialObjectHidden"] = "\u5df2\u9690\u85cf",
            ["SpecialObjectShown"] = "\u5df2\u663e\u793a",
            ["SpecialObjectDeleted"] = "\u5df2\u5220\u9664",
            ["SpecialObjectDeleteBlocked"] = "\u5220\u9664\u88ab\u963b\u6b62",
            ["ManageState"] = "\u7ba1\u7406\u72b6\u6001",
            ["CanToggleActive"] = "\u53ef\u9690\u85cf/\u663e\u793a",
            ["CanDelete"] = "\u53ef\u5220\u9664",
            ["ProtectedObject"] = "\u53d7\u4fdd\u62a4",
            ["RuntimeLive"] = "\u5b9e\u65f6\u5bf9\u8c61",
            ["AreaCount"] = "\u533a\u57df",
            ["StepCount"] = "\u751f\u6210\u6b65\u9aa4",
            ["NoGroupers"] = "\u5f53\u524d\u5173\u5361\u6ca1\u6709\u626b\u63cf\u5230\u751f\u6210\u533a\u57df\u3002",
            ["NoGeneratedObjects"] = "\u5f53\u524d\u533a\u57df\u6ca1\u6709\u626b\u63cf\u5230\u751f\u6210\u6b65\u9aa4\u3002",
            ["NoCatalogForStep"] = "\u6682\u65e0\u5339\u914d\u7684\u6a21\u677f\u5e93\u6761\u76ee",
            ["Segment"] = "\u533a\u6bb5",
            ["SegmentEditMode"] = "\u7f16\u8f91\u6a21\u5f0f",
            ["OfficialTemplate"] = "\u5b98\u65b9\u6a21\u677f",
            ["CustomBlank"] = "\u7a7a\u767d\u81ea\u5b9a\u4e49",
            ["Hybrid"] = "\u6df7\u5408\u6a21\u5f0f",
            ["SegmentEditModeChanged"] = "\u5df2\u5207\u6362\u533a\u6bb5\u7f16\u8f91\u6a21\u5f0f",
            ["ModeOfficialTemplateHint"] = "\u4fdd\u7559\u539f\u7248\u751f\u6210\u5668\uff0c\u5728\u5b98\u65b9\u6a21\u677f\u57fa\u7840\u4e0a\u8c03\u6574\u53c2\u6570\u3002",
            ["ModeCustomBlankHint"] = "\u70b9\u51fb\u751f\u6210\u672c\u6bb5\u65f6\u4f1a\u6e05\u7406\u8be5\u533a\u6bb5\u5df2\u751f\u6210\u7684\u5b98\u65b9\u7269\u54c1\uff1b\u540e\u7eed\u5c06\u53ea\u6267\u884c\u7528\u6237\u81ea\u5b9a\u4e49\u89c4\u5219\u3002",
            ["OfficialAfterBlankLimited"] = "\u7a7a\u767d\u540e\u5207\u56de\u5b98\u65b9\u4e0d\u4fdd\u8bc1\u5b8c\u6574\u6062\u590d\uff0c\u5efa\u8bae\u91cd\u5f00\u6216\u65b0\u56fe\u590d\u6d4b\u3002",
            ["ModeHybridHint"] = "\u4fdd\u7559\u5b98\u65b9\u751f\u6210\u5668\uff0c\u540e\u7eed\u53e0\u52a0\u7528\u6237\u81ea\u5b9a\u4e49\u7269\u54c1\u89c4\u5219\u3002",
            ["Groupers"] = "\u751f\u6210\u7ec4",
            ["Grouper"] = "\u751f\u6210\u7ec4",
            ["Steps"] = "\u6b65\u9aa4",
            ["Step"] = "\u6b65\u9aa4",
            ["Type"] = "\u7c7b\u578b",
            ["Name"] = "\u540d\u79f0",
            ["Reason"] = "\u8bc6\u522b\u539f\u56e0",
            ["HierarchyPath"] = "\u5c42\u7ea7\u8def\u5f84",
            ["ParentPath"] = "\u7236\u8282\u70b9\u8def\u5f84",
            ["RootPath"] = "\u533a\u6bb5\u6839\u8def\u5f84",
            ["Active"] = "\u6fc0\u6d3b",
            ["Layer"] = "\u56fe\u5c42",
            ["Tag"] = "\u6807\u7b7e",
            ["LocalPosition"] = "\u672c\u5730\u5750\u6807",
            ["WorldPosition"] = "\u4e16\u754c\u5750\u6807",
            ["RendererCount"] = "\u6e32\u67d3\u5668",
            ["ColliderCount"] = "\u78b0\u649e\u4f53",
            ["Components"] = "\u7ec4\u4ef6",
            ["Materials"] = "\u6750\u8d28",
            ["Properties"] = "\u5c5e\u6027",
            ["Modifiers"] = "\u4fee\u9970\u5668",
            ["Constraints"] = "\u7ea6\u675f",
            ["PostConstraints"] = "\u751f\u6210\u540e\u7ea6\u675f",
            ["ParametersTab"] = "\u53c2\u6570",
            ["Catalog"] = "\u533a\u6bb5\u6a21\u677f\u5e93",
            ["CatalogItems"] = "\u7269\u54c1",
            ["CatalogMaterials"] = "\u6750\u8d28",
            ["CatalogChild"] = "\u5b50\u751f\u6210",
            ["CatalogSingleItemSpawner"] = "\u5355\u7269\u54c1\u751f\u6210\u5668",
            ["CatalogDefaults"] = "\u9ed8\u8ba4\u53c2\u6570",
            ["CurrentVariantBaseline"] = "\u5f53\u524d\u53d8\u4f53\u9ed8\u8ba4\u6a21\u677f",
            ["BaselineReady"] = "\u57fa\u7ebf\u53ef\u7528",
            ["TemplateVariant"] = "\u6a21\u677f\u53d8\u4f53",
            ["ObjectRegistry"] = "\u5168\u5c40\u6a21\u677f\u6ce8\u518c\u8868",
            ["ObjectRegistryMissing"] = "\u672a\u627e\u5230\u79bb\u7ebf\u6a21\u677f\u6ce8\u518c\u8868",
            ["RegistryTemplates"] = "\u6a21\u677f",
            ["RegistryMaterials"] = "\u6750\u8d28",
            ["RegistryRecommended"] = "\u9996\u6279\u63a8\u8350\u5019\u9009",
            ["RegistryTechnicalLowRisk"] = "\u6280\u672f\u4f4e\u98ce\u9669",
            ["RegistryRiskTags"] = "\u98ce\u9669\u6807\u8bb0",
            ["RegistryId"] = "\u6ce8\u518c ID",
            ["ParentChildRegistry"] = "\u7236\u5b50\u5173\u7cfb\u6ce8\u518c\u8868",
            ["ParentChildRegistryMissing"] = "\u672a\u627e\u5230\u7236\u5b50\u5173\u7cfb\u6ce8\u518c\u8868",
            ["ParentChildTemplates"] = "\u7236\u5b50\u6a21\u677f",
            ["ParentChildRegistryLoaded"] = "\u5df2\u52a0\u8f7d\u7236\u5b50\u5173\u7cfb\u6ce8\u518c\u8868",
            ["PlacementRegions"] = "\u653e\u7f6e\u5b50\u533a",
            ["PlacementConfig"] = "\u653e\u7f6e\u914d\u7f6e",
            ["PlacementRules"] = "\u653e\u7f6e\u89c4\u5219",
            ["PlacementConfigHint"] = "\u7b2c\u4e00\u7248\u53ea\u76f4\u63a5\u653e\u7f6e\u9759\u6001\u4f4e\u98ce\u9669\u6a21\u677f\uff1bSpawner/BerryBush\u3001\u7236\u5b50\u751f\u6210\u3001\u5355\u7269\u54c1\u751f\u6210\u5668\u548c Photon \u5bf9\u8c61\u4f1a\u5148\u8df3\u8fc7\u3002",
            ["OpenPlacementConfig"] = "\u6253\u5f00\u914d\u7f6e",
            ["PlacementSummaryHint"] = "\u6b64\u5904\u663e\u793a\u653e\u7f6e\u89c4\u5219\u6458\u8981\uff1b\u8fdb\u5165\u914d\u7f6e\u9875\u53ef\u624b\u52a8\u751f\u6210\u6216\u6e05\u7406\u81ea\u5b9a\u4e49\u7269\u54c1\u3002",
            ["RunPlacementRules"] = "\u751f\u6210\u81ea\u5b9a\u4e49",
            ["ClearPlacementRuntime"] = "\u6e05\u7406\u81ea\u5b9a\u4e49",
            ["ClearedPlacementRuntime"] = "\u5df2\u6e05\u7406\u81ea\u5b9a\u4e49\u5b9e\u4f8b",
            ["PlacementRuntimeSpawned"] = "\u81ea\u5b9a\u4e49\u751f\u6210",
            ["PlacementRuntimeAppliedRules"] = "\u6267\u884c\u89c4\u5219",
            ["PlacementRuntimeFailedRules"] = "\u8df3\u8fc7/\u5931\u8d25\u89c4\u5219",
            ["PlacementRuntimeWarnings"] = "\u8b66\u544a",
            ["ParameterHints"] = "\u53c2\u6570\u8bf4\u660e",
            ["NoKnownParameterHints"] = "\u5f53\u524d\u6b65\u9aa4\u6682\u65e0\u5df2\u6574\u7406\u7684\u4e2d\u6587\u53c2\u6570\u8bf4\u660e\u3002",
            ["NoFocusedProperty"] = "\u70b9\u51fb\u6216\u4fee\u6539\u53f3\u4fa7\u67d0\u4e2a\u53c2\u6570\u540e\uff0c\u8fd9\u91cc\u4f1a\u663e\u793a\u5b83\u7684\u8bf4\u660e\u3002",
            ["SampleAssets"] = "\u6837\u672c\u8d44\u4ea7",
            ["LoadSampleAssets"] = "\u52a0\u8f7d\u6837\u672c\u8d44\u4ea7",
            ["RefreshSampleAssets"] = "\u91cd\u65b0\u52a0\u8f7d\u6837\u672c\u8d44\u4ea7",
            ["UnloadSampleAssets"] = "\u5378\u8f7d\u6837\u672c\u8d44\u4ea7",
            ["SampleAssetsNotLoaded"] = "\u6837\u672c\u8d44\u4ea7\u672a\u52a0\u8f7d",
            ["SampleAssetsManualLoadHint"] = "\u4e3a\u964d\u4f4e\u542f\u52a8\u548c\u6253\u5f00 UI \u65f6\u7684\u5185\u5b58\u5cf0\u503c\uff0c\u79bb\u7ebf\u6a21\u677f\u5feb\u7167/\u6ce8\u518c\u8868\u6539\u4e3a\u624b\u52a8\u52a0\u8f7d\u3002",
            ["SampleRegistryLoaded"] = "\u5df2\u52a0\u8f7d\u79bb\u7ebf\u6a21\u677f\u6ce8\u518c\u8868",
            ["SampleRegistryMissing"] = "\u672a\u52a0\u8f7d\u79bb\u7ebf\u6a21\u677f\u6ce8\u518c\u8868",
            ["SampleDevAssetsHint"] = "\u6a21\u677f\u5feb\u7167\u548c\u56de\u5f52\u62a5\u544a\u662f\u5f00\u53d1\u671f\u8d44\u4ea7\uff0c\u540e\u7eed\u7528\u4e8e\u7a33\u5b9a\u5339\u914d\u3001\u5b50\u533a\u63a8\u8350\u548c\u56de\u5f52\u68c0\u67e5\u3002",
            ["TemplateSnapshotMatch"] = "\u6a21\u677f\u5feb\u7167\u5339\u914d",
            ["SnapshotMatchStatus"] = "\u72b6\u6001",
            ["SnapshotMatchScore"] = "\u5339\u914d\u5206\u6570",
            ["SnapshotCandidates"] = "\u5019\u9009",
            ["SnapshotMatchDetail"] = "\u5339\u914d\u7ed3\u679c",
            ["TemplateBaseline"] = "\u5b98\u65b9\u57fa\u7ebf",
            ["BaselineMatched"] = "\u57fa\u7ebf\u547d\u4e2d",
            ["BaselineWarnings"] = "\u57fa\u7ebf\u8b66\u544a",
            ["matched"] = "\u5df2\u5339\u914d",
            ["weak-match"] = "\u5f31\u5339\u914d",
            ["snapshot-file-missing"] = "\u5feb\u7167\u6587\u4ef6\u7f3a\u5931",
            ["no-segment-variant-snapshot"] = "\u6ca1\u6709\u5f53\u524d\u533a\u6bb5/\u53d8\u4f53\u5feb\u7167",
            ["no-match"] = "\u672a\u5339\u914d",
            ["segment-missing"] = "\u533a\u6bb5\u7f3a\u5931",
            ["EnableValue"] = "\u542f\u7528",
            ["DisableValue"] = "\u5173\u95ed",
            ["BoolCurrentValue"] = "\u5f53\u524d\u72b6\u6001",
            ["ReadOnlyProperty"] = "\u53ea\u8bfb\u53c2\u6570",
            ["UnsupportedPropertyValue"] = "\u8fd9\u4e2a\u503c\u662f Unity \u590d\u6742\u7c7b\u578b\uff0c\u6682\u65f6\u53ea\u663e\u793a\uff0c\u4e0d\u63d0\u4f9b\u76f4\u63a5\u7f16\u8f91\u3002",
            ["CurrentValue"] = "\u5f53\u524d\u503c",
            ["Enabled"] = "\u5df2\u542f\u7528",
            ["AddPlacementRule"] = "\u6dfb\u52a0\u89c4\u5219",
            ["AddDefaultSubArea"] = "\u6dfb\u52a0\u9ed8\u8ba4\u5b50\u533a",
            ["DefaultSubArea"] = "\u9ed8\u8ba4\u5b50\u533a",
            ["NoSubAreas"] = "\u5c1a\u672a\u521b\u5efa\u653e\u7f6e\u5b50\u533a\u3002",
            ["NoPlacementRules"] = "\u5c1a\u672a\u521b\u5efa\u653e\u7f6e\u89c4\u5219\u3002",
            ["PlacementRuleAdded"] = "\u5df2\u6dfb\u52a0\u653e\u7f6e\u89c4\u5219",
            ["PlacementRuleUpdated"] = "\u5df2\u66f4\u65b0\u653e\u7f6e\u89c4\u5219",
            ["PlacementRuleDeleted"] = "\u5df2\u5220\u9664\u653e\u7f6e\u89c4\u5219",
            ["PlacementRuleInvalid"] = "\u653e\u7f6e\u89c4\u5219\u6570\u503c\u65e0\u6548",
            ["PlacementRuleCountClamped"] = "\u6570\u91cf\u5df2\u9650\u5236\u5230",
            ["PlacementTemplateUnsupported"] = "\u6682\u4e0d\u652f\u6301\u76f4\u63a5\u653e\u7f6e",
            ["SubAreaAdded"] = "\u5df2\u6dfb\u52a0\u653e\u7f6e\u5b50\u533a",
            ["SubAreaDeleted"] = "\u5df2\u5220\u9664\u653e\u7f6e\u5b50\u533a",
            ["TargetSubArea"] = "\u76ee\u6807\u5b50\u533a",
            ["Count"] = "\u6570\u91cf",
            ["MinScale"] = "\u6700\u5c0f\u7f29\u653e",
            ["MaxScale"] = "\u6700\u5927\u7f29\u653e",
            ["PlacementMode"] = "\u653e\u7f6e\u6a21\u5f0f",
            ["RotationMode"] = "\u65cb\u8f6c\u6a21\u5f0f",
            ["OwnershipMode"] = "\u5f52\u5c5e\u6a21\u5f0f",
            ["CenterOffset"] = "\u4e2d\u5fc3\u504f\u79fb",
            ["Size"] = "\u5c3a\u5bf8",
            ["SegmentBounds"] = "\u6574\u4e2a\u533a\u6bb5",
            ["Box"] = "\u76d2\u5f62",
            ["Circle"] = "\u5706\u5f62",
            ["Source"] = "\u6765\u6e90",
            ["Shader"] = "\u7740\u8272\u5668",
            ["PhotonView"] = "\u8054\u673a\u540c\u6b65",
            ["GenerateGrouper"] = "\u751f\u6210\u672c\u7ec4",
            ["GenerateSegment"] = "\u751f\u6210\u672c\u6bb5",
            ["GeneratedGrouper"] = "\u5df2\u751f\u6210\u7ec4",
            ["GeneratedSegment"] = "\u5df2\u751f\u6210\u6bb5",
            ["ClearedSegmentObjects"] = "\u5df2\u6e05\u7406\u533a\u6bb5\u5b98\u65b9\u5b9e\u4f8b",
            ["NoStepSelected"] = "\u5c1a\u672a\u9009\u4e2d\u751f\u6210\u6b65\u9aa4\u3002",
            ["Apply"] = "\u5e94\u7528",
            ["Applied"] = "\u5df2\u5e94\u7528",
            ["ApplyFailed"] = "\u5e94\u7528\u5931\u8d25",
            ["RescanDone"] = "\u91cd\u65b0\u626b\u63cf\u5b8c\u6210",
            ["RescanFailed"] = "\u91cd\u65b0\u626b\u63cf\u5931\u8d25",
            ["DiagnosticsWritten"] = "\u8bca\u65ad\u5df2\u5199\u51fa",
            ["Exported"] = "\u5df2\u5bfc\u51fa",
            ["NoImportFiles"] = "\u5730\u5f62\u6587\u4ef6\u76ee\u5f55\u4e2d\u6ca1\u6709 JSON",
            ["SelectedImport"] = "\u5df2\u9009\u5bfc\u5165\u6587\u4ef6",
            ["NoRuntimeDataShort"] = "\u6ca1\u6709\u8fd0\u884c\u65f6\u5730\u5f62\u6570\u636e",
            ["ImportFailed"] = "\u5bfc\u5165\u5931\u8d25",
            ["ImportedApplied"] = "\u5df2\u5bfc\u5165\u5e76\u5e94\u7528",
            ["GenerateAfterImport"] = "\u8bf7\u518d\u70b9\u751f\u6210\u672c\u7ec4\u6216\u751f\u6210\u672c\u6bb5",
            ["Preview"] = "\u9884\u89c8",
            ["PreviewHelp"] = "\u5de6\u952e\u62d6\u52a8\u89c6\u89d2\uff0c\u6eda\u8f6e\u7f29\u653e",
            ["PreviewMainCameraNote"] = "\u9884\u89c8\u5df2\u5207\u6362\u5230\u6e38\u620f\u4e3b\u76f8\u673a\u753b\u9762\u3002\u53f3\u4fa7\u7559\u7a7a\uff0c\u5730\u56fe\u5168\u8c8c\u663e\u793a\u5728\u7a97\u53e3\u540e\u65b9\uff1b\u6309\u4f4f\u5de6\u952e\u62d6\u52a8\u6b64\u533a\u57df\u53ef\u65cb\u8f6c\u89c6\u89d2\uff0c\u6eda\u8f6e\u7f29\u653e\u3002",
            ["PreviewScreenHelp"] = "\u53f3\u4fa7\u4e3a\u5730\u56fe\u9884\u89c8\uff1a\u5de6\u952e\u62d6\u52a8\u65cb\u8f6c\u89c6\u89d2\uff0c\u6eda\u8f6e\u7f29\u653e\u3002\u5173\u95ed\u754c\u9762\u540e\u4f1a\u6062\u590d\u73a9\u5bb6\u89c6\u89d2\u3002",
            ["PreviewUnavailable"] = "\u9884\u89c8\u4e0d\u53ef\u7528",
            ["AutoGeneratedGrouper"] = "\u5df2\u81ea\u52a8\u751f\u6210\u672c\u7ec4",
            ["GenerateQueued"] = "\u5df2\u6392\u961f\u751f\u6210",
            ["Beach_Segment"] = "\u6d77\u6ee9",
            ["Roots Segment"] = "\u6839\u7cfb",
            ["Jungle_Segment"] = "\u4e1b\u6797",
            ["Snow_Segment"] = "\u96ea\u5c71",
            ["Desert_Segment"] = "\u6c99\u6f20",
            ["Caldera_Segment"] = "\u706b\u5c71\u53e3",
            ["Volcano_Segment"] = "\u706b\u5c71",
            ["Beach-Segment"] = "\u6d77\u6ee9",
            ["Jungle-Segment"] = "\u4e1b\u6797",
            ["Snow-Segment"] = "\u96ea\u5c71",
            ["Desert-Segment"] = "\u6c99\u6f20",
            ["Caldera-Segment"] = "\u706b\u5c71\u53e3",
            ["Volcano-Segment"] = "\u706b\u5c71",
            ["Default"] = "\u9ed8\u8ba4",
            ["BlackSand"] = "\u9ed1\u6c99\u6d77\u6ee9",
            ["CactusForest"] = "\u4ed9\u4eba\u638c\u6797",
            ["Edges"] = "\u8fb9\u7f18",
            ["LavaRivers"] = "\u7194\u5ca9\u6cb3",
            ["Lights"] = "\u5149\u6e90",
            ["Middle"] = "\u4e2d\u90e8",
            ["PlateauProps"] = "\u9ad8\u53f0\u7269\u4ef6",
            ["PlateauRocks"] = "\u9ad8\u53f0\u5ca9\u77f3",
            ["Platteau"] = "\u9ad8\u53f0",
            ["Pops_Plat"] = "\u9ad8\u53f0\u7269\u4ef6",
            ["Props"] = "\u7269\u4ef6",
            ["Props_Wall"] = "\u5899\u9762\u7269\u4ef6",
            ["Redwood"] = "\u7ea2\u6749",
            ["Rocks"] = "\u5ca9\u77f3",
            ["Rocks_Plat"] = "\u9ad8\u53f0\u5ca9\u77f3",
            ["Rocks_Wall"] = "\u5899\u9762\u5ca9\u77f3",
            ["WallProps"] = "\u5899\u9762\u7269\u4ef6",
            ["WallRocks"] = "\u5899\u9762\u5ca9\u77f3",
            ["Waterfalls"] = "\u7011\u5e03",
            ["IceRockSpawn_L"] = "\u5de6\u4fa7\u51b0\u5ca9\u751f\u6210",
            ["IceRockSpawn_R"] = "\u53f3\u4fa7\u51b0\u5ca9\u751f\u6210",
            ["- Bomb Beetle Variant"] = "\u70b8\u5f39\u7532\u866b\u53d8\u4f53",
            ["- Cave Mania Variant"] = "\u6d1e\u7a74\u72c2\u70ed\u53d8\u4f53",
            ["- Deep Water variant"] = "\u6df1\u6c34\u53d8\u4f53",
            ["- Redwood Clearcut Variant"] = "\u7ea2\u6749\u780d\u4f10\u53d8\u4f53",
            ["Aloe"] = "\u82a6\u835f",
            ["Antlion"] = "\u8681\u72ee",
            ["Base Rocks"] = "\u57fa\u5e95\u5ca9\u77f3",
            ["base Stumps"] = "\u57fa\u5e95\u6811\u6869",
            ["Bassalt"] = "\u7384\u6b66\u5ca9",
            ["BassaltClusters"] = "\u7384\u6b66\u5ca9\u7fa4",
            ["Beach"] = "\u6d77\u6ee9",
            ["BeachGrass"] = "\u6d77\u6ee9\u8349",
            ["Beetles"] = "\u7532\u866b",
            ["Behive"] = "\u8702\u5de2",
            ["Big"] = "\u5927\u578b",
            ["Big Redwood"] = "\u5927\u7ea2\u6749",
            ["Big_Overhang"] = "\u5927\u578b\u60ac\u6311",
            ["BigTree"] = "\u5927\u6811",
            ["Bridges"] = "\u6865",
            ["Bushes"] = "\u704c\u6728",
            ["Cactus"] = "\u4ed9\u4eba\u638c",
            ["CactusOnTop"] = "\u9876\u90e8\u4ed9\u4eba\u638c",
            ["Cactus_Balls"] = "\u7403\u5f62\u4ed9\u4eba\u638c",
            ["Cactus_Big"] = "\u5927\u4ed9\u4eba\u638c",
            ["Cactus_Big_Dry"] = "\u5e72\u67af\u5927\u4ed9\u4eba\u638c",
            ["CanyonScorps"] = "\u5ce1\u8c37\u874e\u5b50",
            ["Caves"] = "\u6d1e\u7a74",
            ["ClusterBerries"] = "\u4e1b\u751f\u8393\u679c",
            ["Cold"] = "\u5bd2\u51b7",
            ["ColdMedium"] = "\u4e2d\u578b\u51b0\u51b7\u7269",
            ["Connecting rocks"] = "\u8fde\u63a5\u5ca9\u77f3",
            ["Connecting Vines"] = "\u8fde\u63a5\u85e4\u8513",
            ["Dead Grass"] = "\u67af\u8349",
            ["DeadTree"] = "\u67af\u6811",
            ["Destroyer"] = "\u6e05\u7406\u5668",
            ["Driftwood"] = "\u6f02\u6d41\u6728",
            ["Dolo"] = "\u767d\u4e91\u77f3",
            ["Dynamite"] = "\u70b8\u836f",
            ["Dynamite_Outside"] = "\u5916\u90e8\u70b8\u836f",
            ["Edge"] = "\u8fb9\u7f18",
            ["Eggs"] = "\u86cb",
            ["End"] = "\u7ec8\u70b9",
            ["End_L"] = "\u5de6\u7ec8\u70b9",
            ["End_R"] = "\u53f3\u7ec8\u70b9",
            ["ErikSpawner"] = "Erik \u751f\u6210\u5668",
            ["ExploShrooms"] = "\u7206\u70b8\u8611\u83c7",
            ["Ferns"] = "\u8568\u7c7b",
            ["FlashPlant"] = "\u95ea\u5149\u690d\u7269",
            ["Flat"] = "\u5e73\u5766",
            ["Foot"] = "\u5e95\u90e8",
            ["Foot_Small"] = "\u5c0f\u5e95\u90e8",
            ["Foundation"] = "\u57fa\u7840",
            ["Funky Mushrooms"] = "\u5947\u5f02\u8611\u83c7",
            ["Geysers"] = "\u95f4\u6b47\u6cc9",
            ["Ivy"] = "\u5e38\u6625\u85e4",
            ["Jellies"] = "\u6c34\u6bcd",
            ["Light"] = "\u5149\u6e90",
            ["LuggageSpawner"] = "\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner Platforms"] = "\u5e73\u53f0\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_Canyon"] = "\u5ce1\u8c37\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_High"] = "\u9ad8\u5904\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_Inside"] = "\u5185\u90e8\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_Low"] = "\u4f4e\u5904\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_Mirrage"] = "\u6d77\u5e02\u8703\u697c\u884c\u674e\u751f\u6210\u5668",
            ["LuggageSpawner_Outside"] = "\u5916\u90e8\u884c\u674e\u751f\u6210\u5668",
            ["Magma"] = "\u5ca9\u6d46",
            ["Medium"] = "\u4e2d\u578b",
            ["Medium_Foot"] = "\u4e2d\u578b\u5e95\u90e8",
            ["Mid"] = "\u4e2d\u6bb5",
            ["Mineshafts"] = "\u77ff\u9053",
            ["MirageOasis"] = "\u6d77\u5e02\u8703\u697c\u7eff\u6d32",
            ["Monsteras"] = "\u9f9f\u80cc\u7af9",
            ["moss patches"] = "\u82d4\u85d3\u5757",
            ["Moss Spawners"] = "\u82d4\u85d3\u751f\u6210\u5668",
            ["Mush Trees"] = "\u8611\u83c7\u6811",
            ["Mush Trees (spores)"] = "\u5b62\u5b50\u8611\u83c7\u6811",
            ["Mushroom"] = "\u8611\u83c7",
            ["Mushrooms"] = "\u8611\u83c7",
            ["NapBerry"] = "\u7761\u7720\u8393\u679c",
            ["Oasis"] = "\u7eff\u6d32",
            ["Oasis Palms"] = "\u7eff\u6d32\u68d5\u6988",
            ["Palms"] = "\u68d5\u6988",
            ["Pillars"] = "\u77f3\u67f1",
            ["Pine"] = "\u677e\u6811",
            ["Platform Spawns"] = "\u5e73\u53f0\u751f\u6210\u70b9",
            ["Platforms"] = "\u5e73\u53f0",
            ["PoisonShrooms"] = "\u6bd2\u8611\u83c7",
            ["redwoods"] = "\u7ea2\u6749",
            ["- redwoods"] = "\u7ea2\u6749",
            ["Rings"] = "\u73af\u5f62",
            ["Rings small"] = "\u5c0f\u73af\u5f62",
            ["Rocks Flat"] = "\u5e73\u5766\u5ca9\u77f3",
            ["RocksBig"] = "\u5927\u5ca9\u77f3",
            ["RocksSmall"] = "\u5c0f\u5ca9\u77f3",
            ["Roots"] = "\u6839\u7cfb",
            ["Ropes"] = "\u7ef3\u7d22",
            ["Scorpions"] = "\u874e\u5b50",
            ["ScorpionsHell"] = "\u5730\u72f1\u874e\u5b50",
            ["SeaShells"] = "\u8d1d\u58f3",
            ["Shape"] = "\u5f62\u72b6",
            ["Shelf Shroom Spawns"] = "\u67b6\u5f0f\u8611\u83c7\u751f\u6210\u70b9",
            ["Shelf Shrooms"] = "\u67b6\u5f0f\u8611\u83c7",
            ["ShittyPiton"] = "\u7834\u65e7\u5ca9\u9489",
            ["ShroomSpawner"] = "\u8611\u83c7\u751f\u6210\u5668",
            ["Shrub"] = "\u704c\u6728",
            ["Small"] = "\u5c0f\u578b",
            ["small"] = "\u5c0f\u578b",
            ["Small Rocks"] = "\u5c0f\u5ca9\u77f3",
            ["SmallRocks"] = "\u5c0f\u5ca9\u77f3",
            ["Small_End"] = "\u5c0f\u7ec8\u70b9",
            ["Small_Foot"] = "\u5c0f\u5e95\u90e8",
            ["Small_Inner"] = "\u5c0f\u5185\u4fa7",
            ["Snake (2)"] = "\u86c7",
            ["Spider Spawners"] = "\u8718\u86db\u751f\u6210\u5668",
            ["Spiders"] = "\u8718\u86db",
            ["Spore Shroom Trees"] = "\u5b62\u5b50\u8611\u83c7\u6811",
            ["Spore Shrooms"] = "\u5b62\u5b50\u8611\u83c7",
            ["SporeShrooms"] = "\u5b62\u5b50\u8611\u83c7",
            ["Start"] = "\u8d77\u70b9",
            ["Stumps"] = "\u6811\u6869",
            ["Tall"] = "\u9ad8\u5927",
            ["Thorns"] = "\u8346\u68d8",
            ["Timple"] = "\u795e\u5e99",
            ["TreePlatformBridges"] = "\u6811\u5e73\u53f0\u6865",
            ["Trees"] = "\u6811\u6728",
            ["Trees_RockOnly"] = "\u4ec5\u5ca9\u77f3\u6811\u6728",
            ["Trees_Tall"] = "\u9ad8\u5927\u6811\u6728",
            ["TumblerHell"] = "\u5730\u72f1\u6eda\u866b",
            ["Tumblers"] = "\u6eda\u866b",
            ["Tunnels"] = "\u96a7\u9053",
            ["Tunnels_Foot"] = "\u96a7\u9053\u5e95\u90e8",
            ["Urchins"] = "\u6d77\u80c6",
            ["Vines"] = "\u85e4\u8513",
            ["Weed"] = "\u6742\u8349",
            ["Zombie Spawners"] = "\u50f5\u5c38\u751f\u6210\u5668",
            ["PropSpawner"] = "\u7269\u4ef6\u751f\u6210\u5668",
            ["PropSpawner_Line"] = "\u7ebf\u6027\u751f\u6210\u5668",
            ["DecorSpawner"] = "\u88c5\u9970\u751f\u6210\u5668",
            ["PropDeleter"] = "\u7269\u4ef6\u5220\u9664\u5668",
            ["DesertRockSpawner"] = "\u6c99\u6f20\u5ca9\u77f3\u751f\u6210\u5668",
            ["mute"] = "\u7981\u7528",
            ["addRotation"] = "\u6dfb\u52a0\u65cb\u8f6c",
            ["height"] = "\u9ad8\u5ea6",
            ["axisMultipliers"] = "\u8f74\u5411\u500d\u7387",
            ["blend"] = "\u6df7\u5408",
            ["rayLength"] = "\u5c04\u7ebf\u957f\u5ea6",
            ["nrOfSpawns"] = "\u751f\u6210\u6570\u91cf",
            ["randomSpawns"] = "\u968f\u673a\u6570\u91cf",
            ["minSpawnCount"] = "\u6700\u5c0f\u751f\u6210\u6570",
            ["rayCastSpawn"] = "\u5c04\u7ebf\u751f\u6210",
            ["raycastPosition"] = "\u5c04\u7ebf\u5b9a\u4f4d",
            ["rayNearCutoff"] = "\u8fd1\u8ddd\u79bb\u622a\u65ad",
            ["rayDirectionOffset"] = "\u5c04\u7ebf\u65b9\u5411\u504f\u79fb",
            ["layerType"] = "\u5c42\u7ea7\u7c7b\u578b",
            ["syncTransforms"] = "\u540c\u6b65\u53d8\u6362",
            ["minMaxSpawn"] = "\u751f\u6210\u6570\u8303\u56f4",
            ["area"] = "\u8303\u56f4",
            ["chanceToUseSpawner"] = "\u542f\u7528\u6982\u7387",
            ["overallSpawnChance"] = "\u603b\u751f\u6210\u6982\u7387",
            ["scaleMinMax"] = "\u7f29\u653e\u8303\u56f4",
            ["radius"] = "\u534a\u5f84",
            ["circleSize"] = "\u5706\u5f62\u8303\u56f4",
            ["inverted"] = "\u53cd\u5411",
            ["invert"] = "\u53cd\u5411",
            ["childName"] = "\u5b50\u7269\u4ef6\u540d",
            ["Deadzone"] = "\u6b7b\u533a",
            ["DesiredResult"] = "\u671f\u671b\u7ed3\u679c",
            ["effectedLayers"] = "\u53d7\u5f71\u54cd\u5c42",
            ["eulerAngles"] = "\u6b27\u62c9\u89d2",
            ["eulerAnglesRandom"] = "\u968f\u673a\u6b27\u62c9\u89d2",
            ["findAllSpawners"] = "\u67e5\u627e\u6240\u6709\u751f\u6210\u5668",
            ["flipNormal"] = "\u7ffb\u8f6c\u6cd5\u7ebf",
            ["increment"] = "\u589e\u91cf",
            ["LayerType"] = "\u5c42\u7ea7\u7c7b\u578b",
            ["LayerType.AllPhysical"] = "\u5168\u90e8\u7269\u7406",
            ["LayerType.TerrainMap"] = "\u5730\u5f62\u5730\u56fe",
            ["LayerType.Terrain"] = "\u5730\u5f62",
            ["LayerType.Map"] = "\u5730\u56fe",
            ["LayerType.Default"] = "\u9ed8\u8ba4",
            ["LayerType.AllPhysicalExceptCharacter"] = "\u9664\u89d2\u8272\u5916\u7684\u7269\u7406",
            ["LayerType.CharacterAndDefault"] = "\u89d2\u8272\u548c\u9ed8\u8ba4",
            ["LayerType.AllPhysicalExceptDefault"] = "\u9664\u9ed8\u8ba4\u5916\u7684\u7269\u7406",
            ["localEnd"] = "\u672c\u5730\u7ec8\u70b9",
            ["localStart"] = "\u672c\u5730\u8d77\u70b9",
            ["maxAngle"] = "\u6700\u5927\u89d2\u5ea6",
            ["maxEffect"] = "\u6700\u5927\u6548\u679c",
            ["maxHeight"] = "\u6700\u5927\u9ad8\u5ea6",
            ["maxOffset"] = "\u6700\u5927\u504f\u79fb",
            ["maxRotation"] = "\u6700\u5927\u65cb\u8f6c",
            ["maxScaleMult"] = "\u6700\u5927\u7f29\u653e\u500d\u7387",
            ["minAngle"] = "\u6700\u5c0f\u89d2\u5ea6",
            ["minDistance"] = "\u6700\u5c0f\u8ddd\u79bb",
            ["minEffect"] = "\u6700\u5c0f\u6548\u679c",
            ["minHeight"] = "\u6700\u5c0f\u9ad8\u5ea6",
            ["minMax"] = "\u6700\u5c0f/\u6700\u5927",
            ["minOffset"] = "\u6700\u5c0f\u504f\u79fb",
            ["minRotation"] = "\u6700\u5c0f\u65cb\u8f6c",
            ["minScaleMult"] = "\u6700\u5c0f\u7f29\u653e\u500d\u7387",
            ["offset"] = "\u504f\u79fb",
            ["outVal"] = "\u8f93\u51fa\u503c",
            ["perlinOffset"] = "Perlin \u504f\u79fb",
            ["perlinSize"] = "Perlin \u5927\u5c0f",
            ["random"] = "\u968f\u673a",
            ["randomPow"] = "\u968f\u673a\u5e42\u6b21",
            ["RaycastDistance"] = "\u5c04\u7ebf\u8ddd\u79bb",
            ["returnVal"] = "\u8fd4\u56de\u503c",
            ["snapToIncrement"] = "\u5438\u9644\u5230\u589e\u91cf",
            ["PropertyHint.mute"] = "\u5173\u95ed\u8fd9\u4e2a\u751f\u6210\u6b65\u9aa4\uff0c\u901a\u5e38\u7528\u4e8e\u4e34\u65f6\u7981\u7528\u67d0\u7c7b\u7269\u4f53\u3002",
            ["PropertyHint.nrOfSpawns"] = "\u76ee\u6807\u751f\u6210\u6570\u91cf\u3002\u6570\u503c\u8d8a\u5927\uff0c\u7269\u4f53\u8d8a\u5bc6\uff0c\u751f\u6210\u8017\u65f6\u4e5f\u53ef\u80fd\u589e\u52a0\u3002",
            ["PropertyHint.minSpawnCount"] = "\u6700\u5c11\u751f\u6210\u6570\u91cf\uff0c\u7528\u6765\u907f\u514d\u968f\u673a\u7ed3\u679c\u8fc7\u5c11\u3002",
            ["PropertyHint.randomSpawns"] = "\u542f\u7528\u540e\u4f1a\u5728\u6700\u5c11\u6570\u91cf\u548c\u76ee\u6807\u6570\u91cf\u4e4b\u95f4\u968f\u673a\u53d6\u503c\u3002",
            ["PropertyHint.area"] = "\u751f\u6210\u8303\u56f4\uff0c\u901a\u5e38\u662f X/Z \u5e73\u9762\u4e0a\u7684\u5bbd\u5ea6\u548c\u6df1\u5ea6\u3002",
            ["PropertyHint.chanceToUseSpawner"] = "\u8fd9\u4e2a\u751f\u6210\u5668\u88ab\u4f7f\u7528\u7684\u6982\u7387\uff0c1 \u8868\u793a\u603b\u662f\u4f7f\u7528\uff0c0 \u8868\u793a\u4e0d\u4f7f\u7528\u3002",
            ["PropertyHint.overallSpawnChance"] = "\u5355\u6b21\u751f\u6210\u901a\u8fc7\u7684\u603b\u4f53\u6982\u7387\uff0c\u964d\u4f4e\u540e\u4f1a\u51cf\u5c11\u5b9e\u9645\u51fa\u73b0\u6570\u91cf\u3002",
            ["PropertyHint.rayLength"] = "\u5411\u5730\u5f62\u6295\u5c04\u5c04\u7ebf\u7684\u6700\u5927\u8ddd\u79bb\uff0c\u5f71\u54cd\u80fd\u5426\u627e\u5230\u843d\u5730\u70b9\u3002",
            ["PropertyHint.rayCastSpawn"] = "\u7528\u5c04\u7ebf\u5bfb\u627e\u843d\u5730\u70b9\uff0c\u5173\u95ed\u540e\u53ef\u80fd\u4e0d\u6309\u5730\u9762\u653e\u7f6e\u3002",
            ["PropertyHint.raycastPosition"] = "\u751f\u6210\u4f4d\u7f6e\u4f1a\u901a\u8fc7\u5c04\u7ebf\u8d34\u5230\u76ee\u6807\u8868\u9762\u3002",
            ["PropertyHint.rayNearCutoff"] = "\u9760\u8fd1\u5c04\u7ebf\u8d77\u70b9\u7684\u547d\u4e2d\u4f1a\u88ab\u5ffd\u7565\uff0c\u7528\u4e8e\u907f\u5f00\u592a\u8fd1\u7684\u8868\u9762\u3002",
            ["PropertyHint.rayDirectionOffset"] = "\u8c03\u6574\u5c04\u7ebf\u65b9\u5411\uff0c\u5e38\u7528\u4e8e\u659c\u5761\u3001\u5899\u9762\u6216\u7279\u6b8a\u843d\u70b9\u3002",
            ["PropertyHint.layerType"] = "\u5c04\u7ebf\u68c0\u6d4b\u7684\u76ee\u6807\u5c42\u3002\u5e38\u89c1 TerrainMap \u8868\u793a\u6309\u5730\u5f62\u843d\u5730\u3002",
            ["PropertyHint.effectedLayers"] = "\u53d7\u5f71\u54cd\u7684 Unity LayerMask\u3002\u8fd9\u662f\u590d\u6742\u7c7b\u578b\uff0c\u76ee\u524d\u53ea\u8bfb\uff0c\u540e\u7eed\u9700\u8981\u505a\u4e13\u95e8\u7684\u5c42\u7ea7\u9009\u62e9 UI\u3002",
            ["PropertyHint.syncTransforms"] = "\u751f\u6210\u540e\u540c\u6b65 Transform\uff0c\u901a\u5e38\u4fdd\u6301\u5f00\u542f\u66f4\u5b89\u5168\u3002",
            ["PropertyHint.minMaxSpawn"] = "\u751f\u6210\u6570\u91cf\u8303\u56f4\uff0c\u901a\u5e38\u548c\u968f\u673a\u6570\u91cf\u4e00\u8d77\u4f7f\u7528\u3002",
            ["PropertyHint.scaleMinMax"] = "\u7f29\u653e\u8303\u56f4\uff0c\u751f\u6210\u65f6\u4f1a\u5728\u6700\u5c0f\u548c\u6700\u5927\u7f29\u653e\u4e4b\u95f4\u53d6\u503c\u3002",
            ["PropertyHint.radius"] = "\u534a\u5f84\u8303\u56f4\uff0c\u901a\u5e38\u5f71\u54cd\u5706\u5f62\u68c0\u6d4b\u6216\u8ddd\u79bb\u9650\u5236\u3002",
            ["PropertyHint.circleSize"] = "\u5706\u5f62\u533a\u57df\u5927\u5c0f\uff0c\u5f71\u54cd\u5706\u5f62\u751f\u6210\u6216\u7ea6\u675f\u8303\u56f4\u3002",
            ["PropertyHint.inverted"] = "\u53cd\u5411\u5224\u65ad\uff0c\u628a\u539f\u672c\u5141\u8bb8\u7684\u6761\u4ef6\u53d8\u6210\u7981\u6b62\uff0c\u6216\u53cd\u8fc7\u6765\u3002",
            ["PropertyHint.height"] = "\u9ad8\u5ea6\u9608\u503c\uff0c\u5e38\u7528\u4e8e\u9650\u5236\u7269\u4f53\u53ea\u80fd\u51fa\u73b0\u5728\u67d0\u4e2a\u9ad8\u5ea6\u8303\u56f4\u3002",
            ["PlacementHint.Count"] = "\u751f\u6210\u6570\u91cf\u3002\u7b2c\u4e00\u7248\u4e3a\u4e86\u907f\u514d\u5361\u987f\u548c\u8bef\u751f\u6210\uff0c\u5355\u6761\u89c4\u5219\u6700\u591a 25 \u4e2a\u3002",
            ["PlacementHint.MinScale"] = "\u6700\u5c0f\u7f29\u653e\uff0c\u7528\u4e8e\u9650\u5236\u6bcf\u6b21\u653e\u7f6e\u7684\u4f4e\u503c\u3002",
            ["PlacementHint.MaxScale"] = "\u6700\u5927\u7f29\u653e\uff0c\u7528\u4e8e\u9650\u5236\u6bcf\u6b21\u653e\u7f6e\u7684\u9ad8\u503c\u3002",
            ["PlacementHint.TargetSubArea"] = "\u76ee\u6807\u5b50\u533a\uff0c\u6c7a\u5b9a\u8fd9\u6761\u89c4\u5219\u4f1a\u653e\u5230\u54ea\u4e2a\u533a\u57df\u5185\u3002",
            ["PlacementHint.Enabled"] = "\u662f\u5426\u542f\u7528\u8fd9\u6761\u89c4\u5219\u3002",
            ["PSC_Height"] = "\u9ad8\u5ea6\u7ea6\u675f",
            ["PSC_LineCheck"] = "\u7ebf\u68c0\u67e5\u7ea6\u675f",
            ["PSC_Normal"] = "\u6cd5\u7ebf\u7ea6\u675f",
            ["PSC_Perlin"] = "\u566a\u58f0\u7ea6\u675f",
            ["PSC_SameTypeDistance"] = "\u540c\u7c7b\u8ddd\u79bb\u7ea6\u675f",
            ["PSC_Embedded"] = "\u5d4c\u5165\u7ea6\u675f",
            ["PSC_NearObject"] = "\u8fd1\u7269\u4f53\u7ea6\u675f",
            ["PSC_RequiredMaterial"] = "\u5fc5\u9700\u6750\u8d28\u7ea6\u675f",
            ["PSC_SurfaceRestrictions"] = "\u8868\u9762\u9650\u5236\u7ea6\u675f",
            ["PSC_VolumeLight"] = "\u4f53\u79ef\u5149\u7ea6\u675f",
            ["PSC_CircleMask"] = "\u5706\u5f62\u906e\u7f69\u7ea6\u675f",
            ["PSC_BannedMaterial"] = "\u7981\u7528\u6750\u8d28\u7ea6\u675f",
            ["PSCP_ConnectTreePlatforms"] = "\u8fde\u63a5\u6811\u5e73\u53f0\u7ea6\u675f",
            ["PSCP_Custom"] = "\u81ea\u5b9a\u4e49\u5e73\u53f0\u7ea6\u675f",
            ["PSCP_LineCheck"] = "\u5e73\u53f0\u7ebf\u68c0\u67e5\u7ea6\u675f",
            ["PSM_RandomScale"] = "\u968f\u673a\u7f29\u653e",
            ["PSM_RayDirectionOffset"] = "\u5c04\u7ebf\u65b9\u5411\u504f\u79fb",
            ["PSM_SpecificRotation"] = "\u6307\u5b9a\u65cb\u8f6c",
            ["PSM_UpLerp"] = "\u5411\u4e0a\u63d2\u503c",
            ["PSM_RandomRotation"] = "\u968f\u673a\u65cb\u8f6c",
            ["PSM_LocalOffset"] = "\u672c\u5730\u504f\u79fb",
            ["PSM_RandomOffset"] = "\u968f\u673a\u504f\u79fb",
            ["PSM_PlacementOffset"] = "\u653e\u7f6e\u504f\u79fb",
            ["PSM_SetUpRotationToNormal"] = "\u5bf9\u9f50\u6cd5\u7ebf",
            ["PSM_ChildSpawners"] = "\u5b50\u751f\u6210\u5668",
            ["PSM_SingleItemSpawner"] = "\u5355\u7269\u54c1\u751f\u6210\u5668",
            ["PSM_NormalOffset"] = "\u6cd5\u7ebf\u504f\u79fb",
            ["PSM_PitonNormal"] = "\u5ca9\u9489\u6cd5\u7ebf",
            ["PSM_ReplaceMaterial"] = "\u66ff\u6362\u6750\u8d28",
            ["PSM_SetForwardRotationToNormal"] = "\u524d\u5411\u5bf9\u9f50\u6cd5\u7ebf",
            ["PSM_SetMaterial"] = "\u8bbe\u7f6e\u6750\u8d28",
            ["PSM_SetMaterialOnChild"] = "\u8bbe\u7f6e\u5b50\u7269\u4ef6\u6750\u8d28",
            ["PSM_SetRandomMaterial"] = "\u8bbe\u7f6e\u968f\u673a\u6750\u8d28",
            ["step-prop-prefab"] = "\u751f\u6210\u5668\u6a21\u677f",
            ["single-item-prefab"] = "\u5355\u7269\u54c1\u6a21\u677f",
            ["parent-child-template"] = "\u7236\u5b50\u6a21\u677f",
            ["set-material"] = "\u8bbe\u7f6e\u6750\u8d28",
            ["set-child-material"] = "\u8bbe\u7f6e\u5b50\u6750\u8d28",
            ["random-material"] = "\u968f\u673a\u6750\u8d28",
            ["replace-material-from"] = "\u88ab\u66ff\u6362\u6750\u8d28",
            ["replace-material-to"] = "\u66ff\u6362\u4e3a\u6750\u8d28",
            ["required-material"] = "\u5fc5\u9700\u6750\u8d28",
            ["banned-material"] = "\u7981\u7528\u6750\u8d28",
            ["Jungle_PalmTree_Thick"] = "\u7c97\u4e1b\u6797\u68d5\u6988",
            ["Jungle_PalmTree_Thin"] = "\u7ec6\u4e1b\u6797\u68d5\u6988",
            ["Jungle_PalmTree_Crook"] = "\u5f2f\u66f2\u4e1b\u6797\u68d5\u6988",
            ["Jungle_Weed_Small"] = "\u5c0f\u4e1b\u6797\u6742\u8349",
            ["Jungle_Weed_Big"] = "\u5927\u4e1b\u6797\u6742\u8349",
            ["Jungle_Willow_Big"] = "\u5927\u4e1b\u6797\u67f3",
            ["Jungle_Willow_Small"] = "\u5c0f\u4e1b\u6797\u67f3",
            ["Jungle_Willow_Medium"] = "\u4e2d\u578b\u4e1b\u6797\u67f3",
            ["Jungle_Willow_Tall"] = "\u9ad8\u5927\u4e1b\u6797\u67f3",
            ["JungleVine"] = "\u4e1b\u6797\u85e4",
            ["berrybush Variant"] = "\u8393\u679c\u704c\u6728",
            ["Jungle_GiantTree 1"] = "\u4e1b\u6797\u5de8\u6811 1",
            ["Jungle_Monstera"] = "\u4e1b\u6797\u9f9f\u80cc\u7af9",
            ["Jungle_Monstera_Thick"] = "\u7c97\u4e1b\u6797\u9f9f\u80cc\u7af9",
            ["Jungle_PoisonIvy"] = "\u4e1b\u6797\u6bd2\u85e4",
            ["Jungle_SharpPlant"] = "\u4e1b\u6797\u5c16\u523a\u690d\u7269",
            ["Jungle_Tiny"] = "\u4e1b\u6797\u5c0f\u690d\u7269",
            ["Jungle_Aleo"] = "\u4e1b\u6797\u82a6\u835f",
            ["Jungle_MiniBanana"] = "\u4e1b\u6797\u5c0f\u9999\u8549",
            ["LuggageAncient"] = "\u53e4\u4ee3\u884c\u674e",
            ["LuggageEpic"] = "\u53f2\u8bd7\u884c\u674e",
            ["LuggageBig"] = "\u5927\u884c\u674e",
            ["LuggageSmall"] = "\u5c0f\u884c\u674e",
            ["MirageLuggageAncient"] = "\u6d77\u5e02\u8703\u697c\u53e4\u4ee3\u884c\u674e",
            ["MirageLuggageEpic"] = "\u6d77\u5e02\u8703\u697c\u53f2\u8bd7\u884c\u674e",
            ["MirageLuggageBig"] = "\u6d77\u5e02\u8703\u697c\u5927\u884c\u674e",
            ["MirageLuggage"] = "\u6d77\u5e02\u8703\u697c\u884c\u674e",
            ["Urch"] = "\u6d77\u80c6",
            ["berrybush Beach"] = "\u6d77\u6ee9\u8393\u679c\u704c\u6728",
            ["Shell Big"] = "\u5927\u8d1d\u58f3",
            ["Shell Small"] = "\u5c0f\u8d1d\u58f3",
            ["SlipperyJellyfish"] = "\u6ed1\u6eba\u6c34\u6bcd",
            ["Redwood Trunk Holow"] = "\u4e2d\u7a7a\u7ea2\u6749\u6811\u5e72",
            ["Redwood Trunk"] = "\u7ea2\u6749\u6811\u5e72",
            ["Redwood Massive"] = "\u5de8\u578b\u7ea2\u6749",
            ["Forest Roots"] = "\u68ee\u6797\u6839\u7cfb",
            ["Mushroom tree Dome Variant"] = "\u5706\u9876\u8611\u83c7\u6811",
            ["Mushroom tree Flat"] = "\u5e73\u9876\u8611\u83c7\u6811",
            ["Mushroom tree Round Variant"] = "\u5706\u5f62\u8611\u83c7\u6811",
            ["Mushroom tree Flat tall"] = "\u9ad8\u5927\u5e73\u9876\u8611\u83c7\u6811",
            ["Mushroom tree Spore Cloud"] = "\u5b62\u5b50\u4e91\u8611\u83c7\u6811",
            ["Mushroom tree Flat_Evil Variant"] = "\u90aa\u6076\u5e73\u9876\u8611\u83c7\u6811",
            ["Mushroom tree Dome Evil Variant"] = "\u90aa\u6076\u5706\u9876\u8611\u83c7\u6811",
            ["Mushroom tree Flat tall_Evil Variant"] = "\u90aa\u6076\u9ad8\u5927\u5e73\u9876\u8611\u83c7\u6811",
            ["Funky Mushroom Spawner"] = "\u5947\u5f02\u8611\u83c7\u751f\u6210\u5668",
            ["Mushroom Spawner"] = "\u8611\u83c7\u751f\u6210\u5668",
            ["MushroomZombieSpawner"] = "\u8611\u83c7\u50f5\u5c38\u751f\u6210\u5668",
            ["Forest Cave Safe"] = "\u68ee\u6797\u5b89\u5168\u6d1e\u7a74",
            ["Tree Platform"] = "\u6811\u5e73\u53f0",
            ["HangBridgeTreePlatformForest"] = "\u68ee\u6797\u6811\u5e73\u53f0\u540a\u6865",
            ["HangBridge"] = "\u540a\u6865",
            ["Bridge"] = "\u6865",
            ["RopeAnchorWithRope"] = "\u5e26\u7ef3\u7d22\u951a\u70b9",
            ["ShelfShroomPrePlaced"] = "\u9884\u653e\u7f6e\u67b6\u5f0f\u8611\u83c7",
            ["ShelfShroomPrePlaced_Mega Variant"] = "\u5de8\u578b\u9884\u653e\u7f6e\u67b6\u5f0f\u8611\u83c7",
            ["Hanging Moss"] = "\u60ac\u6302\u82d4\u85d3",
            ["moss patch"] = "\u82d4\u85d3\u5757",
            ["Moss Vine"] = "\u82d4\u85d3\u85e4\u8513",
            ["Fern"] = "\u8568\u7c7b",
            ["Fern Old"] = "\u8001\u8568\u7c7b",
            ["Beetle"] = "\u7532\u866b",
            ["SpiderDropper"] = "\u8718\u86db\u6295\u653e\u5668",
            ["SpiderDropper Web Variant"] = "\u8718\u86db\u7f51\u6295\u653e\u5668",
            ["ErikTower"] = "Erik \u5854",
            ["AntLion"] = "\u8681\u72ee",
            ["AloeVera"] = "\u82a6\u835f",
            ["TumbleWeedSpawner"] = "\u98ce\u6eda\u8349\u751f\u6210\u5668",
            ["Scorpion"] = "\u874e\u5b50",
            ["FakeCactusBall"] = "\u5047\u7403\u5f62\u4ed9\u4eba\u638c",
            ["Cactus Ball Big"] = "\u5927\u7403\u5f62\u4ed9\u4eba\u638c",
            ["Cactus Ball Base"] = "\u57fa\u7840\u7403\u5f62\u4ed9\u4eba\u638c",
            ["Short Cactus"] = "\u77ee\u4ed9\u4eba\u638c",
            ["Tall Cactus"] = "\u9ad8\u4ed9\u4eba\u638c",
            ["Tall Cactus Variant_Dry"] = "\u5e72\u67af\u9ad8\u4ed9\u4eba\u638c",
            ["Short Cactus Variant_Dry"] = "\u5e72\u67af\u77ee\u4ed9\u4eba\u638c",
            ["Desert_Bridge"] = "\u6c99\u6f20\u6865",
            ["mineshaft"] = "\u77ff\u9053",
            ["SingleItemSpawner"] = "\u5355\u7269\u54c1\u751f\u6210\u5668",
            ["PickAxeHammered_Shitty"] = "\u7834\u65e7\u9550\u5934",
            ["CaveLight_Jungle"] = "\u4e1b\u6797\u6d1e\u7a74\u706f",
            ["CaveLight"] = "\u6d1e\u7a74\u706f",
            ["Ice_DeadTree"] = "\u51b0\u96ea\u67af\u6811",
            ["Ice_Pine 1"] = "\u51b0\u96ea\u677e\u6811 1",
            ["Ice_Pine 2"] = "\u51b0\u96ea\u677e\u6811 2",
            ["Ice_Pine 3"] = "\u51b0\u96ea\u677e\u6811 3",
            ["Ice_DeadShrub 1"] = "\u51b0\u96ea\u67af\u704c\u6728 1",
            ["Ice_DeadShrub 2"] = "\u51b0\u96ea\u67af\u704c\u6728 2",
            ["Ice_DeadShrub 3"] = "\u51b0\u96ea\u67af\u704c\u6728 3",
            ["Ice_Horsetail"] = "\u51b0\u96ea\u6728\u8d3c\u8349",
            ["Rock_Plat"] = "\u5e73\u53f0\u5ca9\u77f3",
            ["Spike"] = "\u5c16\u523a",
            ["BI_Rock39"] = "\u5ca9\u77f3 39",
            ["RockCold"] = "\u5bd2\u51b7\u5ca9\u77f3",
            ["IceVine"] = "\u51b0\u96ea\u85e4",
            ["Rock_Round"] = "\u5706\u5f62\u5ca9\u77f3",
            ["Geyser"] = "\u95f4\u6b47\u6cc9",
            ["NapBerrySpawn"] = "\u7761\u7720\u8393\u679c\u751f\u6210",
            ["ClimbingSpikeHammered_Shitty"] = "\u7834\u65e7\u6500\u722c\u5ca9\u9489",
            ["M_Rock_ice"] = "\u51b0\u5ca9\u6750\u8d28",
            ["M_Rock_Ice_Cold"] = "\u5bd2\u51b7\u51b0\u5ca9\u6750\u8d28",
            ["M_RopeBridgeCold"] = "\u5bd2\u51b7\u7ef3\u6865\u6750\u8d28",
            ["EggNest"] = "\u86cb\u5de2",
            ["LavaVine"] = "\u7194\u5ca9\u85e4",
            ["LavaPillar"] = "\u7194\u5ca9\u77f3\u67f1",
            ["LavaPillar2"] = "\u7194\u5ca9\u77f3\u67f1 2",
            ["LavaPillar_Tall"] = "\u9ad8\u7194\u5ca9\u77f3\u67f1",
            ["LavaPillar_Plartform"] = "\u7194\u5ca9\u5e73\u53f0\u77f3\u67f1",
            ["LavaBridge"] = "\u7194\u5ca9\u6865",
            ["LavaRiver Variant_NoLight"] = "\u65e0\u5149\u7194\u5ca9\u6cb3",
            ["Waterfall_Spline"] = "\u7011\u5e03\u66f2\u7ebf",
            ["Jungle_SporeMushroomExplo"] = "\u4e1b\u6797\u7206\u70b8\u5b62\u5b50\u8611\u83c7",
            ["Jungle_SporeMushroom"] = "\u4e1b\u6797\u5b62\u5b50\u8611\u83c7",
            ["Forest_SporeFungus"] = "\u68ee\u6797\u5b62\u5b50\u771f\u83cc",
            ["Plane"] = "\u5e73\u9762",
            ["rock_Snake"] = "\u86c7\u5f62\u5ca9\u77f3",
            ["rock_Ring"] = "\u73af\u5f62\u5ca9\u77f3",
            ["Rock_Wall"] = "\u5899\u9762\u5ca9\u77f3",
            ["Rock_MagmaRock"] = "\u5ca9\u6d46\u5ca9\u77f3",
            ["Rock_Hold2"] = "\u6500\u722c\u5ca9\u70b9 2",
            ["RockFinal"] = "\u6700\u7ec8\u5ca9\u77f3",
            ["RockFinalSmall"] = "\u5c0f\u6700\u7ec8\u5ca9\u77f3",
            ["RockFinal_Forest"] = "\u68ee\u6797\u6700\u7ec8\u5ca9\u77f3",
            ["RockFinal_Desert"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u77f3",
            ["RockFinal_Desert 1"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u77f3 1",
            ["RockFinal_Desert 2"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u77f3 2",
            ["RockFinal_Desert_Bridge"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u6865",
            ["RockFinal_Desert_Bridge 1"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u6865 1",
            ["RockFinal_Desert_Bridge 2"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u6865 2",
            ["RockFinal_Desert_Bridge 3"] = "\u6c99\u6f20\u6700\u7ec8\u5ca9\u6865 3",
            ["RockFinal_Desert_BridgePillar"] = "\u6c99\u6f20\u5ca9\u6865\u67f1",
            ["Basalt Cluster"] = "\u7384\u6b66\u5ca9\u7fa4",
            ["Basalt Cluster 02"] = "\u7384\u6b66\u5ca9\u7fa4 02",
            ["Basalt Column Fixed Variant"] = "\u56fa\u5b9a\u7384\u6b66\u5ca9\u67f1",
            ["M_Foliage_Beach Bark"] = "\u6d77\u6ee9\u6811\u76ae\u6750\u8d28",
            ["M_SaltRock"] = "\u76d0\u5ca9\u6750\u8d28",
            ["M_Forest_rock"] = "\u68ee\u6797\u5ca9\u77f3\u6750\u8d28",
            ["M_Forest_rock_Bald"] = "\u5149\u79c3\u68ee\u6797\u5ca9\u77f3\u6750\u8d28",
            ["M_Wood 1"] = "\u6728\u6750\u6750\u8d28",
            ["M_Rock"] = "\u5ca9\u77f3\u6750\u8d28",
            ["M_Rock_Volcano"] = "\u706b\u5c71\u5ca9\u77f3\u6750\u8d28",
            ["M_DesertSand"] = "\u6c99\u6f20\u6c99\u5730\u6750\u8d28",
            ["M_Foliage_Cactus_tri"] = "\u4ed9\u4eba\u638c\u690d\u88ab\u6750\u8d28",
            ["M_Foliage Pine colony"] = "\u677e\u6811\u7fa4\u843d\u690d\u88ab\u6750\u8d28",
            ["RedWood"] = "\u7ea2\u6749\u6750\u8d28"
        };

        private static readonly object s_loadLock = new object();
        private static bool s_externalLoaded;
        private static string s_localizationPath;

        public static void Initialize(string pluginDirectory)
        {
            if (s_externalLoaded)
            {
                return;
            }

            s_localizationPath = DaPathResolver.ResolveDataFile(pluginDirectory, LocalizationFileName);
            LoadExternalTranslations();
        }

        public static string Translate(string key, string context)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            LoadExternalTranslations();
            if (TryTranslate(key, out string translated))
            {
                return translated;
            }

            DaLog.OnceWarn("missing-loc:" + context + ":" + key.Trim(), "Missing localization mapping: context=" + context + ", key=" + key.Trim());
            return key.Trim();
        }

        public static string TranslateOrOriginal(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            LoadExternalTranslations();
            return TryTranslate(key, out string translated) ? translated : key.Trim();
        }

        private static void LoadExternalTranslations()
        {
            if (s_externalLoaded)
            {
                return;
            }

            lock (s_loadLock)
            {
                if (s_externalLoaded)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(s_localizationPath) || !System.IO.File.Exists(s_localizationPath))
                {
                    s_externalLoaded = true;
                    return;
                }

                try
                {
                    Dictionary<string, string> external = JsonConvert.DeserializeObject<Dictionary<string, string>>(System.IO.File.ReadAllText(s_localizationPath));
                    if (external != null)
                    {
                        foreach (KeyValuePair<string, string> pair in external)
                        {
                            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                            {
                                continue;
                            }

                            s_translations[pair.Key.Trim()] = pair.Value.Trim();
                        }
                    }

                    DaLog.Info("Loaded localization overrides: " + (external != null ? external.Count : 0) + " from " + s_localizationPath);
                }
                catch (Exception ex)
                {
                    DaLog.Warn("Failed to load localization file " + s_localizationPath + ": " + ex.Message);
                }
                finally
                {
                    s_externalLoaded = true;
                }
            }
        }

        private static bool TryTranslate(string key, out string translated)
        {
            translated = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            key = key.Trim();

            if (s_translations.TryGetValue(key, out translated))
            {
                return true;
            }

            string normalized = NormalizeKey(key);
            if (!string.Equals(normalized, key, StringComparison.Ordinal) &&
                s_translations.TryGetValue(normalized, out translated))
            {
                return true;
            }

            if (key.EndsWith("_Segment", StringComparison.Ordinal))
            {
                string shortKey = key.Substring(0, key.Length - "_Segment".Length);
                if (s_translations.TryGetValue(shortKey, out translated))
                {
                    return true;
                }
            }

            if (TryTranslateCatalogPattern(key, out translated))
            {
                return true;
            }

            string compound = TranslateCompoundName(key);
            if (!string.Equals(compound, key, StringComparison.Ordinal))
            {
                translated = compound;
                return true;
            }

            return false;
        }

        private static bool TryTranslateCatalogPattern(string key, out string translated)
        {
            translated = null;
            string normalized = NormalizeKey(key);

            if (TryNumberSuffix(normalized, "Desert_Rock ", out string suffix))
            {
                translated = "\u6c99\u6f20\u5ca9\u77f3 " + suffix;
                return true;
            }

            if (TryNumberSuffix(normalized, "LavaBridge ", out suffix))
            {
                translated = "\u7194\u5ca9\u6865 " + suffix;
                return true;
            }

            if (TryNumberSuffix(normalized, "Ice_Pine ", out suffix))
            {
                translated = "\u51b0\u96ea\u677e\u6811 " + suffix;
                return true;
            }

            if (TryNumberSuffix(normalized, "Ice_DeadShrub ", out suffix))
            {
                translated = "\u51b0\u96ea\u67af\u704c\u6728 " + suffix;
                return true;
            }

            if (TryNumberSuffix(normalized, "BI_Rock", out suffix))
            {
                translated = "\u5ca9\u77f3 " + suffix;
                return true;
            }

            if (TryDottedSuffix(normalized, "Rock_Round.", out suffix) ||
                TryNumberSuffix(normalized, "Rock_Round", out suffix))
            {
                translated = "\u5706\u5f62\u5ca9\u77f3 " + suffix;
                return true;
            }

            if (TryDottedSuffix(normalized, "Rock_Lil.", out suffix) ||
                TryNumberSuffix(normalized, "Rock_Lil", out suffix))
            {
                translated = "\u5c0f\u5ca9\u77f3 " + suffix;
                return true;
            }

            return false;
        }

        private static bool TryNumberSuffix(string key, string prefix, out string suffix)
        {
            suffix = null;
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length <= prefix.Length)
            {
                return false;
            }

            suffix = key.Substring(prefix.Length).Trim();
            return IsDigits(suffix);
        }

        private static bool TryDottedSuffix(string key, string prefix, out string suffix)
        {
            suffix = null;
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length <= prefix.Length)
            {
                return false;
            }

            suffix = key.Substring(prefix.Length).Trim();
            return IsDigits(suffix);
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!char.IsDigit(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeKey(string key)
        {
            string normalized = key.Trim();

            while (normalized.StartsWith("-", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1).TrimStart();
            }

            int suffixIndex = normalized.LastIndexOf(" (", StringComparison.Ordinal);
            if (suffixIndex > 0 && normalized.EndsWith(")", StringComparison.Ordinal))
            {
                string suffix = normalized.Substring(suffixIndex + 2, normalized.Length - suffixIndex - 3);
                if (int.TryParse(suffix, out _))
                {
                    normalized = normalized.Substring(0, suffixIndex).TrimEnd();
                }
            }

            return normalized;
        }

        private static string TranslateCompoundName(string key)
        {
            string normalized = NormalizeKey(key);
            string spaced = normalized.Replace("_", " ").Replace("-", " ");
            string[] parts = spaced.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
            {
                return key;
            }

            string result = string.Empty;
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                if (s_translations.TryGetValue(part, out string translated))
                {
                    result += translated;
                }
                else
                {
                    return key;
                }
            }

            return result;
        }
    }
}


