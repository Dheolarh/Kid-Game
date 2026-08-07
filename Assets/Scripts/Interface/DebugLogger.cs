using UnityEngine;

public class DebugLogger : MonoBehaviour
{
    private void OnEnable()
    {
       Debug.Log(gameObject.name + " is enabled");
    }

    private void OnDisable()
    {
        Debug.Log(gameObject.name + " is disabled");
    }

    public void EventListener(){
        Debug.Log(gameObject.name + " is toggled");     
    }
}
