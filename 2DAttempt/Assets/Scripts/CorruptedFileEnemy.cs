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

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float erraticInterval = 1.0f;
    public float erraticIntensity = 3.0f;
    public float detectionRange = 6.0f;
    public float pulseSpeed = 2.0f;
    public float pulseAmount = 0.15f;

    [Header("Combat")]
    public int health = 50;
    public int contactDamage = 8;
    public int damageToSplit = 25;
    public int splitsRemaining = 1; // How many times it can split into smaller versions
    public bool splitOnDeath = true;
    public LayerMask playerLayer;

    [Header("Visual Effects")]
    public Color[] glitchColors;
    public float glitchInterval = 0.2f;
    public float colorChangeInterval = 0.5f;
    public float binaryFlipInterval = 0.1f;

    // Private state
    private Rigidbody2D rb;
    private Transform player;
    private Vector2 moveDirection;
    private float erraticTimer;
    private float glitchTimer;
    private float colorTimer;
    private float binaryTimer;
    private bool isGlitching = false;
    private int currentColor = 0;
    private SpriteRenderer mainRenderer;
    private SpriteRenderer[] blobRenderers;
    private bool isDead = false;
    private float lastDamageTime;
    private float damageInterval = 0.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Set up random initial values
        erraticTimer = Random.Range(0f, erraticInterval);
        glitchTimer = Random.Range(0f, glitchInterval);
        colorTimer = Random.Range(0f, colorChangeInterval);
        binaryTimer = Random.Range(0f, binaryFlipInterval);

        // Cache renderers
        mainRenderer = mainBlob.GetComponent<SpriteRenderer>();

        blobRenderers = new SpriteRenderer[secondaryBlobs.Length];
        for (int i = 0; i < secondaryBlobs.Length; i++)
        {
            blobRenderers[i] = secondaryBlobs[i].GetComponent<SpriteRenderer>();
        }

        // Set random initial direction
        moveDirection = Random.insideUnitCircle.normalized;
    }

    void Update()
    {
        if (isDead) return;

        // Update timers
        erraticTimer -= Time.deltaTime;
        glitchTimer -= Time.deltaTime;
        colorTimer -= Time.deltaTime;
        binaryTimer -= Time.deltaTime;

        // Handle erratic movement changes
        if (erraticTimer <= 0)
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

        // Animate blobs with pulsing
        AnimateBlobs();

        // Check for player proximity for chase behavior
        if (player != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer < detectionRange)
            {
                // Calculate direction to player
                Vector2 dirToPlayer = (player.position - transform.position).normalized;

                // Blend between random and player-following direction based on distance
                float followStrength = 1.0f - (distanceToPlayer / detectionRange);
                moveDirection = Vector2.Lerp(moveDirection, dirToPlayer, followStrength * 0.1f).normalized;
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // Apply movement
        rb.velocity = moveDirection * moveSpeed;
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

    void ChangeColor()
    {
        if (glitchColors.Length == 0) return;

        // Go to next color
        currentColor = (currentColor + 1) % glitchColors.Length;
        Color newColor = glitchColors[currentColor];

        // Apply to main blob
        if (mainRenderer != null)
        {
            mainRenderer.color = newColor;
        }

        // Apply to some secondary blobs randomly
        foreach (SpriteRenderer renderer in blobRenderers)
        {
            if (renderer != null && Random.value > 0.5f)
            {
                renderer.color = newColor;
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
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(-0.2f, 0.2f),
                    0
                );

                secondaryBlobs[i].localPosition = originalBlobPositions[i] + randomOffset;

                // Maybe distort scale too
                if (Random.value > 0.5f)
                {
                    secondaryBlobs[i].localScale = originalBlobScales[i] * Random.Range(0.8f, 1.2f);
                }
            }

            // Maybe flash main blob
            if (mainRenderer != null && Random.value > 0.7f)
            {
                mainRenderer.color = new Color(1, 1, 1, 0.8f); // Flash white
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

        // Restore main blob color if it was flashed
        if (mainRenderer != null)
        {
            mainRenderer.color = glitchColors.Length > 0 ? glitchColors[currentColor] : Color.white;
        }

        isGlitching = false;
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

            Die();
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

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Shrink and fade
            mainBlob.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

            foreach (Transform blob in secondaryBlobs)
            {
                blob.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            }

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
        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}