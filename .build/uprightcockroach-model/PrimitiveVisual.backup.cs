using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UprightCockroach;

/// <summary>
/// Client-local visual shell. The original Character, Animator, ragdoll, IK,
/// colliders and PhotonView remain untouched; this component only owns renderers.
/// </summary>
internal sealed class UprightCockroachVisual : MonoBehaviour
{
    private const string RootName = "UprightCockroachVisual";
    private readonly List<Renderer> _originalRenderers = new List<Renderer>();
    private readonly List<bool> _originalRendererStates = new List<bool>();
    private readonly List<Renderer> _shellRenderers = new List<Renderer>();
    private readonly List<GameObject> _parts = new List<GameObject>();
    private Character _character;
    private Transform _hip;
    private Transform _torso;
    private Transform _head;
    private Material _shellMaterial;
    private Material _wingMaterial;
    private Material _eyeMaterial;
    private bool _built;
    private bool _failed;
    private float _nextBuildAttempt;
    private int _configurationRevision = -1;
    private bool _lastFirstPerson;
    private readonly List<AntennaParts> _antennae = new List<AntennaParts>();

    private sealed class AntennaParts
    {
        internal Transform Root, Lower, Upper;
        internal AntennaConfiguration Settings;
    }

    private void Awake()
    {
        _character = GetComponent<Character>();
        TryBuild();
    }

    private void LateUpdate()
    {
        if (Plugin.IsShuttingDown || _failed) return;
        if (!_built)
        {
            TryBuild();
            return;
        }

        // Placeholder camera-distance policy. Full HideTheBody state integration
        // remains a separate in-game validation task.
        bool visible = _character != null && _character.gameObject.activeInHierarchy;
        bool firstPerson = visible && _character != null && _character.IsLocal && _head != null && Camera.main != null &&
            (Camera.main.transform.position - _head.position).sqrMagnitude <= 0.64f;
        if (_configurationRevision != Plugin.ConfigurationRevision || firstPerson != _lastFirstPerson)
            ApplyAntennaConfiguration(firstPerson);

        for (int i = 0; i < _shellRenderers.Count; i++)
        {
            if (_shellRenderers[i] != null)
            {
                _shellRenderers[i].enabled = visible && (!firstPerson || IsFirstPersonPart(_shellRenderers[i]));
            }
        }

        // CharacterCustomization can refresh the stock renderers after Start
        // (for example when cosmetics arrive). The visual replacement owns
        // their visibility, so keep them hidden without changing their state.
        for (int i = 0; i < _originalRenderers.Count; i++)
        {
            if (_originalRenderers[i] != null)
            {
                _originalRenderers[i].forceRenderingOff = true;
            }
        }
    }

    private static bool IsFirstPersonPart(Renderer renderer)
    {
        string name = renderer.gameObject.name;
        // Keep a readable roach silhouette in first person: arms/hands,
        // antennae, wings and the lower abdomen. Hide the face/head because it
        // would sit inside the camera and obscure the game view.
        return name.StartsWith("Roach_Arm_", StringComparison.Ordinal) ||
            name.StartsWith("Roach_UpperArm_", StringComparison.Ordinal) ||
            name.StartsWith("Roach_Forearm_", StringComparison.Ordinal) ||
            name.StartsWith("Roach_Hand_", StringComparison.Ordinal) ||
            name.StartsWith("Roach_Antenna_", StringComparison.Ordinal) ||
            name.Equals("Roach_Abdomen", StringComparison.Ordinal) ||
            name.StartsWith("Roach_Wing_", StringComparison.Ordinal);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _originalRenderers.Count; i++)
        {
            if (_originalRenderers[i] != null)
            {
                _originalRenderers[i].forceRenderingOff = _originalRendererStates[i];
            }
        }

