using System.Globalization;

namespace CableTrayHandler
{
    public class CableTrayRun
    {
        public string RunId { get; set; }
        public List<ElementId> ConnectedElementIds { get; private set; }
        public string CableTraySize { get; set; }
        public double TotalLengthMeters { get; private set; }  // Changed property name to be explicit
        public string DrofusTag { get; set; }        // New property
        public int DrofusOccId { get; set; }      // New property
        public int ElementCount => ConnectedElementIds.Count;
        public int DrofusItemId { get; set; } // Changed from string to int
        public bool IsChecked { get; set; } // New property

        // Additional properties for flexible user-defined parameters
        public string AdditionalProp1 { get; set; } = "";
        public string AdditionalProp2 { get; set; } = "";
        public string AdditionalProp3 { get; set; } = "";
        public string AdditionalProp4 { get; set; } = "";
        public string AdditionalProp5 { get; set; } = "";
        public string AdditionalProp6 { get; set; } = "";

        public CableTrayRun()
        {
            RunId = Guid.NewGuid().ToString();
            ConnectedElementIds = new List<ElementId>();
            CableTraySize = "0.0";
            TotalLengthMeters = 0.0;
            DrofusTag = "No tag";       // Initialize with default value
            DrofusOccId = 0;      // Initialize with default value
            DrofusItemId = 0; // Use -1 or 0 to indicate "not set"
            IsChecked = false; // Initialize as unchecked
        }

        public void AddElement(Element element)
        {
            ConnectedElementIds.Add(element.Id);

            if (element is MEPCurve mepCurve)
            {
                var curve = (element.Location as LocationCurve)?.Curve;
                if (curve != null)
                {
                    double lengthInMeters = UnitUtils.ConvertFromInternalUnits(curve.Length, UnitTypeId.Meters);
                    TotalLengthMeters += Math.Round(lengthInMeters, 2);
                }
            }
            else if (element is FamilyInstance fitting)
            {
                // Try to estimate length for elbows based on bend radius and angle
                string familyName = fitting.Symbol.FamilyName.ToLower();

                var connectors = fitting.MEPModel?.ConnectorManager?.Connectors?.Cast<Connector>().ToList();
                if (connectors != null && connectors.Count == 2 && familyName.Contains("elbow"))
                {
                    // Get bend radius (fallback to 0.05m if not found)
                    double bendRadius = 0.05;
                    var radiusParam = fitting.LookupParameter("Bend Radius");
                    if (radiusParam != null && radiusParam.StorageType == StorageType.Double)
                        bendRadius = UnitUtils.ConvertFromInternalUnits(radiusParam.AsDouble(), UnitTypeId.Meters);

                    // Get vectors for connectors
                    var v1 = connectors[0].CoordinateSystem.BasisZ;
                    var v2 = connectors[1].CoordinateSystem.BasisZ;
                    double angle = v1.AngleTo(v2); // in radians

                    // Arc length for the elbow
                    double arcLength = bendRadius * angle;
                    TotalLengthMeters += Math.Round(arcLength, 2);
                }
                else
                {
                    // For tees, couplings, or unknowns, use a fixed guess
                    double fittingLength = 0.05; // Default 5cm
                    if (familyName.Contains("tee"))
                        fittingLength = 0.08;
                    else if (familyName.Contains("coupling"))
                        fittingLength = 0.03;

                    TotalLengthMeters += fittingLength;
                }
            }
        }
    }

    public class RevitCableTrays
    {
        private static readonly string RUN_ID_PARAM = AssistantArgs.UserArgsRevitParameters["RevitRunGuid"];

        //Properties:
        public string CableTrayDimension { get; set; }
        public int CableTrayOccId { get; set; }
        public string CableTrayTag { get; set; }

