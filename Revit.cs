using System.Globalization;

namespace CableTrayHandler
{
    public class CableTrayRun
    {
        public string RunId { get; private set; }
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

                        // Get properties from first cable tray in the run
                        if (elem is CableTray firstCableTray)
                        {
                            // Use normalized size (max of width/height) to handle cable channels with swapped parameters
                            cableTrayRun.CableTraySize = GetNormalizedCableTraySize(firstCableTray);

                            // Add dRofus properties using majority values from all elements in the run
                            var tagValues = cableTrayRun.ConnectedElementIds
                                .Select(id => document.GetElement(id)?.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitTag"])?.AsString())
                                .Where(val => !string.IsNullOrEmpty(val))
                                .Cast<string>()
                                .ToList();
                            cableTrayRun.DrofusTag = tagValues.Any() ? 
                                tagValues.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : 
                                "No tag";

                            var idValues = cableTrayRun.ConnectedElementIds
                                .Select(id => document.GetElement(id)?.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitDrofusId"])?.AsInteger())
                                .Where(val => val.HasValue && val.Value != 0)
                                .Select(val => val!.Value)
                                .ToList();
                            cableTrayRun.DrofusOccId = idValues.Any() ? 
                                idValues.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : 
                                0;

                            // Extract additional properties using majority values
                            cableTrayRun.AdditionalProp1 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp1");
                            cableTrayRun.AdditionalProp2 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp2");
                            cableTrayRun.AdditionalProp3 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp3");
                            cableTrayRun.AdditionalProp4 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp4");
                            cableTrayRun.AdditionalProp5 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp5");
                            cableTrayRun.AdditionalProp6 = GetMajorityParameterValue(cableTrayRun.ConnectedElementIds, document, "RevitAdditionalProp6");
                        }

                        // Set IsChecked based on the checkbox parameter
                        cableTrayRun.IsChecked = cableTrayRun.ConnectedElementIds
                            .Select(id => document.GetElement(id))
                            .Any(element =>
                                element.LookupParameter(AssistantArgs.UserArgsRevitParameters["RevitCheckbox"])?.AsInteger() == 1);

                        cableTrayRuns.Add(cableTrayRun);
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

            // Debug logging for specific elements
            var targetElementIds = new[] { 8990992, 5586413, 5586699 };
            if (targetElementIds.Contains(element.Id.IntegerValue))
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CableTrayConnections.txt");
                var logLines = new List<string>
                {
                    $"\n=== Element {element.Id.IntegerValue} ===",
                    $"Type: {element.GetType().Name}",
                    $"Category: {element.Category?.Name ?? "Unknown"}",
                    $"Connector count: {elementConnectors.Count}"
                };

                foreach (Connector conn in elementConnectors)
                {
                    logLines.Add($"  Connector: {conn.Origin.X:F2}, {conn.Origin.Y:F2}, {conn.Origin.Z:F2}");
                    foreach (Connector ref_conn in conn.AllRefs)
                    {
                        var connected = ref_conn.Owner;
                        if (connected != null && connected.Id != element.Id)
                        {
                            logLines.Add($"    -> Connected to Element {connected.Id.IntegerValue} ({connected.GetType().Name}, {connected.Category?.Name ?? "Unknown"})");
                        }
                    }
                }

                File.AppendAllLines(logPath, logLines);
            }

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
                        if (!(element is CableTray)) continue; // Skip non-cable traystrays

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

            // Get all cable tray elements
            var allCableTrays = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<CableTray>()
                .ToList();

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

                    // Only consider merging if same normalized size (handles cable channels with swapped width/height)
                    if (run1Width != run2Width) continue;

                    // Get all cable tray elements and fittings for each run
                    var run1Elements = run1.ConnectedElementIds
                        .Select(id => document.GetElement(id))
                        .Where(e => e != null && (e is CableTray || 
                            (e is FamilyInstance fi && fi.Category.Id.Value == (int)BuiltInCategory.OST_CableTrayFitting)))
                        .ToList();

                    var run2Elements = run2.ConnectedElementIds
                        .Select(id => document.GetElement(id))
                        .Where(e => e != null && (e is CableTray || 
                            (e is FamilyInstance fi && fi.Category.Id.Value == (int)BuiltInCategory.OST_CableTrayFitting)))
                        .ToList();

                    // Check if any element from run1 is within tolerance of any element from run2
                    bool shouldMerge = false;
                    foreach (var elem1 in run1Elements)
                    {
                        if (shouldMerge) break;

                        var bbox1 = elem1.get_BoundingBox(null);
                        if (bbox1 == null) continue;

                        foreach (var elem2 in run2Elements)
                        {
                            var bbox2 = elem2.get_BoundingBox(null);
                            if (bbox2 == null) continue;

                            // Calculate minimum distance between bounding boxes
                            double minDistance = CalculateMinDistanceBetweenBoundingBoxes(bbox1, bbox2);

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

        private static double CalculateMinDistanceBetweenBoundingBoxes(BoundingBoxXYZ box1, BoundingBoxXYZ box2)
        {
            double dx = CalculateAxisDistance(box1.Min.X, box1.Max.X, box2.Min.X, box2.Max.X);
            double dy = CalculateAxisDistance(box1.Min.Y, box1.Max.Y, box2.Min.Y, box2.Max.Y);
            double dz = CalculateAxisDistance(box1.Min.Z, box1.Max.Z, box2.Min.Z, box2.Max.Z);

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double CalculateAxisDistance(double min1, double max1, double min2, double max2)
        {
            if (max1 < min2) return min2 - max1;
            if (max2 < min1) return min1 - max2;
            return 0; // Boxes overlap on this axis
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