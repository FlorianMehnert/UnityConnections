using UnityEngine;

public class SnowBall : MonoBehaviour
{
    public float ballSpeed;

    public GameObject SnowballEffect;

    private Rigidbody2D theRB;

    // Start is called before the first frame update
    void Start()
    {
        theRB = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        theRB.linearVelocity = new Vector2(ballSpeed * transform.localScale.x, 0);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Instantiate(SnowballEffect, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}