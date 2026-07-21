using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using TerrainCustomiserCN.Map;
using TerrainCustomiserCN.Session;
using TerrainCustomiserCN.UI;
using TerrainCustomiserCN.UI.Windows;
using TerrainCustomiserCN.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.Core;
using Object = UnityEngine.Object;

namespace TerrainCustomiserCN.Managers
{
	public class EditorManager : Singleton<EditorManager>
	{
		internal static void Setup()
		{
			SessionState.Set(SessionState.State.InEditor);
			Plugin.Instance.gameObject.AddComponent<EditorManager>();
		}

		public GameObject CreateObject(string name, Transform parent, Vector3 position, Quaternion rotation)
		{
			GameObject gameObject = new GameObject(name);
			bool flag = parent == null;
			if (flag)
			{
				parent = this.currentSegment.customVariant.transform;
			}
			gameObject.transform.SetParent(parent);
			gameObject.transform.position = position;
			gameObject.transform.rotation = rotation;
			return gameObject;
		}

		public GameObject CreateObjectWithComponent(string name, Transform parent, Type type, Vector3 position, Quaternion rotation)
		{
			GameObject gameObject = this.CreateObject(name, parent, position, rotation);
			Component component = gameObject.AddComponent(type);
			return gameObject;
		}

		private void InitializeNullCollections(Component component)
		{
			foreach (FieldInfo fieldInfo in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				Type fieldType = fieldInfo.FieldType;
				bool flag = fieldInfo.GetValue(component) != null;
				if (!flag)
				{
					bool isArray = fieldType.IsArray;
					if (isArray)
					{
						Type elementType = fieldType.GetElementType();
						Array value = Array.CreateInstance(elementType, 0);
						fieldInfo.SetValue(component, value);
					}
					else
					{
						bool flag2 = fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>);
						if (flag2)
						{
							object value2 = Activator.CreateInstance(fieldType);
							fieldInfo.SetValue(component, value2);
						}
					}
				}
			}
		}

		public GameObject DuplicateObject(Transform target)
		{
			Transform transform = Object.Instantiate<Transform>(target);
			bool flag = target.parent != null;
			if (flag)
			{
				transform.SetParent(target.parent);
			}
			return transform.gameObject;
		}

		public void DestroyObject(GameObject go)
		{
			Object.Destroy(go);
		}

		private void SetupCamera()
		{
			CameraOverride cameraOverride = Camera.main.gameObject.AddComponent<CameraOverride>();
			cameraOverride.fov = Camera.main.fieldOfView;
			MainCamera component = Camera.main.GetComponent<MainCamera>();
			bool flag = component != null;
			if (flag)
			{
				Camera.main.GetComponent<MainCamera>().SetCameraOverride(cameraOverride);
				GodCam godcam = Singleton<MainCameraMovement>.Instance.godcam;
				godcam.drag = 10f;
				godcam.force = 100f;
				godcam.lookDrag = 10f;
				godcam.lookSens = 100f;
			}
		}

		private void Update()
		{
			this.HandleInput();
			this.TryDrawGizmos();
		}

		private void TryDrawGizmos()
		{
			bool isEnabled = EditorGizmos.IsEnabled;
			if (isEnabled)
			{
				bool flag = this.editorWindow.hierarchy.Selected != null;
				if (flag)
				{
					LevelGenStep component = this.editorWindow.hierarchy.Selected.gameObject.GetComponent<LevelGenStep>();
					GizmoUtils.TryDrawGizmos(this.editorWindow.hierarchy.Selected, component);
					GizmoUtils.TryDrawOverlays(this.editorWindow.hierarchy.Selected);
				}
			}
		}

		private void HandleInput()
		{
			bool mouseButtonDown = UnityInput.Current.GetMouseButtonDown(1);
			if (mouseButtonDown)
			{
				this.SetGodCamActive(true);
				this.menuWindow.Hide();
			}
			bool mouseButtonUp = UnityInput.Current.GetMouseButtonUp(1);
			if (mouseButtonUp)
			{
				this.SetGodCamActive(false);
				this.menuWindow.Show();
			}
			bool flag = EditorManager.action_pause.WasPressedThisFrame();
			if (flag)
			{
				GUIManager.instance.pauseMenu.SetActive(!GUIManager.instance.pauseMenu.activeSelf);
			}
		}

		private void SetGodCamActive(bool active)
		{
			Singleton<MainCameraMovement>.Instance.isGodCam = active;
			GodCam godcam = Singleton<MainCameraMovement>.Instance.godcam;
			bool flag = !active;
			if (flag)
			{
				godcam.lookVel = Vector2.zero;
				godcam.vel = Vector3.zero;
				godcam.targetFov = Singleton<MainCameraMovement>.Instance.currentFov;
			}
		}

