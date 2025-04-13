using UnityEngine;
using UnityEngine.UI;

public class HealthComponentAnimator : MonoBehaviour
{
    [System.Serializable]
    public class ComponentAnimation
    {
        public string componentDescription; // For inspector readability
        public string innerShieldImageName = "InnerShield";
        public string scanLineImageName = "ScanLines";
        public string flowParticleImageName = "FlowParticle";
        public AnimationType animationType;
        public float speed = 1f;
        public float minValue = 0.1f;
        public float maxValue = 0.3f;

        // Runtime cached references
        [System.NonSerialized]
        public Image targetImage;
    }

    public enum AnimationType
    {
        InnerShieldPulse,
        ScanLineVertical,
        ConnectionFlowHorizontal
    }

    public ComponentAnimation[] animations;

    void Start()
    {
        // Find the actual image components in the instantiated prefabs
        foreach (var anim in animations)
        {
            anim.targetImage = FindImageByName(anim);
        }
    }

    Image FindImageByName(ComponentAnimation anim)
    {
        Image targetImage = null;
        string targetName = "";

        switch (anim.animationType)
        {
            case AnimationType.InnerShieldPulse:
                targetName = anim.innerShieldImageName;
                break;
            case AnimationType.ScanLineVertical:
                targetName = anim.scanLineImageName;
                break;
            case AnimationType.ConnectionFlowHorizontal:
                targetName = anim.flowParticleImageName;
                break;
        }

        // Search through all child images
        Image[] images = GetComponentsInChildren<Image>();
        foreach (var image in images)
        {
            if (image.transform.name == targetName)
            {
                targetImage = image;
                break;
            }
        }

        if (targetImage == null)
        {
            Debug.LogWarning($"Could not find image named {targetName} for {anim.animationType}");
        }

        return targetImage;
    }

    void Update()
    {
        foreach (var anim in animations)
        {
            if (anim.targetImage == null) continue;

            switch (anim.animationType)
            {
                case AnimationType.InnerShieldPulse:
                    AnimateInnerShieldPulse(anim);
                    break;
                case AnimationType.ScanLineVertical:
                    AnimateScanLineVertical(anim);
                    break;
                case AnimationType.ConnectionFlowHorizontal:
                    AnimateConnectionFlowHorizontal(anim);
                    break;
            }
        }
    }

    void AnimateInnerShieldPulse(ComponentAnimation anim)
    {
        // Pulsing alpha for inner shield
        float pulseAlpha = Mathf.PingPong(Time.time * anim.speed, anim.maxValue - anim.minValue) + anim.minValue;
        Color currentColor = anim.targetImage.color;
        anim.targetImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, pulseAlpha);
    }

    void AnimateScanLineVertical(ComponentAnimation anim)
    {
        // Vertical movement of scan line
        RectTransform parentRect = anim.targetImage.transform.parent.GetComponent<RectTransform>();
        float yPos = Mathf.PingPong(Time.time * anim.speed, parentRect.rect.height);
        anim.targetImage.rectTransform.anchoredPosition = new Vector2(
            anim.targetImage.rectTransform.anchoredPosition.x,
            yPos - parentRect.rect.height / 2
        );
    }

    void AnimateConnectionFlowHorizontal(ComponentAnimation anim)
    {
        // Horizontal movement of flow particle
        RectTransform parentRect = anim.targetImage.transform.parent.GetComponent<RectTransform>();
        float xPos = Mathf.PingPong(Time.time * anim.speed, parentRect.rect.width);
        anim.targetImage.rectTransform.anchoredPosition = new Vector2(
            xPos - parentRect.rect.width / 2,
            anim.targetImage.rectTransform.anchoredPosition.y
        );
    }
}