using UnityEngine;

public class SnowBall : MonoBehaviour
{
    public float ballSpeed;

    public GameObject snowballEffect;

    private Rigidbody2D _theRb;

    // Start is called before the first frame update
    private void Start()
    {
        _theRb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    private void Update()
    {
        _theRb.linearVelocity = new Vector2(ballSpeed * transform.localScale.x, 0);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Instantiate(snowballEffect, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}