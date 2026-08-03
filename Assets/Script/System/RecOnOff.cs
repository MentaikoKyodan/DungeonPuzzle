using System.Collections;
using UnityEngine;

public class RecOnOff : MonoBehaviour
{
    [Header("点滅の間隔（秒）")]
    [SerializeField] private float interval = 0.8f; // 0.8秒ごとに切り替え

    private CanvasGroup canvasGroup;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        // CanvasGroupが付いていなければ自動で追加
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        // オブジェクトが有効化されたら点滅開始
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void OnDisable()
    {
        // 非アクティブになったら停止し、表示状態を元に戻す
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // OFF にする (透明)
            canvasGroup.alpha = 0f;
            yield return new WaitForSeconds(interval);

            // ON にする (不透明)
            canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(interval);
        }
    }
}