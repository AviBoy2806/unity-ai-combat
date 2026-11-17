using UnityEngine;

public class StayInPlace : MonoBehaviour
{
    private Vector3 initialLocalPosition;

    void Start()
    {
        initialLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        initialLocalPosition.y = transform.localPosition.y;
        transform.localPosition = initialLocalPosition;
    }
}