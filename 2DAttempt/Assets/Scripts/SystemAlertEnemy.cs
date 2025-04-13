using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SystemAlertEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform alertBody;
    public Transform exclamationMark;
    public Transform indicatorDot;
    public GameObject warningAreaPrefab;
    public GameObject alertSound;
    private Transform player;
    private Rigidbody2D rb;

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float patrolRange = 5f;
    public float detectionRange = 8f;
    public float alertDuration = 1.5f;
    public float alertCooldown = 4f;

    [Header("Warning Area")]
    public float warningRadius = 3f;
    public int warningDamage = 15;
    public float warningDuration = 2f;
    public float warningExplosionDelay = 1.5f;
    public Color warningColor = new Color(1f, 0.9f, 0f, 0.5f);

    [Header("Visual Effects")]
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.15f;
    public float bounceHeight = 0.2f;
    public float bounceSpeed = 2f;
    public Color normalColor = new Color(1f, 0.9f, 0);
    public Color alertColor = new Color(1f, 0.5f, 0);

    [Header("Health & Combat")]
    public int health = 70;
    public int contactDamage = 8;
    public float invincibilityDuration = 0.5f;

    // State variables
    private enum State { Patrol, Alert, Warning, Cooldown }
    private State currentState = State.Patrol;
    private Vector2 patrolTarget;
    private float alertTimer;
    private float cooldownTimer;
    private bool isInvincible = false;
    private bool isDead = false;
    private Vector2 initialPosition;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer exclamationRenderer;
    private SpriteRenderer indicatorRenderer;
    private float lastDamageTime;
    private float damageInterval = 0.5f;

    // Warning area reference
    private GameObject activeWarningArea;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        initialPosition = transform.position;

        // Cache renderers
        bodyRenderer = alertBody?.GetComponent<SpriteRenderer>();
        exclamationRenderer = exclamationMark?.GetComponent<SpriteRenderer>();
        indicatorRenderer = indicatorDot?.GetComponent<SpriteRenderer>();

        // Set initial patrol target
        SetNewPatrolTarget();

        // Ensure exclamation mark is initially invisible
        if (exclamationRenderer != null)
        {
            exclamationRenderer.enabled = false;
        }

        // Make indicator dot pulse
        StartCoroutine(PulseIndicator());
    }

    void Update()
    {
        if (isDead) return;

        // Update timers
        if (alertTimer > 0) alertTimer -= Time.deltaTime;
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        // Apply visual effects
        ApplyVisualEffects();

        // State machine
        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();

                // Check if player is in detection range
                if (player != null && Vector2.Distance(transform.position, player.position) < detectionRange)
                {
                    EnterAlertState();
                }
                break;

            case State.Alert:
                // Alert state is visual only, actual transition happens in the coroutine
                rb.velocity = Vector2.zero;

                // Check if alert is finished
                if (alertTimer <= 0)
                {
                    EnterWarningState();
                }
                break;

            case State.Warning:
                // Warning state is handled by coroutine
                rb.velocity = Vector2.zero;
                break;

            case State.Cooldown:
                UpdateCooldown();

                // Return to patrol state after cooldown
                if (cooldownTimer <= 0)
                {
                    currentState = State.Patrol;
                    SetNewPatrolTarget();
                }
                break;
        }
    }

    void UpdatePatrol()
    {
        // Move toward patrol target
        Vector2 direction = (patrolTarget - (Vector2)transform.position).normalized;
        rb.velocity = direction * moveSpeed;

        // Check if reached target
        if (Vector2.Distance(transform.position, patrolTarget) < 0.5f)
        {
            SetNewPatrolTarget();
        }
    }

    void UpdateCooldown()
    {
        // During cooldown, move back toward initial position
        Vector2 direction = (initialPosition - (Vector2)transform.position).normalized;
        rb.velocity = direction * (moveSpeed * 0.7f);
    }

    void SetNewPatrolTarget()
    {
        // Set a random target position within patrol range of initial position
        patrolTarget = initialPosition + Random.insideUnitCircle * patrolRange;
    }

    void EnterAlertState()
    {
        currentState = State.Alert;
        alertTimer = alertDuration;

        // Stop movement
        rb.velocity = Vector2.zero;

        // Start alert animation
        StartCoroutine(AlertAnimation());
    }

    void EnterWarningState()
    {
        currentState = State.Warning;

        // Create warning area
        StartCoroutine(CreateWarningArea());
    }

    IEnumerator AlertAnimation()
    {
        // Show exclamation mark
        if (exclamationRenderer != null)
        {
            exclamationRenderer.enabled = true;
        }

        // Play sound if available
        if (alertSound != null)
        {
            Instantiate(alertSound, transform.position, Quaternion.identity);
        }

        // Change color to alert color
        if (bodyRenderer != null)
        {
            bodyRenderer.color = alertColor;
        }

        // Alert bounce animation
        Vector3 startPos = transform.position;
        Vector3 highPos = startPos + new Vector3(0, 0.5f, 0);
        Vector3 lowPos = startPos - new Vector3(0, 0.2f, 0);

        // Quick up
        float duration = 0.2f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, highPos, t);
            yield return null;
        }

        // Quick down
        elapsed = 0;
        duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(highPos, lowPos, t);
            yield return null;
        }

        // Back to middle
        elapsed = 0;
        duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(lowPos, startPos, t);
            yield return null;
        }

        // Wait for alert duration to complete
        yield return new WaitForSeconds(alertDuration - 0.5f);

        // Hide exclamation mark
        if (exclamationRenderer != null)
        {
            exclamationRenderer.enabled = false;
        }
    }

    IEnumerator CreateWarningArea()
    {
        // Determine warning area position (at player's current position)
        Vector3 warningPosition = player.position;

        // Create warning area visualization
        if (warningAreaPrefab != null)
        {
            activeWarningArea = Instantiate(warningAreaPrefab, warningPosition, Quaternion.identity);

            // Set size and color
            Transform areaTransform = activeWarningArea.transform;
            areaTransform.localScale = new Vector3(warningRadius * 2, warningRadius * 2, 1);

            // Try to set color if it has a renderer
            SpriteRenderer areaRenderer = activeWarningArea.GetComponent<SpriteRenderer>();
            if (areaRenderer != null)
            {
                areaRenderer.color = warningColor;
            }

            // Make it grow from small to full size
            areaTransform.localScale = Vector3.zero;

            float growDuration = 0.3f;
            float elapsed = 0;

            while (elapsed < growDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / growDuration;
                areaTransform.localScale = Vector3.Lerp(Vector3.zero, new Vector3(warningRadius * 2, warningRadius * 2, 1), t);
                yield return null;
            }

            // Wait for warning duration
            yield return new WaitForSeconds(warningExplosionDelay);

            // Check for player in warning area
            if (Vector2.Distance(player.position, warningPosition) <= warningRadius)
            {
                // Damage player
                PlayerController playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TakeDamage(warningDamage, transform);
                }
            }

            // Create explosion effect
            StartCoroutine(WarningExplosion(warningPosition));

            // Destroy warning area
            Destroy(activeWarningArea);
            activeWarningArea = null;
        }

        // Enter cooldown state
        currentState = State.Cooldown;
        cooldownTimer = alertCooldown;

        // Reset color
        if (bodyRenderer != null)
        {
            bodyRenderer.color = normalColor;
        }
    }

    IEnumerator WarningExplosion(Vector3 position)
    {
        // Create simple explosion effect
        GameObject explosion = new GameObject("Explosion");
        explosion.transform.position = position;

        // Add a sprite renderer with a circle sprite
        SpriteRenderer explosionRenderer = explosion.AddComponent<SpriteRenderer>();
        explosionRenderer.sprite = indicatorRenderer?.sprite; // Reuse indicator sprite
        explosionRenderer.color = new Color(1, 0.3f, 0, 0.8f);

        // Start small
        explosion.transform.localScale = Vector3.zero;

        // Grow quickly
        float growDuration = 0.2f;
        float elapsed = 0;

        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / growDuration;
            explosion.transform.localScale = Vector3.Lerp(Vector3.zero, new Vector3(warningRadius * 2.5f, warningRadius * 2.5f, 1), t);
            explosionRenderer.color = new Color(1, 0.3f, 0, 0.8f * (1 - t));
            yield return null;
        }

        // Destroy explosion
        Destroy(explosion);
    }

    IEnumerator PulseIndicator()
    {
        // Get the original scale of the indicator dot
        Vector3 originalScale = indicatorDot.localScale;

        // Make indicator dot pulse continuously, but with a much smaller range
        while (!isDead)
        {
            if (indicatorRenderer != null)
            {
                // Pulse size - use a much smaller pulse amount (0.05-0.1 instead of previous value)
                float pulse = 1 + Mathf.Sin(Time.time * pulseSpeed) * 0.05f; // Reduced from pulseAmount to 0.05f

                // Apply to original scale instead of Vector3.one
                indicatorDot.localScale = originalScale * pulse;

                // Pulse opacity in alert state
                if (currentState == State.Alert || currentState == State.Warning)
                {
                    float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 4);
                    indicatorRenderer.color = new Color(indicatorRenderer.color.r, indicatorRenderer.color.g, indicatorRenderer.color.b, alpha);
                }
                else
                {
                    indicatorRenderer.color = new Color(indicatorRenderer.color.r, indicatorRenderer.color.g, indicatorRenderer.color.b, 1);
                }
            }

            yield return null;
        }
    }

    void ApplyVisualEffects()
    {
        // Apply bounce effect
        if (currentState == State.Patrol || currentState == State.Cooldown)
        {
            float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = new Vector3(transform.position.x, transform.position.y + bounce * Time.deltaTime, transform.position.z);
        }

        // Apply pulsing to alert body if not in alert state
        if (currentState != State.Alert && currentState != State.Warning && bodyRenderer != null)
        {
            float pulse = 1 + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            alertBody.localScale = Vector3.one * pulse;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || isInvincible) return;

        // Apply damage
        health -= damage;

        // Visual feedback
        StartCoroutine(DamageFlash());

        // Check if dead
        if (health <= 0)
        {
            Die();
        }
        else
        {
            // Brief invincibility
            StartCoroutine(InvincibilityFrames());
        }
    }

    IEnumerator DamageFlash()
    {
        // Flash white
        if (bodyRenderer != null)
        {
            Color originalColor = bodyRenderer.color;
            bodyRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            bodyRenderer.color = originalColor;
        }
    }

    IEnumerator InvincibilityFrames()
    {
        isInvincible = true;

        // Flash effect
        if (bodyRenderer != null)
        {
            Color originalColor = bodyRenderer.color;

            for (int i = 0; i < 3; i++)
            {
                bodyRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
                yield return new WaitForSeconds(0.1f);
                bodyRenderer.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        yield return new WaitForSeconds(invincibilityDuration - 0.6f);
        isInvincible = false;
    }

    void Die()
    {
        isDead = true;

        // Stop all coroutines
        StopAllCoroutines();

        // Disable collision
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Stop movement
        rb.velocity = Vector2.zero;

        // Start death effect
        StartCoroutine(DeathEffect());
    }

    IEnumerator DeathEffect()
    {
        // Get all renderers
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        // Fade out
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Color color = renderer.color;
                    renderer.color = new Color(color.r, color.g, color.b, 1 - t);
                }
            }

            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

            yield return null;
        }

        // Destroy game object
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        // Check if it's the player
        if (other.CompareTag("Player"))
        {
            // Damage player
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(contactDamage, transform);
                lastDamageTime = Time.time;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead) return;

        // Check if enough time has passed for another damage tick
        if (Time.time >= lastDamageTime + damageInterval)
        {
            // Check if it's the player
            if (other.CompareTag("Player"))
            {
                // Damage player
                PlayerController playerController = other.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TakeDamage(contactDamage, transform);
                    lastDamageTime = Time.time;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw patrol range
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(Application.isPlaying ? initialPosition : transform.position, patrolRange);

        // Draw detection range
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw warning area preview
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.right * 3, warningRadius);
    }
}