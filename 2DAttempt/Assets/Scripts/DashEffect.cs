using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject dashTrailPrefab; // Optional - can create trails procedurally

    [Header("Dash Streak Settings")]
    [SerializeField] private int streakCount = 5;
    [SerializeField] private float streakSpacing = 0.15f;
    [SerializeField] private float streakLifetime = 0.3f;
    [SerializeField]
    private Color[] streakColors = new Color[] {
        new Color(1f, 1f, 1f, 0.8f),
        new Color(0.9f, 0.9f, 0.9f, 0.6f),
        new Color(0.8f, 0.8f, 0.8f, 0.4f)
    };
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private bool useTrailRenderer = false;

    [Header("Dash Line Settings")]
    [SerializeField] private bool createDashLine = true;
    [SerializeField] private float dashLineWidth = 0.15f;
    [SerializeField] private Color dashLineColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private float dashLineDuration = 0.2f;

    [Header("Dash Particles")]
    [SerializeField] private bool createDashParticles = true;
    [SerializeField] private int particlesPerDash = 15;
    [SerializeField] private float particleMinSize = 0.05f;
    [SerializeField] private float particleMaxSize = 0.15f;
    [SerializeField] private float particleLifetime = 0.5f;
    [SerializeField] private float particleSpeedMultiplier = 1.5f;
    [SerializeField] private Color[] particleColors;

    // State tracking
    private bool wasDashing = false;
    private Vector3 lastPosition;
    private Vector3 dashDirection;
    private float dashSpeed;

    // Object pooling
    private Queue<GameObject> streakPool = new Queue<GameObject>();
    private Queue<GameObject> particlePool = new Queue<GameObject>();
    private int poolSize = 30;

    // Line renderer for dash line
    private LineRenderer dashLineRenderer;

    private void Start()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        // Create object pools
        for (int i = 0; i < poolSize; i++)
        {
            if (!useTrailRenderer)
            {
                streakPool.Enqueue(CreateStreak());
            }

            if (createDashParticles)
            {
                particlePool.Enqueue(CreateParticle());
            }
        }

        // Create dash line renderer
        if (createDashLine)
        {
            GameObject lineObj = new GameObject("DashLine");
            lineObj.transform.SetParent(transform);
            dashLineRenderer = lineObj.AddComponent<LineRenderer>();
            dashLineRenderer.startWidth = dashLineWidth;
            dashLineRenderer.endWidth = dashLineWidth;
            dashLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            dashLineRenderer.startColor = dashLineColor;
            dashLineRenderer.endColor = new Color(dashLineColor.r, dashLineColor.g, dashLineColor.b, 0);
            dashLineRenderer.positionCount = 2;
            dashLineRenderer.enabled = false;
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (playerController == null) return;

        // Get player state
        bool isDashing = GetFieldValue<bool>(playerController, "isDashing");

        // If just started dashing
        if (isDashing && !wasDashing)
        {
            // Cache dash parameters
            dashDirection = transform.position - lastPosition;
            if (dashDirection.magnitude > 0.01f)
            {
                dashDirection.Normalize();
            }
            else
            {
                dashDirection = transform.localScale.x > 0 ? Vector3.right : Vector3.left;
            }

            dashSpeed = GetFieldValue<float>(playerController, "dashSpeed");

            // Create dash effects
            StartCoroutine(CreateDashEffects());
        }

        // Update tracking
        wasDashing = isDashing;
        lastPosition = transform.position;
    }

    private IEnumerator CreateDashEffects()
    {
        // Store dash start position
        Vector3 dashStartPos = transform.position;

        // Create streak effects
        if (!useTrailRenderer)
        {
            // Create a sequence of streak objects
            for (int i = 0; i < streakCount; i++)
            {
                // Wait a short delay between streaks
                yield return new WaitForSeconds(streakSpacing / dashSpeed);

                // Get streak from pool
                GameObject streak = GetStreakFromPool();
                if (streak == null) continue;

                // Position streak at current position
                streak.transform.position = transform.position;

                // Set sprite to match player's sprite
                SpriteRenderer playerRenderer = GetComponent<SpriteRenderer>();
                SpriteRenderer streakRenderer = streak.GetComponent<SpriteRenderer>();

                if (playerRenderer != null && streakRenderer != null)
                {
                    streakRenderer.sprite = playerRenderer.sprite;
                    streakRenderer.flipX = playerRenderer.flipX;

                    // Set random color from streak colors
                    Color streakColor = streakColors[Random.Range(0, streakColors.Length)];
                    streakRenderer.color = streakColor;

                    // Start streak fade out
                    StartCoroutine(FadeOutStreak(streak, streakLifetime));
                }
            }
        }

        // Spawn dash particles in burst
        if (createDashParticles)
        {
            for (int i = 0; i < particlesPerDash; i++)
            {
                GameObject particle = GetParticleFromPool();
                if (particle == null) continue;

                // Position particle at current position with slight randomness
                particle.transform.position = transform.position + new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(-0.2f, 0.2f),
                    0
                );

                // Set random color
                SpriteRenderer particleRenderer = particle.GetComponent<SpriteRenderer>();
                if (particleRenderer != null)
                {
                    // Use particle colors if defined, otherwise use streak colors
                    Color[] colorsToUse = particleColors != null && particleColors.Length > 0 ?
                                         particleColors : streakColors;

                    particleRenderer.color = colorsToUse[Random.Range(0, colorsToUse.Length)];

                    // Set random size
                    float size = Random.Range(particleMinSize, particleMaxSize);
                    particle.transform.localScale = new Vector3(size, size, size);

                    // Start particle movement and fade out
                    StartCoroutine(AnimateParticle(particle, particleLifetime));
                }
            }
        }

        // Create dash line
        if (createDashLine && dashLineRenderer != null)
        {
            dashLineRenderer.enabled = true;
            dashLineRenderer.SetPosition(0, dashStartPos);

            // Animate dash line
            float time = 0;
            while (time < dashLineDuration)
            {
                // Update end position to current position
                dashLineRenderer.SetPosition(1, transform.position);

                // Fade out over time
                float normalizedTime = time / dashLineDuration;
                Color fadeColor = dashLineColor;
                fadeColor.a = dashLineColor.a * (1 - normalizedTime);
                dashLineRenderer.endColor = fadeColor;

                time += Time.deltaTime;
                yield return null;
            }

            dashLineRenderer.enabled = false;
        }
    }

    private IEnumerator FadeOutStreak(GameObject streak, float duration)
    {
        SpriteRenderer renderer = streak.GetComponent<SpriteRenderer>();
        if (renderer == null) yield break;

        Color initialColor = renderer.color;
        float time = 0;

        while (time < duration)
        {
            float t = time / duration;
            Color newColor = initialColor;
            newColor.a = initialColor.a * fadeOutCurve.Evaluate(t);
            renderer.color = newColor;

            time += Time.deltaTime;
            yield return null;
        }

        // Return to pool
        streak.SetActive(false);
        streakPool.Enqueue(streak);
    }

    private IEnumerator AnimateParticle(GameObject particle, float duration)
    {
        SpriteRenderer renderer = particle.GetComponent<SpriteRenderer>();
        if (renderer == null) yield break;

        Color initialColor = renderer.color;
        Vector3 initialScale = particle.transform.localScale;

        // Calculate random direction (opposite to dash direction with some variation)
        Vector3 direction = -dashDirection * particleSpeedMultiplier;
        direction += new Vector3(
            Random.Range(-0.5f, 0.5f),
            Random.Range(-0.5f, 0.5f),
            0
        );

        float speed = Random.Range(1f, 3f) * particleSpeedMultiplier;
        float time = 0;

        while (time < duration)
        {
            float t = time / duration;

            // Move particle
            particle.transform.position += direction * speed * Time.deltaTime;

            // Slow down over time
            speed = Mathf.Lerp(speed, 0.2f, Time.deltaTime * 5f);

            // Fade out
            Color newColor = initialColor;
            newColor.a = initialColor.a * (1 - t);
            renderer.color = newColor;

            // Shrink slightly
            particle.transform.localScale = initialScale * (1 - (t * 0.5f));

            time += Time.deltaTime;
            yield return null;
        }

        // Return to pool
        particle.SetActive(false);
        particlePool.Enqueue(particle);
    }

    private GameObject CreateStreak()
    {
        GameObject streak = new GameObject("DashStreak");
        streak.transform.SetParent(transform);

        // Add sprite renderer
        SpriteRenderer renderer = streak.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = -1; // Behind player

        // Disable initially
        streak.SetActive(false);

        return streak;
    }

    private GameObject CreateParticle()
    {
        GameObject particle = new GameObject("DashParticle");
        particle.transform.SetParent(transform);

        // Add sprite renderer with square sprite
        SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite();
        renderer.sortingOrder = -1; // Behind player

        // Disable initially
        particle.SetActive(false);

        return particle;
    }

    private Sprite CreateSquareSprite()
    {
        // Create a small white square texture
        Texture2D texture = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++)
        {
            colors[i] = Color.white;
        }
        texture.SetPixels(colors);
        texture.Apply();

        // Create sprite from texture
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    private GameObject GetStreakFromPool()
    {
        if (streakPool.Count == 0)
        {
            return CreateStreak();
        }

        GameObject streak = streakPool.Dequeue();
        streak.SetActive(true);
        return streak;
    }

    private GameObject GetParticleFromPool()
    {
        if (particlePool.Count == 0)
        {
            return CreateParticle();
        }

        GameObject particle = particlePool.Dequeue();
        particle.SetActive(true);
        return particle;
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
}