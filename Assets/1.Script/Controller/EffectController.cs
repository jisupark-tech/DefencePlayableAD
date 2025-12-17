using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EffectType
{
    Building,
    Hit,
}

public class EffectController : MonoBehaviour
{
    [Header("Effect Settings")]
    public float durationLength;

    [Header("Animation Settings")]
    public float speed = 2f;
    public float noiseScale = 0.1f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Offset")]
    public Vector3 m_offest;
    private float curDuration = 0;
    private EffectType m_type;

    public void Init(EffectType _type, float _x = 0, float _z = 0, float _rotationY = 0, float _scale = 1f)
    {
        curDuration = 0;
        m_type = _type;
        transform.eulerAngles = new Vector3(0, _rotationY,0);
        transform.localScale = Vector3.one*_scale;
        switch (m_type)
        {
            case EffectType.Building:
                // GameObject의 위치를 설정
                transform.position = new Vector3(_x + m_offest.x, 0f + m_offest.y, _z + m_offest.z);
                StartCoroutine(ShowAnim());
                break;
            case EffectType.Hit:

                StartCoroutine(ShowAnim());
                break;
        }
    }

    IEnumerator ShowAnim()
    {
        while (curDuration <= durationLength)
        {
            curDuration += 0.05f;

            yield return new WaitForSeconds(0.05f);
        }

        ObjectPool.Instance.ReturnToPool(this.gameObject);
    }
}