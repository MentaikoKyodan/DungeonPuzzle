using UnityEngine;

public class TitleIllustrationAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer; // Imageコンポーネントを使う場合は下のUI版を見てね
    [SerializeField] private Sprite[] frames; // ★3枚入れる想定（枚数は可変でOK）
    [SerializeField] private float switchInterval = 0.5f; // 切り替え間隔（秒）

    private int frameIndex;
    private float timer;

    private void OnEnable()
    {
        timer = 0f;
        frameIndex = 0;

        if (frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (frames.Length == 0) return;

        timer += Time.deltaTime;

        if (timer >= switchInterval)
        {
            timer -= switchInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            spriteRenderer.sprite = frames[frameIndex];
        }
    }
}