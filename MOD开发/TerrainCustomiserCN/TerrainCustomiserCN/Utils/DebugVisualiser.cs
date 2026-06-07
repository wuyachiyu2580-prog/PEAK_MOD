using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TerrainCustomiserCN.Utils
{
	public class DebugVisualiser : MonoBehaviour
	{
		private void Awake()
		{
			bool flag = DebugVisualiser._instance == null;
			if (flag)
			{
				DebugVisualiser._instance = this;
			}
			else
			{
				Object.Destroy(base.gameObject);
			}
			this.lineMaterialNormal = new Material(Shader.Find("Hidden/Internal-Colored"));
			this.lineMaterialOverlay = new Material(Shader.Find("Hidden/Internal-Colored"));
			this.lineMaterialOverlay.SetInt("_ZTest", 8);
			RenderPipelineManager.endCameraRendering += this.OnEndCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.endCameraRendering -= this.OnEndCameraRendering;
		}

		private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
		{
			bool flag = this._shapes.Count == 0;
			if (!flag)
			{
				this.DrawShapes(false, this.lineMaterialNormal);
				this.DrawShapes(true, this.lineMaterialOverlay);
			}
		}

		public static void DrawRay(Vector3 from, Vector3 dir, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			bool flag = DebugVisualiser._instance == null;
			if (flag)
			{
				DebugVisualiser.CreateInstance();
			}
			DebugVisualiser._instance._shapes.Add(new DebugVisualiser.RayShape(from, dir, color, duration, alwaysOnTop));
		}

		public static void DrawSphere(Vector3 pos, float radius, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			bool flag = DebugVisualiser._instance == null;
			if (flag)
			{
				DebugVisualiser.CreateInstance();
			}
			DebugVisualiser._instance._shapes.Add(new DebugVisualiser.SphereShape(pos, radius, color, duration, alwaysOnTop));
		}

		public static void DrawLine(Vector3 from, Vector3 to, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			bool flag = DebugVisualiser._instance == null;
			if (flag)
			{
				DebugVisualiser.CreateInstance();
			}
			DebugVisualiser._instance._shapes.Add(new DebugVisualiser.LineShape(from, to, color, duration, alwaysOnTop));
		}

		public static void DrawBox(Vector3 center, Vector3 size, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			DebugVisualiser.DrawBox(center, size, Quaternion.identity, color, duration, alwaysOnTop);
		}

		public static void DrawBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			bool flag = DebugVisualiser._instance == null;
			if (flag)
			{
				DebugVisualiser.CreateInstance();
			}
			DebugVisualiser._instance._shapes.Add(new DebugVisualiser.BoxShape(center, size, rotation, color, duration, alwaysOnTop));
		}

		public static void DrawMeshCollider(MeshCollider collider, Color color, float duration = 0f, bool alwaysOnTop = false)
		{
			bool flag = collider == null || collider.sharedMesh == null;
			if (!flag)
			{
				bool flag2 = DebugVisualiser._instance == null;
				if (flag2)
				{
					DebugVisualiser.CreateInstance();
				}
				DebugVisualiser._instance._shapes.Add(new DebugVisualiser.MeshShape(collider.sharedMesh, collider.transform, color, duration, alwaysOnTop));
			}
		}

		private static void CreateInstance()
		{
			DebugVisualiser._instance = new GameObject("TCCN_DebugVisualiser")
			{
				hideFlags = HideFlags.HideAndDontSave
			}.AddComponent<DebugVisualiser>();
		}

		private void DrawShapes(bool overlay, Material mat)
		{
			mat.SetPass(0);
			GL.Begin(1);
			for (int i = this._shapes.Count - 1; i >= 0; i--)
			{
				bool expired = this._shapes[i].Expired;
				if (expired)
				{
					this._shapes.RemoveAt(i);
				}
				else
				{
					bool flag = this._shapes[i].alwaysOnTop != overlay;
					if (!flag)
					{
						this._shapes[i].Draw();
					}
				}
			}
			GL.End();
		}

		private static DebugVisualiser _instance;

		private List<DebugVisualiser.Shape> _shapes = new List<DebugVisualiser.Shape>();

		private Material lineMaterialNormal;

		private Material lineMaterialOverlay;

		private abstract class Shape
		{
			public bool Expired
			{
				get
				{
					return (this.duration <= 0f) ? (Time.frameCount > this.startFrame) : (Time.time - this.startTime > this.duration);
				}
			}

			protected Shape(Color color, float duration, bool alwaysOnTop)
			{
				this.color = color;
				this.duration = duration;
				this.alwaysOnTop = alwaysOnTop;
				this.startTime = Time.time;
				this.startFrame = Time.frameCount;
			}

			public abstract void Draw();

			public Color color;

			public float duration;

			public float startTime;

			private int startFrame;

			public bool alwaysOnTop;
		}

		private class MeshShape : DebugVisualiser.Shape
		{
			public MeshShape(Mesh mesh, Transform transform, Color color, float duration, bool alwaysOnTop) : base(color, duration, alwaysOnTop)
			{
				this.mesh = mesh;
				this.transform = transform;
			}

			public override void Draw()
			{
				bool flag = this.mesh == null || this.transform == null;
				if (!flag)
				{
					Vector3[] vertices = this.mesh.vertices;
					int[] triangles = this.mesh.triangles;
					for (int i = 0; i < triangles.Length; i += 3)
					{
						int num = triangles[i];
						int num2 = triangles[i + 1];
						int num3 = triangles[i + 2];
						Vector3 vector = this.transform.TransformPoint(vertices[num]);
						Vector3 vector2 = this.transform.TransformPoint(vertices[num2]);
						Vector3 vector3 = this.transform.TransformPoint(vertices[num3]);
						GL.Color(this.color);
						GL.Vertex(vector);
						GL.Vertex(vector2);
						GL.Vertex(vector2);
						GL.Vertex(vector3);
						GL.Vertex(vector3);
						GL.Vertex(vector);
					}
				}
			}

			private Mesh mesh;

			private Transform transform;
		}

		private class BoxShape : DebugVisualiser.Shape
		{
			public BoxShape(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration, bool alwaysOnTop) : base(color, duration, alwaysOnTop)
			{
				this.center = center;
				this.size = size;
				this.rotation = rotation;
			}

			public override void Draw()
			{
				Vector3 vector = this.size * 0.5f;
				Vector3[] array = new Vector3[]
				{
					new Vector3(-vector.x, -vector.y, -vector.z),
					new Vector3(vector.x, -vector.y, -vector.z),
					new Vector3(vector.x, -vector.y, vector.z),
					new Vector3(-vector.x, -vector.y, vector.z),
					new Vector3(-vector.x, vector.y, -vector.z),
					new Vector3(vector.x, vector.y, -vector.z),
					new Vector3(vector.x, vector.y, vector.z),
					new Vector3(-vector.x, vector.y, vector.z)
				};
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = this.center + this.rotation * array[i];
				}
				int[,] array2 = new int[,]
				{
					{
						0,
						1
					},
					{
						1,
						2
					},
					{
						2,
						3
					},
					{
						3,
						0
					},
					{
						4,
						5
					},
					{
						5,
						6
					},
					{
						6,
						7
					},
					{
						7,
						4
					},
					{
						0,
						4
					},
					{
						1,
						5
					},
					{
						2,
						6
					},
					{
						3,
						7
					}
				};
				GL.Color(this.color);
				for (int j = 0; j < array2.GetLength(0); j++)
				{
					GL.Vertex(array[array2[j, 0]]);
					GL.Vertex(array[array2[j, 1]]);
				}
			}

			private Vector3 center;

			private Vector3 size;

			private Quaternion rotation;
		}

		private class RayShape : DebugVisualiser.Shape
		{
			public RayShape(Vector3 from, Vector3 dir, Color color, float duration, bool alwaysOnTop) : base(color, duration, alwaysOnTop)
			{
				this.from = from;
				this.dir = dir;
			}

			public override void Draw()
			{
				GL.Color(this.color);
				GL.Vertex(this.from);
				GL.Vertex(this.from + this.dir);
			}

			private Vector3 from;

			private Vector3 dir;
		}

		private class LineShape : DebugVisualiser.Shape
		{
			public LineShape(Vector3 from, Vector3 to, Color color, float duration, bool alwaysOnTop) : base(color, duration, alwaysOnTop)
			{
				this.from = from;
				this.to = to;
			}

			public override void Draw()
			{
				GL.Color(this.color);
				GL.Vertex(this.from);
				GL.Vertex(this.to);
			}

			private Vector3 from;

			private Vector3 to;
		}

		private class SphereShape : DebugVisualiser.Shape
		{
			public SphereShape(Vector3 pos, float radius, Color color, float duration, bool alwaysOnTop) : base(color, duration, alwaysOnTop)
			{
				this.pos = pos;
				this.radius = radius;
			}

			public override void Draw()
			{
				int num = 24;
				for (int i = 0; i < num; i++)
				{
					float num2 = (float)i / (float)num * 3.1415927f * 2f;
					float num3 = (float)(i + 1) / (float)num * 3.1415927f * 2f;
					Vector3 vector = this.pos + new Vector3(Mathf.Cos(num2), 0f, Mathf.Sin(num2)) * this.radius;
					Vector3 vector2 = this.pos + new Vector3(Mathf.Cos(num3), 0f, Mathf.Sin(num3)) * this.radius;
					GL.Color(this.color);
					GL.Vertex(vector);
					GL.Vertex(vector2);
					Vector3 vector3 = this.pos + new Vector3(0f, Mathf.Cos(num2), Mathf.Sin(num2)) * this.radius;
					Vector3 vector4 = this.pos + new Vector3(0f, Mathf.Cos(num3), Mathf.Sin(num3)) * this.radius;
					GL.Vertex(vector3);
					GL.Vertex(vector4);
					Vector3 vector5 = this.pos + new Vector3(Mathf.Cos(num2), Mathf.Sin(num2), 0f) * this.radius;
					Vector3 vector6 = this.pos + new Vector3(Mathf.Cos(num3), Mathf.Sin(num3), 0f) * this.radius;
					GL.Vertex(vector5);
					GL.Vertex(vector6);
				}
			}

			private Vector3 pos;

			private float radius;
		}
	}
}
