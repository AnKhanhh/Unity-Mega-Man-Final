using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BombScript : MonoBehaviour
{
    Animator animator;
    CircleCollider2D circle2d;
    Rigidbody2D rb2d;
    SpriteRenderer sprite;

    public UnityEvent ExplosionEvent;

    bool startTimer;
    float explodeTimer;

    bool freezeBomb;
    Color bombColor;
    Vector2 freezeVelocity;
    RigidbodyConstraints2D rb2dConstraints;

    // default the settings for use by the player
    [Header("Bomb Damage")]
    [SerializeField] int contactDamage = 0;
    [SerializeField] int explosionDamage = 4;

    [Header("Audio Clips")]
    [SerializeField] AudioClip explosionClip;

    [Header("Timers & Collision")]
    [SerializeField] float explodeDelay;

    [SerializeField] string[] collideWithTags;

    [Header("Positions & Physics")]
    [SerializeField] float gravity;
    [SerializeField] float height = 1f;
    [SerializeField] float targetOffset = 0.15f;

    [SerializeField] Vector3 sourcePosition;
    [SerializeField] Vector3 targetPosition;

    [SerializeField] Vector2 bombDirection = Vector2.right;
    [SerializeField] Vector2 launchVelocity = new Vector3(2f, 1.5f);

    [Header("Materials & Prefabs")]
    [SerializeField] PhysicsMaterial2D bounceMaterial;
    [SerializeField] GameObject explodeEffectPrefab;

    void Awake()
    {
        // get the attached components
        animator = GetComponent<Animator>();
        circle2d = GetComponent<CircleCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();

        // make bomb as a static type of object
        circle2d.isTrigger = true;
        rb2d.isKinematic = true;
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        // if the bomb is frozen then don't allow it to destroy
        if (freezeBomb) return;

        if (startTimer)
        {
            explodeTimer -= Time.deltaTime;
            if (explodeTimer <= 0)
            {
                startTimer = false;
                Explode();
            }
        }
    }

    public void SetContactDamageValue(int damage)
    {
        this.contactDamage = damage;
    }

    public void SetExplosionDamageValue(int damage)
    {
        this.explosionDamage = damage;
    }

    public void SetExplosionDelay(float delay)
    {
        this.explodeDelay = delay;
    }

    public void SetVelocity(Vector2 velocity)
    {
        this.launchVelocity = velocity;
    }

    public void SetDirection(Vector2 direction)
    {
        // if no target is set then this direction is used in conjunction with the velocity
        this.bombDirection = direction;
        // bomb faces left by default, flip if necessary
        if (direction.x > 0)
        {
            transform.Rotate(0, 180f, 0);
        }
    }

    public void SetHeight(float height)
    {
        //for the LaunchData calculation
        this.height = height;
    }

    public void SetSourcePosition(Vector3 position)
    {
        //for the LaunchData calculation
        this.sourcePosition = position;
    }

    public void SetTargetPosition(Vector3 position)
    {
        //for the LaunchData calculation
        this.targetPosition = position;
    }

    public void SetTargetOffset(float offset)
    {
        // offset to apply if LaunchData target exact hit isn't desired
        this.targetOffset = offset;
    }

    public void Bounces(bool bounce)
    {
        // apply bounce material or set to null
        rb2d.sharedMaterial = bounce ? bounceMaterial : null;
    }

    public void SetCollideWithTags(params string[] tags)
    {
        this.collideWithTags = tags;
    }


    // CASE1 - target position isn't null means the velocity and direction vectors won't be used
    // the velocity is calculated using the kinematic equation and gravity and offset will be used
    // this case is for BombMan however could be used by other characters
    // CASE2 - target position is null so use straight velocity and direction to launch the bomb
    // this case is for MegaMan however could be used by other characters
    public void Launch(bool calculateLaunch = true)
    {
        // make the bomb solid and have a dynamic rigidbody
        circle2d.isTrigger = false;
        rb2d.isKinematic = false;

        if (calculateLaunch)
        {
            // launch bomb to target and apply offset if any
            if (gravity == 0) gravity = Physics2D.gravity.y;
            Vector3 bombPos = sourcePosition;
            Vector3 playerPos = targetPosition;
            if (targetOffset != 0) playerPos.x += targetOffset;
            rb2d.velocity = UtilityFunctions.CalculateLaunchData(bombPos, playerPos, height, gravity).initialVelocity;
        }
        else
        {
            // no target set - use launch velocity instead
            Vector2 velocity = this.launchVelocity;
            velocity.x *= this.bombDirection.x;
            rb2d.AddForce(velocity, ForceMode2D.Impulse);
        }
    }

    private void Explode()
    {
        GameObject explodeEffect = Instantiate(explodeEffectPrefab);
        explodeEffect.name = explodeEffectPrefab.name;
        explodeEffect.transform.position = sprite.bounds.center;
        explodeEffect.GetComponent<ExplosionScript>().SetCollideWithTags(this.collideWithTags);
        explodeEffect.GetComponent<ExplosionScript>().SetDamageValue(this.explosionDamage);
        explodeEffect.GetComponent<ExplosionScript>().SetDestroyDelay(explodeDelay);

        sprite.color = Color.clear;
        Destroy(gameObject, 1f);
        SoundManager.Instance.Play(explosionClip);

        ExplosionEvent.Invoke();
    }

    public void FreezeBomb(bool freeze)
    {
        // save the current velocity and freeze XYZ rigidbody constraints
        // NOTE: this will be called from the GameManager but could be used in other scripts
        if (freeze)
        {
            freezeBomb = true;
            rb2dConstraints = rb2d.constraints;
            freezeVelocity = rb2d.velocity;
            rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            freezeBomb = false;
            rb2d.constraints = rb2dConstraints;
            rb2d.velocity = freezeVelocity;
        }
    }

    public void HideBomb(bool hide)
    {
        // hide/show the bombs on the screen
        // get the current color then set to transparent
        // restore the bomb to its saved color
        if (hide)
        {
            bombColor = sprite.color;
            sprite.color = Color.clear;
        }
        else
        {
            sprite.color = bombColor;
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // check for bomb colliding with the ground layer
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            // look for an explosion delay and start the timer
            if (!startTimer && explodeDelay > 0)
            {
                startTimer = true;
                explodeTimer = explodeDelay;
            }

            // if there is no delay then destroy the bomb immediately
            if (explodeDelay == 0)
            {
                Explode();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        foreach (string tag in collideWithTags)
        {
            if (other.gameObject.CompareTag(tag))
            {
                switch (tag)
                {
                    case "Enemy":
                        EnemyController enemy = other.gameObject.GetComponent<EnemyController>();
                        if (enemy != null)
                        {
                            enemy.TakeDamage(this.contactDamage);
                        }
                        break;
                    case "Player":
                        PlayerController player = other.gameObject.GetComponent<PlayerController>();
                        if (player != null)
                        {
                            player.HitSide(transform.position.x > player.transform.position.x);
                            player.TakeDamage(this.contactDamage);
                        }
                        break;
                }
            }
        }
    }
}