        CleanupGeneratedParts();
    }

    private void CleanupGeneratedParts()
    {
        for (int i = 0; i < _parts.Count; i++)
        {
            if (_parts[i] != null)
            {
                _parts[i].SetActive(false);
                Destroy(_parts[i]);
            }
        }
        _parts.Clear();
        _shellRenderers.Clear();
        _antennae.Clear();

        if (_shellMaterial != null)
        {
            Destroy(_shellMaterial);
        }
        if (_wingMaterial != null)
        {
            Destroy(_wingMaterial);
        }
        if (_eyeMaterial != null)
        {
            Destroy(_eyeMaterial);
        }
    }

    private void TryBuild()
    {
        if (_built || _failed || _character == null || Plugin.IsShuttingDown || Time.unscaledTime < _nextBuildAttempt)
        {
            return;
        }
        _nextBuildAttempt = Time.unscaledTime + 1f;

        _hip = FindBone("Hip", "Hips", "Pelvis");
        _torso = FindBone("Torso", "Chest", "Mid") ?? _hip;
        _head = FindBone("Head", "Neck") ?? _torso;
        if (_hip == null || _torso == null || _head == null)
        {
            return;
        }

        if (_character.refs?.customization?.refs?.AllRenderers == null) return;
        try
        {
            CreateMaterials();
            if (_shellMaterial == null || _wingMaterial == null || _eyeMaterial == null)
                throw new InvalidOperationException("Required shell material unavailable.");
            BuildShell();
            ApplyAntennaConfiguration(false);
            if (_shellRenderers.Count == 0 || !CacheAndHideOriginalRenderers())
                throw new InvalidOperationException("Replacement renderers or original renderer references unavailable.");
            _built = true;
            Plugin.Log?.LogInfo($"Attached upright cockroach visual to {_character.name}.");
        }
        catch (Exception ex)
        {
            for (int i = 0; i < _originalRenderers.Count; i++)
                if (_originalRenderers[i] != null) _originalRenderers[i].forceRenderingOff = _originalRendererStates[i];
            _originalRenderers.Clear();
            _originalRendererStates.Clear();
            CleanupGeneratedParts();
            _failed = true;
            Plugin.Log?.LogWarning($"Cockroach visual skipped; original renderers retained: {ex.Message}");
        }
    }

    private Transform FindBone(params string[] names)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        // Prefer an exact Scout bone name. Several cosmetic/helper objects can
        // contain the same word (for example HeadShadow), and must not win the
        // mapping merely because they appear earlier in the hierarchy.
        for (int i = 0; i < transforms.Length; i++)
        {
            string candidate = transforms[i].name;
            for (int j = 0; j < names.Length; j++)
            {
                if (string.Equals(candidate, names[j], StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            string candidate = transforms[i].name;
            for (int j = 0; j < names.Length; j++)
            {
                if (candidate.IndexOf(names[j], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return transforms[i];
                }
            }
        }

        return null;
    }

    private bool CacheAndHideOriginalRenderers()
    {
        // CharacterCustomization exposes the exact renderer list used by the
        // game's own skeleton/mushroom swaps. Reuse that list so backpacks,
        // held items and other gameplay visuals remain visible.
        CustomizationRefs customizationRefs = null;
        if (_character != null && _character.refs != null && _character.refs.customization != null)
        {
            customizationRefs = _character.refs.customization.refs;
        }

        if (customizationRefs == null || customizationRefs.AllRenderers == null)
        {
            Plugin.Log?.LogDebug("Customization renderer references are not ready; retrying cockroach visual build.");
            return false;
        }

        Renderer[] renderers = customizationRefs.AllRenderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform.name == RootName || _originalRenderers.Contains(renderer))
            {
                continue;
            }

            _originalRenderers.Add(renderer);
            _originalRendererStates.Add(renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }

        return true;
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("W/Character") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Plugin.Log?.LogWarning("No supported Unity shader was found; upright cockroach visual was skipped.");
            return;
        }
        _shellMaterial = new Material(shader) { name = "UprightCockroach Shell" };
        _shellMaterial.color = new Color(0.12f, 0.045f, 0.018f, 1f);
        _wingMaterial = new Material(shader) { name = "UprightCockroach Wings" };
        _wingMaterial.color = new Color(0.28f, 0.10f, 0.035f, 0.78f);
        _wingMaterial.SetFloat("_Mode", 3f);
        _wingMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _wingMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _wingMaterial.SetInt("_ZWrite", 0);
        _wingMaterial.renderQueue = 3000;
        _eyeMaterial = new Material(shader) { name = "UprightCockroach Eyes" };
        _eyeMaterial.color = new Color(0.75f, 0.38f, 0.035f, 1f);
    }

    private void BuildShell()
    {
        // Every part is parented to a live humanoid bone, so the stock Animator,
        // ragdoll and carry animations move the roach automatically.
        CreatePart(PrimitiveType.Sphere, _hip, "Roach_Abdomen", new Vector3(0f, 0.10f, 0f),
            new Vector3(0.62f, 0.92f, 0.42f), _shellMaterial);
        CreatePart(PrimitiveType.Sphere, _torso, "Roach_Thorax", new Vector3(0f, 0.26f, -0.01f),
            new Vector3(0.56f, 0.58f, 0.46f), _shellMaterial);
        CreatePart(PrimitiveType.Sphere, _head, "Roach_Head", new Vector3(0f, 0.04f, 0.03f),
            new Vector3(0.46f, 0.40f, 0.44f), _shellMaterial);

        CreatePart(PrimitiveType.Capsule, _torso, "Roach_Wing_L", new Vector3(-0.22f, 0.04f, -0.16f),
            new Vector3(0.25f, 0.62f, 0.06f), _wingMaterial, Quaternion.Euler(0f, -18f, -14f));
        CreatePart(PrimitiveType.Capsule, _torso, "Roach_Wing_R", new Vector3(0.22f, 0.04f, -0.16f),
            new Vector3(0.25f, 0.62f, 0.06f), _wingMaterial, Quaternion.Euler(0f, 18f, 14f));

        CreatePart(PrimitiveType.Sphere, _head, "Roach_Eye_L", new Vector3(-0.17f, 0.08f, 0.20f),
            new Vector3(0.11f, 0.13f, 0.06f), _eyeMaterial);
        CreatePart(PrimitiveType.Sphere, _head, "Roach_Eye_R", new Vector3(0.17f, 0.08f, 0.20f),
            new Vector3(0.11f, 0.13f, 0.06f), _eyeMaterial);

        BuildLimbs();

        CreateAntenna(-1f);
        CreateAntenna(1f);
    }

    private void BuildLimbs()
    {
        Transform legL = FindBone("Leg_L");
        Transform legR = FindBone("Leg_R");
        Transform footL = FindBone("Foot_L");
        Transform footR = FindBone("Foot_R");
        Transform armL = FindBone("Arm_L");
        Transform armR = FindBone("Arm_R");

        CreateBoneSegment(legL, FindBone("Knee_L"), "Roach_Thigh_L");
        CreateBoneSegment(legR, FindBone("Knee_R"), "Roach_Thigh_R");
        CreateBoneSegment(FindBone("Knee_L"), footL, "Roach_Shin_L");
        CreateBoneSegment(FindBone("Knee_R"), footR, "Roach_Shin_R");
        if (footL != null)
        {
            CreatePart(PrimitiveType.Sphere, footL, "Roach_Foot_L", Vector3.zero,
                new Vector3(0.16f, 0.10f, 0.22f), _shellMaterial);
        }
        if (footR != null)
        {
            CreatePart(PrimitiveType.Sphere, footR, "Roach_Foot_R", Vector3.zero,
                new Vector3(0.16f, 0.10f, 0.22f), _shellMaterial);
        }
        CreateBoneSegment(armL, FindBone("Elbow_L"), "Roach_UpperArm_L");
        CreateBoneSegment(armR, FindBone("Elbow_R"), "Roach_UpperArm_R");
        CreateBoneSegment(FindBone("Elbow_L"), FindBone("Hand_L"), "Roach_Forearm_L");
        CreateBoneSegment(FindBone("Elbow_R"), FindBone("Hand_R"), "Roach_Forearm_R");
        Transform handL = FindBone("Hand_L");
        Transform handR = FindBone("Hand_R");
        if (handL != null)
        {
            CreatePart(PrimitiveType.Sphere, handL, "Roach_Hand_L", Vector3.zero,
                new Vector3(0.14f, 0.12f, 0.14f), _shellMaterial);
        }
        if (handR != null)
        {
            CreatePart(PrimitiveType.Sphere, handR, "Roach_Hand_R", Vector3.zero,
                new Vector3(0.14f, 0.12f, 0.14f), _shellMaterial);
        }
    }

    private void CreateBoneSegment(Transform start, Transform end, string name)
    {
        if (start == null || end == null)
        {
            return;
        }

        Vector3 localEnd = start.InverseTransformPoint(end.position);
        float length = localEnd.magnitude;
        if (length < 0.001f)
        {
            return;
        }

        CreatePart(PrimitiveType.Capsule, start, name, localEnd * 0.5f,
            new Vector3(0.10f, length * 0.5f, 0.10f), _shellMaterial,
            Quaternion.FromToRotation(Vector3.up, localEnd.normalized));
    }

    private void CreateAntenna(float side)
    {
        var root = new GameObject(side < 0 ? "Roach_AntennaRoot_L" : "Roach_AntennaRoot_R");
        root.transform.SetParent(_head, false);
        _parts.Add(root);
        GameObject lower = CreatePart(PrimitiveType.Cylinder, root.transform, side < 0 ? "Roach_Antenna_L1" : "Roach_Antenna_R1",
            new Vector3(0.16f * side, 0.20f, 0.08f), new Vector3(0.025f, 0.25f, 0.025f), _shellMaterial,
            Quaternion.Euler(0f, side * 24f, side * -18f));
        GameObject upper = CreatePart(PrimitiveType.Cylinder, root.transform, side < 0 ? "Roach_Antenna_L2" : "Roach_Antenna_R2",
            new Vector3(0.22f * side, 0.43f, 0.18f), new Vector3(0.018f, 0.23f, 0.018f), _shellMaterial,
            Quaternion.Euler(0f, side * 20f, side * 14f));
        _antennae.Add(new AntennaParts { Root = root.transform, Lower = lower.transform, Upper = upper.transform,
            Settings = side < 0 ? Plugin.LeftAntenna : Plugin.RightAntenna });
    }

    private void ApplyAntennaConfiguration(bool firstPerson)
    {
        foreach (AntennaParts antenna in _antennae)
        {
            AntennaConfiguration settings = antenna.Settings;
            if (settings == null || antenna.Root == null) continue;
            Transform parent = _head;
            string boneName = (settings.AttachmentBone.Value ?? "Head").Trim();
            if (!string.Equals(boneName, "Head", StringComparison.OrdinalIgnoreCase))
            {
                parent = null;
                var body = _character.refs?.customization?.refs?.mainRenderer;
                if (body != null)
                    foreach (Transform bone in body.bones)
                        if (bone != null && string.Equals(bone.name, boneName, StringComparison.OrdinalIgnoreCase))
                        { parent = bone; break; }
                if (parent == null)
                {
                    parent = _head;
                    if (_configurationRevision != Plugin.ConfigurationRevision)
                        Plugin.Log?.LogWarning($"Antenna bone '{boneName}' not found; using Head.");
                }
            }
            antenna.Root.SetParent(parent, false);
            antenna.Root.localPosition = settings.Position.Value + (firstPerson ? settings.FirstPersonOffset.Value : Vector3.zero);
            antenna.Root.localRotation = Quaternion.Euler(settings.Rotation.Value);
            antenna.Root.localScale = Vector3.one;
            antenna.Lower.localPosition = settings.LowerPosition.Value;
            antenna.Lower.localRotation = Quaternion.Euler(settings.LowerRotation.Value);
            antenna.Upper.localPosition = settings.UpperPosition.Value;
            antenna.Upper.localRotation = Quaternion.Euler(settings.UpperRotation.Value);
            float length = AntennaConfiguration.Finite(settings.LengthScale.Value, 1f);
            float width = AntennaConfiguration.Finite(settings.ThicknessScale.Value, 1f);
            antenna.Lower.localScale = new Vector3(0.025f * width, 0.25f * length, 0.025f * width);
            antenna.Upper.localScale = new Vector3(0.018f * width, 0.23f * length, 0.018f * width);
        }
        _configurationRevision = Plugin.ConfigurationRevision;
        _lastFirstPerson = firstPerson;
    }

    private GameObject CreatePart(PrimitiveType type, Transform parent, string name, Vector3 position,
        Vector3 scale, Material material, Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        _parts.Add(part);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        _shellRenderers.Add(renderer);
        return part;
    }
}
