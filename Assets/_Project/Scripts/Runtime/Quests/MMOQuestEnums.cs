namespace RPGClone.Quests
{
    public enum MMOQuestState
    {
        Unavailable,
        Available,
        Accepted,
        ReadyToTurnIn,
        Completed
    }

    public enum MMOQuestObjectiveType
    {
        CollectQuestItem,
        CollectItem,
        KillCreature,
        UseItemOnWorldObject,
        SpeakToNpc
    }
}
