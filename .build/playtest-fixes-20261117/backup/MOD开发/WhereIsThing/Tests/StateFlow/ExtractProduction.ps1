param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$sourceRoot=Join-Path $PSScriptRoot '../../WhereIsThing'
function Read-Body([string]$file,[string]$name,[bool]$type=$false) {
    $source=[IO.File]::ReadAllText((Join-Path $sourceRoot $file))
    if ($type) { $pattern='(?m)^        (?:private|public|internal) (?:sealed )?class '+[regex]::Escape($name)+'\b' }
    else { $pattern='(?m)^        (?:private|public|internal) (?:static )?[^\r\n]+\b'+[regex]::Escape($name)+'(?:<[^>]+>)?\(' }
    $match=[regex]::Match($source,$pattern)
    if (-not $match.Success) { throw "Missing production method/type: $name" }
    $cursor=$source.IndexOf('{',$match.Index+$match.Length)+1
    $depth=1
    while($depth -gt 0) {
        if ($source[$cursor] -eq '{') {$depth++}
        if ($source[$cursor] -eq '}') {$depth--}
        $cursor++
    }
    return $source.Substring($match.Index,$cursor-$match.Index)
}
$parts=[Collections.Generic.List[string]]::new()
$parts.Add('using System; using System.Linq; using System.Collections.Generic; using UnityEngine; using Photon.Pun; using Stopwatch = System.Diagnostics.Stopwatch; namespace WhereIsThing { public sealed partial class WhereIsThingPlugin {')
foreach($name in @('Update','BeginScan','SaveLocalPresets','SelectLocalPreset','SelectSharedPreset','CycleLocalScanMode','AdjustLocalDisplayDuration','SetMaxDistance','SetMapScope','SetLocationScope','SetShowOwnerNames','MigratePlacedSceneTargetsToItems','MigratePlacedSceneTarget','SerializeSceneTargetTypes','SerializeLuggageTypes','RefreshActiveSceneTargets','AddSceneLabel','GetMainCamera')) {
    $parts.Add((Read-Body 'WhereIsThingPlugin.cs' $name))
}
$parts.Add((Read-Body 'WhereIsThingPlugin.cs' 'NetworkObjectRecord' $true))
$parts.Add((Read-Body 'WhereIsThingPlugin.cs' 'NetworkLabelBinding' $true))
$parts.Add((Read-Body 'WhereIsThingPlugin.cs' 'PlacedTargetRecord' $true))
$parts.Add((Read-Body 'WhereIsThingPlugin.Discovery.cs' 'SameClassification'))
$parts.Add((Read-Body 'WhereIsThingPlugin.Discovery.cs' 'PrepareNetworkLabel'))
$parts.Add((Read-Body 'WhereIsThingPlugin.Discovery.cs' 'ReconcileNetworkRecordLabels'))
$parts.Add((Read-Body 'WhereIsThingPlugin.Discovery.cs' 'ProcessClassificationRetries'))
$parts.Add((Read-Body 'WhereIsThingPlugin.Discovery.cs' 'RemoveNetworkRecordLabels'))
$parts.Add('} internal partial class TestPicker {')
$parts.Add((Read-Body 'ThingPresetPickerWindow.cs' 'Tick'))
$parts.Add('} internal partial class TestLabelText {')
$parts.Add((Read-Body 'ThingLabel.cs' 'SetText'))
$parts.Add('} }')
$parent=Split-Path -Parent $Destination
[IO.Directory]::CreateDirectory($parent) | Out-Null
[IO.File]::WriteAllText($Destination,($parts -join [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
