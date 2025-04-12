using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpywareDrone : MonoBehaviour
{
    [Header("Debug & Control")]
    public bool isPaused = false;


    [Header("References")]
    public Transform body;
    public Transform iris;
    public Transform pupil;
    private Transform player;
    private Rigidbody2D rb;

    [Header("Movement")]
    public float floatSpeed = 1.2f;
    public float chaseSpeed = 2.5f;
    public float aggroRange = 7f;
    public float maintainDistance = 3f;
    public float orbitSpeed = 1f;
    public float bobAmount = 0.3f;
    public float bobSpeed = 2f;

    [Header("Scanning")]
    public float scanDuration = 2f;
    public float scanCooldown = 4f;
    public float scanRange = 5f;
    public int scanDamage = 5;
    public Color scanColor = new Color(0.77f, 0.4f, 0.99f, 0.7f); // Light purple
    private bool isScanning = false;
    private float scanTimer = 0f;
    private LineRenderer scanBeam;

    [Header("Tail Settings")]
    public float tailLength = 0.5f;
    public float tailWidth = 0.05f;
    public float tailWaveAmount = 0.2f;
    public float tailWaveSpeed = 2f;
    public float tailCircleSize = 0.15f;
    private LineRenderer tailLine;
    private Transform tailCircle;

    [Header("Visual Effects")]
    public float pulseSpeed = 1.5f;
    public float pulseAmount = 0.2f;
    public Color trailColor = new Color(0.77f, 0.4f, 0.99f, 0.5f); // Light purple
    public float blinkInterval = 4f;
    private TrailRenderer trail;
    private float blinkTimer;

    // State Variables
    private enum State { Idle, Following, Scanning, Retreating }
    private State currentState = State.Idle;
    private Vector2 idleTarget;
    private float idleTimer;
    private float idleDuration = 3f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player").transform;

        // Initialize idle behavior
        SetNewIdleTarget();

        // Set up scan beam
        InitializeScanBeam();

        // Set up trail
        InitializeTrail();

        // Set up tail
        InitializeTail();

        // Start blink timer
        blinkTimer = Random.Range(0f, blinkInterval);
    }

    void Update()
    {
        if (isPaused)
        {
            rb.velocity = Vector2.zero;

            if (scanBeam != null)
                scanBeam.enabled = false;

            currentState = State.Idle;
            return;
        }

        // Update timers
        if (scanTimer > 0)
            scanTimer -= Time.deltaTime;

        // Update pupil look
        UpdatePupilLook();

        // Update visual effects
        UpdateVisualEffects();

        // Update tail
        UpdateTail();

        // Handle blinking
        HandleBlinking();

        // Distance to player
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // State machine
        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();

                // Check if player is in range
                if (distanceToPlayer < aggroRange)
                {
                    currentState = State.Following;
                }
                break;

            case State.Following:
                UpdateFollowing();

                // Start scanning if cooldown is over
                if (scanTimer <= 0 && distanceToPlayer < scanRange)
                {
                    StartScanning();
                }

                // Retreat if player gets too close
                if (distanceToPlayer < maintainDistance * 0.5f)
                {
                    currentState = State.Retreating;
                }

                // Go back to idle if player is out of range
                if (distanceToPlayer > aggroRange * 1.5f)
                {
                    currentState = State.Idle;
                    SetNewIdleTarget();
                }
                break;

            case State.Scanning:
                UpdateScanning();
                break;

            case State.Retreating:
                UpdateRetreating();

                // Return to following when distance is good
                if (distanceToPlayer > maintainDistance)
                {
                    currentState = State.Following;
                }
                break;
        }
    }

    void UpdateIdle()
    {
        // Move toward idle target with smooth bobbing
        Vector2 targetPos = idleTarget;
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        targetPos.y += bobOffset;

        // Move toward target
        Vector2 moveDirection = (targetPos - (Vector2)transform.position).normalized;
        rb.velocity = moveDirection * floatSpeed;

        // Update idle timer
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0)
        {
            SetNewIdleTarget();
        }
    }

    void UpdateFollowing()
    {
        // Calculate direction to orbit around player
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        Vector2 orbitDirection = new Vector2(-directionToPlayer.y, directionToPlayer.x);

        // Get current distance
        float distance = Vector2.Distance(transform.position, player.position);

        // Adjust velocity based on distance
        Vector2 targetVelocity;

        if (Mathf.Abs(distance - maintainDistance) < 0.5f)
        {
            // We're at the right distance, orbit the player
            targetVelocity = orbitDirection * orbitSpeed;
        }
        else
        {
            // Get closer or further as needed
            float speedFactor = distance > maintainDistance ? 1f : -0.7f;
            targetVelocity = directionToPlayer * chaseSpeed * speedFactor;

            // Add some orbit motion
            targetVelocity += orbitDirection * orbitSpeed * 0.3f;
        }

        // Apply bobbing motion
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        rb.velocity = targetVelocity + new Vector2(0, bobOffset);
    }

    void UpdateScanning()
    {
        // Keep relatively still during scanning, just bob slightly
        rb.velocity = new Vector2(0, Mathf.Sin(Time.time * bobSpeed) * bobAmount);

        // Update scan beam
        UpdateScanBeamPosition();

        // Check if scan is complete
        if (!isScanning)
        {
            scanBeam.enabled = false;
            currentState = State.Following;
        }
    }

    void UpdateRetreating()
    {
        // Move away from player
        Vector2 directionFromPlayer = (transform.position - player.position).normalized;

        // Add bobbing
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        // Apply velocity
        rb.velocity = directionFromPlayer * chaseSpeed * 1.5f + new Vector2(0, bobOffset);
    }

    void SetNewIdleTarget()
    {
        // Set a random target position
        float targetX = transform.position.x + Random.Range(-3f, 3f);
        float targetY = transform.position.y + Random.Range(-2f, 2f);
        idleTarget = new Vector2(targetX, targetY);

        // Reset idle timer
        idleTimer = idleDuration;
    }

    void UpdatePupilLook()
    {
        // Make pupil look at player
        Vector3 directionToPlayer = player.position - transform.position;
        directionToPlayer.Normalize();

        // Limit pupil movement to stay within iris
        float maxOffset = 0.15f;
        pupil.localPosition = new Vector3(
            directionToPlayer.x * maxOffset,
            directionToPlayer.y * maxOffset,
            0
        );
    }

    void UpdateVisualEffects()
    {
        // Pulse the body
        float pulse = 1 + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        body.localScale = new Vector3(pulse, pulse, 1);
    }

    void InitializeTail()
    {
        // Create the line for the tail
        GameObject tailLineObj = new GameObject("TailLine");
        tailLineObj.transform.SetParent(transform);
        tailLineObj.transform.localPosition = Vector3.zero;

        tailLine = tailLineObj.AddComponent<LineRenderer>();
        tailLine.startWidth = tailWidth;
        tailLine.endWidth = tailWidth;
        tailLine.material = new Material(Shader.Find("Sprites/Default"));
        tailLine.startColor = trailColor;
        tailLine.endColor = trailColor;
        tailLine.positionCount = 2;

        // Create the circle at the end of the tail
        GameObject circleObj = new GameObject("TailCircle");
        circleObj.transform.SetParent(transform);
        SpriteRenderer sr = circleObj.AddComponent<SpriteRenderer>();

        // Use a circle sprite or create one
        if (body != null && body.GetComponent<SpriteRenderer>() != null &&
            body.GetComponent<SpriteRenderer>().sprite != null)
        {
            sr.sprite = body.GetComponent<SpriteRenderer>().sprite; // Reuse body's circle sprite
        }
        else
        {
            // Create a basic circle sprite (or use a default one)
            sr.sprite = Resources.Load<Sprite>("Circle");
            if (sr.sprite == null)
            {
                Debug.LogWarning("No circle sprite found. Please assign a circle sprite to the body or add one to Resources folder.");
            }
        }

        sr.color = trailColor;
        circleObj.transform.localScale = new Vector3(tailCircleSize, tailCircleSize, 1);

        tailCircle = circleObj.transform;
    }

    void UpdateTail()
    {
        if (tailLine == null || tailCircle == null)
            return;

        // Calculate tail positions with wave motion
        float timeOffset = Time.time * tailWaveSpeed;
        float waveOffset = Mathf.Sin(timeOffset) * tailWaveAmount;

        // Body bottom position (starting point)
        Vector3 startPos = body.position;
        startPos.y -= body.GetComponent<SpriteRenderer>().bounds.extents.y;

        // End position with wave and length
        Vector3 endPos = startPos + new Vector3(waveOffset, -tailLength, 0);

        // Update the line positions
        tailLine.SetPosition(0, startPos);
        tailLine.SetPosition(1, endPos);

        // Position the circle at the end
        tailCircle.position = endPos;
    }

    void HandleBlinking()
    {
        blinkTimer -= Time.deltaTime;

        if (blinkTimer <= 0)
        {
            StartCoroutine(Blink());
            blinkTimer = blinkInterval + Random.Range(-1f, 1f); // Add variation
        }
    }

    IEnumerator Blink()
    {
        // Store original scale
        Vector3 originalIrisScale = iris.localScale;
        Vector3 originalPupilScale = pupil.localScale;

        // Close eye
        float duration = 0.1f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            iris.localScale = new Vector3(originalIrisScale.x, originalIrisScale.y * (1 - t), 1);
            pupil.localScale = new Vector3(originalPupilScale.x, originalPupilScale.y * (1 - t), 1);

            yield return null;
        }

        // Brief pause
        yield return new WaitForSeconds(0.05f);

        // Open eye
        elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            iris.localScale = new Vector3(originalIrisScale.x, originalIrisScale.y * t, 1);
            pupil.localScale = new Vector3(originalPupilScale.x, originalPupilScale.y * t, 1);

            yield return null;
        }

        // Reset scales to be sure
        iris.localScale = originalIrisScale;
        pupil.localScale = originalPupilScale;
    }

    void InitializeScanBeam()
    {
        GameObject beamObj = new GameObject("ScanBeam");
        beamObj.transform.SetParent(transform);
        beamObj.transform.localPosition = Vector3.zero;

        scanBeam = beamObj.AddComponent<LineRenderer>();
        scanBeam.startWidth = 0.1f;
        scanBeam.endWidth = 0.4f;
        scanBeam.material = new Material(Shader.Find("Sprites/Default"));
        scanBeam.startColor = scanColor;
        scanBeam.endColor = new Color(scanColor.r, scanColor.g, scanColor.b, 0.3f);
        scanBeam.positionCount = 2;
        scanBeam.enabled = false;
    }

    void InitializeTrail()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.5f;
        trail.startWidth = 0.3f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0);
    }

    void StartScanning()
    {
        currentState = State.Scanning;
        isScanning = true;
        scanBeam.enabled = true;

        // Stop movement
        rb.velocity = Vector2.zero;

        // Start scan coroutine
        StartCoroutine(PerformScan());
    }

    IEnumerator PerformScan()
    {
        float elapsed = 0;

        while (elapsed < scanDuration)
        {
            elapsed += Time.deltaTime;

            // Update beam appearance
            float intensity = 0.5f + 0.5f * Mathf.Sin(elapsed * 5);
            scanBeam.startColor = new Color(scanColor.r, scanColor.g, scanColor.b, 0.7f * intensity);

            yield return null;
        }

        // Check if player is in range for damage
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer < scanRange)
        {
            // Deal damage to player
            player.GetComponent<PlayerController>().TakeDamage(scanDamage, transform);
        }

        // End scan and set cooldown
        isScanning = false;
        scanTimer = scanCooldown;
    }

    void UpdateScanBeamPosition()
    {
        if (scanBeam.enabled)
        {
            scanBeam.SetPosition(0, transform.position);
            scanBeam.SetPosition(1, player.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw aggro range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Draw scan range
        Gizmos.color = new Color(0.77f, 0.4f, 0.99f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, scanRange);

        // Draw maintain distance
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maintainDistance);
    }
}