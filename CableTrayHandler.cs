using CableTrayHandler.Services;

namespace CableTrayHandler;

public class CableTrayHandlerCommand : IRevitExtension<AssistantArgs>
{
    public IExtensionResult Run(IRevitExtensionContext context, AssistantArgs args, CancellationToken cancellationToken)
    {
        try
        {
            // Validate inputs
            var document = context.UIApplication.ActiveUIDocument?.Document;
            if (document is null)
                return Result.Text.Failed("Revit has no active model open");

            // Validate essential parameters are configured
            var validationResult = ValidateEssentialParameters();
            if (validationResult != null)
                return validationResult;

            if (!RevitCableTrays.ParameterChecker(document))
                return Result.Text.Failed("Required parameters are not present in the current model");

            // Initialize services

            var client = DrofusService.CreateClient(document);

            var drofusService = new DrofusService(client);
            var processor = new CableTrayRunProcessor();

            // Get data
            var cableTrayRuns = RevitCableTrays.GetCableTrayRuns(document, cancellationToken, args.UserArgsEnableProximityMerging);
            var existingDrofusIds = drofusService.GetExistingDrofusIds(cancellationToken);

            // Process cable tray runs
            var (newRuns, existingRuns, existingCount, totalRunsFound, skippedUnchecked) = processor.ProcessAndFilterCableTrayRuns(document, cableTrayRuns, existingDrofusIds);

            // Assign dRofus item IDs
            RevitCableTrays.AssignDrofusItemIds(newRuns, args.UserArgsCableTraySizes);
            var cableTrayRunsWithNoId = processor.GetRunsWithoutItemIds(newRuns);

            int createdCount = 0;
            int updatedCount = 0;

            // NOTE: dRofus sync is disabled for V2 branch (testing run identification only)
            // Uncomment below to re-enable dRofus processing
            /*
            // Process runs in dRofus
            var (createdCount, updatedCount) = drofusService.ProcessCableTrayRuns(newRuns, existingRuns, document, args.UserArgsDryRun);

            // Write metadata back to Revit (if not dry run)
            if (!args.UserArgsDryRun)
            {
                // Write both ID and tag for new runs
                RevitCableTrays.WriteRunMetadataToRevit(document, newRuns,
                    AssistantArgs.UserArgsRevitParameters["RevitDrofusId"],
                    AssistantArgs.UserArgsRevitParameters["RevitTag"],
                    writeOccurrenceId: true);
                
                // Only write tag for existing runs (they already have the ID)
                RevitCableTrays.WriteRunMetadataToRevit(document, existingRuns,
                    AssistantArgs.UserArgsRevitParameters["RevitDrofusId"],
                    AssistantArgs.UserArgsRevitParameters["RevitTag"],
                    writeOccurrenceId: false);
            }
            */

            // Generate result message
            var resultMessage = processor.GenerateResultMessage(existingCount, createdCount, updatedCount, cableTrayRunsWithNoId.Count, args.UserArgsDryRun, totalRunsFound, skippedUnchecked);

            return cableTrayRunsWithNoId.Count == 0
                ? Result.Text.Succeeded(resultMessage)
                : Result.Text.PartiallySucceeded(resultMessage);
        }
        catch (OperationCanceledException)
        {
            return Result.Text.PartiallySucceeded("Operation was canceled by the user.");
        }
        catch (Exception ex)
        {
            return Result.Text.Failed($"An error occurred: {ex.Message}");
        }
    }

    private static IExtensionResult? ValidateEssentialParameters()
    {
        var missingParameters = new List<string>();

        // Check Revit parameters
        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsRevitParameters["RevitTag"]))
            missingParameters.Add("RevitTag");

        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"]))
            missingParameters.Add("RevitDrofusId");

        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsRevitParameters["RevitRunGuid"]))
            missingParameters.Add("RevitRunGuid");

        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsRevitParameters["RevitCheckbox"]))
            missingParameters.Add("RevitCheckbox");

        // Check dRofus parameters
        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsDrofusParameters["DrofusModelName"]))
            missingParameters.Add("DrofusModelName");

        if (string.IsNullOrWhiteSpace(AssistantArgs.UserArgsDrofusParameters["DrofusLengthDouble"]))
            missingParameters.Add("DrofusLengthDouble");

        if (missingParameters.Count > 0)
        {
            var parameterList = string.Join(", ", missingParameters);
            return Result.Text.Failed($"The following essential parameters must be configured: {parameterList}");
        }

        return null;
    }
}