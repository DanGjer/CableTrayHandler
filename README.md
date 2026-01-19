# Cable Tray Handler

## Description
Cable Tray Handler is a Revit extension that automates the management of cable tray runs by synchronizing them with dRofus. The extension identifies continuous cable tray runs (including straight sections and fittings), calculates their total length, and creates or updates corresponding occurrences in your dRofus database. This eliminates manual data entry and ensures your facility management database stays synchronized with your Revit MEP model.

## Configuration

### Cable Tray Size Mapping
- **Cable Tray Sizes**: A dictionary that maps cable tray widths (in millimeters) to dRofus Item IDs
  - **Key**: Cable tray width as it appears in Revit (e.g., "100", "200", "300" for 100mm, 200mm, 300mm wide trays)
  - **Value**: The corresponding dRofus Item ID number
  - **Example**: `"200" → "12345"` maps all 200mm wide cable trays to dRofus item 12345
  - **Purpose**: This tells the extension which dRofus item template to use when creating occurrences for each cable tray size

### General Settings
- **Dry Run**: Enable this to preview what would happen without making actual changes
  - **Default**: False (disabled)
  - **When enabled**: The extension shows what it would create/update in dRofus without writing anything
  - **Use case**: Test your configuration before committing changes to dRofus

### Revit Parameter Mapping
These settings map your Revit shared parameters to the extension's internal logic. All parameters must exist as shared parameters on both Cable Tray and Cable Tray Fitting categories.

- **RevitCheckbox**: Name of a Yes/No parameter used to select which cable tray runs to process
  - **Purpose**: Only cable tray runs with at least one element checked will be processed
  - **Example**: "Export to dRofus"

- **RevitTag**: Name of the parameter that will store the dRofus classification number
  - **Purpose**: Stores the tag/mark assigned by dRofus for reference and coordination
  - **Example**: "dRofus Tag"

- **RevitDrofusId**: Name of the parameter that will store the dRofus occurrence ID
  - **Purpose**: Links the Revit element to its dRofus occurrence for updates
  - **Storage**: Integer or Text parameter
  - **Example**: "dRofus ID"

- **RevitRunGuid**: Name of a text parameter used to group elements into runs
  - **Purpose**: Internal identifier that marks which elements belong to the same continuous run
  - **Example**: "Cable Tray Run GUID"

- **RevitAdditionalProp1-4**: Optional parameters to sync additional data to dRofus
  - **Purpose**: Custom properties specific to your project (e.g., fire rating, system type, voltage)
  - **Note**: Leave blank if not needed

- **RevitAdditionalStatusProp1**: Optional status field parameter
  - **Purpose**: Typically used for workflow states (e.g., "Proposed", "Approved", "Installed")
  - **Note**: Leave blank if not needed

### dRofus Parameter Mapping
These settings specify which dRofus fields to populate with data from Revit.

- **DrofusModelName**: The dRofus field name for storing which Revit model the data came from
  - **Source**: Retrieved from the Revit project parameter "model_name_drofus"
  - **Example**: "occurrence_attr_01"

- **DrofusLengthDouble**: The dRofus field name for storing the total cable tray run length
  - **Units**: Automatically converted to meters from Revit's internal units
  - **Example**: "occurrence_data_23_10_06_01"

- **DrofusAdditionalProp1-4**: dRofus field names corresponding to the Revit additional properties
  - **Purpose**: Must match the RevitAdditionalProp settings above
  - **Example**: "occurrence_attr_fire_rating"

- **DrofusAdditionalStatusProp1**: dRofus status field name for the additional status property
  - **Example**: "occurrence_status_01"

## Functionality

### Description
The Cable Tray Handler extension automates the following workflow:

1. **Identifies Cable Tray Runs**: Scans the active Revit model and traces continuous cable tray runs by following connections between cable tray segments and fittings (elbows, tees, crosses, reducers, etc.)

