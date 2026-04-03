using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);

    private Quaternion fixedRotation;
    private bool hasFixedRotation;

    private void Awake()
    {
        UpdateFixedRotation();

        if (hasFixedRotation)
        {
            transform.rotation = fixedRotation;
        }
    }

    private void OnValidate()
    {
        UpdateFixedRotation();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        if (hasFixedRotation)
        {
            transform.rotation = fixedRotation;
        }
    }

    private void UpdateFixedRotation()
    {
        Vector3 lookDirection = lookOffset - offset;

        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            hasFixedRotation = false;
            return;
        }

        fixedRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        hasFixedRotation = true;
    }
}
