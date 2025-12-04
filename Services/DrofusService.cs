using CableTrayHandler.Utilities;

namespace CableTrayHandler.Services
{
    public class DrofusService
    {
        private readonly IdRofusClient _client;

        public DrofusService(IdRofusClient client)
        {
            _client = client;
        }

        public static IdRofusClient CreateClient(Document document)
        {
            return new dRofusClientFactory().Create(document);
        }

        public List<Occurence> GetAllOccurrences(CancellationToken cancellationToken)
        {
            var queryOccurrences = Query.List().Select("Id");
            return _client.GetOccurrences(queryOccurrences, cancellationToken);
        }

        public HashSet<int> GetExistingDrofusIds(CancellationToken cancellationToken)
        {
            var occurrences = GetAllOccurrences(cancellationToken);
            return new HashSet<int>(occurrences.Select(o => o.Id ?? 0));
        }

        public (int createdCount, int updatedCount) ProcessCableTrayRuns(
            List<CableTrayRun> newRuns, 
            List<CableTrayRun> existingRuns, 
            Document document, 
            bool isDryRun)
        {
            int createdCount = 0;
            int updatedCount = 0;

            // Process new runs
            foreach (var run in newRuns.Where(r => r.DrofusItemId != 0))
            {
                try
                {
                    var occurrence = CreateOccurrenceForRun(run, document);
                    
                    if (!isDryRun)
                    {
                        var createdOccurrence = _client.CreateOccurrence((CreateOccurence)occurrence);
                        var queryOccurrence = Query.List().Select("classification_number");
                        var createdOccurrenceRead = _client.GetOccurrence(createdOccurrence.Id ?? 0, queryOccurrence);
                        
                        run.DrofusOccId = createdOccurrenceRead.Id ?? 0;
                        run.DrofusTag = createdOccurrenceRead.ClassificationNumber ?? "";
                    }
                    else
                    {
                        run.DrofusOccId = -1; // Dry run indicator
                        run.DrofusTag = "DRY_RUN_TAG";
                    }
                    
                    createdCount++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create occurrence in dRofus for run with {run.ElementCount} elements. Error: {ex.Message}");
                }
            }

            // Process existing runs
            foreach (var run in existingRuns)
            {
                try
                {
                    var queryExistingOccurrence = Query.List().Select("classification_number");
                    var existingOccurrenceRead = _client.GetOccurrence(run.DrofusOccId, queryExistingOccurrence);
                    
                    if (existingOccurrenceRead != null)
                    {
                        UpdateOccurrenceProperties(existingOccurrenceRead, run, document);
                        
                        if (!isDryRun)
                        {
                            _client.UpdateOccurrence(existingOccurrenceRead);
                        }
                        
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to update occurrence {run.DrofusOccId} in dRofus. Error: {ex.Message}");
                }
            }

            return (createdCount, updatedCount);
        }

        private dynamic CreateOccurrenceForRun(CableTrayRun run, Document document)
        {
            var tempItem = _client.GetItem(run.DrofusItemId);
            var occurrence = CreateOccurence.Of(tempItem) with
            {
                Quantity = run.ElementCount,
            };

            UpdateOccurrenceProperties(occurrence, run, document);
            return occurrence;
        }

        private void UpdateOccurrenceProperties(dynamic occurrence, CableTrayRun run, Document document)
        {
            // Set basic occurrence properties
            occurrence.Set(AssistantArgs.UserArgsDrofusParameters["DrofusLengthDouble"], run.TotalLengthMeters);
            occurrence.Set(AssistantArgs.UserArgsDrofusParameters["DrofusModelName"], 
                document.ProjectInformation.LookupParameter("model_name_drofus")?.AsString() ?? "No model name parameter found");

            // Handle all user-defined properties dynamically
            SetAdditionalProperties(occurrence, run);
        }

        private void SetAdditionalProperties(dynamic occurrence, CableTrayRun run)
        {
            // Set AdditionalProp1 if configured
            SetProperty(occurrence, "RevitAdditionalProp1", "DrofusAdditionalProp1", run.AdditionalProp1, "AdditionalProp1");

            // Set AdditionalProp2 if configured
            SetProperty(occurrence, "RevitAdditionalProp2", "DrofusAdditionalProp2", run.AdditionalProp2, "AdditionalProp2");

            // Set AdditionalProp3 if configured
            SetProperty(occurrence, "RevitAdditionalProp3", "DrofusAdditionalProp3", run.AdditionalProp3, "AdditionalProp3");

            // Set AdditionalProp4 if configured
            SetProperty(occurrence, "RevitAdditionalProp4", "DrofusAdditionalProp4", run.AdditionalProp4, "AdditionalProp4");
            // Set AdditionalProp5 if configured
            SetProperty(occurrence, "RevitAdditionalProp5", "DrofusAdditionalProp5", run.AdditionalProp5, "AdditionalProp5");
            // Set AdditionalProp6 if configured
            SetProperty(occurrence, "RevitAdditionalProp6", "DrofusAdditionalProp6", run.AdditionalProp6, "AdditionalProp6");
        }

        private void SetProperty(dynamic occurrence, string revitKey, string drofusKey, string value, string propertyName)
        {
            if (!string.IsNullOrEmpty(AssistantArgs.UserArgsRevitParameters[revitKey]) &&
                AssistantArgs.UserArgsDrofusParameters.ContainsKey(drofusKey) &&
                !string.IsNullOrEmpty(AssistantArgs.UserArgsDrofusParameters[drofusKey]) &&
                !string.IsNullOrEmpty(value))
            {
                try
                {
                    occurrence.Set(AssistantArgs.UserArgsDrofusParameters[drofusKey], value);
                }
                catch (Exception ex)
                {
                    var typeHint = FieldTypeHelper.GetTypeHint(AssistantArgs.UserArgsDrofusParameters[drofusKey]);
                    var errorMsg = $"Warning: Failed to set dRofus field '{AssistantArgs.UserArgsDrofusParameters[drofusKey]}' with value '{value}' from {propertyName}. " +
                                  $"Field expects: {typeHint}. Error: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                }
            }
        }
    }
}