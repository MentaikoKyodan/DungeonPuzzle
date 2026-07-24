using UnityEngine;

public class DestroyAfterAnimation : MonoBehaviour
{
    void Start()
    {
        Animator anim = GetComponent<Animator>();
        float length = anim.GetCurrentAnimatorStateInfo(0).length;
        float speed = anim.speed;

        //speedで割ることで、実際の再生時間に合わせてDestroyする
        Destroy(gameObject, length / speed);
    }
}