        public RevitCableTrays(string dimension, int occId, string tag)
        {
            CableTrayDimension = dimension;
            CableTrayOccId = occId;
            CableTrayTag = tag;
        }
        public static List<RevitCableTrays> CableTrayCollector(Document doc)
        {
            var cableTrayList = new List<RevitCableTrays>();

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                if (elem is CableTray cableTray)
                {
                    var widthParam = cableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                    string dimension = widthParam != null ?
                        UnitUtils.ConvertFromInternalUnits(widthParam.AsDouble(), UnitTypeId.Millimeters).ToString("0.##") :
                        "0.0";

                    var param = cableTray.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"]);
                    int occId = 0;

                    if (param != null)
                    {
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
                        // Optionally handle other storage types if needed
                    }

                    string tag = cableTray.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitTag"])?.AsString() ?? "";

                    cableTrayList.Add(new RevitCableTrays(dimension, occId, tag));
                }
            }

            return cableTrayList;
        }

        private static string GetMajorityParameterValue(List<ElementId> elementIds, Document doc, string parameterKey)
        {
            // Check if the parameter is configured
            if (!AssistantArgs.UserArgsRevitParameters.ContainsKey(parameterKey) || 
                string.IsNullOrEmpty(AssistantArgs.UserArgsRevitParameters[parameterKey]))
            {
                return "";
            }

            var parameterName = AssistantArgs.UserArgsRevitParameters[parameterKey];
            var parameterValues = new List<string>();

            foreach (var elementId in elementIds)
            {
                var element = doc.GetElement(elementId);
                var param = element?.LookupParameter(parameterName);
                if (param != null)
                {
                    string paramValue = "";
                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            paramValue = param.AsString() ?? "";
                            break;
                        case StorageType.Integer:
                            paramValue = param.AsInteger().ToString();
                            break;
                        case StorageType.Double:
                            paramValue = param.AsDouble().ToString(CultureInfo.InvariantCulture);
                            break;
                        case StorageType.ElementId:
                            paramValue = param.AsElementId().ToString();
                            break;
                    }

                    if (!string.IsNullOrEmpty(paramValue))
                    {
                        parameterValues.Add(paramValue);
                    }
                }
            }

