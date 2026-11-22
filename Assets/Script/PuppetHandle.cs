using UnityEngine;
using UnityEngine.EventSystems;

public class PuppetHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IInitializePotentialDragHandler
{
    [SerializeField] private RectTransform link;
    [SerializeField] private Vector2 bodyAnchor;
    [SerializeField] private PuppetHandle otherFoot;

    [Header("Jointed limb (optional)")]
    [Tooltip("Upper segment (shoulder/hip to elbow/knee). Pivot at its joint, +X along the bone.")]
    [SerializeField] private RectTransform upperBone;
    [Tooltip("Lower segment (elbow/knee to hand/foot). Pivot at its joint, +X along the bone.")]
    [SerializeField] private RectTransform lowerBone;
    [SerializeField] private float upperLength;
    [SerializeField] private float lowerLength;
    [Tooltip("Which side the elbow/knee bends to.")]
    [SerializeField] private bool bendClockwise;

    [Header("Rigid piece on a fixed radius (optional, e.g. head)")]
    [Tooltip("If > 0 and no bones are set, the handle stays this far from the body anchor.")]
    [SerializeField] private float fixedRadius;
    [Tooltip("Rotated to follow the direction from the body anchor to the handle.")]
    [SerializeField] private RectTransform orientWithLink;
    [SerializeField] private float orientOffset;

    private RectTransform handle;
    private RectTransform puppetRoot;
    private RectTransform stage;
    private int? activePointerId;
    private Vector2 lastPointerPosition;

    private void Awake()
    {
        handle = (RectTransform)transform;
        puppetRoot = (RectTransform)handle.parent;
        stage = (RectTransform)puppetRoot.parent;
        UpdateLink();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId.HasValue ||
            eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                stage, eventData.position, eventData.pressEventCamera,
                out lastPointerPosition))
            return;

        activePointerId = eventData.pointerId;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (activePointerId != eventData.pointerId)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                stage, eventData.position, eventData.pressEventCamera,
                out Vector2 currentPosition))
            return;

        Vector2 displacement = currentPosition - lastPointerPosition;
        lastPointerPosition = currentPosition;

        if (otherFoot != null && otherFoot.activePointerId.HasValue)
        {
            Vector2 bodyDisplacement = displacement * 0.5f;
            puppetRoot.anchoredPosition += bodyDisplacement;

            // 父物体已经带这只脚移动了一半，补上剩下的位移。
            handle.anchoredPosition += displacement - bodyDisplacement;

            // 抵消父物体对另一只脚的位移，让它留在自己的手指下。
            otherFoot.handle.anchoredPosition -= bodyDisplacement;

            UpdateLink();
            otherFoot.UpdateLink();
            return;
        }

        handle.anchoredPosition += displacement;
        UpdateLink();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activePointerId == eventData.pointerId &&
            eventData.button == PointerEventData.InputButton.Left)
            activePointerId = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            activePointerId = null;
    }

    private void OnDisable()
    {
        activePointerId = null;
    }

    private void UpdateLink()
    {
        if (upperBone != null && lowerBone != null)
            UpdateJointedLimb();
        else if (fixedRadius > 0f)
            UpdateFixedRadius();
        else
            UpdateStraightLink();
    }

    private void UpdateStraightLink()
    {
        Vector2 direction = handle.anchoredPosition - bodyAnchor;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (link != null)
        {
            link.anchoredPosition = bodyAnchor;
            link.localRotation = Quaternion.Euler(0f, 0f, angle);
            link.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, direction.magnitude);
        }

        Orient(angle);
    }

    // Rigid piece (e.g. the head): keeps a fixed distance from the anchor and turns around it.
    private void UpdateFixedRadius()
    {
        Vector2 direction = handle.anchoredPosition - bodyAnchor;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        handle.anchoredPosition = bodyAnchor + direction.normalized * fixedRadius;
        Orient(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    // Two-bone IK: places the elbow/knee so the hand/foot lands on the handle.
    // The handle is pulled back in if it goes beyond the limb's reach.
    private void UpdateJointedLimb()
    {
        Vector2 toTarget = handle.anchoredPosition - bodyAnchor;
        float distance = toTarget.magnitude;
        Vector2 direction = distance > 0.0001f ? toTarget / distance : Vector2.down;

        float maxReach = upperLength + lowerLength - 0.01f;
        float minReach = Mathf.Abs(upperLength - lowerLength) + 0.01f;
        float clamped = Mathf.Clamp(distance, minReach, maxReach);
        if (!Mathf.Approximately(clamped, distance))
            handle.anchoredPosition = bodyAnchor + direction * clamped;

        float cosA = (upperLength * upperLength + clamped * clamped - lowerLength * lowerLength)
                     / (2f * upperLength * clamped);
        float bend = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float upperAngle = baseAngle + (bendClockwise ? -bend : bend);

        Vector2 joint = bodyAnchor +
            (Vector2)(Quaternion.Euler(0f, 0f, upperAngle) * Vector3.right) * upperLength;
        Vector2 toHandle = handle.anchoredPosition - joint;
        float lowerAngle = Mathf.Atan2(toHandle.y, toHandle.x) * Mathf.Rad2Deg;

        upperBone.anchoredPosition = bodyAnchor;
        upperBone.localRotation = Quaternion.Euler(0f, 0f, upperAngle);
        lowerBone.anchoredPosition = joint;
        lowerBone.localRotation = Quaternion.Euler(0f, 0f, lowerAngle);

        Orient(lowerAngle);
    }

    private void Orient(float angle)
    {
        if (orientWithLink != null)
            orientWithLink.localRotation = Quaternion.Euler(0f, 0f, angle + orientOffset);
    }

#if UNITY_EDITOR
    // Lets the editor tool lay out the pose outside play mode.
    public void EditorRefresh()
    {
        handle = (RectTransform)transform;
        UpdateLink();
    }
#endif
}
