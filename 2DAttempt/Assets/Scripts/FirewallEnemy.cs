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

    [Header("Active Fire Colors")]
    public Color[] fireColors = new Color[] {
        new Color(1f, 0.3f, 0.1f, 1f),  // Deep orange
        new Color(1f, 0.5f, 0.1f, 1f),  // Orange
        new Color(1f, 0.7f, 0.2f, 1f),  // Light orange/yellow
        new Color(1f, 0.8f, 0.3f, 1f)   // Yellow
    };
    public float colorChangeSpeed = 1.5f; // Speed at which colors shift

    [Header("Inactive Ember Effect")]
    public Color baseEmberColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Base gray color
    public Color emberGlowColor = new Color(0.6f, 0.2f, 0.1f, 1f); // Reddish ember color
    public float emberGlowIntensity = 0.3f; // How strong the ember effect is (0-1)
    public float emberFlowSpeed = 0.8f; // How quickly the ember effect moves
    public float emberVariation = 0.4f; // How varied the ember effect is

    // State tracking
    private bool isActive;
    private float lastDamageTime;
    private float activationTimer;
    private SpriteRenderer[] layerRenderers;
    private SpriteRenderer[] flameRenderers;
    private Vector3[] flameOriginalPositions;
    private Vector3[] flameOriginalScales;
    private float[] brickColorOffsets; // Random offset for each brick's color animation

    void Start()
    {
        // Find player
        player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerController>();

        // Initialize state
        isActive = startActive;
        lastDamageTime = -damageInterval; // Allow immediate damage on first contact

        // Cache renderers and initial transforms
        InitializeRenderers();

        // Generate random offsets for each brick's color animation
        InitializeBrickColorOffsets();

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

        // Process animations based on state
        if (isActive)
        {
            // Check for player contact
            CheckPlayerContact();

            // Update visual effects for active state
            AnimateFirewall();
            AnimateBrickColors();
        }
        else
        {
            // Update visual effects for inactive state
            AnimateEmbers();
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

    void InitializeBrickColorOffsets()
    {
        // Create random offsets for each brick to make them animate at different phases
        brickColorOffsets = new float[layerRects.Length];
        for (int i = 0; i < layerRects.Length; i++)
        {
            brickColorOffsets[i] = Random.Range(0f, 10f); // Random starting offset
        }
    }

    void UpdateVisuals()
    {
        // Update layer colors
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            if (layerRenderers[i] != null)
            {
                if (isActive)
                {
                    // Set initial fire color
                    int colorIndex = i % fireColors.Length;
                    layerRenderers[i].color = fireColors[colorIndex];
                }
                else
                {
                    // Set initial ember color (gray with slight variation)
                    layerRenderers[i].color = baseEmberColor;
                }
            }
        }

        // Update flame visibility
        foreach (Transform flame in flames)
        {
            flame.gameObject.SetActive(isActive);
        }
    }

    void AnimateBrickColors()
    {
        // Skip if no fire colors defined
        if (fireColors.Length == 0)
            return;

        // Animate each brick with fire-like color transitions
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            if (layerRenderers[i] != null)
            {
                // Calculate unique time value for this brick
                float timeValue = Time.time * colorChangeSpeed + brickColorOffsets[i];

                // Make bottom bricks redder/darker and top bricks more yellow/brighter
                float rowFactor = (float)i / layerRenderers.Length; // 0 = bottom, 1 = top

                // Calculate which two colors to blend between
                int colorIndex = Mathf.FloorToInt(timeValue % fireColors.Length);
                int nextColorIndex = (colorIndex + 1) % fireColors.Length;

                // Get blend factor between the two colors (0-1)
                float blendFactor = Mathf.PingPong(timeValue, 1f);

                // Create the brick color by blending between the two chosen colors
                Color brickColor = Color.Lerp(fireColors[colorIndex], fireColors[nextColorIndex], blendFactor);

                // Apply row-based tinting (bottom rows darker, top rows brighter)
                brickColor = Color.Lerp(
                    new Color(brickColor.r * 0.8f, brickColor.g * 0.6f, brickColor.b * 0.6f, brickColor.a),
                    new Color(brickColor.r, brickColor.g * 1.1f, brickColor.b * 0.5f, brickColor.a),
                    rowFactor);

                // Apply the color to the brick
                layerRenderers[i].color = brickColor;
            }
        }
    }

    void AnimateEmbers()
    {
        // Animate each brick with ember-like effects
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            if (layerRenderers[i] != null)
            {
                // Calculate unique time value for this brick
                float timeValue = Time.time * emberFlowSpeed + brickColorOffsets[i];

                // Generate ember glow pattern using Perlin noise
                // This creates a flowing, organic pattern that moves across the bricks
                float emberX = (i % 3) * 0.5f; // Horizontal position within grid
                float emberY = (i / 3) * 0.5f; // Vertical position within grid

                // Use noise to create flowing pattern
                float noiseVal = Mathf.PerlinNoise(
                    emberX + timeValue * 0.3f,
                    emberY + timeValue * 0.2f
                );

                // Add some variation with sin waves
                float sineVal = Mathf.Sin(timeValue + i * 0.7f) * 0.5f + 0.5f;

                // Combine noise and sine for more organic flow
                float emberValue = Mathf.Lerp(noiseVal, sineVal, 0.3f);

                // Apply random variation
                emberValue *= 1f + Random.Range(-emberVariation, emberVariation);

                // Clamp and scale the ember intensity
                emberValue = Mathf.Clamp01(emberValue) * emberGlowIntensity;

                // Create ember color by blending base color with glow color
                Color emberColor = Color.Lerp(baseEmberColor, emberGlowColor, emberValue);

                // Bottom bricks should be slightly more intense (like real embers)
                float bottomFactor = 1f - ((float)i / layerRenderers.Length);
                emberColor = Color.Lerp(emberColor, emberGlowColor, bottomFactor * 0.2f);

                // Apply the color to the brick
                layerRenderers[i].color = emberColor;
            }
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

                // Apply pulse to opacity without changing the color
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

                // Also animate flame colors
                if (flameRenderers[i] != null)
                {
                    // Make flames shift between colors a bit faster than bricks
                    float timeValue = Time.time * (colorChangeSpeed * 1.5f) + i;

                    // Calculate which two colors to blend between
                    int colorIndex = Mathf.FloorToInt(timeValue % fireColors.Length);
                    int nextColorIndex = (colorIndex + 1) % fireColors.Length;

                    // Get blend factor between the two colors (0-1)
                    float blendFactor = Mathf.PingPong(timeValue, 1f);

                    // Create the flame color by blending between the two chosen colors
                    Color flameColor = Color.Lerp(fireColors[colorIndex], fireColors[nextColorIndex], blendFactor);

                    // Make flames brighter than bricks
                    flameColor = new Color(
                        Mathf.Min(flameColor.r * 1.2f, 1f),
                        Mathf.Min(flameColor.g * 1.2f, 1f),
                        flameColor.b,
                        flameColor.a
                    );

                    // Apply the color
                    flameRenderers[i].color = flameColor;
                }
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

        // Store original colors of bricks before transition
        Color[] originalBrickColors = new Color[layerRenderers.Length];
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            if (layerRenderers[i] != null)
            {
                originalBrickColors[i] = layerRenderers[i].color;
            }
        }

        // Prepare target colors based on transition direction
        Color[] targetColors = new Color[layerRenderers.Length];
        for (int i = 0; i < layerRenderers.Length; i++)
        {
            if (wasActive) // Deactivating - use varied ember colors
            {
                // Calculate a unique ember color for this brick
                float rowFactor = (float)i / layerRenderers.Length; // Bottom-to-top factor
                float randomFactor = Random.Range(0f, 0.3f); // Random variation
                targetColors[i] = Color.Lerp(baseEmberColor, emberGlowColor, randomFactor + (rowFactor * 0.1f));
            }
            else // Activating - use fire colors
            {
                // Get a color from the fire palette based on brick position
                int colorIndex = i % fireColors.Length;

                // Bottom bricks more red, top bricks more yellow
                float rowFactor = (float)i / layerRenderers.Length;
                targetColors[i] = Color.Lerp(
                    fireColors[0], // More red
                    fireColors[Mathf.Min(fireColors.Length - 1, 2)], // More yellow
                    rowFactor
                );
            }
        }

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

                // Fade each brick to its target ember color
                for (int i = 0; i < layerRenderers.Length; i++)
                {
                    if (layerRenderers[i] != null)
                    {
                        layerRenderers[i].color = Color.Lerp(originalBrickColors[i], targetColors[i], t);
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

                // Fade each brick from ember to its target fire color
                for (int i = 0; i < layerRenderers.Length; i++)
                {
                    if (layerRenderers[i] != null)
                    {
                        layerRenderers[i].color = Color.Lerp(originalBrickColors[i], targetColors[i], t);
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