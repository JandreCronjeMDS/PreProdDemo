using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 60f;
    [SerializeField] private float velPower = 0.9f;
    [SerializeField] private float frictionAmount = 0.2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invincibilityDuration = 1f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackDuration = 0.2f;

    // Private variables
    private Rigidbody2D rb;
    private float moveInput;
    private bool isGrounded;
    private bool isJumping;
    private bool canDash = true;
    private bool isDashing;
    private bool isInvincible;
    private bool isKnockedBack;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int currentHealth;

    // Components
    private SpriteRenderer spriteRenderer;

    // Health properties
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    // Events
    public delegate void HealthChangedHandler(int currentHealth, int maxHealth);
    public event HealthChangedHandler OnHealthChanged;

    public delegate void PlayerDeathHandler();
    public event PlayerDeathHandler OnPlayerDeath;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDashing || isKnockedBack) return;

        // Get horizontal input
        moveInput = Input.GetAxisRaw("Horizontal");

        // Ground check
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Coyote time
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Jump buffer
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // Jump
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !isJumping)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpBufferCounter = 0f;
            isJumping = true;
        }

        // Variable jump height
        if (Input.GetKeyUp(KeyCode.Space) && rb.velocity.y > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
        }

        // Reset jump
        if (isGrounded && rb.velocity.y <= 0)
        {
            isJumping = false;
        }

        // Better jump feel
        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (rb.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        // Dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash());
        }

        // Flip sprite based on movement direction
        if (moveInput > 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (moveInput < 0)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void FixedUpdate()
    {
        if (isDashing || isKnockedBack) return;

        MovePlayer();
        ApplyFriction();
    }

    private void MovePlayer()
    {
        // Calculate target speed
        float targetSpeed = moveInput * moveSpeed;

        // Calculate speed difference
        float speedDiff = targetSpeed - rb.velocity.x;

        // Calculate acceleration rate
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

        // Calculate movement force
        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, velPower) * Mathf.Sign(speedDiff);

        // Apply movement force
        rb.AddForce(movement * Vector2.right);
    }

    private void ApplyFriction()
    {
        if (Mathf.Abs(moveInput) < 0.01f && isGrounded)
        {
            // Apply friction
            float amount = Mathf.Min(Mathf.Abs(rb.velocity.x), frictionAmount);
            amount *= Mathf.Sign(rb.velocity.x);
            rb.AddForce(Vector2.right * -amount, ForceMode2D.Impulse);
        }
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        // Store original gravity
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;

        // Determine dash direction (use sprite direction if no input)
        float dashDirection = moveInput != 0 ? Mathf.Sign(moveInput) : (spriteRenderer.flipX ? -1 : 1);

        // Set velocity for dash
        rb.velocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        // Reset gravity
        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void TakeDamage(int damage, Transform damageSource = null)
    {
        // Check if player can take damage
        if (isInvincible || !IsAlive) return;

        // Apply damage
        currentHealth = Mathf.Max(0, currentHealth - damage);

        // Trigger health changed event
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Activate invincibility
        StartCoroutine(InvincibilityFrames());

        // Apply knockback if damage source is provided
        if (damageSource != null)
        {
            ApplyKnockback(damageSource.position);
        }

        // Visual feedback
        StartCoroutine(DamageFlash());

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void ApplyKnockback(Vector3 damageSourcePosition)
    {
        StartCoroutine(KnockbackRoutine(damageSourcePosition));
    }

    private IEnumerator KnockbackRoutine(Vector3 damageSourcePosition)
    {
        isKnockedBack = true;

        // Calculate knockback direction (away from damage source)
        Vector2 knockbackDirection = (transform.position - damageSourcePosition).normalized;

        // Apply knockback force
        rb.velocity = Vector2.zero; // Reset velocity
        rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        isKnockedBack = false;
    }

    private IEnumerator DamageFlash()
    {
        // Flash the sprite red a few times
        Color originalColor = spriteRenderer.color;
        Color damageColor = Color.red;

        for (int i = 0; i < 3; i++)
        {
            spriteRenderer.color = damageColor;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void RestoreHealth(int amount)
    {
        if (!IsAlive) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        // Trigger death event
        OnPlayerDeath?.Invoke();

        // Disable player controls
        this.enabled = false;

        // You might want to play death animation here

        // Optionally disable colliders
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }

        // You could add respawn logic or game over handling here
        Debug.Log("Player died!");
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize ground check
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}