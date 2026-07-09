using UnityEngine;

namespace NumbersBlast.UI
{
    /// <summary>
    /// Fits this RectTransform to the device safe area so HUD content clears notches/rounded
    /// corners on phones. The background stays full-screen behind it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafe;
        private Vector2Int _lastScreen;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafe || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rt == null)
            {
                _rt = (RectTransform)transform;
            }

            _lastSafe = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 min = _lastSafe.position;
            Vector2 max = _lastSafe.position + _lastSafe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
