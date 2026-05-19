using System.Collections.Generic;
using UnityEngine;

namespace DreamyAscent.Data
{
    internal sealed class DaSpecialSceneObjectData
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Reason { get; set; }
        public string Path { get; set; }
        public string ParentPath { get; set; }
        public string RootPath { get; set; }
        public bool ActiveSelf { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public int Layer { get; set; }
        public string Tag { get; set; }
        public Vector3 LocalPosition { get; set; }
        public Vector3 WorldPosition { get; set; }
        public int RendererCount { get; set; }
        public int ColliderCount { get; set; }
        public bool CanToggleActive { get; set; }
        public bool CanDelete { get; set; }
        public bool IsProtected { get; set; }
        public string ProtectionReason { get; set; }
        public GameObject SourceObject { get; set; }
        public List<string> Components { get; } = new List<string>();
        public List<string> Materials { get; } = new List<string>();
    }
}
