using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// View: a glow (2D point light) that follows this object, such as a player's torch or an item's shine.
/// It is made at runtime from the values below, so it exists the same way on every peer.
/// </summary>
public class LightGlow : MonoBehaviour
{
    [SerializeField] private Color _color = Color.white;
    [SerializeField, Min(0.0f)] private float _intensity = 1.0f;
    [SerializeField, Min(0.0f)] private float _innerRadius;
    [SerializeField, Min(0.1f)] private float _outerRadius = 2.0f;
    [Tooltip("Higher = softer edge.")]
    [SerializeField, Range(0.0f, 1.0f)] private float _falloff = 0.8f;
    [Tooltip("Where the light sits, relative to this object.")]
    [SerializeField] private Vector2 _offset;

    private void Awake()
    {
        var lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = _offset;

        // Keep the light the same size however this object is scaled.
        Vector3 scale = transform.lossyScale;
        lightObject.transform.localScale = new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f);

        var glow = lightObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = _color;
        glow.intensity = _intensity;
        glow.pointLightInnerRadius = _innerRadius;
        glow.pointLightOuterRadius = Mathf.Max(_outerRadius, _innerRadius);
        glow.falloffIntensity = _falloff;
    }
}
