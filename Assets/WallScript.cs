using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class WallScript : MonoBehaviour
{
    private static readonly int WallHitTrigger = Animator.StringToHash("WallHit");
    private Animator _animator;

    // Start is called before the first frame update
    [UsedImplicitly]
    void Start()
    {
        _animator = GetComponent<Animator>();
    }


    [UsedImplicitly]
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Ball")
        {
            _animator.SetTrigger(WallHitTrigger);
        }
    }

}
