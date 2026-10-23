using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UprightCockroach;

/// <summary>Only renderers and visual bones are owned here. No gameplay rig is edited.</summary>
internal sealed class UprightCockroachVisual : MonoBehaviour
{
    private sealed class Original
    { internal Renderer Renderer; internal bool ForceOff; }
    private sealed class ExtraBone
    { internal Transform Proxy, Driver; internal Vector3 BasePosition; }
    private sealed class Antenna
    {
        internal Transform Root, Lower, Upper; internal AntennaConfiguration Settings;
        internal Vector3 LowerPosition, UpperPosition, LowerScale, UpperScale; internal Quaternion LowerRotation, UpperRotation;
    }
    private Character _character;
    private SkinnedMeshRenderer _body;
    private RoachModelAsset _asset;
    private readonly List<GameObject> _owned = new List<GameObject>();
    private readonly List<Original> _originals = new List<Original>();
    private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
    private readonly List<Vector3> _rendererPositions = new List<Vector3>();
    private readonly List<Quaternion> _rendererRotations = new List<Quaternion>();
    private readonly List<Vector3> _rendererScales = new List<Vector3>();
    private readonly List<ExtraBone> _extra = new List<ExtraBone>();
    private readonly List<Antenna> _antennae = new List<Antenna>();
    private readonly Dictionary<string, Transform> _live = new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, Matrix4x4> _binds = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
    private Transform _head, _torso;
    private Vector3 _extraOffsetInTorso;
    private bool _built, _failed, _lastFirstPerson;
    private float _retry;
    private int _revision = -1;

    private void Awake() { _character = GetComponent<Character>(); TryBuild(); }

