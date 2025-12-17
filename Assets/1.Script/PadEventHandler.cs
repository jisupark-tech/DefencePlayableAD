using UnityEngine;
using UnityEngine.EventSystems;

public class PadEventHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private VirtualPad virtualPad;

    public void Initialize(VirtualPad pad)
    {
        virtualPad = pad;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (virtualPad != null)
            virtualPad.OnPadPointerDown(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (virtualPad != null)
            virtualPad.OnPadPointerUp(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (virtualPad != null)
            virtualPad.OnPadDrag(eventData);
    }
}