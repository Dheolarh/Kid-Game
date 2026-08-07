using UnityEngine;

public class SortLayer : MonoBehaviour
{
    public string layerName;
    public int sortOrder;
    void Start()
    {
        gameObject.GetComponent<Canvas>().sortingLayerName = layerName;
        gameObject.GetComponent<Canvas>().sortingOrder = sortOrder;
    }
}
