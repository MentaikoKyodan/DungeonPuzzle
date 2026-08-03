using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private string canvasName = "Canvas"; // 探したいCanvasの名前

    private void Start()
    {
        // 1. シーン内から該当のCanvasを探す
        GameObject canvasObj = GameObject.Find(canvasName);

        if (canvasObj != null)
        {
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            Camera myCamera = GetComponent<Camera>();

            if (canvas != null && myCamera != null)
            {
                // 2. このカメラをCanvasのRender Cameraに割り当てる
                canvas.worldCamera = myCamera;
            }
        }
        else
        {
            Debug.LogWarning($"[AssignCameraToCanvas] '{canvasName}' という名前のCanvasが見つかりませんでした。");
        }
    }
}