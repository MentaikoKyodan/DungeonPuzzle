using UnityEngine;
using UnityEngine.UI;

public class TitleIllustrationAnimationUI : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float switchInterval = 0.5f;

    private int frameIndex;
    private float timer;

    private void OnEnable()
    {
        timer = 0f;
        frameIndex = 0;

        if (frames.Length > 0)
            targetImage.sprite = frames[0];
    }

    private void Update()
    {
        if (frames.Length == 0) return;

        timer += Time.deltaTime;

        if (timer >= switchInterval)
        {
            timer -= switchInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            targetImage.sprite = frames[frameIndex];
        }
    }
}