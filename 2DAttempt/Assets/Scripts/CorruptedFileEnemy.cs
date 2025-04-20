using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CorruptedFileEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform mainBlob;
    public Transform[] secondaryBlobs;
    public Transform[] binaryBits;
    public GameObject splitCorruptedFilePrefab;
    public GameObject corruptedProjectilePrefab; // Projectile prefab for ranged attacks
    public GameObject glitchEffectPrefab; // Optional visual effect for attacks

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float burstSpeed = 3.5f; // Speed for quick bursts
    public float erraticInterval = 1.0f;
    public float erraticIntensity = 3.0f;
    public float detectionRange = 6.0f;
    public float retreatRange = 2.5f; // Enemy will try to maintain this distance from player
    public float pulseSpeed = 2.0f;
    public float pulseAmount = 0.15f;

    [Header("Combat")]
    public int health = 50;
    public int contactDamage = 8;
    public int projectileDamage = 5;
    public int damageToSplit = 25;
    public int splitsRemaining = 1; // How many times it can split into smaller versions
    public bool splitOnDeath = true;
    public LayerMask playerLayer;
    public float attackCooldown = 2f;
    public float projectileSpeed = 5f;
    public float projectileLifetime = 3f;
    public float burstRetreatDuration = 0.5f; // How long the retreat burst lasts

    [Header("Advanced Combat")]
    public bool useDefensiveBurst = true; // Quick burst away when player gets too close
    public bool useProjectileAttacks = true; // Shoot projectiles at player
    public float projectileSpread = 15f; // Angle spread for multi-projectile attacks
    public int projectilesPerAttack = 3; // Number of projectiles per attack
    public float defensiveBurstThreshold = 3f; // Distance to trigger defensive burst

    [Header("Visual Effects")]
    [ColorUsage(true, true)] public Color mainGlitchColor = new Color(0.7f, 0.2f, 1f, 1f); // More visible purple
    [ColorUsage(true, true)] public Color[] glitchColors; // Different colors for main and secondary blobs
    public float glitchInterval = 0.2f;
    public float colorChangeInterval = 0.5f;
    public float binaryFlipInterval = 0.1f;
    public float disruptionWaveRadius = 4f; // Special attack effect radius
    public float disruptionWaveCooldown = 5f;

    // Private state
    private Rigidbody2D rb;
    private Transform player;
    private Vector2 moveDirection;
    private float erraticTimer;
    private float glitchTimer;
    private float colorTimer;
    private float binaryTimer;
    private float attackTimer;
    private float disruptionWaveTimer;
    private bool isGlitching = false;
    private int currentColor = 0;
    private SpriteRenderer mainRenderer;
    private SpriteRenderer[] blobRenderers;
    private bool isDead = false;
    private float lastDamageTime;
    private float damageInterval = 0.5f;
    private bool isRetreating = false;
    private bool isAttacking = false;
    private Vector2 retreatDirection;

    // Attack detection
    private float lastPlayerDistance;
    private float approachSpeed;
    private bool playerApproaching = false;
    private Vector3 lastPlayerPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Set up random initial values
        erraticTimer = Random.Range(0f, erraticInterval);
        glitchTimer = Random.Range(0f, glitchInterval);
        colorTimer = Random.Range(0f, colorChangeInterval);
        binaryTimer = Random.Range(0f, binaryFlipInterval);
        attackTimer = Random.Range(1f, attackCooldown);
        disruptionWaveTimer = Random.Range(2f, disruptionWaveCooldown);

        // Cache renderers
        mainRenderer = mainBlob.GetComponent<SpriteRenderer>();

        blobRenderers = new SpriteRenderer[secondaryBlobs.Length];
        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            blobRenderers[i] = secondaryBlobs[i].GetComponent<SpriteRenderer>();
            // Position secondary blobs slightly away from main blob to make colors more visible
            if (secondaryBlobs[i] != null)
            {
                float angle = i * (360f / secondaryBlobs.Length) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.5f;
                secondaryBlobs[i].localPosition = offset;
            }
        }

        // Set random initial direction
        moveDirection = Random.insideUnitCircle.normalized;

        // Initialize player tracking
        if (player != null)
        {
            lastPlayerDistance = Vector2.Distance(transform.position, player.position);
            lastPlayerPosition = player.position;
        }

        // Apply initial colors
        ApplyGlitchColors();
    }

    void Update()
    {
        if (isDead) return;

        // Update timers
        erraticTimer -= Time.deltaTime;
        glitchTimer -= Time.deltaTime;
        colorTimer -= Time.deltaTime;
        binaryTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        disruptionWaveTimer -= Time.deltaTime;

        // Handle player approach detection
        if (player != null)
        {
            float currentDistance = Vector2.Distance(transform.position, player.position);
            approachSpeed = lastPlayerDistance - currentDistance;
            lastPlayerDistance = currentDistance;

            // Detect if player is dashing toward us (quick approach)
            playerApproaching = approachSpeed > 0.2f; // Threshold to detect player approach

            // Track player movement
            Vector3 playerMovement = player.position - lastPlayerPosition;
            lastPlayerPosition = player.position;
        }

        // Handle erratic movement changes
        if (erraticTimer <= 0 && !isRetreating && !isAttacking)
        {
            ChangeDirection();
            erraticTimer = Random.Range(erraticInterval * 0.5f, erraticInterval * 1.5f);
        }

        // Handle visual glitching
        if (glitchTimer <= 0)
        {
            StartCoroutine(GlitchEffect());
            glitchTimer = Random.Range(glitchInterval * 0.8f, glitchInterval * 1.2f);
        }

        // Handle color changes
        if (colorTimer <= 0 && glitchColors.Length > 0)
        {
            ChangeColor();
            colorTimer = Random.Range(colorChangeInterval * 0.8f, colorChangeInterval * 1.2f);
        }

        // Handle binary bit flipping
        if (binaryTimer <= 0 && binaryBits.Length > 0)
        {
            FlipBinaryValues();
            binaryTimer = Random.Range(binaryFlipInterval * 0.8f, binaryFlipInterval * 1.2f);
        }

        // Handle special disruption wave attack
        if (disruptionWaveTimer <= 0)
        {
            StartCoroutine(DisruptionWaveAttack());
            disruptionWaveTimer = disruptionWaveCooldown;
        }

        // Animate blobs with pulsing
        AnimateBlobs();

        // Process player interaction
        if (player != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // Defensive burst if player gets too close and is approaching fast
            if (useDefensiveBurst &&
                distanceToPlayer < defensiveBurstThreshold &&
                playerApproaching &&
                !isRetreating &&
                !isAttacking)
            {
                StartCoroutine(DefensiveBurst());
            }

            // Projectile attack if cooldown is ready
            if (useProjectileAttacks &&
                attackTimer <= 0 &&
                distanceToPlayer < detectionRange &&
                !isRetreating &&
                !isAttacking)
            {
                StartCoroutine(ProjectileAttack());
                attackTimer = attackCooldown;
            }

            // Movement behavior if not doing other actions
            if (!isRetreating && !isAttacking)
            {
                // If player is too close, try to maintain distance
                if (distanceToPlayer < retreatRange)
                {
                    Vector2 awayFromPlayer = (transform.position - player.position).normalized;
                    moveDirection = Vector2.Lerp(moveDirection, awayFromPlayer, 0.2f).normalized;
                }
                // If player is within detection range but not too close, adjust direction
                else if (distanceToPlayer < detectionRange)
                {
                    // Calculate direction to player
                    Vector2 dirToPlayer = (player.position - transform.position).normalized;

                    // Blend between random and player-following direction based on distance
                    float followStrength = 1.0f - (distanceToPlayer / detectionRange);
                    moveDirection = Vector2.Lerp(moveDirection, dirToPlayer, followStrength * 0.1f).normalized;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // Apply movement
        if (isRetreating)
        {
            // Higher speed during retreat
            rb.velocity = retreatDirection * burstSpeed;
        }
        else if (!isAttacking)
        {
            // Normal movement
            rb.velocity = moveDirection * moveSpeed;
        }
    }

    void ChangeDirection()
    {
        // Generate a new random direction with some influence from previous direction
        Vector2 randomDir = Random.insideUnitCircle.normalized * erraticIntensity;
        moveDirection = (moveDirection + randomDir).normalized;
    }

    void AnimateBlobs()
    {
        // Pulse the main blob
        float mainPulse = 1.0f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        mainBlob.localScale = Vector3.one * mainPulse;

        // Animate secondary blobs with varied pulsing
        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            float offset = i * 0.5f;
            float pulse = 1.0f + Mathf.Sin((Time.time + offset) * pulseSpeed) * pulseAmount;
            secondaryBlobs[i].localScale = Vector3.one * pulse;
        }
    }

    void ApplyGlitchColors()
    {
        // Apply main color to main blob
        if (mainRenderer != null)
        {
            mainRenderer.color = mainGlitchColor;
        }

        // Apply random glitch colors to secondary blobs
        if (glitchColors.Length > 0)
        {
            for (int i = 0; i < blobRenderers.Length; i++)
            {
                if (blobRenderers[i] != null)
                {
                    // Give each secondary blob a different color
                    int colorIndex = i % glitchColors.Length;
                    blobRenderers[i].color = glitchColors[colorIndex];
                }
            }
        }
    }

    void ChangeColor()
    {
        if (glitchColors.Length == 0) return;

        // Cycle through colors for main blob
        currentColor = (currentColor + 1) % glitchColors.Length;

        // Apply to secondary blobs - shift colors around
        for (int i = 0; i < blobRenderers.Length; i++)
        {
            if (blobRenderers[i] != null)
            {
                int colorIndex = (i + currentColor) % glitchColors.Length;
                blobRenderers[i].color = glitchColors[colorIndex];
            }
        }
    }

    void FlipBinaryValues()
    {
        // Randomly flip binary displays (0 to 1, or 1 to 0)
        foreach (Transform bit in binaryBits)
        {
            if (Random.value > 0.7f)
            {
                // Find the Text component or TextMeshPro component
                UnityEngine.UI.Text textComponent = bit.GetComponent<UnityEngine.UI.Text>();
                if (textComponent != null)
                {
                    textComponent.text = textComponent.text == "0" ? "1" : "0";
                }

                // Try TextMeshPro if present
                TMPro.TextMeshPro tmpComponent = bit.GetComponent<TMPro.TextMeshPro>();
                if (tmpComponent != null)
                {
                    tmpComponent.text = tmpComponent.text == "0" ? "1" : "0";
                }

                // Try TextMeshProUGUI if present
                TMPro.TextMeshProUGUI tmpuiComponent = bit.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmpuiComponent != null)
                {
                    tmpuiComponent.text = tmpuiComponent.text == "0" ? "1" : "0";
                }
            }
        }
    }

    IEnumerator GlitchEffect()
    {
        if (isGlitching) yield break;

        isGlitching = true;

        // Store original positions and scales
        Vector3[] originalBlobPositions = new Vector3[secondaryBlobs.Length];
        Vector3[] originalBlobScales = new Vector3[secondaryBlobs.Length];

        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            originalBlobPositions[i] = secondaryBlobs[i].localPosition;
            originalBlobScales[i] = secondaryBlobs[i].localScale;
        }

        // Apply rapid visual distortions
        for (int j = 0; j < 3; j++) // Do 3 quick glitch frames
        {
            // Distort blob positions
            for (int i = 0; i < secondaryBlobs.Length; i++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.3f, 0.3f),
                    0
                );

                secondaryBlobs[i].localPosition = originalBlobPositions[i] + randomOffset;

                // Maybe distort scale too
                if (Random.value > 0.5f)
                {
                    secondaryBlobs[i].localScale = originalBlobScales[i] * Random.Range(0.7f, 1.4f);
                }
            }

            // Maybe flash main blob
            if (mainRenderer != null && Random.value > 0.5f)
            {
                // Pick a random high-intensity glitch color for flashing
                Color flashColor = Random.value > 0.5f ?
                    Color.white :
                    (glitchColors.Length > 0 ? glitchColors[Random.Range(0, glitchColors.Length)] : Color.cyan);

                mainRenderer.color = flashColor;
            }

            // Brief pause between glitch frames
            yield return new WaitForSeconds(0.05f);
        }

        // Restore original positions and scales
        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            secondaryBlobs[i].localPosition = originalBlobPositions[i];
            secondaryBlobs[i].localScale = originalBlobScales[i];
        }

        // Restore main blob color
        if (mainRenderer != null)
        {
            mainRenderer.color = mainGlitchColor;
        }

        isGlitching = false;
    }

    IEnumerator ProjectileAttack()
    {
        if (corruptedProjectilePrefab == null || player == null) yield break;

        isAttacking = true;

        // Visual telegraph
        StartCoroutine(AttackTelegraph());
        yield return new WaitForSeconds(0.5f);

        // Calculate direction to player
        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        // Fire projectiles in a spread pattern
        for (int i = 0; i < projectilesPerAttack; i++)
        {
            // Calculate spread angle for multiple projectiles
            float spreadAngle = 0;
            if (projectilesPerAttack > 1)
            {
                spreadAngle = projectileSpread * ((float)i / (projectilesPerAttack - 1) - 0.5f);
            }

            // Rotate the direction by spread angle
            Vector2 projectileDirection = RotateVector(directionToPlayer, spreadAngle);

            // Create projectile
            GameObject projectile = Instantiate(
                corruptedProjectilePrefab,
                transform.position,
                Quaternion.identity
            );

            // Set projectile velocity
            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.velocity = projectileDirection * projectileSpeed;
            }

            // Configure projectile properties
            CorruptedProjectile projectileComponent = projectile.GetComponent<CorruptedProjectile>();
            if (projectileComponent != null)
            {
                projectileComponent.damage = projectileDamage;
                projectileComponent.lifetime = projectileLifetime;
                // Pass any other needed properties
            }

            yield return new WaitForSeconds(0.1f); // Slight delay between projectiles
        }

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    IEnumerator AttackTelegraph()
    {
        // Visual indicator for attack telegraph
        float duration = 0.5f;
        float elapsed = 0f;
        Color originalMainColor = mainRenderer.color;

        // Store original colors for all blobs
        Color[] originalBlobColors = new Color[blobRenderers.Length];
        for (int i = 0; i < blobRenderers.Length; i++)
        {
            if (blobRenderers[i] != null)
            {
                originalBlobColors[i] = blobRenderers[i].color;
            }
        }

        // Flash all blobs to indicate attack is coming
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Pulse size to indicate attack
            float pulseFactor = 1f + 0.3f * Mathf.Sin(t * Mathf.PI * 4);
            mainBlob.localScale = Vector3.one * pulseFactor;

            // Flash colors
            if (Mathf.FloorToInt(elapsed * 10f) % 2 == 0)
            {
                mainRenderer.color = Color.white;
                foreach (var renderer in blobRenderers)
                {
                    if (renderer != null) renderer.color = Color.white;
                }
            }
            else
            {
                mainRenderer.color = Color.red;
                foreach (var renderer in blobRenderers)
                {
                    if (renderer != null) renderer.color = Color.red;
                }
            }

            yield return null;
        }

        // Restore original colors
        mainRenderer.color = originalMainColor;
        for (int i = 0; i < blobRenderers.Length; i++)
        {
            if (blobRenderers[i] != null)
            {
                blobRenderers[i].color = originalBlobColors[i];
            }
        }
    }

    IEnumerator DefensiveBurst()
    {
        if (player == null) yield break;

        isRetreating = true;

        // Calculate retreat direction (away from player)
        retreatDirection = (transform.position - player.position).normalized;

        // Visual effect for defensive burst
        StartCoroutine(BurstEffect());

        // Apply burst movement for a short duration
        yield return new WaitForSeconds(burstRetreatDuration);

        isRetreating = false;
    }

    IEnumerator BurstEffect()
    {
        // Quick flash effect for burst
        if (mainRenderer != null)
        {
            Color originalColor = mainRenderer.color;

            for (int i = 0; i < 3; i++)
            {
                mainRenderer.color = Color.cyan;
                yield return new WaitForSeconds(0.05f);
                mainRenderer.color = originalColor;
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    IEnumerator DisruptionWaveAttack()
    {
        // Special AOE attack
        isAttacking = true;

        // Visual telegraph - grow and pulse
        float telegraphDuration = 0.7f;
        float elapsed = 0f;
        Vector3 originalMainScale = mainBlob.localScale;

        // Store original positions & scales
        Vector3[] originalPositions = new Vector3[secondaryBlobs.Length];
        Vector3[] originalScales = new Vector3[secondaryBlobs.Length];

        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            originalPositions[i] = secondaryBlobs[i].localPosition;
            originalScales[i] = secondaryBlobs[i].localScale;
        }

        // Telegraph animation - pull in secondary blobs
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / telegraphDuration;

            // Expand main blob
            mainBlob.localScale = Vector3.Lerp(originalMainScale, originalMainScale * 1.5f, t);

            // Pull in secondary blobs
            for (int i = 0; i < secondaryBlobs.Length; i++)
            {
                secondaryBlobs[i].localPosition = Vector3.Lerp(
                    originalPositions[i],
                    originalPositions[i] * 0.3f, // Pull toward center
                    t
                );

                // Shrink secondary blobs
                secondaryBlobs[i].localScale = Vector3.Lerp(
                    originalScales[i],
                    originalScales[i] * 0.5f,
                    t
                );
            }

            yield return null;
        }

        // Execute disruption wave
        if (glitchEffectPrefab != null)
        {
            // Spawn wave effect
            GameObject waveEffect = Instantiate(glitchEffectPrefab, transform.position, Quaternion.identity);

            // Scale to match radius
            waveEffect.transform.localScale = Vector3.one * (disruptionWaveRadius * 2);

            // Destroy after animation
            Destroy(waveEffect, 1f);
        }

        // Check for player in range
        if (player != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= disruptionWaveRadius)
            {
                // Apply effects to player
                PlayerController playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    // Deal damage
                    playerController.TakeDamage(contactDamage * 2, transform);

                    // Could add additional effects like screen glitching here
                }
            }
        }

        // Explosion animation
        elapsed = 0f;
        float explosionDuration = 0.3f;

        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;

            // Expand secondary blobs outward rapidly
            for (int i = 0; i < secondaryBlobs.Length; i++)
            {
                secondaryBlobs[i].localPosition = Vector3.Lerp(
                    originalPositions[i] * 0.3f,
                    originalPositions[i] * 2f, // Explode outward
                    t
                );

                // Flash colors
                if (blobRenderers[i] != null)
                {
                    if (Mathf.FloorToInt(elapsed * 20f) % 2 == 0)
                    {
                        blobRenderers[i].color = Color.white;
                    }
                    else if (glitchColors.Length > 0)
                    {
                        blobRenderers[i].color = glitchColors[i % glitchColors.Length];
                    }
                }
            }

            yield return null;
        }

        // Reset everything
        mainBlob.localScale = originalMainScale;

        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            secondaryBlobs[i].localPosition = originalPositions[i];
            secondaryBlobs[i].localScale = originalScales[i];

            if (blobRenderers[i] != null && glitchColors.Length > 0)
            {
                blobRenderers[i].color = glitchColors[i % glitchColors.Length];
            }
        }

        isAttacking = false;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;

        // Visual feedback
        StartCoroutine(DamageFlash());

        // Check if should split
        if (health <= damageToSplit && splitsRemaining > 0 && splitCorruptedFilePrefab != null)
        {
            Split();
        }
        // Check if dead
        else if (health <= 0)
        {
            if (splitOnDeath && splitsRemaining > 0 && splitCorruptedFilePrefab != null)
            {
                Split();
            }
            else
            {
                Die();
            }
        }
        else
        {
            // If not dead or splitting, try defensive burst
            if (useDefensiveBurst && player != null && !isRetreating)
            {
                StartCoroutine(DefensiveBurst());
            }
        }
    }

    void Split()
    {
        // Create smaller versions
        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
            GameObject splitBlob = Instantiate(splitCorruptedFilePrefab, transform.position + offset, Quaternion.identity);

            // Configure the new blob with reduced stats
            CorruptedFileEnemy splitEnemy = splitBlob.GetComponent<CorruptedFileEnemy>();
            if (splitEnemy != null)
            {
                splitEnemy.health = Mathf.Max(10, health / 2);
                splitEnemy.moveSpeed = moveSpeed * 1.2f;
                splitEnemy.splitsRemaining = splitsRemaining - 1;
                splitEnemy.transform.localScale = transform.localScale * 0.6f;

                // Transfer some other properties
                if (glitchColors.Length > 0)
                {
                    splitEnemy.glitchColors = glitchColors;
                }
            }
        }

        // Kill the original without further splitting
        isDead = true;
        Destroy(gameObject);
    }

    void Die()
    {
        isDead = true;
        StartCoroutine(DeathEffect());
    }

    IEnumerator DamageFlash()
    {
        // Flash white on all renderers
        if (mainRenderer != null)
        {
            Color originalColor = mainRenderer.color;
            mainRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            mainRenderer.color = originalColor;
        }

        foreach (SpriteRenderer renderer in blobRenderers)
        {
            if (renderer != null)
            {
                Color originalColor = renderer.color;
                renderer.color = Color.white;
                yield return new WaitForSeconds(0.1f);
                renderer.color = originalColor;
            }
        }
    }

    IEnumerator DeathEffect()
    {
        // Disable collider and physics
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        // Play death animation
        float duration = 0.5f;
        float elapsed = 0f;

        // Get all sprite renderers
        List<SpriteRenderer> allRenderers = new List<SpriteRenderer>();
        if (mainRenderer != null) allRenderers.Add(mainRenderer);
        allRenderers.AddRange(blobRenderers);

        // Create fragment effect as part of death
        Vector3 originalMainPos = mainBlob.position;
        Vector3[] originalBlobPos = new Vector3[secondaryBlobs.Length];
        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            originalBlobPos[i] = secondaryBlobs[i].position;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Shrink main blob
            mainBlob.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

            // Add explosion-like effect to secondary blobs
            for (int i = 0; i < secondaryBlobs.Length; i++)
            {
                // Calculate explosion direction
                Vector3 explosionDir = (secondaryBlobs[i].position - transform.position).normalized;
                if (explosionDir.magnitude < 0.1f)
                {
                    explosionDir = Random.insideUnitSphere;
                }

                // Move outward while shrinking
                secondaryBlobs[i].position = Vector3.Lerp(
                    originalBlobPos[i],
                    originalBlobPos[i] + explosionDir * 2f,
                    t
                );

                secondaryBlobs[i].localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            }

            // Fade all renderers
            foreach (SpriteRenderer renderer in allRenderers)
            {
                if (renderer != null)
                {
                    Color color = renderer.color;
                    renderer.color = new Color(color.r, color.g, color.b, 1 - t);
                }
            }

            yield return null;
        }

        // Destroy the game object
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

                // Activate defensive burst when touched
                if (useDefensiveBurst && !isRetreating)
                {
                    StartCoroutine(DefensiveBurst());
                }
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

    // Helper methods
    private Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw retreat range
        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // Draw disruption wave range
        Gizmos.color = new Color(0.5f, 0, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, disruptionWaveRadius);
    }
}

// Add companion class for projectiles
public class CorruptedProjectile : MonoBehaviour
{
    public int damage = 5;
    public float lifetime = 3f;
    public GameObject hitEffectPrefab;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if hit player
        if (other.CompareTag("Player"))
        {
            // Apply damage
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(damage, transform);
            }

            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // Destroy projectile
            Destroy(gameObject);
        }
        // Also destroy on collision with environment
        else if (other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}