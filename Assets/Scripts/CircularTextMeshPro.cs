using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CircularTextMeshPro : MonoBehaviour
{
    public float radius = 100f;
    public float angleOffset = 0f;
    
    private TMP_Text m_TextComponent;
    private bool _isUpdating = false;

    // 儲存前一次的數值，用來偵測 Inspector 中的改動
    private float _prevRadius;
    private float _prevAngle;

    void Awake()
    {
        m_TextComponent = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ON_TEXT_CHANGED);
        _prevRadius = radius;
        _prevAngle = angleOffset;
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
        if (m_TextComponent == null) return;

        // 檢查自訂參數（半徑或角度）是否被使用者更改
        bool customParamsChanged = (radius != _prevRadius || angleOffset != _prevAngle);

        // 如果文本內容改變，或者半徑/角度改變，就觸發更新
        if ((m_TextComponent.havePropertiesChanged || customParamsChanged) && !_isUpdating)
        {
            _prevRadius = radius;
            _prevAngle = angleOffset;
            UpdateTextCurve();
        }
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

                float angleRad = (center.x / radius) + (angleOffset * Mathf.Deg2Rad);
                float sin = Mathf.Sin(angleRad);
                float cos = Mathf.Cos(angleRad);

                Vector3 newCenter = new Vector3(
                    sin * (center.y + radius), 
                    cos * (center.y + radius), 
                    0
                );

                Quaternion rotation = Quaternion.Euler(0, 0, -angleRad * Mathf.Rad2Deg);

                for (int j = 0; j < 4; j++)
                {
                    Vector3 offset = sourceVertices[vertexIndex + j] - center;
                    sourceVertices[vertexIndex + j] = newCenter + rotation * offset;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(radius));
    }
}