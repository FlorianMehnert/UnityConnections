using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed;
    public float jumpForce;

    public KeyCode left;
    public KeyCode right;
    public KeyCode jump;
    public KeyCode throwBall;

    public Transform groundCheckPoint;
    public float groundCheckRadius;
    public LayerMask whatIsGround;

    public bool isGrounded;

    public GameObject snowBall;

    public Transform throwPoint;

    private Animator anim;

    private Rigidbody2D theRB;


    // Start is called before the first frame update
    void Start()
    {
        theRB = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, whatIsGround);
        if (Input.GetKey(left))
        {
            theRB.linearVelocity = new Vector2(-moveSpeed, theRB.linearVelocity.y);
        }
        else if (Input.GetKey(right))
        {
            theRB.linearVelocity = new Vector2(moveSpeed, theRB.linearVelocity.y);
        }
        else
        {
            theRB.linearVelocity = new Vector2(0, theRB.linearVelocity.y);
        }

        if (Input.GetKeyDown(jump) && isGrounded)
        {
            theRB.linearVelocity = new Vector2(theRB.linearVelocity.x, jumpForce);
        }

        if (Input.GetKeyDown(throwBall))
        {
            GameObject ballClone = (GameObject)Instantiate(snowBall, throwPoint.position, throwPoint.rotation);
            ballClone.transform.localScale = transform.localScale;
            anim.SetTrigger("throw");
        }

        if (theRB.linearVelocity.x < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (theRB.linearVelocity.x > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        anim.SetFloat("speed", Mathf.Abs(theRB.linearVelocity.x));
        anim.SetBool("grounded", isGrounded);
        //anim.SetBool("throw", theRB.velocity.x);
    }
}