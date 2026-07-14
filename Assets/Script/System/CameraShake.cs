using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalLocalPos;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        Instance = this;
        originalLocalPos = transform.localPosition;
    }

    // magnitude: 揺れの強さ（大きいほど激しく揺れる）
    // duration: 揺れる時間（秒）
    public void Shake(float magnitude, float duration)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalLocalPos + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeRoutine = null;
    }
}