2. **Calculates Run Properties**: For each run, calculates:
   - Total length (sum of all segments and estimated fitting lengths)
   - Cable tray width (from the standard width parameter)
   - Element count (number of segments and fittings)

3. **Filters Runs**: Only processes runs where at least one element has the checkbox parameter checked

4. **Matches to dRofus Items**: Uses the cable tray width to look up the corresponding dRofus Item ID from your configuration

5. **Syncs with dRofus**:
   - **New runs**: Creates new occurrences in dRofus with all configured properties
   - **Existing runs**: Updates existing occurrences if they already have a dRofus ID

6. **Writes Back to Revit**: Stores the dRofus occurrence ID and tag back into the Revit elements for future reference

### How to Use

#### Prerequisites
1. **Revit Model Setup**:
   - Your model must contain cable tray systems with properly connected segments and fittings
   - All required shared parameters must be added to Cable Tray and Cable Tray Fitting categories
   - The project parameter "model_name_drofus" should be set with your model identifier

2. **dRofus Setup**:
   - You must have access to a dRofus database
   - Cable tray items must exist in dRofus with known Item IDs
   - You need valid credentials for the dRofus API

#### Step-by-Step Instructions

1. **Configure the Extension**:
   - Set up the cable tray size to Item ID mapping (e.g., "100" → "12345", "200" → "12346")
   - Map all Revit parameters to their corresponding shared parameter names
   - Map all dRofus parameters to their corresponding field names in dRofus
   - Enable "Dry Run" for your first test

2. **Select Cable Tray Runs**:
   - In Revit, check the checkbox parameter on cable tray elements you want to export
   - You only need to check one element per run (the extension will find the entire connected run)

3. **Run the Extension**:
   - Execute the Cable Tray Handler extension from Assistant
   - Review the dry run results to verify what will be created/updated
   - If satisfied, disable dry run and execute again to commit changes

4. **Verify Results**:
   - Check the result message for statistics on created/updated runs
   - In Revit, verify that dRofus IDs and tags have been written to your cable tray elements
   - In dRofus, confirm that occurrences were created/updated correctly

### Visual Aids
[Note: Screenshots should be added here showing:
- A Revit model with cable tray runs highlighted
- The shared parameter setup for cable trays
- The configuration interface in Assistant
- Example result messages
- dRofus occurrence view with synced data]

## Troubleshooting

### Issue 1: "Required parameters are not present in the current model"
- **Causes**: 
  - One or more required shared parameters are missing from the Cable Tray or Cable Tray Fitting categories
  - Parameter names in configuration don't match the actual parameter names in Revit
  - Parameters are project parameters instead of shared parameters
- **Solution**: 
  1. Verify all configured parameter names exist in your Revit model
  2. Check that parameters are added to both OST_CableTray and OST_CableTrayFitting categories
  3. Ensure parameters are shared parameters, not project parameters
  4. Parameter names are case-sensitive - verify exact spelling
- **Resources**: Revit Shared Parameters documentation

