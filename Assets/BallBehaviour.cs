using UnityEngine;

public class BallBehaviour : MonoBehaviour
{
    public event CollisionEventHandler CollisionEnter;
    public event TriggerEventHandler TriggerEnter;

    void OnCollisionEnter(Collision collision)
    {
        CollisionEnter?.Invoke(this, collision);
    }

    void OnTriggerEnter(Collider other)
    {
        TriggerEnter?.Invoke(this, other);
    }
}
