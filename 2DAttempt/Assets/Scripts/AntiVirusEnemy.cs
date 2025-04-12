using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntivirusEnemy : MonoBehaviour
{
    [Header("Debug & Control")]
    public bool isPaused = false;

    // References
    [Header("References")]
    public Transform body;
    public Transform eye;
    public Transform pupil;
    public Transform mouth;
    private Transform player;
    private Rigidbody2D rb;

    // Optional visual indicator
    public GameObject dashIndicator;
    public TrailRenderer dashTrail;

    // Movement
    [Header("Movement")]
    public float normalSpeed = 1.5f;
    public float dashSpeed = 8f;
    public float aggroRange = 5f;
    public float dashRange = 2.5f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 2f;

    // Telegraph settings
    [Header("Telegraph")]
    public float telegraphDuration = 0.8f;
    public Color telegraphColor = Color.red;
    public float pulseSpeed = 5f;
    public float pulseSize = 0.2f;
    private Color originalColor;
    private Vector3 originalScale;
    private Vector2 dashDirection;

    // Combat
    [Header("Combat")]
    public int damageAmount = 10;
    public LayerMask playerLayer;

    // Eye Movement
    [Header("Eye Movement")]
    public float maxPupilOffset = 0.01f;
    public Vector2 pupilCenterOffset = new Vector2(0f, 0f);
    private Vector2 defaultPupilPosition;

    // State Variables
    private enum State { Idle, Alert, Chase, Telegraph, Dash, Cooldown }
    private State currentState;
    private bool canDash = true;
    private Vector2 lookDirection;
    private float yawnTimer;
    private float dashTimer;
    private float alertTimer;
    private float idleLookTimer;
    private float telegraphTimer;

    // Mouth animation
    private Vector3 mouthOriginalScale;
    private bool isYawning = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        mouthOriginalScale = mouth.localScale;
        currentState = State.Idle;
        idleLookTimer = Random.Range(1f, 3f);
        yawnTimer = Random.Range(3f, 8f);

        // Store default pupil position relative to the eye
        defaultPupilPosition = pupil.localPosition;

        // Store original visual properties
        if (body != null && body.GetComponent<SpriteRenderer>() != null)
        {
            originalColor = body.GetComponent<SpriteRenderer>().color;
        }
        originalScale = transform.localScale;

        // Initialize optional components
        if (dashIndicator != null)
        {
            dashIndicator.SetActive(false);
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }
    }

    void Update()
    {
        if (isPaused)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // Update timers
        if (dashTimer > 0) dashTimer -= Time.deltaTime;
        if (alertTimer > 0) alertTimer -= Time.deltaTime;
        if (idleLookTimer > 0) idleLookTimer -= Time.deltaTime;
        if (yawnTimer > 0) yawnTimer -= Time.deltaTime;
        if (telegraphTimer > 0) telegraphTimer -= Time.deltaTime;

        // Calculate distance to player
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // State machine
        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();

                // Check if player enters aggro range
                if (distanceToPlayer < aggroRange)
                {
                    currentState = State.Alert;
                    alertTimer = 0.5f; // Alert for half a second before chasing
                    StopYawning();
                }
                break;

            case State.Alert:
                // Look at player
                LookAtPlayer();

                // Transition to chase after alert time
                if (alertTimer <= 0)
                {
                    currentState = State.Chase;
                }
                break;

            case State.Chase:
                // Look at player
                LookAtPlayer();

                // Move toward player
                Vector2 direction = (player.position - transform.position).normalized;
                rb.velocity = direction * normalSpeed;

                // Check if close enough to dash
                if (distanceToPlayer < dashRange && canDash)
                {
                    // Enter telegraph state instead of immediately dashing
                    currentState = State.Telegraph;
                    telegraphTimer = telegraphDuration;
                    rb.velocity = Vector2.zero; // Stop movement during telegraph

                    // Store dash direction
                    dashDirection = (player.position - transform.position).normalized;

                    // Start visual telegraph
                    StartCoroutine(TelegraphEffect());
                }
                break;

            case State.Telegraph:
                // Keep looking at player during telegraph
                LookAtPlayer();

                // Prevent movement during telegraph
                rb.velocity = Vector2.zero;

                // Transition to dash after telegraph time
                if (telegraphTimer <= 0)
                {
                    currentState = State.Dash;
                    dashTimer = dashDuration;
                    canDash = false;

                    // Start dash
                    rb.velocity = dashDirection * dashSpeed;

                    // Start cooldown timer as coroutine
                    StartCoroutine(DashCooldown());

                    // Enable dash trail if available
                    if (dashTrail != null)
                    {
                        dashTrail.emitting = true;
                    }
                }
                break;

            case State.Dash:
                // Continue dashing until dash timer expires
                if (dashTimer <= 0)
                {
                    currentState = State.Cooldown;
                    rb.velocity = Vector2.zero;

                    // Disable dash trail if available
                    if (dashTrail != null)
                    {
                        dashTrail.emitting = false;
                    }
                }
                break;

            case State.Cooldown:
                // Briefly pause after dashing
                if (canDash)
                {
                    currentState = State.Chase;
                }
                break;
        }

        // Check for collision with player during dash
        if (currentState == State.Dash)
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, GetComponent<CircleCollider2D>().radius, playerLayer);
            if (hit != null)
            {
                PlayerController playerController = hit.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TakeDamage(damageAmount, transform);
                }
            }
        }
    }

    void UpdateIdle()
    {
        // Stop any movement
        rb.velocity = Vector2.zero;

        // Randomly look around
        if (idleLookTimer <= 0)
        {
            // Generate random direction but constrain magnitude
            lookDirection = Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.8f);

            // Apply limited pupil movement around its default position
            Vector2 targetOffset = lookDirection * maxPupilOffset;
            pupil.localPosition = defaultPupilPosition + pupilCenterOffset + targetOffset;

            idleLookTimer = Random.Range(1f, 3f);
        }

        // Yawn occasionally
        if (yawnTimer <= 0 && !isYawning)
        {
            StartCoroutine(Yawn());
            yawnTimer = Random.Range(5f, 10f);
        }
    }

    void LookAtPlayer()
    {
        // Create a vector from enemy to player
        Vector3 directionToPlayer = player.position - transform.position;

        // Normalize the direction
        directionToPlayer.Normalize();

        // Calculate pupil offset based on look direction, but keep movement minimal
        Vector2 targetOffset = new Vector2(directionToPlayer.x, directionToPlayer.y) * maxPupilOffset;

        // Apply the calculated position to the pupil, maintaining the default position
        pupil.localPosition = defaultPupilPosition + pupilCenterOffset + targetOffset;
    }

    IEnumerator TelegraphEffect()
    {
        // Get sprite renderer
        SpriteRenderer bodyRenderer = null;
        if (body != null)
        {
            bodyRenderer = body.GetComponent<SpriteRenderer>();
        }
        else if (GetComponent<SpriteRenderer>() != null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
        }

        // Show dash indicator if available
        if (dashIndicator != null)
        {
            dashIndicator.SetActive(true);

            // Point indicator in dash direction
            float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
            dashIndicator.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Pulse effect during telegraph
        float startTime = Time.time;

        while (currentState == State.Telegraph)
        {
            // Calculate pulse value (0-1-0)
            float pulse = Mathf.Abs(Mathf.Sin((Time.time - startTime) * pulseSpeed));

            // Apply color change if renderer exists
            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(originalColor, telegraphColor, pulse);
            }

            // Apply scale pulse
            transform.localScale = originalScale * (1 + pulse * pulseSize);

            // Mouth animation for telegraph (open wider)
            mouth.localScale = new Vector3(
                mouthOriginalScale.x,
                mouthOriginalScale.y * (1 + pulse),
                mouthOriginalScale.z);

            yield return null;
        }

        // Reset visuals when telegraph is over
        if (bodyRenderer != null)
        {
            bodyRenderer.color = originalColor;
        }

        transform.localScale = originalScale;

        // Hide dash indicator
        if (dashIndicator != null)
        {
            dashIndicator.SetActive(false);
        }
    }

    // Reset pupil position to default
    public void ResetPupilPosition()
    {
        pupil.localPosition = defaultPupilPosition + pupilCenterOffset;
    }

    IEnumerator Yawn()
    {
        isYawning = true;

        // Animate mouth opening
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // First half: open mouth
            if (t < 0.5f)
            {
                float openAmount = Mathf.Lerp(1f, 2f, t * 2f);
                mouth.localScale = new Vector3(mouthOriginalScale.x, mouthOriginalScale.y * openAmount, mouthOriginalScale.z);
            }
            // Second half: close mouth
            else
            {
                float closeAmount = Mathf.Lerp(2f, 1f, (t - 0.5f) * 2f);
                mouth.localScale = new Vector3(mouthOriginalScale.x, mouthOriginalScale.y * closeAmount, mouthOriginalScale.z);
            }

            yield return null;
        }

        mouth.localScale = mouthOriginalScale;
        isYawning = false;
    }

    void StopYawning()
    {
        // Reset mouth if yawning when transitioning to alert
        StopAllCoroutines();
        mouth.localScale = mouthOriginalScale;
        isYawning = false;
    }

    IEnumerator DashCooldown()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Draw Gizmos to visualize ranges in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dashRange);
    }
}