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

        public static List<CableTrayRun> GetCableTrayRuns(Document document, CancellationToken cancellationToken)
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
                            var widthParam = firstCableTray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                            cableTrayRun.CableTraySize = widthParam != null ?
                                UnitUtils.ConvertFromInternalUnits(widthParam.AsDouble(), UnitTypeId.Millimeters).ToString("0.##") :
                                "0.0";

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

    }
}