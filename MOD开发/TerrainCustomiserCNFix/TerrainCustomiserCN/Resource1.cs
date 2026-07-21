using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace TerrainCustomiserCN
{
	[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "18.0.0.0")]
	[DebuggerNonUserCode]
	[CompilerGenerated]
	internal class Resource1
	{
		internal Resource1()
		{
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		internal static ResourceManager ResourceManager
		{
			get
			{
				bool flag = Resource1.resourceMan == null;
				if (flag)
				{
					ResourceManager resourceManager = new ResourceManager("TerrainCustomiserCN.Resource1", typeof(Resource1).Assembly);
					Resource1.resourceMan = resourceManager;
				}
				return Resource1.resourceMan;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		internal static CultureInfo Culture
		{
			get
			{
				return Resource1.resourceCulture;
			}
			set
			{
				Resource1.resourceCulture = value;
			}
		}

		internal static byte[] selectionoverlay
		{
			get
			{
				object @object = Resource1.ResourceManager.GetObject("selectionoverlay", Resource1.resourceCulture);
				return (byte[])@object;
			}
		}

		internal static byte[] visibilitycompute
		{
			get
			{
				object @object = Resource1.ResourceManager.GetObject("visibilitycompute", Resource1.resourceCulture);
				return (byte[])@object;
			}
		}

		private static ResourceManager resourceMan;

		private static CultureInfo resourceCulture;
	}
}
