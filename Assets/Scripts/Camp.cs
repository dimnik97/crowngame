using UnityEngine;

public class Camp : MonoBehaviour
{
    [SerializeField] private Transform waitingPoint;
    [SerializeField] private float waitingRadius = 3f;

    public Vector3 WaitingCenter
    {
        get
        {
            if (waitingPoint != null)
                return waitingPoint.position;

            return transform.position;
        }
    }

    public Vector3 GetRandomWaitingPosition()
    {
        Vector2 offset = Random.insideUnitCircle * waitingRadius;
        Vector3 center = WaitingCenter;
        return center + new Vector3(offset.x, 0f, offset.y);
    }
}