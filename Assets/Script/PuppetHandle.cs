using UnityEngine;
using UnityEngine.EventSystems;

public class PuppetHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IInitializePotentialDragHandler
{
    [SerializeField] private RectTransform link;
    [SerializeField] private Vector2 bodyAnchor;
    [SerializeField] private PuppetHandle otherFoot;

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
        Vector2 direction = handle.anchoredPosition - bodyAnchor;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        link.anchoredPosition = bodyAnchor;
        link.localRotation = Quaternion.Euler(0f, 0f, angle);
        link.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, direction.magnitude);
    }
}