		public void SetCurrentSegment(MapData.BiomeSegment biomeSegment)
		{
			this.currentSegment = biomeSegment;
			foreach (MapHandler.MapSegment mapSegment in Singleton<MapHandler>.Instance.segments)
			{
				bool flag = mapSegment.segmentParent.transform == biomeSegment.transform;
				if (flag)
				{
					mapSegment.segmentParent.SetActive(true);
					bool flag2 = mapSegment.segmentCampfire != null;
					if (flag2)
					{
						mapSegment.segmentCampfire.SetActive(true);
					}
					Camera.main.transform.position = mapSegment.reconnectSpawnPos.transform.position + new Vector3(0f, 25f, 0f);
					bool flag3 = mapSegment.wallNext != null;
					if (flag3)
					{
						Camera.main.transform.LookAt(mapSegment.wallNext.transform);
					}
				}
				else
				{
					mapSegment.segmentParent.SetActive(false);
					bool flag4 = mapSegment.segmentCampfire != null;
					if (flag4)
					{
						mapSegment.segmentCampfire.SetActive(false);
					}
				}
			}
		}

		public void GenerateCurrentSegment()
		{
			this.currentSegment.transform.GetComponentInParent<PropGrouper>().RunAll(false);
			EditorGizmos.RefreshGizmos();
		}

		public void ChangeBiome(MapData.BiomeSection biomeSection, MapData.BiomeOption targetBiome)
		{
			MapManager.ChangeBiome(biomeSection, targetBiome);
			this.SetCurrentSegment(targetBiome.segments.FirstOrDefault<MapData.BiomeSegment>());
		}

		protected override void Awake()
		{
			base.Awake();
		}

		private void Start()
		{
			this.PrepareEditorMode();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			WindowsManager.UnregisterAll();
		}

		public void EndSession()
		{
			this.menuWindow.Close();
			Object.Destroy(this);
		}

		public void LoadSave()
		{
		}

		private void PrepareEditorMode()
		{
			EditorManager.action_pause = InputSystem.actions.FindAction("Pause", false);
			this.DisableFog();
			this.DisableScoutmaster();
			this.DisableCharacter();
			this.DisableGameUI();
			MapManager.DisableInactiveSegments();
			Singleton<MapHandler>.Instance.globalParent.GetComponent<PropGrouper>().ClearAll();
			this.menuWindow = new GameObject("MenuWindow")
			{
				transform = 
				{
					parent = base.gameObject.transform
				}
			}.AddComponent<MenuWindow>();
			WindowsManager.Register<InspectorWindow>();
			WindowsManager.Register<FreeCamWindow>();
			WindowsManager.Register<EditorWindow>();
			WindowsManager.Register<ResourceWindow>();
			bool flag = this.menuWindow != null;
			if (flag)
			{
				this.menuWindow.Show();
			}
			this.editorWindow = WindowsManager.Get<EditorWindow>();
			HierarchyView hierarchy = this.editorWindow.hierarchy;
			hierarchy.OnSelectionChanged = (Action<Transform>)Delegate.Combine(hierarchy.OnSelectionChanged, new Action<Transform>(EditorGizmos.OnSelectionChanged));
		}

		private void DisableFog()
		{
			GameObject gameObject = GameObject.Find("Misc/Post Fog");
			bool flag = gameObject != null;
			if (flag)
			{
				gameObject.SetActive(false);
			}
			GameObject gameObject2 = GameObject.Find("FogSphereSystem");
			bool flag2 = gameObject2 != null;
			if (flag2)
			{
				gameObject2.SetActive(false);
			}
		}

		private void DisableScoutmaster()
		{
			Scoutmaster scoutmaster;
			bool primaryScoutmaster = Scoutmaster.GetPrimaryScoutmaster(out scoutmaster);
			if (primaryScoutmaster)
			{
				scoutmaster.gameObject.SetActive(false);
			}
		}

		private void DisableCharacter()
		{
			bool flag = Character.localCharacter != null;
			if (flag)
			{
				Character.localCharacter.gameObject.SetActive(false);
				Character.localCharacter = null;
			}
		}

		private void DisableGameUI()
		{
			bool flag = GUIManager.instance != null && GUIManager.instance.hudCanvas != null;
			if (flag)
			{
				GUIManager.instance.hudCanvas.gameObject.SetActive(false);
			}
			bool flag2 = GamefeelHandler.instance != null;
			if (flag2)
			{
				PerlinShake componentInChildren = GamefeelHandler.instance.gameObject.GetComponentInChildren<PerlinShake>();
				bool flag3 = componentInChildren != null;
				if (flag3)
				{
					componentInChildren.gameObject.SetActive(false);
				}
			}
		}

		public List<Type> levelGenStepTypes = new List<Type>
		{
			typeof(PropSpawner),
			typeof(PropSpawner_Line),
			typeof(PropSpawner_Sphere),
			typeof(PropDeleter)
		};

		private MenuWindow menuWindow;

		public int currentSegmentIndex = 0;

		public MapData.BiomeSegment currentSegment;

		private EditorWindow editorWindow;

		public static InputAction action_pause;
	}
}
