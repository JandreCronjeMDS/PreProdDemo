using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AdwareEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform starburstBody;
    public Transform[] popupWindows;
    public Transform[] exclamationMarks;
    public TextMeshPro[] popupTexts;
    private Transform player;
    private Rigidbody2D rb;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float jitterAmount = 0.3f;
    public float jitterSpeed = 8f;
    public float rotationSpeed = 20f;
    public float aggroRange = 8f;
    public float pursuitSpeed = 3.5f;

    [Header("Attack")]
    public float attackRange = 5f;
    public float attackCooldown = 3f;
    public int contactDamage = 10;
    public GameObject popupProjectilePrefab;
    public float projectileSpeed = 5f;
    public int projectileDamage = 8;
    private float attackTimer;

    [Header("Popups")]
    public float popupSpawnInterval = 2f;
    public float popupDuration = 4f;
    public string[] popupMessages = new string[] {
        "FREE!", "CLICK!", "YOU WON!", "HOT SINGLES!", "DOWNLOAD NOW!"
    };
    public Color[] popupColors = new Color[] {
        Color.cyan, Color.yellow, Color.magenta, Color.red
    };
    private float popupTimer;

    [Header("Health")]
    public int health = 80;
    public float damageFlashDuration = 0.1f;
    private bool isDead = false;

    // State variables
    private enum State { Wander, Chase, Attack, Stunned }
    private State currentState = State.Wander;
    private Vector2 wanderTarget;
    private float wanderTimer;
    private float lastDamageTime;
    private float damageInterval = 0.5f;
    private int activePopups = 0;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Initialize timers
        attackTimer = attackCooldown;
        popupTimer = popupSpawnInterval;

        // Set initial wander target
        SetNewWanderTarget();

        // Deactivate all popups initially
        foreach (Transform popup in popupWindows)
        {
            popup.gameObject.SetActive(false);
        }

        // Start exclamation mark blinking
        StartCoroutine(BlinkExclamationMarks());
    }

    void Update()
    {
        if (isDead) return;

        // Update timers
        attackTimer -= Time.deltaTime;
        popupTimer -= Time.deltaTime;

        // Handle popup spawn logic
        if (popupTimer <= 0 && activePopups < popupWindows.Length)
        {
            SpawnPopup();
            popupTimer = popupSpawnInterval;
        }

        // Rotate starburst body
        if (starburstBody != null)
        {
            starburstBody.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }

        // Apply jitter to position
        transform.position += (Vector3)Random.insideUnitCircle * jitterAmount * Time.deltaTime;

        // Check player distance for state changes
        if (player != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // State transitions
            switch (currentState)
            {
                case State.Wander:
                    // Update wander behavior
                    UpdateWander();

                    // Switch to chase if player is in range
                    if (distanceToPlayer < aggroRange)
                    {
                        currentState = State.Chase;
                    }
                    break;

                case State.Chase:
                    // Chase player
                    Vector2 direction = (player.position - transform.position).normalized;
                    rb.velocity = direction * pursuitSpeed;

                    // Switch back to wander if player is out of range
                    if (distanceToPlayer > aggroRange * 1.2f)
                    {
                        currentState = State.Wander;
                        SetNewWanderTarget();
                    }

                    // Switch to attack if in range and cooldown is ready
                    if (distanceToPlayer < attackRange && attackTimer <= 0)
                    {
                        currentState = State.Attack;
                        StartCoroutine(PerformAttack());
                    }
                    break;

                case State.Attack:
                    // Attack state is handled by coroutine
                    rb.velocity = Vector2.zero;
                    break;

                case State.Stunned:
                    // Stunned state - do nothing, handled by coroutine
                    break;
            }
        }
    }

    void UpdateWander()
    {
        // Move toward wander target
        Vector2 direction = (wanderTarget - (Vector2)transform.position).normalized;
        rb.velocity = direction * moveSpeed;

        // Update wander timer
        wanderTimer -= Time.deltaTime;

        // Set new wander target when timer expires or we're close to current target
        float distanceToTarget = Vector2.Distance(transform.position, wanderTarget);
        if (wanderTimer <= 0 || distanceToTarget < 0.5f)
        {
            SetNewWanderTarget();
        }
    }

    void SetNewWanderTarget()
    {
        // Set a random target position
        wanderTarget = (Vector2)transform.position + Random.insideUnitCircle * 5f;
        wanderTimer = Random.Range(3f, 5f);
    }

    void SpawnPopup()
    {
        // Find an inactive popup window
        for (int i = 0; i < popupWindows.Length; i++)
        {
            if (!popupWindows[i].gameObject.activeSelf)
            {
                StartCoroutine(ShowPopup(i));
                break;
            }
        }
    }

    IEnumerator ShowPopup(int index)
    {
        if (index >= popupWindows.Length) yield break;

        // Increment active popups counter
        activePopups++;

        // Get random position around the adware
        Vector3 popupPos = transform.position;
        popupPos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);

        // Set random color and message
        SpriteRenderer renderer = popupWindows[index].GetComponent<SpriteRenderer>();
        if (renderer != null && popupColors.Length > 0)
        {
            renderer.color = popupColors[Random.Range(0, popupColors.Length)];
        }

        // Set text if available
        if (index < popupTexts.Length && popupTexts[index] != null && popupMessages.Length > 0)
        {
            popupTexts[index].text = popupMessages[Random.Range(0, popupMessages.Length)];
        }

        // Activate and animate popup
        Transform popup = popupWindows[index];
        popup.gameObject.SetActive(true);
        popup.position = popupPos;

        // Scale in animation
        Vector3 originalScale = popup.localScale;
        popup.localScale = Vector3.zero;

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            popup.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
            yield return null;
        }

        // Wait for popup duration
        yield return new WaitForSeconds(popupDuration);

        // Scale out animation
        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            popup.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }

        // Deactivate popup
        popup.gameObject.SetActive(false);

        // Decrement active popups counter
        activePopups--;
    }

    IEnumerator PerformAttack()
    {
        // Prepare for attack
        rb.velocity = Vector2.zero;

        // Flash effect
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            Color originalColor = renderer.color;
            renderer.color = Color.white;
            yield return new WaitForSeconds(0.2f);
            renderer.color = originalColor;
        }

        // Attack (shoot popups)
        int popupsToShoot = Random.Range(3, 6);

        for (int i = 0; i < popupsToShoot; i++)
        {
            // Launch popup projectile if prefab exists
            if (popupProjectilePrefab != null && player != null)
            {
                // Calculate direction to player with slight variation
                Vector2 direction = (player.position - transform.position).normalized;
                direction += Random.insideUnitCircle * 0.2f;
                direction.Normalize();

                // Instantiate projectile
                GameObject projectile = Instantiate(popupProjectilePrefab, transform.position, Quaternion.identity);

                // Set projectile properties
                Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
                if (projectileRb != null)
                {
                    projectileRb.velocity = direction * projectileSpeed;

                    // Auto-rotate projectile to face movement direction
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
                }

                // Set damage amount
                PopupProjectile projectileScript = projectile.GetComponent<PopupProjectile>();
                if (projectileScript != null)
                {
                    projectileScript.damage = projectileDamage;
                }

                // Brief pause between shots
                yield return new WaitForSeconds(0.2f);
            }
        }

        // Reset attack timer
        attackTimer = attackCooldown;

        // Return to chase state
        currentState = State.Chase;
    }

    IEnumerator BlinkExclamationMarks()
    {
        while (!isDead)
        {
            // Set random blink intervals for each mark
            foreach (Transform mark in exclamationMarks)
            {
                SpriteRenderer renderer = mark.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    // Toggle visibility
                    renderer.enabled = !renderer.enabled;
                }

                // Randomize interval for next mark
                yield return new WaitForSeconds(Random.Range(0.1f, 0.3f));
            }

            // Wait before next blink sequence
            yield return new WaitForSeconds(0.2f);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

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
            // Get briefly stunned
            StartCoroutine(Stunned());
        }
    }

    IEnumerator DamageFlash()
    {
        // Flash all renderers white
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        List<Color> originalColors = new List<Color>();

        foreach (SpriteRenderer renderer in renderers)
        {
            originalColors.Add(renderer.color);
            renderer.color = Color.white;
        }

        yield return new WaitForSeconds(damageFlashDuration);

        // Restore original colors
        for (int i = 0; i < renderers.Length; i++)
        {
            if (i < originalColors.Count)
            {
                renderers[i].color = originalColors[i];
            }
        }
    }

    IEnumerator Stunned()
    {
        // Enter stunned state
        State previousState = currentState;
        currentState = State.Stunned;

        // Stop movement
        rb.velocity = Vector2.zero;

        // Visual feedback - rapid flashing
        SpriteRenderer bodyRenderer = starburstBody?.GetComponent<SpriteRenderer>();
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
        else
        {
            // If no renderer, just wait
            yield return new WaitForSeconds(0.6f);
        }

        // Return to previous state
        currentState = previousState;
    }

    void Die()
    {
        isDead = true;

        // Stop all movement
        rb.velocity = Vector2.zero;

        // Disable collider
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Play death effect
        StartCoroutine(DeathEffect());
    }

    IEnumerator DeathEffect()
    {
        // Hide all popups immediately
        foreach (Transform popup in popupWindows)
        {
            popup.gameObject.SetActive(false);
        }

        // Get all renderers
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        // Fade out and shrink
        float duration = 1.0f;
        float elapsed = 0f;

        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Shrink
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);

            // Fade
            foreach (SpriteRenderer renderer in renderers)
            {
                Color color = renderer.color;
                renderer.color = new Color(color.r, color.g, color.b, 1 - t);
            }

            yield return null;
        }

        // Destroy the object
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
        // Draw aggro range
        Gizmos.color = new Color(1f, 1f, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Draw attack range
        Gizmos.color = new Color(1f, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

// Add this class for the popup projectiles
public class PopupProjectile : MonoBehaviour
{
    public int damage = 8;
    public float lifetime = 5f;
    public GameObject hitEffectPrefab;

    private void Start()
    {
        // Destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if hit player
        if (other.CompareTag("Player"))
        {
            // Deal damage
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(damage, transform);
            }

            // Spawn hit effect if available
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // Destroy projectile
            Destroy(gameObject);
        }
        // Check if hit wall/obstacle
        else if (other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            // Spawn hit effect if available
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // Destroy projectile
            Destroy(gameObject);
        }
    }
}