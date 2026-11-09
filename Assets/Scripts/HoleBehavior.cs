using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class HoleBehaviour : MonoBehaviour
{
    public float captureRadius = 0.3f; //The ball falls in when its centre is this close to the middle of the hole

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        DragLaunchBall ball = other.GetComponent<DragLaunchBall>();
        if (ball == null) return;

        if (Vector2.Distance(other.transform.position, transform.position) <= captureRadius)
            ball.FallIntoHole(transform.position);
    }
}