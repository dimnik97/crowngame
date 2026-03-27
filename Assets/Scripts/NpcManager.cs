using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    [Header("Names")]
    [SerializeField] private string[] possibleNames =
    {
        "Альрик", "Бьерн", "Сиверт", "Лео", "Томас",
        "Эдгар", "Мартин", "Роланд", "Хьюго", "Оскар",
        "Ингвар", "Руфус", "Ганс", "Эрик", "Норман",
        "Мира", "Эльза", "Ханна", "Фрея", "Ингрид",
        "Лотта", "Марта", "Сигне", "Агна", "Тильда"
    };

    [Header("Debug")]
    [SerializeField] private bool showRecruitedDebug = true;

    private readonly List<RecruitableNpc> recruitedNpcs = new List<RecruitableNpc>();

    public IReadOnlyList<RecruitableNpc> RecruitedNpcs => recruitedNpcs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public string GetRandomName()
    {
        if (possibleNames == null || possibleNames.Length == 0)
            return "Безымянный";

        return possibleNames[Random.Range(0, possibleNames.Length)];
    }

    public void RegisterRecruited(RecruitableNpc npc)
    {
        if (npc == null)
            return;

        if (!recruitedNpcs.Contains(npc))
            recruitedNpcs.Add(npc);
    }

    public void UnregisterRecruited(RecruitableNpc npc)
    {
        if (npc == null)
            return;

        recruitedNpcs.Remove(npc);
    }

    public RecruitableNpc GetFirstFreeCampVillager()
    {
        CleanupNulls();

        for (int i = 0; i < recruitedNpcs.Count; i++)
        {
            RecruitableNpc npc = recruitedNpcs[i];
            if (npc != null && npc.IsAvailableForWork())
                return npc;
        }

        return null;
    }

    private void CleanupNulls()
    {
        for (int i = recruitedNpcs.Count - 1; i >= 0; i--)
        {
            if (recruitedNpcs[i] == null)
                recruitedNpcs.RemoveAt(i);
        }
    }

    private void OnGUI()
    {
        if (!showRecruitedDebug)
            return;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(new Rect(10, 10, 320, 35 + 24 * Mathf.Max(1, recruitedNpcs.Count)), "");

        GUI.color = Color.white;
        GUI.Label(new Rect(20, 18, 280, 20), $"Завербовано: {recruitedNpcs.Count}");

        if (recruitedNpcs.Count == 0)
        {
            GUI.Label(new Rect(20, 42, 280, 20), "Пока никого нет");
            return;
        }

        for (int i = 0; i < recruitedNpcs.Count; i++)
        {
            RecruitableNpc npc = recruitedNpcs[i];
            if (npc == null)
                continue;

            GUI.Label(
                new Rect(20, 42 + i * 24, 280, 20),
                $"{npc.NpcName} - {npc.GetDebugStateText()}"
            );
        }
    }
}