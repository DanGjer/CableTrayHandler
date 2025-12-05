
namespace CableTrayHandler;

public class AssistantArgs
{
    [Description("Dry run"), ControlData]
    public bool UserArgsDryRun { get; set; } = false;

    [Description("Enable proximity-based run merging"), ControlData(ToolTip = "Merge cable tray runs that are within 1mm of each other")]
    public bool UserArgsEnableProximityMerging { get; set; } = false;

    [Description("Tolerance"), ControlData(ToolTip = "Tolerance value in millimeters for proximity-based merging")]
    public double UserArgsTolerance { get; set; } = 1.0;

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
        { "RevitAdditionalProp5", "" },
        { "RevitAdditionalProp6", "" }

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
        { "DrofusAdditionalProp5", "" },
        { "DrofusAdditionalProp6", "" }
    };
}