using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirewallEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform[] layerRects;
    public Transform[] flames;
    private PlayerController player;

    [Header("Behavior")]
    public bool startActive = true;
    public float activationCooldown = 2f;
    public float deactivationDuration = 3f;

    [Header("Combat")]
    public int contactDamage = 10;
    public float damageInterval = 0.5f;
    public float damageRadius = 0.5f;
    public LayerMask playerLayer;

    [Header("Visual Effects")]
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.2f;
    public float flameSpeed = 4f;
    public Color activeColor = new Color(1f, 0.4f, 0.2f, 1f);   // Orange/red
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Gray

    // State tracking
    private bool isActive;
    private float lastDamageTime;
    private float activationTimer;
    private SpriteRenderer[] layerRenderers;
    private SpriteRenderer[] flameRenderers;
    private Vector3[] flameOriginalPositions;
    private Vector3[] flameOriginalScales;

    void Start()
    {
        // Find player
        player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerController>();

        // Initialize state
        isActive = startActive;
        lastDamageTime = -damageInterval; // Allow immediate damage on first contact

        // Cache renderers and initial transforms
        InitializeRenderers();

        // Apply initial visual state
        UpdateVisuals();
    }

    void Update()
    {
        // Update timers
        if (activationTimer > 0)
        {
            activationTimer -= Time.deltaTime;

            // Toggle state when timer expires
            if (activationTimer <= 0)
            {
                isActive = !isActive;
                UpdateVisuals();
            }
        }

        // Only process damage and animations when active
        if (isActive)
        {
            // Check for player contact
            CheckPlayerContact();

            // Update visual effects
            AnimateFirewall();
        }
    }

    void InitializeRenderers()
    {
        // Initialize layer renderers
        layerRenderers = new SpriteRenderer[layerRects.Length];
        for (int i = 0; i < layerRects.Length; i++)
        {
            layerRenderers[i] = layerRects[i].GetComponent<SpriteRenderer>();
            if (layerRenderers[i] == null)
            {
                Debug.LogWarning("Layer rect " + i + " is missing a SpriteRenderer component!");
            }
        }

        // Initialize flame renderers and store original positions and scales
        flameRenderers = new SpriteRenderer[flames.Length];
        flameOriginalPositions = new Vector3[flames.Length];
        flameOriginalScales = new Vector3[flames.Length];

        for (int i = 0; i < flames.Length; i++)
        {
            flameRenderers[i] = flames[i].GetComponent<SpriteRenderer>();
            if (flameRenderers[i] == null)
            {
                Debug.LogWarning("Flame " + i + " is missing a SpriteRenderer component!");
            }

            // Store original position and scale
            flameOriginalPositions[i] = flames[i].localPosition;
            flameOriginalScales[i] = flames[i].localScale;
        }
    }

    void UpdateVisuals()
    {
        // Update layer colors
        foreach (SpriteRenderer renderer in layerRenderers)
        {
            if (renderer != null)
            {
                renderer.color = isActive ? activeColor : inactiveColor;
            }
        }

        // Update flame visibility
        foreach (Transform flame in flames)
        {
            flame.gameObject.SetActive(isActive);
        }
    }

    void AnimateFirewall()
    {
        // Animate layers with subtle pulsing
        for (int i = 0; i < layerRects.Length; i++)
        {
            if (layerRenderers[i] != null)
            {
                // Each layer pulses slightly differently
                float pulseOffset = i * 0.2f;
                float pulse = 1 + Mathf.Sin((Time.time + pulseOffset) * pulseSpeed) * pulseAmount;

                // Apply pulse to opacity
                Color color = layerRenderers[i].color;
                layerRenderers[i].color = new Color(color.r, color.g, color.b,
                                                   Mathf.Lerp(0.7f, 1f, pulse));
            }
        }

        // Animate flames - accounting for center pivot
        for (int i = 0; i < flames.Length; i++)
        {
            if (flames[i] != null)
            {
                // Each flame animates a bit differently
                float heightOffset = i * 0.15f;
                float widthOffset = i * 0.1f;

                // Vary height using scale
                float heightScale = 1 + Mathf.Sin((Time.time + heightOffset) * flameSpeed) * 0.2f;

                // Vary width slightly
                float widthScale = 1 + Mathf.Sin((Time.time + widthOffset) * flameSpeed * 1.5f) * 0.1f;

                // Apply animation - scale relative to original scale
                Vector3 newScale = new Vector3(
                    flameOriginalScales[i].x * widthScale,
                    flameOriginalScales[i].y * heightScale,
                    flameOriginalScales[i].z);

                flames[i].localScale = newScale;

                // Adjust Y position to compensate for center pivot
                // This ensures the flame stays "attached" at the bottom
                float originalHeight = flameOriginalScales[i].y;
                float newHeight = newScale.y;
                float heightDifference = newHeight - originalHeight;

                // Move position up by half the difference to keep bottom fixed
                flames[i].localPosition = new Vector3(
                    flameOriginalPositions[i].x,
                    flameOriginalPositions[i].y + (heightDifference / 2),
                    flameOriginalPositions[i].z);
            }
        }
    }

    void CheckPlayerContact()
    {
        // Check for player in contact radius
        Collider2D hit = Physics2D.OverlapCircle(transform.position, damageRadius, playerLayer);

        if (hit != null && player != null && Time.time >= lastDamageTime + damageInterval)
        {
            // Deal damage to player
            player.TakeDamage(contactDamage, transform);

            // Update last damage time
            lastDamageTime = Time.time;

            // Visual feedback
            StartCoroutine(DamageFlash());
        }
    }

    IEnumerator DamageFlash()
    {
        // Briefly increase intensity of firewall when damaging player
        foreach (SpriteRenderer renderer in layerRenderers)
        {
            if (renderer != null)
            {
                // Store original color
                Color originalColor = renderer.color;

                // Set to intense color
                renderer.color = new Color(1f, 1f, 0.7f, 1f);

                // Wait briefly
                yield return new WaitForSeconds(0.1f);

                // Restore original color
                renderer.color = originalColor;
            }
        }
    }

    // Call this to toggle the firewall state
    public void Toggle()
    {
        // If already transitioning, do nothing
        if (activationTimer > 0)
            return;

        // Set timer based on current state
        activationTimer = isActive ? deactivationDuration : activationCooldown;

        // Start transition effect
        StartCoroutine(TransitionEffect());
    }

    IEnumerator TransitionEffect()
    {
        // Track original state
        bool wasActive = isActive;

        // Calculate transition duration
        float duration = wasActive ? deactivationDuration : activationCooldown;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Create visual transition effect
            if (wasActive) // Deactivating
            {
                // Fade out flames
                foreach (SpriteRenderer sr in flameRenderers)
                {
                    if (sr != null)
                    {
                        Color color = sr.color;
                        sr.color = new Color(color.r, color.g, color.b, 1 - t);
                    }
                }

                // Shrink flames while keeping bottom fixed
                for (int i = 0; i < flames.Length; i++)
                {
                    if (flames[i] != null)
                    {
                        // Scale down gradually
                        float scaleT = 1 - t;
                        Vector3 newScale = new Vector3(
                            flameOriginalScales[i].x * scaleT,
                            flameOriginalScales[i].y * scaleT,
                            flameOriginalScales[i].z);

                        flames[i].localScale = newScale;

                        // Adjust position to keep bottom fixed
                        float heightDifference = flameOriginalScales[i].y - newScale.y;
                        flames[i].localPosition = new Vector3(
                            flameOriginalPositions[i].x,
                            flameOriginalPositions[i].y - (heightDifference / 2),
                            flameOriginalPositions[i].z);
                    }
                }

                // Fade layers to inactive color
                for (int i = 0; i < layerRenderers.Length; i++)
                {
                    if (layerRenderers[i] != null)
                    {
                        layerRenderers[i].color = Color.Lerp(activeColor, inactiveColor, t);
                    }
                }
            }
            else // Activating
            {
                // Fade in flames
                foreach (SpriteRenderer sr in flameRenderers)
                {
                    if (sr != null)
                    {
                        Color color = sr.color;
                        sr.color = new Color(color.r, color.g, color.b, t);
                    }
                }

                // Grow flames while keeping bottom fixed
                for (int i = 0; i < flames.Length; i++)
                {
                    if (flames[i] != null)
                    {
                        // Scale up gradually
                        Vector3 newScale = new Vector3(
                            flameOriginalScales[i].x * t,
                            flameOriginalScales[i].y * t,
                            flameOriginalScales[i].z);

                        flames[i].localScale = newScale;

                        // Adjust position to keep bottom fixed
                        float heightDifference = flameOriginalScales[i].y - newScale.y;
                        flames[i].localPosition = new Vector3(
                            flameOriginalPositions[i].x,
                            flameOriginalPositions[i].y - (heightDifference / 2),
                            flameOriginalPositions[i].z);
                    }
                }

                // Fade layers to active color
                for (int i = 0; i < layerRenderers.Length; i++)
                {
                    if (layerRenderers[i] != null)
                    {
                        layerRenderers[i].color = Color.Lerp(inactiveColor, activeColor, t);
                    }
                }
            }

            yield return null;
        }

        // Make sure everything is set correctly at the end
        if (!wasActive) // Finished activating
        {
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] != null)
                {
                    flames[i].localScale = flameOriginalScales[i];
                    flames[i].localPosition = flameOriginalPositions[i];
                }
            }
        }
    }

    // Method to directly set the active state (for events/triggers)
    public void SetActive(bool active)
    {
        // Skip if already in desired state
        if (isActive == active)
            return;

        // Start transition
        isActive = !active; // Toggle to trigger transition
        Toggle();
    }

    private void OnDrawGizmosSelected()
    {
        // Draw damage radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, damageRadius);
    }

    // Used by external triggers or events
    public void OnPlayerSwitch(bool playerOn)
    {
        // Toggle state based on player interaction
        if (playerOn && isActive)
        {
            Toggle();
        }
        else if (!playerOn && !isActive)
        {
            Toggle();
        }
    }

    // Handle collision directly
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Skip if not active
        if (!isActive)
            return;

        // Check for player
        if (other.CompareTag("Player"))
        {
            // Deal immediate damage on first contact
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(contactDamage, transform);
                lastDamageTime = Time.time;
            }
        }
    }
}