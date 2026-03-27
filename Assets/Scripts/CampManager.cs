using UnityEngine;

public class CampManager : MonoBehaviour
{
    public static CampManager Instance { get; private set; }

    public Camp CurrentCamp { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetCamp(Camp newCamp)
    {
        if (CurrentCamp != null && CurrentCamp != newCamp)
        {
            Destroy(CurrentCamp.gameObject);
        }

        CurrentCamp = newCamp;
    }

    public bool HasCamp()
    {
        return CurrentCamp != null;
    }

    public Vector3 GetCampWaitingPosition()
    {
        if (CurrentCamp == null)
            return Vector3.zero;

        return CurrentCamp.WaitingCenter;
    }
}