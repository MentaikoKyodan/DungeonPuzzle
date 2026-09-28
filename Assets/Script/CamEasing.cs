using UnityEngine;

public class CamEasing : MonoBehaviour
{
    [SerializeField] private Transform player; // 追尾対象のPlayer
    [SerializeField] private float smoothTime = 0.3f; // 遅延・滑らかさ（大きいほど遅れて動く）
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f); // 2Dカメラの位置決め用

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (player == null) return;

        // プレイヤーの位置 + オフセットを目標位置にする
        Vector3 targetPosition = player.position + offset;

        // SmoothDampを使って、現在のカメラ位置から目標位置へ遅延と緩急をつけて移動
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );
    }
}