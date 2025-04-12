using System.Collections;
using UnityEngine;

public class PlayerReactions : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform leftEyeTransform;
    [SerializeField] private Transform rightEyeTransform;
    [SerializeField] private Transform mouthTransform;
    [SerializeField] private PlayerController playerController; // Reference to your player controller

    [Header("Eye Follow")]
    [SerializeField] private float eyeFollowSpeed = 8f;
    [SerializeField] private float maxEyeOffset = 0.1f;
    [SerializeField] private bool followMouse = true;
    [SerializeField] private float movementFollowThreshold = 0.1f; // Minimum movement input to follow direction instead of mouse

    [Header("Mouth")]
    [SerializeField] private float mouthCloseScale = 0.05f; // Almost disappears when closed
    [SerializeField] private float mouthAnimSpeed = 8f;
    [SerializeField] private bool mouthReactToMovement = true;

    [Header("Blinking")]
    [SerializeField] private float blinkInterval = 3f;
    [SerializeField] private float blinkDuration = 0.1f;
    [SerializeField] private float blinkRandomness = 2f;

    [Header("Squash & Stretch")]
    [SerializeField] private float jumpSquishAmount = 0.2f;
    [SerializeField] private float landSquishAmount = 0.3f;
    [SerializeField] private float dashSquishAmount = 0.4f;
    [SerializeField] private float animationSpeed = 12f;

    // Default positions
    private Vector3 bodyDefaultScale;
    private Vector3 leftEyeDefaultLocalPos;
    private Vector3 rightEyeDefaultLocalPos;
    private Vector3 leftEyeDefaultScale;
    private Vector3 rightEyeDefaultScale;
    private Vector3 mouthDefaultScale;
    private Vector3 mouthDefaultLocalPos;

    // State tracking
    private bool wasGrounded;
    private bool wasJumping;
    private bool wasDashing;
    private float moveInput;
    private Vector2 lastVelocity;
    private bool isBlinking;
    private bool isMouthClosed;
    private Coroutine blinkCoroutine;
    private Coroutine squashStretchCoroutine;
    private Coroutine mouthCoroutine;

    private Rigidbody2D rb;

    private void Start()
    {
        // Cache the default transform values
        bodyDefaultScale = transform.localScale;
        if (leftEyeTransform) leftEyeDefaultLocalPos = leftEyeTransform.localPosition;
        if (rightEyeTransform) rightEyeDefaultLocalPos = rightEyeTransform.localPosition;
        if (leftEyeTransform) leftEyeDefaultScale = leftEyeTransform.localScale;
        if (rightEyeTransform) rightEyeDefaultScale = rightEyeTransform.localScale;
        if (mouthTransform)
        {
            mouthDefaultScale = mouthTransform.localScale;
            mouthDefaultLocalPos = mouthTransform.localPosition;
        }

        rb = GetComponent<Rigidbody2D>();

        // Start the blinking coroutine
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void Update()
    {
        if (playerController == null) return;

        // Get state from player controller using reflection to access private fields
        bool isGrounded = GetFieldValue<bool>(playerController, "isGrounded");
        bool isJumping = GetFieldValue<bool>(playerController, "isJumping");
        bool isDashing = GetFieldValue<bool>(playerController, "isDashing");
        moveInput = GetFieldValue<float>(playerController, "moveInput");

        // Handle landing
        if (isGrounded && !wasGrounded)
        {
            HandleLanding();
        }

        // Handle jumping
        if (isJumping && !wasJumping)
        {
            HandleJumping();
        }

        // Handle dashing
        if (isDashing && !wasDashing)
        {
            HandleDashing();
        }

        // Eye following (unless blinking)
        if (!isBlinking)
        {
            UpdateEyePosition();
        }

        // Update mouth based on movement
        if (mouthTransform && mouthReactToMovement)
        {
            UpdateMouth(isJumping, isDashing, isGrounded);
        }

        // Store current state for next frame
        wasGrounded = isGrounded;
        wasJumping = isJumping;
        wasDashing = isDashing;
        lastVelocity = rb.velocity;
    }

    private void UpdateEyePosition()
    {
        if (leftEyeTransform == null || rightEyeTransform == null) return;

        // Determine look target
        Vector2 targetDirection;
        bool isMovingSignificantly = Mathf.Abs(moveInput) > movementFollowThreshold;

        if (followMouse && !isMovingSignificantly)
        {
            // Follow mouse position
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = transform.position.z; // Keep on same Z plane
            targetDirection = (mouseWorldPos - transform.position).normalized;
        }
        else
        {
            // Look in movement direction with some influence from velocity for jumps/falls
            targetDirection = new Vector2(moveInput, rb.velocity.y * 0.2f).normalized;

            // If no movement, default to forward
            if (targetDirection.magnitude < 0.1f)
            {
                targetDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            }
        }

        // Calculate eye offset
        Vector2 eyeOffset = targetDirection * maxEyeOffset;

        // Squint eyes when jumping/falling
        float squintAmount = 0f;
        if (wasJumping || rb.velocity.y < -2f)
        {
            // Squint more during faster vertical movement
            squintAmount = Mathf.Clamp01(Mathf.Abs(rb.velocity.y) / 15f) * 0.3f;
        }

        // Apply position to eyes with smoothing
        leftEyeTransform.localPosition = Vector3.Lerp(
            leftEyeTransform.localPosition,
            leftEyeDefaultLocalPos + new Vector3(eyeOffset.x, eyeOffset.y, 0),
            Time.deltaTime * eyeFollowSpeed
        );

        rightEyeTransform.localPosition = Vector3.Lerp(
            rightEyeTransform.localPosition,
            rightEyeDefaultLocalPos + new Vector3(eyeOffset.x, eyeOffset.y, 0),
            Time.deltaTime * eyeFollowSpeed
        );

        // Apply squint if needed
        if (squintAmount > 0 && !isBlinking)
        {
            Vector3 squintScale = new Vector3(1f, 1f - squintAmount, 1f);
            leftEyeTransform.localScale = Vector3.Lerp(leftEyeDefaultScale, Vector3.Scale(leftEyeDefaultScale, squintScale), squintAmount);
            rightEyeTransform.localScale = Vector3.Lerp(rightEyeDefaultScale, Vector3.Scale(rightEyeDefaultScale, squintScale), squintAmount);
        }
    }

    private void HandleJumping()
    {
        // Vertical stretch when starting jump
        if (squashStretchCoroutine != null)
        {
            StopCoroutine(squashStretchCoroutine);
        }

        squashStretchCoroutine = StartCoroutine(SquashStretchAnimation(
            new Vector3(1f - jumpSquishAmount, 1f + jumpSquishAmount * 1.5f, 1f),
            0.2f
        ));

        // Close mouth for jump concentration
        if (mouthTransform)
        {
            CloseMouth(0.2f);
        }
    }

    private void HandleLanding()
    {
        float impactForce = Mathf.Abs(lastVelocity.y);

        // Only squash if the impact is significant
        if (impactForce > 2f)
        {
            // Calculate squish amount based on impact force
            float squishMultiplier = Mathf.Clamp01(impactForce / 15f);

            // Stop any current animations
            if (squashStretchCoroutine != null)
            {
                StopCoroutine(squashStretchCoroutine);
            }

            // Horizontal squish on landing
            squashStretchCoroutine = StartCoroutine(SquashStretchAnimation(
                new Vector3(1f + landSquishAmount * squishMultiplier, 1f - landSquishAmount * squishMultiplier, 1f),
                0.2f
            ));

            // Open mouth on harder landings
            if (mouthTransform && impactForce > 5f)
            {
                OpenMouthWide(0.1f);
            }
        }
    }

    private void HandleDashing()
    {
        if (squashStretchCoroutine != null)
        {
            StopCoroutine(squashStretchCoroutine);
        }

        // Get dash direction
        float dashDir = moveInput != 0 ? Mathf.Sign(moveInput) : (transform.localScale.x > 0 ? 1 : -1);

        // Stretch in dash direction
        Vector3 stretchScale;
        if (Mathf.Abs(dashDir) > 0.1f)
        {
            // Horizontal dash - stretch horizontally
            stretchScale = new Vector3(1f + dashSquishAmount, 1f - dashSquishAmount * 0.5f, 1f);
        }
        else
        {
            // Assume vertical dash if no horizontal input
            stretchScale = new Vector3(1f - dashSquishAmount * 0.5f, 1f + dashSquishAmount, 1f);
        }

        squashStretchCoroutine = StartCoroutine(SquashStretchAnimation(stretchScale, 0.15f));

        // Open mouth wide during dash
        if (mouthTransform)
        {
            OpenMouthWide(0.15f);
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // Random wait between blinks
            yield return new WaitForSeconds(blinkInterval + Random.Range(-blinkRandomness, blinkRandomness));

            // Start blinking
            isBlinking = true;

            // Close eyes
            if (leftEyeTransform && rightEyeTransform)
            {
                // Save current scales
                Vector3 leftCurrentScale = leftEyeTransform.localScale;
                Vector3 rightCurrentScale = rightEyeTransform.localScale;

                // Animate closing eyes
                float timeElapsed = 0;
                float blinkSpeed = blinkDuration / 2f;

                while (timeElapsed < blinkSpeed)
                {
                    float t = timeElapsed / blinkSpeed;
                    leftEyeTransform.localScale = new Vector3(leftCurrentScale.x, Mathf.Lerp(leftCurrentScale.y, 0.05f, t), leftCurrentScale.z);
                    rightEyeTransform.localScale = new Vector3(rightCurrentScale.x, Mathf.Lerp(rightCurrentScale.y, 0.05f, t), rightCurrentScale.z);

                    timeElapsed += Time.deltaTime;
                    yield return null;
                }

                // Keep eyes closed briefly
                yield return new WaitForSeconds(0.05f);

                // Open eyes
                timeElapsed = 0;
                while (timeElapsed < blinkSpeed)
                {
                    float t = timeElapsed / blinkSpeed;
                    leftEyeTransform.localScale = new Vector3(leftCurrentScale.x, Mathf.Lerp(0.05f, leftCurrentScale.y, t), leftCurrentScale.z);
                    rightEyeTransform.localScale = new Vector3(rightCurrentScale.x, Mathf.Lerp(0.05f, rightCurrentScale.y, t), rightCurrentScale.z);

                    timeElapsed += Time.deltaTime;
                    yield return null;
                }

                // Reset eyes to default
                leftEyeTransform.localScale = leftCurrentScale;
                rightEyeTransform.localScale = rightCurrentScale;
            }

            isBlinking = false;
        }
    }

    private IEnumerator SquashStretchAnimation(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float time = 0;

        // Animate to stretched scale
        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(startScale, Vector3.Scale(bodyDefaultScale, targetScale), time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        // Animate back to default scale with bounce
        time = 0;
        Vector3 stretchedScale = Vector3.Scale(bodyDefaultScale, targetScale);

        while (time < duration * 1.5f)
        {
            // Add slight bounce with Mathf.Sin
            float t = time / (duration * 1.5f);
            float bounceFactor = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;

            transform.localScale = Vector3.Lerp(stretchedScale,
                                               bodyDefaultScale * bounceFactor,
                                               Mathf.SmoothStep(0, 1, t));
            time += Time.deltaTime;
            yield return null;
        }

        // Ensure we end at exactly the default scale
        transform.localScale = bodyDefaultScale;
    }

    // Update mouth based on character state
    private void UpdateMouth(bool isJumping, bool isDashing, bool isGrounded)
    {
        if (isMouthClosed || isDashing) return; // Skip if already in animation

        // Default: mouth open (normal state)
        if (!isJumping && isGrounded && Mathf.Abs(moveInput) < 0.1f)
        {
            // Slightly move mouth based on looking direction
            if (!isBlinking)
            {
                Vector3 newPos = mouthDefaultLocalPos;

                // Get mouse direction 
                if (followMouse)
                {
                    Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    Vector2 mouseDir = (mouseWorldPos - transform.position).normalized;

                    // Subtly adjust mouth position based on look direction
                    newPos.x += mouseDir.x * 0.05f;
                    newPos.y += mouseDir.y * 0.03f;
                }

                // Smoothly move mouth
                mouthTransform.localPosition = Vector3.Lerp(
                    mouthTransform.localPosition,
                    newPos,
                    Time.deltaTime * mouthAnimSpeed
                );
            }
        }
        // When running, bob the mouth slightly
        else if (isGrounded && Mathf.Abs(moveInput) > 0.1f)
        {
            // Bob mouth up and down slightly with movement
            Vector3 newPos = mouthDefaultLocalPos;
            newPos.y += Mathf.Sin(Time.time * 10f) * 0.03f;

            mouthTransform.localPosition = Vector3.Lerp(
                mouthTransform.localPosition,
                newPos,
                Time.deltaTime * mouthAnimSpeed
            );
        }
    }

    // Close the mouth (for jumping/focusing)
    private void CloseMouth(float duration)
    {
        if (mouthCoroutine != null)
        {
            StopCoroutine(mouthCoroutine);
        }

        mouthCoroutine = StartCoroutine(MouthAnimation(
            mouthDefaultScale * mouthCloseScale, // Scale to nearly invisible
            duration
        ));
    }

    // Open mouth wide (for dashing/landing)
    private void OpenMouthWide(float duration)
    {
        if (mouthCoroutine != null)
        {
            StopCoroutine(mouthCoroutine);
        }

        mouthCoroutine = StartCoroutine(MouthAnimation(
            mouthDefaultScale * 1.5f, // 50% bigger than normal
            duration
        ));
    }

    // Animate mouth scale
    private IEnumerator MouthAnimation(Vector3 targetScale, float duration)
    {
        isMouthClosed = true;

        Vector3 startScale = mouthTransform.localScale;
        float time = 0;

        // Animate to target scale
        while (time < duration)
        {
            mouthTransform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        // Hold for a moment
        yield return new WaitForSeconds(0.1f);

        // Animate back to default
        time = 0;
        startScale = mouthTransform.localScale;

        while (time < duration)
        {
            mouthTransform.localScale = Vector3.Lerp(startScale, mouthDefaultScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        // Ensure we end at exactly the default scale
        mouthTransform.localScale = mouthDefaultScale;
        isMouthClosed = false;
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