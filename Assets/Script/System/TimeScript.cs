using UnityEngine;
using TMPro;
using System;

public class TimerScript : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    private float timeCount = 0f;

    void Update()
    {
        timeCount += Time.deltaTime;

        TimeSpan timeSpan = TimeSpan.FromSeconds(timeCount);

        // 時間:分:秒.小数点以下2桁 (例: 01:23:45.67)
        timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D2}",
            timeSpan.Hours,
            timeSpan.Minutes,
            timeSpan.Seconds,
            timeSpan.Milliseconds / 10); // ミリ秒(0-999)を10で割って2桁(0-99)にする
    }
}