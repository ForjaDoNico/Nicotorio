using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>Orthographic camera that eases toward its target and never shows past the map edge.</summary>
    [DefaultExecutionOrder(100)] // after the player has moved this frame
    public sealed class CameraFollow : MonoBehaviour
    {
        private const float OrthoSize = 9f;
        private const float Smoothing = 8f; // higher = snappier

        private Transform _target;
        private Camera _camera;
        private float _mapWidth, _mapHeight;

        public Camera Camera => _camera;

        public void Init(Transform target, int mapWidth, int mapHeight)
        {
            _target = target;
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;

            _camera = gameObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = OrthoSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.Shadow;
            gameObject.tag = "MainCamera";

            transform.position = Clamp(target.position);
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            Vector3 goal = Clamp(_target.position);
            float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, t);
        }

        private Vector3 Clamp(Vector3 p)
        {
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            float x = _mapWidth > 2f * halfW ? Mathf.Clamp(p.x, halfW, _mapWidth - halfW) : _mapWidth * 0.5f;
            float y = _mapHeight > 2f * halfH ? Mathf.Clamp(p.y, halfH, _mapHeight - halfH) : _mapHeight * 0.5f;
            return new Vector3(x, y, -10f);
        }
    }
}
