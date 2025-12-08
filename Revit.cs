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

                // STEP 1: Pre-group elements by existing GUIDs (user overrides from previous runs)
                var preGroupedRuns = new Dictionary<string, CableTrayRun>();
                foreach (Element elem in allCableTrays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var existingGuid = elem.LookupParameter(RUN_ID_PARAM)?.AsString();
                    
                    if (!string.IsNullOrEmpty(existingGuid))
                    {
                        // Element has a user-assigned or previous GUID - respect it
                        if (!preGroupedRuns.ContainsKey(existingGuid))
                        {
                            preGroupedRuns[existingGuid] = new CableTrayRun { RunId = existingGuid };
                        }
                        
                        preGroupedRuns[existingGuid].AddElement(elem);
                        processedElements.Add(elem.Id);
                    }
                }

                // Add pre-grouped runs to results
                foreach (var run in preGroupedRuns.Values)
                {
                    cableTrayRuns.Add(run);
                }

                // STEP 2: Connector-based tracing for remaining elements (not yet assigned a GUID)
                foreach (Element elem in allCableTrays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (processedElements.Contains(elem.Id)) continue;

                    var cableTrayRun = new CableTrayRun();
                    TraceCableTrayRun(elem, cableTrayRun, processedElements);

                    if (cableTrayRun.ElementCount > 0)
                    {
                        cableTrayRuns.Add(cableTrayRun);
                    }
                }

                // STEP 3: Write the GUID to each element in all runs
                foreach (var run in cableTrayRuns)
                {
                    foreach (ElementId id in run.ConnectedElementIds)
                    {
                        Element element = document.GetElement(id);
                        element.LookupParameter(RUN_ID_PARAM)?.Set(run.RunId);
                    }
                }

                // STEP 4: Get properties from first cable tray in each run
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

                trans.Commit();
            }

            // STEP 5: Apply proximity-based merging if enabled
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
                        if (drofusTagElem != null && run.DrofusTag != null)
                            drofusTagElem.Set(run.DrofusTag);
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

                    // Only consider merging if same normalized size
                    if (run1Width != run2Width) continue;

                    // Get all cable tray elements (not fittings) for each run
                    var run1Elements = run1.ConnectedElementIds
                        .Select(id => document.GetElement(id))
                        .Where(e => e != null && e is CableTray)
                        .Cast<CableTray>()
                        .ToList();

                    var run2Elements = run2.ConnectedElementIds
                        .Select(id => document.GetElement(id))
                        .Where(e => e != null && e is CableTray)
                        .Cast<CableTray>()
                        .ToList();

                    // Check if any cable tray from run1 is within tolerance of any cable tray from run2
                    bool shouldMerge = false;
                    foreach (var tray1 in run1Elements)
                    {
                        if (shouldMerge) break;

                        var curve1 = (tray1.Location as LocationCurve)?.Curve;
                        if (curve1 == null) continue;

                        foreach (var tray2 in run2Elements)
                        {
                            var curve2 = (tray2.Location as LocationCurve)?.Curve;
                            if (curve2 == null) continue;

                            // Calculate minimum distance between the two curves' endpoints
                            double minDistance = CalculateMinDistanceBetweenCurves(curve1, curve2);

                            if (minDistance <= toleranceFeet)
                            {
                                shouldMerge = true;
                                break;
                            }
                        }
                    }

                    // If runs should be merged, combine run2 into run1
                    if (shouldMerge)
                    {
                        // Add all elements from run2 to run1
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

            return result;
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