### Issue 2: Cable tray runs are split unexpectedly
- **Causes**: 
  - Cable tray segments are not physically connected in Revit (connectors aren't joined)
  - Multiple fittings are stacked on top of each other without being connected
  - Different cable tray widths in the same run (note: width changes will still be traced together, but may cause dRofus mapping issues)
- **Solution**: 
  1. Use Revit's "Show Connectors" to verify connections between segments
  2. Check for duplicate or overlapping fittings at connection points
  3. Delete duplicate fittings and ensure proper connection topology
  4. Use Revit's MEP connectivity tools to verify the run is continuous
- **Resources**: Revit MEP Connectivity Guide

### Issue 3: Some runs show "dimensions NOT specified in config"
- **Causes**: 
  - The cable tray width doesn't have a corresponding entry in the Cable Tray Sizes dictionary
  - Cable tray width is formatted differently than expected (e.g., "200.00" vs "200")
- **Solution**: 
  1. Check the width parameter of the problematic cable trays in Revit
  2. Add the missing width-to-ItemID mapping in the configuration
  3. Verify the width is formatted as it appears in Revit (use the exact string)
  4. Consider standardizing cable tray widths in your Revit template
- **Resources**: N/A

### Issue 4: dRofus connection fails
- **Causes**: 
  - Incorrect dRofus credentials or connection settings
  - Network connectivity issues
  - dRofus API is unavailable or down
- **Solution**: 
  1. Verify your dRofus credentials are correct
  2. Check network connectivity to the dRofus server
  3. Contact your dRofus administrator to verify API access
  4. Review the error message for specific connection details
- **Resources**: dRofus API documentation, IT support

### Issue 5: Checkbox filtering doesn't work as expected
- **Causes**: 
  - The checkbox parameter name is not configured correctly
  - None of the elements in a run have the checkbox checked
  - The parameter is not a Yes/No type parameter
- **Solution**: 
  1. Verify the RevitCheckbox parameter name matches your model exactly
  2. Ensure at least one element in each run you want to process is checked
  3. Confirm the parameter is a Yes/No (checkbox) type in Revit
- **Resources**: N/A

## FAQ

- **Q: Does the extension work with cable tray of different widths in the same run?**
  - **A: Yes, the extension will trace through the entire connected run regardless of width changes. However, only the width from the first cable tray segment will be used for dRofus item mapping. If your run has multiple widths, consider whether they should be separate runs in dRofus.**

- **Q: How are cable tray fitting lengths calculated?**
  - **A: The extension estimates fitting lengths based on their type:
    - Elbows: Calculated from bend radius and angle (approximately arc length)
    - Tees: Default 80mm
    - Couplings: Default 30mm
    - Other fittings: Default 50mm
    These are estimates and may not reflect exact physical dimensions.**

- **Q: Can I update existing occurrences in dRofus?**
  - **A: Yes. If a cable tray run already has a dRofus occurrence ID written to its elements, the extension will update that occurrence instead of creating a new one. This allows you to modify cable tray runs in Revit and re-sync to dRofus.**

- **Q: What happens if I run the extension multiple times on the same cable trays?**
  - **A: After the first run, cable trays will have dRofus IDs written to them. Subsequent runs will recognize these IDs and update the existing occurrences rather than creating duplicates. This is the intended workflow for keeping Revit and dRofus synchronized.**

- **Q: Why use a checkbox parameter instead of processing all cable trays?**
  - **A: The checkbox gives you control over which cable tray runs to export. This is useful for:
    - Phased exports (export design packages incrementally)
    - Excluding temporary or construction-only cable trays
    - Focusing on specific systems or areas
    - Avoiding re-processing runs that are already complete**

- **Q: Can I map the same Revit parameter to multiple dRofus fields?**
  - **A: No, each Revit parameter (AdditionalProp1-4) should map to exactly one dRofus field. If you need the same data in multiple dRofus fields, you'll need to duplicate the parameter in Revit.**

- **Q: What units are used for length calculations?**
  - **A: Lengths are automatically converted to meters when sent to dRofus, regardless of your Revit project units. Cable tray widths are converted to millimeters.**

## Resources

- [dRofus Documentation](https://www.drofus.com/resources/)
- [Revit MEP Cable Tray Best Practices](https://help.autodesk.com/view/RVT/)
- [Project Repository](https://github.com/DanGjer/CableTrayHandler)
- Related Extensions: ConduitHandler (similar workflow for electrical conduits)

## Support

For assistance or to report issues:
- Contact: DBGJ (dbgj@cowi.com)
- Project Repository: https://github.com/DanGjer/CableTrayHandler

## Version History

- **Version 0.0.1 - November 26, 2025**
  - Initial release
  - Core functionality for cable tray run detection and dRofus synchronization
  - Support for cable tray widths and custom property mapping
  - Dry run mode for testing configuration
  - Checkbox-based filtering for selective export

---

*This documentation was generated based on the extension's code structure. For the most up-to-date information, refer to the source code or contact the development team.*
