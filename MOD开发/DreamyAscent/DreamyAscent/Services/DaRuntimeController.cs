using DreamyAscent.Data;
using DreamyAscent.Helpers;
using DreamyAscent.UI;
using UnityEngine;

namespace DreamyAscent.Services
{
    internal sealed class DaRuntimeController : MonoBehaviour
    {
        private bool _initialExportCompleted;
        private float _nextAttemptTime;
        private DaCustomiserWindow _window;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _window = gameObject.AddComponent<DaCustomiserWindow>();
            gameObject.AddComponent<DaSceneHighlighter>();
            DaLog.Info("Runtime controller initialized.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _window.Toggle();
            }

            if (_initialExportCompleted || Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.unscaledTime + 5f;

            if (!DaTerrainExportService.TryExportCurrent(out DaTerrainData data))
            {
                return;
            }

            _initialExportCompleted = true;
            DaLog.Info(string.Format(
                "Initial runtime snapshot cached. segments={0}",
                data.Map != null && data.Map.Segments != null ? data.Map.Segments.Count : 0));
        }
    }
}


