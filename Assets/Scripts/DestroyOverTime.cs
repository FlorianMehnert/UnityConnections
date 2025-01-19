using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public float lifeTime;

    // Update is called once per frame
    private void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime < 0){
            Destroy(gameObject);
        }
    }
}
