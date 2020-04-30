using JetBrains.Annotations;
using UnityEngine;
// ReSharper disable UseNullPropagation

public class BallBehaviour : MonoBehaviour
{
    public event CollisionEventHandler CollisionEnter;
    public event TriggerEventHandler TriggerEnter;

    [UsedImplicitly]
    private void OnCollisionEnter(Collision collision)
    {
        var handler = CollisionEnter;
        if (handler != null)
            handler.Invoke(this, collision);
    }

    [UsedImplicitly]
    private void OnTriggerEnter(Collider other)
    {
        var handler = TriggerEnter;
        if (handler != null)
            handler.Invoke(this, other);
    }
}
