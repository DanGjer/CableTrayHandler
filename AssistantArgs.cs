namespace CableTrayHandler;

public class AssistantArgs
{
    [Description("Dry run"), ControlData]
    public bool UserArgsDryRun { get; set; } = false;

    [Description("Dictionary containing cable tray sizes"), ControlData(ToolTip = "Example: occurrence_data_23_10_06_01")]
    public Dictionary<string, string> UserArgsCableTraySizes { get; set; } = new();

    [Description("Dictionary containing Revit parameters"), ControlData(ToolTip = "test")]
    public static Dictionary<string, string> UserArgsRevitParameters { get; set; } = new()
    {
        { "RevitCheckbox", "" },
        { "RevitTag", "" },
        { "RevitDrofusId", "" },
        { "RevitRunGuid", "" },
        { "RevitAdditionalProp1", "" },
        { "RevitAdditionalProp2", "" },
        { "RevitAdditionalProp3", "" },
        { "RevitAdditionalProp4", "" },
        { "RevitAdditionalStatusProp1", "" }

    };

    [Description("Dictionary containing dRofus parameters"), ControlData]
    public static Dictionary<string, string> UserArgsDrofusParameters { get; set; } = new()
    {
        { "DrofusModelName", "" },
        { "DrofusLengthDouble", "" },
        { "DrofusAdditionalProp1", "" },
        { "DrofusAdditionalProp2", "" },
        { "DrofusAdditionalProp3", "" },
        { "DrofusAdditionalProp4", "" },
        { "DrofusAdditionalStatusProp1", "" },
    };
}