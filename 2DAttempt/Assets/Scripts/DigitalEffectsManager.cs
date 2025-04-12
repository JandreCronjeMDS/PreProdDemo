using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DigitalEffectsManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Camera mainCamera;

    [Header("Glitch Effects")]
    [SerializeField] private bool enableGlitchEffects = true;
    [SerializeField] private float minTimeBetweenGlitches = 3f;
    [SerializeField] private float maxTimeBetweenGlitches = 8f;
    [SerializeField] private float glitchDuration = 0.2f;
    [SerializeField] private Material glitchMaterial;

    [Header("Screen Shake")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float dashShakeIntensity = 0.1f;
    [SerializeField] private float landingShakeIntensity = 0.15f;
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("Pixel Fragmentation")]
    [SerializeField] private bool enableFragmentation = true;
    [SerializeField] private GameObject pixelFragmentPrefab;
    [SerializeField] private int fragmentsOnDash = 15;
    [SerializeField] private int fragmentsOnLand = 20;
    [SerializeField] private float fragmentSpeed = 3f;
    [SerializeField] private float fragmentLifetime = 1f;
    [SerializeField]
    private Color[] fragmentColors = new Color[] {
        new Color(0.2f, 0.8f, 1f), // Cyan
        new Color(0.8f, 0.2f, 1f), // Magenta
        new Color(0.2f, 1f, 0.2f), // Green
        new Color(1f, 1f, 0.2f)    // Yellow
    };

    [Header("Digital Trails")]
    [SerializeField] private bool enableDigitalTrails = true;
    [SerializeField] private float trailSpawnRate = 0.05f;
    [SerializeField] private float trailSize = 0.15f;
    [SerializeField] private float trailLifetime = 0.5f;
    [SerializeField] private GameObject digitalTrailPrefab;

    [Header("Scan Lines")]
    [SerializeField] private bool enableScanLines = true;
    [SerializeField] private float scanLineSpeed = 2f;
    [SerializeField] private float scanLineWidth = 0.05f;
    [SerializeField] private float scanLineOpacity = 0.2f;

    // State tracking
    private bool wasGrounded = true;
    private bool wasDashing = false;
    private float lastGlitchTime;
    private float lastTrailTime;
    private Vector3 lastPosition;

    // Object pooling
    private Queue<GameObject> fragmentPool = new Queue<GameObject>();
    private Queue<GameObject> trailPool = new Queue<GameObject>();
    private int poolSize = 100;

    // Scan line
    private GameObject scanLine;
    private SpriteRenderer scanLineRenderer;

    // Post-processing
    private Material postProcessMaterial;
    private bool isGlitching = false;

    private void Start()
    {
        // Find references if not set
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Initialize object pools
        if (enableFragmentation)
        {
            InitializeFragmentPool();
        }

        if (enableDigitalTrails)
        {
            InitializeTrailPool();
        }

        // Create scan line
        if (enableScanLines)
        {
            CreateScanLine();
        }

        // Initialize post-processing material
        if (enableGlitchEffects && glitchMaterial != null)
        {
            postProcessMaterial = new Material(glitchMaterial);
        }

        // Start glitch coroutine
        if (enableGlitchEffects)
        {
            StartCoroutine(RandomGlitchRoutine());
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (playerController == null) return;

        // Get player state
        bool isGrounded = GetFieldValue<bool>(playerController, "isGrounded");
        bool isDashing = GetFieldValue<bool>(playerController, "isDashing");

        // Handle landing
        if (isGrounded && !wasGrounded)
        {
            OnPlayerLand();
        }

        // Handle dash
        if (isDashing && !wasDashing)
        {
            OnPlayerDash();
        }

        // Update scan line
        if (enableScanLines && scanLine != null)
        {
            UpdateScanLine();
        }

        // Spawn digital trails
        if (enableDigitalTrails && Time.time > lastTrailTime + trailSpawnRate)
        {
            SpawnDigitalTrail();
            lastTrailTime = Time.time;
        }

        // Store current state for next frame
        wasGrounded = isGrounded;
        wasDashing = isDashing;
        lastPosition = transform.position;
    }

    private void OnPlayerLand()
    {
        // Get impact force from rigidbody velocity
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float impactForce = rb != null ? Mathf.Abs(rb.velocity.y) : 1f;

        // Only apply effects if impact is significant
        if (impactForce > 3f)
        {
            // Screen shake
            if (enableScreenShake)
            {
                StartCoroutine(ShakeCamera(landingShakeIntensity * (impactForce / 10f), shakeDuration));
            }

            // Spawn fragments
            if (enableFragmentation)
            {
                SpawnFragments(fragmentsOnLand, 0.8f, 0.3f);
            }

            // Trigger glitch
            if (enableGlitchEffects && Random.value > 0.5f)
            {
                TriggerGlitch(glitchDuration);
            }
        }
    }

    private void OnPlayerDash()
    {
        // Screen shake
        if (enableScreenShake)
        {
            StartCoroutine(ShakeCamera(dashShakeIntensity, shakeDuration));
        }

        // Spawn fragments
        if (enableFragmentation)
        {
            SpawnFragments(fragmentsOnDash, 1.5f, 0f);
        }

        // Trigger glitch
        if (enableGlitchEffects && Random.value > 0.7f)
        {
            TriggerGlitch(glitchDuration);
        }
    }

    private void SpawnFragments(int count, float speedMultiplier, float downwardBias)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject fragment = GetFragmentFromPool();
            if (fragment == null) continue;

            // Position at player
            fragment.transform.position = transform.position + new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-0.3f, 0.3f),
                0
            );

            // Set random size
            float size = Random.Range(0.05f, 0.15f);
            fragment.transform.localScale = new Vector3(size, size, size);

            // Set random color
            SpriteRenderer renderer = fragment.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = fragmentColors[Random.Range(0, fragmentColors.Length)];
            }

            // Start motion
            StartCoroutine(AnimateFragment(
                fragment,
                GetRandomDirection(downwardBias),
                fragmentSpeed * speedMultiplier,
                fragmentLifetime
            ));
        }
    }

    private IEnumerator AnimateFragment(GameObject fragment, Vector3 direction, float speed, float lifetime)
    {
        float time = 0;
        SpriteRenderer renderer = fragment.GetComponent<SpriteRenderer>();
        Color initialColor = renderer.color;

        // Add slight rotation
        float rotSpeed = Random.Range(-180f, 180f);

        while (time < lifetime)
        {
            // Move fragment
            fragment.transform.position += direction * speed * Time.deltaTime;

            // Rotate fragment
            fragment.transform.Rotate(0, 0, rotSpeed * Time.deltaTime);

            // Fade out near end of lifetime
            if (time > lifetime * 0.7f)
            {
                float fadeProgress = (time - (lifetime * 0.7f)) / (lifetime * 0.3f);
                Color newColor = initialColor;
                newColor.a = Mathf.Lerp(initialColor.a, 0, fadeProgress);
                renderer.color = newColor;
            }

            // Slow down over time
            speed *= 0.98f;

            time += Time.deltaTime;
            yield return null;
        }

        // Return to pool
        fragment.SetActive(false);
        fragmentPool.Enqueue(fragment);
    }

    private void SpawnDigitalTrail()
    {
        GameObject trail = GetTrailFromPool();
        if (trail == null) return;

        // Position at player with slight random offset
        trail.transform.position = transform.position + new Vector3(
            Random.Range(-0.1f, 0.1f),
            Random.Range(-0.1f, 0.1f),
            0
        );

        // Set size
        trail.transform.localScale = new Vector3(trailSize, trailSize, trailSize);

        // Random rotation
        trail.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));

        // Set color
        SpriteRenderer renderer = trail.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Color trailColor = fragmentColors[Random.Range(0, fragmentColors.Length)];
            trailColor.a = 0.7f;
            renderer.color = trailColor;
        }

        // Start fade animation
        StartCoroutine(FadeOutTrail(trail, trailLifetime));
    }

    private IEnumerator FadeOutTrail(GameObject trail, float duration)
    {
        SpriteRenderer renderer = trail.GetComponent<SpriteRenderer>();
        if (renderer == null) yield break;

        Color initialColor = renderer.color;
        float time = 0;

        while (time < duration)
        {
            float t = time / duration;
            Color newColor = initialColor;
            newColor.a = initialColor.a * (1 - t);
            renderer.color = newColor;

            time += Time.deltaTime;
            yield return null;
        }

        // Return to pool
        trail.SetActive(false);
        trailPool.Enqueue(trail);
    }

    private void UpdateScanLine()
    {
        // Move scan line up and down screen
        float yPos = Mathf.Sin(Time.time * scanLineSpeed) * mainCamera.orthographicSize;
        Vector3 scanLinePosition = new Vector3(mainCamera.transform.position.x, yPos, 10);
        scanLine.transform.position = scanLinePosition;
    }

    private IEnumerator ShakeCamera(float intensity, float duration)
    {
        if (mainCamera == null) yield break;

        Vector3 originalPosition = mainCamera.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Calculate position with decreasing intensity over time
            float percentComplete = elapsed / duration;
            float damper = 1.0f - Mathf.Clamp01(4.0f * percentComplete - 3.0f);
            float x = Random.Range(-1f, 1f) * intensity * damper;
            float y = Random.Range(-1f, 1f) * intensity * damper;

            // Apply shake
            mainCamera.transform.position = new Vector3(
                originalPosition.x + x,
                originalPosition.y + y,
                originalPosition.z
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset to original position
        mainCamera.transform.position = originalPosition;
    }

    private IEnumerator RandomGlitchRoutine()
    {
        while (true)
        {
            // Wait random time
            float waitTime = Random.Range(minTimeBetweenGlitches, maxTimeBetweenGlitches);
            yield return new WaitForSeconds(waitTime);

            // Trigger glitch
            TriggerGlitch(glitchDuration);
        }
    }

    private void TriggerGlitch(float duration)
    {
        if (isGlitching) return;
        StartCoroutine(GlitchRoutine(duration));
    }

    private IEnumerator GlitchRoutine(float duration)
    {
        isGlitching = true;

        // Apply glitch shader to camera
        if (mainCamera != null && postProcessMaterial != null)
        {
            // Enable glitch effect
            postProcessMaterial.SetFloat("_GlitchIntensity", Random.Range(0.1f, 0.3f));
            postProcessMaterial.SetFloat("_ColorGlitchIntensity", Random.Range(0.1f, 0.2f));

            // Set material on camera
            mainCamera.GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;

            // Apply material through Graphics.Blit in OnRenderImage
        }

        // Randomly disable sprite renderers briefly
        StartCoroutine(GlitchSpriteRenderers(duration));

        // Wait for duration
        yield return new WaitForSeconds(duration);

        // Disable glitch effect
        if (mainCamera != null && postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_GlitchIntensity", 0);
            postProcessMaterial.SetFloat("_ColorGlitchIntensity", 0);
        }

        isGlitching = false;
    }

    private IEnumerator GlitchSpriteRenderers(float duration)
    {
        // Find all sprite renderers in player
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        // Save original states
        Dictionary<SpriteRenderer, bool> originalStates = new Dictionary<SpriteRenderer, bool>();
        foreach (SpriteRenderer renderer in renderers)
        {
            originalStates[renderer] = renderer.enabled;
        }

        // Do random on/off flickers
        float elapsed = 0;
        while (elapsed < duration)
        {
            // Random flicker
            if (Random.value > 0.5f)
            {
                foreach (SpriteRenderer renderer in renderers)
                {
                    renderer.enabled = Random.value > 0.3f;
                }
            }

            // Short wait
            yield return new WaitForSeconds(0.01f);
            elapsed += 0.01f;
        }

        // Restore original states
        foreach (SpriteRenderer renderer in renderers)
        {
            if (originalStates.ContainsKey(renderer))
            {
                renderer.enabled = originalStates[renderer];
            }
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Apply post-processing if glitch is active
        if (isGlitching && postProcessMaterial != null)
        {
            Graphics.Blit(source, destination, postProcessMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    #region Initialization and Pooling

    private void InitializeFragmentPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            fragmentPool.Enqueue(CreateFragment());
        }
    }

    private GameObject CreateFragment()
    {
        GameObject fragment;

        // Use prefab if provided
        if (pixelFragmentPrefab != null)
        {
            fragment = Instantiate(pixelFragmentPrefab, transform);
        }
        else
        {
            // Create a basic square
            fragment = new GameObject("PixelFragment");
            fragment.transform.SetParent(transform);

            // Add sprite renderer with square sprite
            SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSquareSprite();
            renderer.sortingOrder = 5; // Above player
        }

        // Disable initially
        fragment.SetActive(false);

        return fragment;
    }

    private void InitializeTrailPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            trailPool.Enqueue(CreateTrail());
        }
    }

    private GameObject CreateTrail()
    {
        GameObject trail;

        // Use prefab if provided
        if (digitalTrailPrefab != null)
        {
            trail = Instantiate(digitalTrailPrefab, transform);
        }
        else
        {
            // Create a basic shape (square/circle/diamond)
            trail = new GameObject("DigitalTrail");
            trail.transform.SetParent(transform);

            // Add sprite renderer with shape sprite
            SpriteRenderer renderer = trail.AddComponent<SpriteRenderer>();

            // Randomize between different shapes
            int shapeType = Random.Range(0, 3);
            switch (shapeType)
            {
                case 0:
                    renderer.sprite = CreateSquareSprite();
                    break;
                case 1:
                    renderer.sprite = CreateCircleSprite();
                    break;
                case 2:
                    renderer.sprite = CreateDiamondSprite();
                    break;
            }

            renderer.sortingOrder = -1; // Behind player
        }

        // Disable initially
        trail.SetActive(false);

        return trail;
    }

    private void CreateScanLine()
    {
        scanLine = new GameObject("ScanLine");
        scanLine.transform.SetParent(mainCamera.transform);

        // Add sprite renderer
        scanLineRenderer = scanLine.AddComponent<SpriteRenderer>();

        // Create texture
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        // Create sprite
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        scanLineRenderer.sprite = sprite;

        // Set color
        scanLineRenderer.color = new Color(1, 1, 1, scanLineOpacity);

        // Set scale to cover screen width
        float width = mainCamera.orthographicSize * 2 * mainCamera.aspect;
        scanLine.transform.localScale = new Vector3(width, scanLineWidth, 1);

        // Set sorting layer to overlay
        scanLineRenderer.sortingOrder = 100;
    }

    private GameObject GetFragmentFromPool()
    {
        if (fragmentPool.Count == 0)
        {
            return CreateFragment();
        }

        GameObject fragment = fragmentPool.Dequeue();
        fragment.SetActive(true);
        return fragment;
    }

    private GameObject GetTrailFromPool()
    {
        if (trailPool.Count == 0)
        {
            return CreateTrail();
        }

        GameObject trail = trailPool.Dequeue();
        trail.SetActive(true);
        return trail;
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++)
        {
            colors[i] = Color.white;
        }
        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    private Sprite CreateCircleSprite()
    {
        Texture2D texture = new Texture2D(8, 8);
        Color[] colors = new Color[64];

        // Create circle pattern
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                float distFromCenter = Mathf.Sqrt(Mathf.Pow(x - 3.5f, 2) + Mathf.Pow(y - 3.5f, 2));
                if (distFromCenter < 3.5f)
                {
                    colors[y * 8 + x] = Color.white;
                }
                else
                {
                    colors[y * 8 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
    }

    private Sprite CreateDiamondSprite()
    {
        Texture2D texture = new Texture2D(8, 8);
        Color[] colors = new Color[64];

        // Create diamond pattern
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                float manhattanDist = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f);
                if (manhattanDist < 4f)
                {
                    colors[y * 8 + x] = Color.white;
                }
                else
                {
                    colors[y * 8 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
    }

    private Vector3 GetRandomDirection(float downwardBias)
    {
        // Get random direction
        Vector2 dir = Random.insideUnitCircle.normalized;

        // Add downward bias
        dir.y -= downwardBias;

        return new Vector3(dir.x, dir.y, 0).normalized;
    }

    // Helper method to get private fields from the player controller
    private T GetFieldValue<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            return (T)field.GetValue(obj);
        }

        // Default values if field not found
        if (typeof(T) == typeof(bool)) return (T)(object)false;
        if (typeof(T) == typeof(float)) return (T)(object)0f;
        return default;
    }

    #endregion
}