using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// エンディングシーン用。動画を再生し、終わったらタイトルへ戻る
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class EndingVideoController : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "TitleScene";

    private VideoPlayer videoPlayer;
    private bool isFinished = false;

    private IEnumerator Start()
    {
        // エンディング中はBGMを止める
        if (BGMManager.Instance != null)
            BGMManager.Instance.StopBGM();

        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.errorReceived += OnVideoError;

        // 先に読み込んでおく(再生開始のカクつき防止)
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
            yield return null;

        // アイリスインが終わるまで待ってから再生する
        while (ScreenTransitionManager.Instance != null &&
               ScreenTransitionManager.Instance.IsTransitioning)
            yield return null;

        videoPlayer.Play();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        GoToTitle();
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("動画再生エラー: " + message);
        GoToTitle(); // 詰まらないようにタイトルへ逃がす
    }

    private void GoToTitle()
    {
        if (isFinished) return;
        isFinished = true;

        // タイトルに戻る前にBGMを再開
        if (BGMManager.Instance != null)
            BGMManager.Instance.PlayBGM();

        if (ScreenTransitionManager.Instance != null)
            ScreenTransitionManager.Instance.FadeTransitionToScene(titleSceneName);
        else
            SceneManager.LoadScene(titleSceneName);
    }

    private void OnDestroy()
    {
        if (videoPlayer == null) return;
        videoPlayer.loopPointReached -= OnVideoEnd;
        videoPlayer.errorReceived -= OnVideoError;
    }
}