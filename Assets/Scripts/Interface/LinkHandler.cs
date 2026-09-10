using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LinkHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI tmpText;

    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpText, eventData.position, null);

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
            Application.OpenURL(linkInfo.GetLinkID());
        }
    }
}