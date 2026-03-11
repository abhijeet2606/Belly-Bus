using System;

[Serializable]
public class ChallengeDTO
{
    public string challengeId;
    public string title;
    public int slot;
    public TaskType taskType;
    public MissionEntityType entityType;
    public int entity;
    public MissionDifficulty difficulty;
    public int target;
    public int rewardCoins;
    public int currentProgress; // We will need this from the backend later
}

[Serializable]
public class DayChallengesDTO
{
    public int day;
    public ChallengeDTO[] challenges;
}

[Serializable]
public class MissionPeriodDTO
{
    public string _id;
    public PeriodType periodType;
    public string startDate;
    public string endDate;
    public DayChallengesDTO[] days;
}

[Serializable]
public class MissionsApiResponse
{
    public bool success;
    public MissionsData data;
}

[Serializable]
public class MissionsData
{
    public MissionPeriodDTO daily;
    public MissionPeriodDTO weekly;
    public MissionPeriodDTO monthly;
}
