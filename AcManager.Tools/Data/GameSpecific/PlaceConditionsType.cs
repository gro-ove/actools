namespace AcManager.Tools.Data.GameSpecific {
    public enum PlaceConditionsType {
        [LocalizedDescription(nameof(ToolsStrings.PlaceConditionType_Points))]
        Points,

        [LocalizedDescription(nameof(ToolsStrings.PlaceConditionType_Position))]
        Position,

        [LocalizedDescription(nameof(ToolsStrings.PlaceConditionType_Time))]
        Time,

        [LocalizedDescription(nameof(ToolsStrings.PlaceConditionType_Wins))]
        Wins
    }
}