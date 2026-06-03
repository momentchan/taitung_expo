using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CircularTextMeshPro : MonoBehaviour
{
    public float radius = 100f;
    public float angleOffset = 0f;
    public bool invertRadialOrientation = false;
    
    private TMP_Text m_TextComponent;
    private bool _isUpdating = false;

    private float _prevRadius;
    private float _prevAngle;
    private bool _prevInvertRadialOrientation;

    void Awake()
    {
        m_TextComponent = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ON_TEXT_CHANGED);
        _prevRadius = radius;
        _prevAngle = angleOffset;
        _prevInvertRadialOrientation = invertRadialOrientation;
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ON_TEXT_CHANGED);
    }

    void ON_TEXT_CHANGED(Object obj)
    {
        if (obj == m_TextComponent && !_isUpdating)
        {
            UpdateTextCurve();
        }
    }

    void Update()
    {
        // In edit mode, avoid per-frame checking (causes extra mesh work and editor churn with ExecuteAlways).
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        if (m_TextComponent == null) return;

        bool customParamsChanged = (radius != _prevRadius || angleOffset != _prevAngle);
        customParamsChanged |= invertRadialOrientation != _prevInvertRadialOrientation;

        if ((m_TextComponent.havePropertiesChanged || customParamsChanged) && !_isUpdating)
        {
            _prevRadius = radius;
            _prevAngle = angleOffset;
            _prevInvertRadialOrientation = invertRadialOrientation;
            UpdateTextCurve();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (m_TextComponent == null) m_TextComponent = GetComponent<TMP_Text>();
        if (m_TextComponent == null) return;
        _prevRadius = radius;
        _prevAngle = angleOffset;
        _prevInvertRadialOrientation = invertRadialOrientation;
        if (!Application.isPlaying && !_isUpdating) UpdateTextCurve();
    }
#endif

    /// <summary>Re-applies the circular mesh deformation (call after text/layout changes or before baking offline).</summary>
    public void RefreshCurve()
    {
        if (m_TextComponent == null) m_TextComponent = GetComponent<TMP_Text>();
        if (m_TextComponent == null) return;
        UpdateTextCurve();
    }

    void UpdateTextCurve()
    {
        if (radius == 0) return;

        _isUpdating = true;

        try
        {
            m_TextComponent.ForceMeshUpdate();

            TMP_TextInfo textInfo = m_TextComponent.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0) return;

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                if (materialIndex >= textInfo.meshInfo.Length) continue;
                
                Vector3[] sourceVertices = textInfo.meshInfo[materialIndex].vertices;
                if (sourceVertices == null || vertexIndex + 3 >= sourceVertices.Length) continue;

                Vector3 center = (sourceVertices[vertexIndex + 0] + sourceVertices[vertexIndex + 2]) / 2f;
                Vector3 pivot = invertRadialOrientation
                    ? GetLineCenterPivot(textInfo, charInfo, center)
                    : center;

                float angleRad = (pivot.x / radius) + (angleOffset * Mathf.Deg2Rad);
                float sin = Mathf.Sin(angleRad);
                float cos = Mathf.Cos(angleRad);
 
                Vector3 newPivot = new Vector3(
                    sin * (pivot.y + radius), 
                    cos * (pivot.y + radius), 
                    0
                );

                float radialOrientationOffset = invertRadialOrientation ? 180f : 0f;
                Quaternion rotation = Quaternion.Euler(0, 0, radialOrientationOffset - angleRad * Mathf.Rad2Deg);

                for (int j = 0; j < 4; j++)
                {
                    Vector3 offset = sourceVertices[vertexIndex + j] - pivot;
                    sourceVertices[vertexIndex + j] = newPivot + rotation * offset;
                } 
            }

            for (int i = 0; i < textInfo.materialCount; i++)
            {
                if (textInfo.meshInfo[i].mesh != null)
                {
                    textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                    textInfo.meshInfo[i].mesh.RecalculateBounds(); 
                    m_TextComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
                }
            }
        }
        finally
        {
            _isUpdating = false; 
        }
    }

    static Vector3 GetLineCenterPivot(TMP_TextInfo textInfo, TMP_CharacterInfo charInfo, Vector3 characterCenter)
    {
        int lineNumber = charInfo.lineNumber;
        if (lineNumber < 0 || lineNumber >= textInfo.lineInfo.Length)
            return characterCenter;

        TMP_LineInfo lineInfo = textInfo.lineInfo[lineNumber];
        float lineCenterY = 0.5f * (lineInfo.ascender + lineInfo.descender);
        return new Vector3(characterCenter.x, lineCenterY, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(radius));
    }
}
