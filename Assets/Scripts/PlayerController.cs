using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("speed");
    private static readonly int Grounded = Animator.StringToHash("grounded");
    private static readonly int Property = Animator.StringToHash("throw");
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

    private Animator _anim;

    private Rigidbody2D _theRb;


    // Start is called before the first frame update
    private void Start()
    {
        _theRb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    private void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, whatIsGround);
        if (Input.GetKey(left))
        {
            _theRb.linearVelocity = new Vector2(-moveSpeed, _theRb.linearVelocity.y);
        }
        else if (Input.GetKey(right))
        {
            _theRb.linearVelocity = new Vector2(moveSpeed, _theRb.linearVelocity.y);
        }
        else
        {
            _theRb.linearVelocity = new Vector2(0, _theRb.linearVelocity.y);
        }

        if (Input.GetKeyDown(jump) && isGrounded)
        {
            _theRb.linearVelocity = new Vector2(_theRb.linearVelocity.x, jumpForce);
        }

        if (Input.GetKeyDown(throwBall))
        {
            GameObject ballClone = (GameObject)Instantiate(snowBall, throwPoint.position, throwPoint.rotation);
            ballClone.transform.localScale = transform.localScale;
            _anim.SetTrigger(Property);
        }

        transform.localScale = _theRb.linearVelocity.x switch
        {
            < 0 => new Vector3(-1, 1, 1),
            > 0 => new Vector3(1, 1, 1),
            _ => transform.localScale
        };

        _anim.SetFloat(Speed, Mathf.Abs(_theRb.linearVelocity.x));
        _anim.SetBool(Grounded, isGrounded);
    }
}