            // Return majority value or empty string
            return parameterValues.Any() ? 
                parameterValues.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : 
                "";  
        }

        public static List<CableTrayRun> GetCableTrayRuns(Document document, CancellationToken cancellationToken, bool enableProximityMerging = false, double toleranceMm = 0.0)
        {
            var cableTrayRuns = new List<CableTrayRun>();
            var processedElements = new HashSet<ElementId>();

            using (Transaction trans = new Transaction(document, "Write Cable Tray Run GUIDs"))
            {
                trans.Start();

                var allCableTrays = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_CableTray)
                    .WhereElementIsNotElementType()
                    .ToElements();

                // Connector-based tracing for all elements
                foreach (Element elem in allCableTrays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (processedElements.Contains(elem.Id)) continue;

                    var cableTrayRun = new CableTrayRun();
                    TraceCableTrayRun(elem, cableTrayRun, processedElements);

                    if (cableTrayRun.ElementCount > 0)
                    {
                        // Write the GUID to each element in the run
                        foreach (ElementId id in cableTrayRun.ConnectedElementIds)
                        {
                            Element element = document.GetElement(id);
                            element.LookupParameter(RUN_ID_PARAM)?.Set(cableTrayRun.RunId);
                        }

                        cableTrayRuns.Add(cableTrayRun);
                    }
                }

                // Get properties from first cable tray in each run
                foreach (var run in cableTrayRuns)
                {
                    var firstCableTray = run.ConnectedElementIds
                        .Select(id => document.GetElement(id) as CableTray)
                        .FirstOrDefault(ct => ct != null);

                    if (firstCableTray != null)
                    {
                        // Use normalized size (max of width/height) to handle cable channels with swapped parameters
                        run.CableTraySize = GetNormalizedCableTraySize(firstCableTray);

                        // Add dRofus properties using majority values from all elements in the run
                        var tagValues = run.ConnectedElementIds
                            .Select(id => document.GetElement(id)?.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitTag"])?.AsString())
                            .Where(val => !string.IsNullOrEmpty(val))
                            .Cast<string>()
                            .ToList();
                        run.DrofusTag = tagValues.Any() ? 
                            tagValues.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : 
                            "No tag";

                        var idValues = run.ConnectedElementIds
                            .Select(id => document.GetElement(id)?.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"])?.AsInteger())
                            .Where(val => val.HasValue && val.Value != 0)
                            .Select(val => val!.Value)
                            .ToList();
                        run.DrofusOccId = idValues.Any() ? 
                            idValues.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : 
                            0;

                        // Extract additional properties using majority values
                        run.AdditionalProp1 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp1");
                        run.AdditionalProp2 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp2");
                        run.AdditionalProp3 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp3");
                        run.AdditionalProp4 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp4");
                        run.AdditionalProp5 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp5");
                        run.AdditionalProp6 = GetMajorityParameterValue(run.ConnectedElementIds, document, "RevitAdditionalProp6");
                    }

                    // Set IsChecked based on the checkbox parameter
                    run.IsChecked = run.ConnectedElementIds
                        .Select(id => document.GetElement(id))
                        .Any(element =>
                            element.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitCheckbox"])?.AsInteger() == 1);
                }

                // Create standalone runs for any untraced fittings
                var allFittings = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (Element fitting in allFittings)
                {
                    if (!processedElements.Contains(fitting.Id))
                    {
                        var standaloneFittingRun = new CableTrayRun();
                        standaloneFittingRun.ConnectedElementIds.Add(fitting.Id);
                        processedElements.Add(fitting.Id);

                        // Write the GUID to the fitting
                        fitting.LookupParameter(RUN_ID_PARAM)?.Set(standaloneFittingRun.RunId);

                        // Set properties for the standalone fitting
                        if (fitting is FamilyInstance)
                        {
                            standaloneFittingRun.CableTraySize = "Unknown";
                            standaloneFittingRun.DrofusTag = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitTag"])?.AsString() ?? "No tag";
                            
                            var idParam = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"]);
                            if (idParam != null && idParam.StorageType == StorageType.Integer)
                            {
                                standaloneFittingRun.DrofusOccId = idParam.AsInteger();
                            }

                            standaloneFittingRun.AdditionalProp1 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp1"])?.AsString() ?? "";
                            standaloneFittingRun.AdditionalProp2 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp2"])?.AsString() ?? "";
                            standaloneFittingRun.AdditionalProp3 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp3"])?.AsString() ?? "";
                            standaloneFittingRun.AdditionalProp4 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp4"])?.AsString() ?? "";
                            standaloneFittingRun.AdditionalProp5 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp5"])?.AsString() ?? "";
                            standaloneFittingRun.AdditionalProp6 = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp6"])?.AsString() ?? "";
                            
                            standaloneFittingRun.IsChecked = fitting.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitCheckbox"])?.AsInteger() == 1;
                        }

                        cableTrayRuns.Add(standaloneFittingRun);
                    }
                }

                trans.Commit();
            }

            // Apply proximity-based merging if enabled
            if (enableProximityMerging)
            {
                cableTrayRuns = MergeProximityRuns(cableTrayRuns, document, toleranceMm);
                
                // Update GUIDs for merged runs - all elements in a merged run get the same GUID
                using (Transaction trans = new Transaction(document, "Update Run GUIDs after merging"))
                {
                    trans.Start();
                    
                    foreach (var run in cableTrayRuns)
                    {
                        foreach (var elemId in run.ConnectedElementIds)
                        {
                            var element = document.GetElement(elemId);
                            element?.LookupParameter(RUN_ID_PARAM)?.Set(run.RunId);
                        }
                    }
                    
                    trans.Commit();
                }
            }

            return cableTrayRuns;
        }

        private static void TraceCableTrayRun(Element element, CableTrayRun cableTrayRun, HashSet<ElementId> processedElements)
        {
            if (processedElements.Contains(element.Id)) return;

            // Only add cable trays and cable tray fittings
            if (element is CableTray ||
                (element is FamilyInstance fi && fi.Category.Id.Value == (int)BuiltInCategory.OST_CableTrayFitting))
            {
                cableTrayRun.AddElement(element);
                processedElements.Add(element.Id);
            }
            else
            {
                // Skip other elements
                return;
            }

            // Get connectors based on element type
            IList<Connector>? elementConnectors = null;
            if (element is MEPCurve mepCurve)
            {
                elementConnectors = mepCurve.ConnectorManager?.Connectors?.Cast<Connector>().ToList();
            }
            else if (element is FamilyInstance familyInstance)
            {
                elementConnectors = familyInstance.MEPModel?.ConnectorManager?.Connectors?.Cast<Connector>().ToList();
            }

            if (elementConnectors == null) return;

            foreach (Connector conn in elementConnectors)
            {
                foreach (Connector ref_conn in conn.AllRefs)
                {
                    var connected = ref_conn.Owner;
                    // Only recurse into cable trays and cable tray fittings
                    if (connected is CableTray ||
                        (connected is FamilyInstance fi2 && fi2.Category.Id.Value == (int)BuiltInCategory.OST_CableTrayFitting))
                    {
                        TraceCableTrayRun(connected, cableTrayRun, processedElements);
                    }
                }
            }
        }

        public static void AssignDrofusItemIds(
            List<CableTrayRun> cableTrayRuns,
            Dictionary<string, string> cableTraySizeToItemIdDict)
        {
            foreach (var run in cableTrayRuns)
            {
                if (run.CableTraySize != null && cableTraySizeToItemIdDict.TryGetValue(run.CableTraySize, out var itemIdStr))
                {
                    if (int.TryParse(itemIdStr, out int itemId))
                    {
                        run.DrofusItemId = itemId;
                    }
                    else
                    {
                        run.DrofusItemId = 0;
                    }
                }
                else
                {
                    run.DrofusItemId = 0;
                }
            }
        }

        public static void WriteRunMetadataToRevit(Document doc, List<CableTrayRun> cableTrayRuns, string drofusOccIdParam, string drofusTagParam, bool writeOccurrenceId = true)
        {
            using (Transaction trans = new Transaction(doc, "Write Cable Tray Run Metadata"))
            {
                trans.Start();

                foreach (var run in cableTrayRuns)
                {
                    foreach (var elemId in run.ConnectedElementIds)
                    {
                        var element = doc.GetElement(elemId);
                        if (element == null) continue;

                        // Write DrofusOccId (only for new runs)
                        if (writeOccurrenceId)
                        {
                            var drofusOccIdElem = element.LookupParameter(drofusOccIdParam);
                            if (drofusOccIdElem != null)
                            {
                                if (drofusOccIdElem.StorageType == StorageType.Integer)
                                    drofusOccIdElem.Set(run.DrofusOccId);
                                else if (drofusOccIdElem.StorageType == StorageType.String)
                                    drofusOccIdElem.Set(run.DrofusOccId.ToString());
                            }
                        }

                        // Write DrofusTag (as string)
                        var drofusTagElem = element.LookupParameter(drofusTagParam);
                        if (drofusTagElem != null)
                        {
                            if (run.DrofusTag != null)
                                drofusTagElem.Set(run.DrofusTag);
                        }
                    }
                }

                trans.Commit();
            }
        }

        public static bool ParameterChecker(Document document)
        {
            // Categories to check
            var categoriesToCheck = new[]
            {
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting
            };

            // Parameters to check - only system-required parameters that are configured
            var paramNames = new[]
            {
                AssistantArgs.UserArgsRevitParameters["RevitTag"],
                AssistantArgs.UserArgsRevitParameters["RevitDrofusId"],
                AssistantArgs.UserArgsRevitParameters["RevitRunGuid"],
                AssistantArgs.UserArgsRevitParameters["RevitCheckbox"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp1"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp2"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp3"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp4"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp5"],
                AssistantArgs.UserArgsRevitParameters["RevitAdditionalProp6"]
            }.Where(param => !string.IsNullOrEmpty(param)).ToArray();

            // If no parameters are configured, return true to allow the operation
            if (!paramNames.Any())
                return true;

            BindingMap bindingMap = document.ParameterBindings;
            DefinitionBindingMapIterator it = bindingMap.ForwardIterator();

            foreach (string paramName in paramNames)
            {
                bool foundForAllCategories = true;

                foreach (BuiltInCategory bic in categoriesToCheck)
                {
                    bool foundForCategory = false;
                    it.Reset();

                    while (it.MoveNext())
                    {
                        Definition definition = it.Key;
                        if (definition.Name == paramName)
                        {
                            ElementBinding? binding = it.Current as ElementBinding;
                            if (binding != null)
                            {
                                foreach (Category cat in binding.Categories)
                                {
                                    if (cat.Id.Value == (int)BuiltInCategory.OST_CableTray)
                                    {
                                        foundForCategory = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (foundForCategory) break;
                    }

                    if (!foundForCategory)
                    {
                        foundForAllCategories = false;
                        break;
                    }
                }

                if (!foundForAllCategories)
                    return false;
            }

            return true;
        }

        private static List<CableTrayRun> MergeProximityRuns(List<CableTrayRun> runs, Document document, double toleranceMm)
        {
            double toleranceFeet = toleranceMm / 304.8; // Convert mm to feet (Revit's internal unit)

            var mergedRunIndices = new HashSet<int>(); // Track which runs have been merged
            var runMerges = new Dictionary<int, int>(); // Maps old run index to new run index

            // Debug logging
            var debugTargetIds = new HashSet<int> { 5104874, 5104876, 9347651, 9347661, 9347671 };
            var logLines = new List<string> { $"=== Merge pass {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===" };
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CableTrayMergeLog.txt");

            // For each pair of runs, check if they should be merged
            for (int i = 0; i < runs.Count; i++)
            {
                if (mergedRunIndices.Contains(i)) continue;

                var run1 = runs[i];
                var run1Width = run1.CableTraySize;

                for (int j = i + 1; j < runs.Count; j++)
                {
                    if (mergedRunIndices.Contains(j)) continue;

                    var run2 = runs[j];
                    var run2Width = run2.CableTraySize;

                    // Only enforce size match when both runs have a defined tray size
                    bool run1HasSize = !string.IsNullOrEmpty(run1Width) && run1Width != "Unknown";
                    bool run2HasSize = !string.IsNullOrEmpty(run2Width) && run2Width != "Unknown";
                    if (run1HasSize && run2HasSize && run1Width != run2Width)
                    {
                        continue;
                    }

                    // Collect curves from trays and fittings for each run
                    var run1Curves = GetRunCurves(run1, document);
                    var run2Curves = GetRunCurves(run2, document);

                    // If either run has no usable curves, skip
                    if (!run1Curves.Any() || !run2Curves.Any())
                    {
                        continue;
                    }

                    // Check if any curve from run1 is within tolerance of any curve from run2
                    bool shouldMerge = false;
                    double minDistanceFound = double.MaxValue;

                    foreach (var curve1 in run1Curves)
                    {
                        if (shouldMerge) break;

                        foreach (var curve2 in run2Curves)
                        {
                            double minDistance = CalculateMinDistanceBetweenCurves(curve1, curve2);
                            if (minDistance < minDistanceFound)
                                minDistanceFound = minDistance;

                            // Get XY and Z distance components separately
                            var (xyDistance, zDistance) = CalculateXYAndZDistances(curve1, curve2);

                            // Adjust XY distance to account for cable tray widths (edge-to-edge instead of centerline-to-centerline)
                            double adjustedXyDistance = xyDistance;
                            if (run1HasSize && run2HasSize)
                            {
                                // Parse widths from size strings (e.g., "600" -> 600mm)
                                if (double.TryParse(run1Width, out double width1Mm) && double.TryParse(run2Width, out double width2Mm))
                                {
                                    // Convert widths to feet and subtract half-widths from XY distance only
                                    double combinedHalfWidthsFeet = (width1Mm / 2 + width2Mm / 2) / 304.8;
                                    adjustedXyDistance = xyDistance - combinedHalfWidthsFeet;
                                }
                            }

                            // Merge only if:
                            // 1. XY distance (after width adjustment) is within tolerance
                            // 2. Z distance is very small (e.g., <= 50mm = ~0.164 feet) to avoid merging vertically stacked trays
                            double maxZDifference = 50.0 / 304.8; // 50mm in feet
                            if (adjustedXyDistance <= toleranceFeet && zDistance <= maxZDifference)
                            {
                                shouldMerge = true;
                                break;
                            }
                        }
                    }

                    // Debug logging for target elements
                    bool involvesTargets = run1.ConnectedElementIds.Any(id => debugTargetIds.Contains(id.IntegerValue)) ||
                                           run2.ConnectedElementIds.Any(id => debugTargetIds.Contains(id.IntegerValue));

                    if (involvesTargets)
                    {
                        var run1Ids = string.Join(", ", run1.ConnectedElementIds.Select(id => id.IntegerValue));
                        var run2Ids = string.Join(", ", run2.ConnectedElementIds.Select(id => id.IntegerValue));
                        logLines.Add($"Pair: run1 IDs [{run1Ids}] vs run2 IDs [{run2Ids}]");
                        logLines.Add($"Raw centerline distance: {minDistanceFound:0.######} ft ({minDistanceFound * 304.8:0.###} mm)");
                        
                        if (run1HasSize && run2HasSize && double.TryParse(run1Width, out double width1) && double.TryParse(run2Width, out double width2))
                        {
                            double combinedHalfWidthsMm = width1 / 2 + width2 / 2;
                            double adjustedDistanceFeet = minDistanceFound - (combinedHalfWidthsMm / 304.8);
                            logLines.Add($"Width adjustment: {width1}/2 + {width2}/2 = {combinedHalfWidthsMm} mm");
                            logLines.Add($"Adjusted distance: {adjustedDistanceFeet:0.######} ft ({adjustedDistanceFeet * 304.8:0.###} mm)");
                        }
                        
                        logLines.Add($"Result: {(shouldMerge ? "MERGED" : "NOT MERGED")}");
                        logLines.Add($"---");
                    }

                    // If runs should be merged, combine run2 into run1
                    if (shouldMerge)
                    {
                        foreach (var elemId in run2.ConnectedElementIds)
                        {
                            run1.AddElement(document.GetElement(elemId));
                        }

                        mergedRunIndices.Add(j);
                        runMerges[j] = i;
                    }
                }
            }

            // Return the merged runs (exclude ones that were merged into others)
            var result = new List<CableTrayRun>();
            for (int i = 0; i < runs.Count; i++)
            {
                if (!mergedRunIndices.Contains(i))
                {
                    result.Add(runs[i]);
                }
            }

            // Write debug log
            if (logLines.Count > 1)
            {
                try
                {
                    File.AppendAllLines(logPath, logLines);
                }
                catch
                {
                    // Swallow logging errors
                }
            }

            return result;
        }

        // Build a list of representative curves for trays and fittings in a run
        private static List<Curve> GetRunCurves(CableTrayRun run, Document document)
        {
            var curves = new List<Curve>();

            foreach (var elemId in run.ConnectedElementIds)
            {
                var element = document.GetElement(elemId);
                if (element == null) continue;

                if (element is CableTray tray)
                {
                    var locCurve = (tray.Location as LocationCurve)?.Curve;
                    if (locCurve != null)
                        curves.Add(locCurve);
                }
                else if (element is FamilyInstance fi && fi.Category.Id.Value == (int)BuiltInCategory.OST_CableTrayFitting)
                {
                    // Try to build a curve between two connectors as a proxy for the fitting
                    var connectors = fi.MEPModel?.ConnectorManager?.Connectors;
                    if (connectors != null)
                    {
                        var refs = connectors.Cast<Connector>().ToList();
                        if (refs.Count >= 2)
                        {
                            var p1 = refs[0].Origin;
                            var p2 = refs[1].Origin;
                            if (!p1.IsAlmostEqualTo(p2))
                            {
                                curves.Add(Line.CreateBound(p1, p2));
                            }
                        }
                    }
                }
            }

            return curves;
        }

        private static double CalculateMinDistanceBetweenCurves(Curve curve1, Curve curve2)
        {
            // Get all endpoints
            var p1Start = curve1.GetEndPoint(0);
            var p1End = curve1.GetEndPoint(1);
            var p2Start = curve2.GetEndPoint(0);
            var p2End = curve2.GetEndPoint(1);

            // Calculate all possible distances between endpoints
            double d1 = p1Start.DistanceTo(p2Start);
            double d2 = p1Start.DistanceTo(p2End);
            double d3 = p1End.DistanceTo(p2Start);
            double d4 = p1End.DistanceTo(p2End);

            // Also check if either curve's endpoints are close to the other curve itself
            // This handles T-junctions and L-shapes where an endpoint meets the middle of another curve
            IntersectionResult result1 = curve2.Project(p1Start);
            IntersectionResult result2 = curve2.Project(p1End);
            IntersectionResult result3 = curve1.Project(p2Start);
            IntersectionResult result4 = curve1.Project(p2End);

            double d5 = result1?.Distance ?? double.MaxValue;
            double d6 = result2?.Distance ?? double.MaxValue;
            double d7 = result3?.Distance ?? double.MaxValue;
            double d8 = result4?.Distance ?? double.MaxValue;

            // Return the minimum of all distances
            return new[] { d1, d2, d3, d4, d5, d6, d7, d8 }.Min();
        }

        private static (double xyDistance, double zDistance) CalculateXYAndZDistances(Curve curve1, Curve curve2)
        {
            // Get all endpoints
            var p1Start = curve1.GetEndPoint(0);
            var p1End = curve1.GetEndPoint(1);
            var p2Start = curve2.GetEndPoint(0);
            var p2End = curve2.GetEndPoint(1);

            // Find minimum XY distance (ignoring Z) and corresponding Z difference
            double minXyDistance = double.MaxValue;
            double zDistanceAtMinXy = 0;

            var points1 = new[] { p1Start, p1End };
            var points2 = new[] { p2Start, p2End };

            foreach (var p1 in points1)
            {
                foreach (var p2 in points2)
                {
                    // XY distance
                    double xyDist = Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
                    
                    if (xyDist < minXyDistance)
                    {
                        minXyDistance = xyDist;
                        zDistanceAtMinXy = Math.Abs(p1.Z - p2.Z);
                    }
                }
            }

            // Also check projection distances (XY only)
            IntersectionResult result1 = curve2.Project(p1Start);
            if (result1 != null)
            {
                double xyDist = result1.Distance;
                XYZ projPoint = result1.XYZPoint;
                if (xyDist < minXyDistance)
                {
                    minXyDistance = xyDist;
                    zDistanceAtMinXy = Math.Abs(p1Start.Z - projPoint.Z);
                }
            }

            IntersectionResult result2 = curve2.Project(p1End);
            if (result2 != null)
            {
                double xyDist = result2.Distance;
                XYZ projPoint = result2.XYZPoint;
                if (xyDist < minXyDistance)
                {
                    minXyDistance = xyDist;
                    zDistanceAtMinXy = Math.Abs(p1End.Z - projPoint.Z);
                }
            }

            IntersectionResult result3 = curve1.Project(p2Start);
            if (result3 != null)
            {
                double xyDist = result3.Distance;
                XYZ projPoint = result3.XYZPoint;
                if (xyDist < minXyDistance)
                {
                    minXyDistance = xyDist;
                    zDistanceAtMinXy = Math.Abs(projPoint.Z - p2Start.Z);
                }
            }

            IntersectionResult result4 = curve1.Project(p2End);
            if (result4 != null)
            {
                double xyDist = result4.Distance;
                XYZ projPoint = result4.XYZPoint;
                if (xyDist < minXyDistance)
                {
                    minXyDistance = xyDist;
                    zDistanceAtMinXy = Math.Abs(projPoint.Z - p2End.Z);
                }
            }

            return (minXyDistance, zDistanceAtMinXy);
        }

        private static string GetNormalizedCableTraySize(CableTray cableTray)
        {
            try
            {
                var widthParam = cableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                var heightParam = cableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);

                double width = widthParam?.AsDouble() ?? 0;
                double height = heightParam?.AsDouble() ?? 0;

                // Use the larger dimension to handle cases where width/height are swapped
                double largerDimension = Math.Max(width, height);

                return largerDimension > 0 ?
                    UnitUtils.ConvertFromInternalUnits(largerDimension, UnitTypeId.Millimeters).ToString("0.##") :
                    "0.0";
            }
            catch
            {
                return "0.0";
            }
        }

    }
}