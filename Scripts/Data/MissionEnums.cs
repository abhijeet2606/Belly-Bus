using System;

public enum TaskType
{
    COLLECT_ITEM = 8000,
    USE_POWERUP = 8001,
    COMPLETE_LEVEL = 8002,
    WIN_STREAK = 8003,
    SCORE_POINTS = 8004,
    CLEAR_ROW = 8005,
    CLEAR_COLUMN = 8006,
    TRIGGER_BEE = 8007,
    COLLECT_WITHOUT_LOSING = 8008,
    FINISH_WITH_MOVES_LEFT = 8009,
}

public enum MissionDifficulty
{
    EASY = 8100,
    MEDIUM = 8101,
    HARD = 8102,
}

public enum MissionPowerupType
{
    KNIFE = 8200,
    PAN = 8201,
    OVEN = 8202,
    FLIES = 8203,
}

public enum MissionEntityType
{
    FOOD = 8300,
    POWERUP = 8301,
    NONE = 8302,
}

public enum FoodType
{
    BURGER = 8400,
    CHEESE = 8401,
    ONION = 8402,
    TOMATO = 8403,
    LETTUCE = 8404,
    FRIES = 8405,
    PATTIES = 8406,
}

public enum PeriodType
{
    DAILY = 8500,
    WEEKLY = 8501,
    MONTHLY = 8502,
}
