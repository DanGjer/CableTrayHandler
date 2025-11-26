namespace CableTrayHandler.Services
{
    public class CableTrayRunProcessor
    {
        public (List<CableTrayRun> newRuns, List<CableTrayRun> existingRuns, int existingCount, int totalRunsFound, int skippedUnchecked) 
            ProcessAndFilterCableTrayRuns(Document document, List<CableTrayRun> cableTrayRuns, HashSet<int> existingDrofusIds)
        {
            var filteredCableTrayRuns = new List<CableTrayRun>();
            var existingCableTrayRuns = new List<CableTrayRun>();
            int existingCount = 0;
            int totalRunsFound = cableTrayRuns.Count;

            foreach (var run in cableTrayRuns)
            {
                bool hasExistingId = false;
                foreach (var elemId in run.ConnectedElementIds)
                {
                    var element = document.GetElement(elemId);
                    var param = element.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"]);
                    if (param == null)
                    {
                        throw new InvalidOperationException(
                            $"Parameter '{AssistantArgs.UserArgsRevitParameters["RevitDrofusId"]}' not found on element {element.Id}.");
                    }

                    int occId = ExtractOccurrenceId(param);

                    if (occId != 0 && existingDrofusIds.Contains(occId))
                    {
                        run.DrofusOccId = occId;
                        hasExistingId = true;
                        existingCount++;
                        existingCableTrayRuns.Add(run);
                        break;
                    }
                }
                
                if (!hasExistingId)
                    filteredCableTrayRuns.Add(run);
            }

            // Count runs before checkbox filtering
            int runsBeforeCheckboxFilter = filteredCableTrayRuns.Count;
            
            // Filter to only include checked runs
            filteredCableTrayRuns = filteredCableTrayRuns.Where(run => run.IsChecked).ToList();
            
            // Calculate how many were skipped due to unchecked boxes
            int skippedUnchecked = runsBeforeCheckboxFilter - filteredCableTrayRuns.Count;

            return (filteredCableTrayRuns, existingCableTrayRuns, existingCount, totalRunsFound, skippedUnchecked);
        }

        public List<CableTrayRun> GetRunsWithoutItemIds(List<CableTrayRun> cableTrayRuns)
        {
            return cableTrayRuns.Where(run => run.DrofusItemId == 0).ToList();
        }

        private static int ExtractOccurrenceId(Parameter param)
        {
            int occId = 0;
            
            if (param.StorageType == StorageType.Integer)
            {
                occId = param.AsInteger();
            }
            else if (param.StorageType == StorageType.String)
            {
                var strVal = param.AsString();
                if (!string.IsNullOrWhiteSpace(strVal))
                {
                    int.TryParse(strVal, out occId);
                }
            }
            
            return occId;
        }

        public string GenerateResultMessage(int existingCount, int createdCount, int updatedCount, int cableTrayRunsWithNoIdCount, bool isDryRun, int totalRunsFound, int skippedUnchecked)
        {
            var statsInfo = $"Total cable tray runs found: {totalRunsFound}";
            if (skippedUnchecked > 0)
            {
                statsInfo += $"\nSkipped (unchecked): {skippedUnchecked}";
                var remainingAfterCheckbox = totalRunsFound - skippedUnchecked;
                statsInfo += $"\nRemaining runs to process: {remainingAfterCheckbox}";
            }
            
            if (cableTrayRunsWithNoIdCount == 0)
            {
                if (isDryRun)
                {
                    return $"{statsInfo}\n\nDRY RUN - No changes made!\nWould update runs: {updatedCount}\nWould create new runs in dRofus: {createdCount}";
                }
                else
                {
                    return $"{statsInfo}\n\nSuccess! Updated runs: {updatedCount}\nNew runs created in dRofus: {createdCount}";
                }
            }
            else
            {
                if (isDryRun)
                {
                    return $"{statsInfo}\n\nDRY RUN - No changes made!\n" +
                        $"Cable tray runs with dimensions NOT specified in config: {cableTrayRunsWithNoIdCount}\n" +
                        $"Runs skipped (already have existing dRofus IDs): {existingCount}\n" +
                        $"New cable tray runs that WOULD be created in dRofus: {createdCount}";
                }
                else
                {
                    return $"{statsInfo}\n\nPartial success!\n" +
                        $"Cable tray runs with dimensions NOT specified in config: {cableTrayRunsWithNoIdCount}\n" +
                        $"Runs skipped (already have existing dRofus IDs): {existingCount}\n" +
                        $"New cable tray runs created in dRofus: {createdCount}";
                }
            }
        }
    }
}