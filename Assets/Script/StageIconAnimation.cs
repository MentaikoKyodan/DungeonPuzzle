using UnityEngine;

public class StageIconAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite spriteA;
    [SerializeField] private Sprite spriteB;
    [SerializeField] private float switchInterval = 0.5f; // 切り替え間隔（秒）

    private float timer;
    private bool isShowingA = true;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        isShowingA = true;
        spriteRenderer.sprite = spriteA;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= switchInterval)
        {
            timer -= switchInterval;
            isShowingA = !isShowingA;
            spriteRenderer.sprite = isShowingA ? spriteA : spriteB;
        }
    }
}