    private void LateUpdate()
    {
        if (_failed || Plugin.IsShuttingDown) return;
        if (!_built) { TryBuild(); return; }
        if (_character == null || _body == null || _head == null || _torso == null) return;
        if (_character.refs?.customization?.refs?.mainRenderer != _body)
        { Fail("The source character renderer changed; retaining the game's replacement visual."); return; }

        Vector3 worldOffset = _torso.TransformVector(_extraOffsetInTorso);
        foreach (var extra in _extra)
            if (extra.Proxy != null && extra.Driver != null)
                extra.Proxy.localPosition = extra.BasePosition + extra.Driver.InverseTransformVector(worldOffset);

        var customization = _character.refs.customization.refs;
        // Respect explicit game transformations and detached death/petrify visuals.
        bool special = _character.data.dead || _character.data.isSkeleton || _character.data.shouldPetrify ||
            (customization.skeletonRenderer != null && customization.skeletonRenderer.gameObject.activeInHierarchy) ||
            (customization.chickenRenderer != null && customization.chickenRenderer.enabled && customization.chickenRenderer.gameObject.activeInHierarchy);
        bool visible = _character.gameObject.activeInHierarchy && !special;
        bool firstPerson = _character.IsLocal && !_character.data.dead && !_character.data.fullyPassedOut;
        // Mirror HideTheBody's carry rule so a carried remote shell does not fill the camera.
        if (!_character.IsLocal && _character.data.carrier != null && _character.data.carrier.IsLocal) visible = false;
        Camera camera = Camera.main;
        if (firstPerson && camera != null && (camera.transform.position - _head.position).sqrMagnitude > 1.44f) firstPerson = false;
        if (_revision != Plugin.ConfigurationRevision || firstPerson != _lastFirstPerson)
        {
            ApplyAntennae(firstPerson);
            ApplyFirstPersonPresentation(firstPerson);
        }
        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] == null) continue;
            int category = _asset.Data.Parts[i].Category;
            bool firstPersonVisible = category == 1 ||
                (category == 2 && Plugin.FirstPersonAntennaVisible.Value) ||
                (category == 3 && Plugin.FirstPersonHeadVisible.Value) ||
                (category == 4 && Plugin.FirstPersonShellVisible.Value);
            _renderers[i].enabled = visible && (!firstPerson || firstPersonVisible);
        }
        // Do not enable stock renderers or interfere with equipment.
        foreach (var original in _originals) if (original.Renderer != null) original.Renderer.forceRenderingOff = special ? original.ForceOff : true;
    }

    private void TryBuild()
    {
        if (_built || _failed || Plugin.IsShuttingDown || _character == null || Time.unscaledTime < _retry) return;
        _retry = Time.unscaledTime + 1f;
        var refs = _character.refs?.customization?.refs;
        if (refs == null || refs.mainRenderer == null || refs.AllRenderers == null) return;
        _body = refs.mainRenderer;
        try
        {
            var bones = _body.bones; var poses = _body.sharedMesh?.bindposes;
            if (poses == null || bones.Length == 0 || poses.Length != bones.Length) return;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && !_live.ContainsKey(bones[i].name))
                { _live.Add(bones[i].name, bones[i]); _binds.Add(bones[i].name, poses[i]); }
            _asset = RoachModelAsset.Get();
            foreach (var bone in _asset.Data.Bones)
                if (bone.Driver.Length != 0 && !_live.ContainsKey(bone.Driver)) throw new InvalidOperationException("Missing Scout bone: " + bone.Driver);
            _head = _live["Head"]; _torso = _live["Torso"];
            var proxies = new Transform[_asset.Data.Bones.Length];
            var named = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var rest = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            foreach (var b in _asset.Data.Bones) rest.Add(b.Name, b.Rest);
            Vector3 extraOffset = rest["ExtraArm_L"].GetColumn(3) - rest["Arm_L"].GetColumn(3);
            _extraOffsetInTorso = _binds["Torso"].MultiplyVector(extraOffset);
            for (int i = 0; i < proxies.Length; i++)
            {
                var b = _asset.Data.Bones[i]; Transform parent; Matrix4x4 local;
                if (b.Driver.Length > 0)
                {
                    parent = _live[b.Driver];
                    // Use the actual renderer's bind pose, not assumptions about local axes.
                    var targetRest = b.Name.StartsWith("Extra", StringComparison.Ordinal) ? rest[b.Driver] : b.Rest;
                    local = _binds[b.Driver] * targetRest;
                }
                else
                {
                    parent = b.Parent == "Head" ? _head : named[b.Parent];
                    local = (b.Parent == "Head" ? _binds["Head"] : rest[b.Parent].inverse) * b.Rest;
                }
                var proxy = Own("RoachBone_" + b.Name, parent);
                SetLocal(proxy.transform, local); proxies[i] = proxy.transform; named.Add(b.Name, proxy.transform);
                if (b.Name.StartsWith("Extra", StringComparison.Ordinal))
                    _extra.Add(new ExtraBone { Proxy = proxy.transform, Driver = parent, BasePosition = proxy.transform.localPosition });
            }
            foreach (string side in new[] { "L", "R" })
            {
                var lower = named["Antenna_" + side + "_Lower"]; var upper = named["Antenna_" + side + "_Upper"];
                var root = Own("RoachAntennaControl_" + side, _head).transform;
                var ant = new Antenna { Root = root, Lower = lower, Upper = upper, Settings = side == "L" ? Plugin.LeftAntenna : Plugin.RightAntenna,
                    LowerPosition = lower.localPosition, LowerRotation = lower.localRotation, UpperPosition = upper.localPosition, UpperRotation = upper.localRotation,
                    LowerScale = lower.localScale, UpperScale = upper.localScale };
                lower.SetParent(root, false); _antennae.Add(ant);
            }
            for (int i = 0; i < _asset.Meshes.Length; i++)
            {
                var part = _asset.Data.Parts[i]; var obj = Own(part.Name, _body.transform); obj.layer = _body.gameObject.layer;
                var renderer = obj.AddComponent<SkinnedMeshRenderer>(); _renderers.Add(renderer); renderer.enabled = false;
                _rendererPositions.Add(obj.transform.localPosition); _rendererRotations.Add(obj.transform.localRotation); _rendererScales.Add(obj.transform.localScale);
                renderer.sharedMesh = _asset.Meshes[i]; renderer.sharedMaterial = _asset.Materials[part.Material]; renderer.bones = proxies;
                renderer.rootBone = _live["Hip"]; renderer.quality = SkinQuality.Bone4;
                renderer.localBounds = new Bounds(new Vector3(0,0,3.5f), new Vector3(14,14,14));
                renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            if (_renderers.Count == 0) throw new InvalidOperationException("No model renderers created.");
            foreach (var renderer in refs.AllRenderers) Remember(renderer);
            var hider = _character.GetComponentInChildren<HideTheBody>(true);
            if (hider != null)
            {
                if (hider.shadowCaster != null) foreach (var r in hider.shadowCaster.GetComponentsInChildren<Renderer>(true)) Remember(r);
                if (hider.shadowCasterHat != null) foreach (var r in hider.shadowCasterHat.GetComponentsInChildren<Renderer>(true)) Remember(r);
            }
            if (_originals.Count == 0) throw new InvalidOperationException("Stock appearance renderers unavailable.");
            ApplyAntennae(false);
            foreach (var original in _originals) original.Renderer.forceRenderingOff = true;
            _built = true;
            Plugin.Log?.LogInfo("Attached textured four-arm cockroach to " + _character.name);
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    private void Remember(Renderer r)
    {
        if (r == null || _originals.Exists(x => x.Renderer == r)) return;
        _originals.Add(new Original { Renderer = r, ForceOff = r.forceRenderingOff });
    }
    private GameObject Own(string name, Transform parent)
    { var obj = new GameObject(name); _owned.Add(obj); obj.transform.SetParent(parent, false); return obj; }
    private static void SetLocal(Transform t, Matrix4x4 m)
    { t.localPosition = m.GetColumn(3); t.localRotation = m.rotation; t.localScale = m.lossyScale; }

    private void ApplyAntennae(bool firstPerson)
    {
        foreach (var a in _antennae)
        {
            var s = a.Settings; string name = (s.AttachmentBone.Value ?? "Head").Trim();
            if (!_live.TryGetValue(name, out var parent))
            {
                parent = _head;
                if (_revision != Plugin.ConfigurationRevision) Plugin.Log?.LogWarning("Unknown antenna bone '" + name + "'; using Head.");
            }
            a.Root.SetParent(parent, false); a.Root.localPosition = s.Position.Value + (firstPerson ? s.FirstPersonOffset.Value : Vector3.zero);
            a.Root.localRotation = Quaternion.Euler(s.Rotation.Value); a.Root.localScale = Vector3.one;
            a.Lower.localPosition = a.LowerPosition + s.LowerPosition.Value;
            a.Lower.localRotation = a.LowerRotation * Quaternion.Euler(s.LowerRotation.Value);
            float length = AntennaConfiguration.Finite(firstPerson ? s.FirstPersonLengthScale.Value : s.LengthScale.Value, 1f);
            float width = AntennaConfiguration.Finite(firstPerson ? s.FirstPersonThicknessScale.Value : s.ThicknessScale.Value, 1f);
            a.Lower.localScale = Vector3.Scale(a.LowerScale, new Vector3(width, length, width));
            a.Upper.localPosition = a.UpperPosition + s.UpperPosition.Value;
            a.Upper.localRotation = a.UpperRotation * Quaternion.Euler(s.UpperRotation.Value);
            a.Upper.localScale = a.UpperScale;
        }
        _revision = Plugin.ConfigurationRevision; _lastFirstPerson = firstPerson;
    }

    private void ApplyFirstPersonPresentation(bool firstPerson)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] == null) continue;
            var t = _renderers[i].transform;
            t.localPosition = _rendererPositions[i];
            t.localRotation = _rendererRotations[i];
            t.localScale = _rendererScales[i];
            if (!firstPerson) continue;
            int category = _asset.Data.Parts[i].Category;
            if (category == 3)
            {
                t.localPosition += Plugin.FirstPersonHeadOffset.Value;
                float scale = AntennaConfiguration.Finite(Plugin.FirstPersonHeadScale.Value, 0.32f);
                t.localScale = Vector3.Scale(t.localScale, new Vector3(scale, scale, scale));
            }
            else if (category == 4)
            {
                t.localPosition += Plugin.FirstPersonShellOffset.Value;
                float scale = AntennaConfiguration.Finite(Plugin.FirstPersonShellScale.Value, 0.9f);
                t.localScale = Vector3.Scale(t.localScale, new Vector3(scale, scale, scale));
            }
        }
    }
    private void Fail(string reason)
    { Cleanup(); _failed = true; Plugin.Log?.LogWarning("Cockroach visual skipped; original appearance retained: " + reason); }
    private void Cleanup()
    {
        foreach (var original in _originals) if (original.Renderer != null) original.Renderer.forceRenderingOff = original.ForceOff;
        _originals.Clear();
        foreach (var obj in _owned) if (obj != null) { obj.SetActive(false); Destroy(obj); }
        _owned.Clear(); _renderers.Clear(); _rendererPositions.Clear(); _rendererRotations.Clear(); _rendererScales.Clear(); _antennae.Clear(); _extra.Clear(); _live.Clear(); _binds.Clear(); _built = false;
    }
    private void OnDestroy() => Cleanup();
}
