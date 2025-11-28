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

            if (!RevitCableTrays.ParameterChecker(document))
                return Result.Text.Failed("Required parameters are not present in the current model");

            // Initialize services

            var client = DrofusService.CreateClient(document);

            var drofusService = new DrofusService(client);
            var processor = new CableTrayRunProcessor();

            // Get data
            var cableTrayRuns = RevitCableTrays.GetCableTrayRuns(document, cancellationToken);
            var existingDrofusIds = drofusService.GetExistingDrofusIds(cancellationToken);

            // Process cable tray runs
            var (newRuns, existingRuns, existingCount, totalRunsFound, skippedUnchecked) = processor.ProcessAndFilterCableTrayRuns(document, cableTrayRuns, existingDrofusIds);

            // Assign dRofus item IDs
            RevitCableTrays.AssignDrofusItemIds(newRuns, args.UserArgsCableTraySizes);
            var cableTrayRunsWithNoId = processor.GetRunsWithoutItemIds(newRuns);

            // Process runs in dRofus
            var (createdCount, updatedCount) = drofusService.ProcessCableTrayRuns(newRuns, existingRuns, document, args.UserArgsDryRun);

            // Write metadata back to Revit (if not dry run)
            if (!args.UserArgsDryRun)
            {
                RevitCableTrays.WriteRunMetadataToRevit(document, newRuns,
                    AssistantArgs.UserArgsRevitParameters["RevitDrofusId"],
                    AssistantArgs.UserArgsRevitParameters["RevitTag"]);
                RevitCableTrays.WriteRunMetadataToRevit(document, existingRuns,
                    AssistantArgs.UserArgsRevitParameters["RevitDrofusId"],
                    AssistantArgs.UserArgsRevitParameters["RevitTag"]);
            }

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
}