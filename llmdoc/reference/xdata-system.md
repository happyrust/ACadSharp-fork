# Reference: XData Extension Data System

## 1. Core Summary

XData (Extended Data) is a flexible key-value system for attaching custom metadata to any CAD object. Each CadObject automatically owns an ExtendedDataDictionary organized by AppId (application identifier). XData consists of typed ExtendedDataRecord instances, supporting 15 fundamental types (strings, integers, reals, coordinates, handles, binary data, etc.). All CadObjects—entities, table entries, and non-graphical objects—can host XData, enabling application-specific data storage without modifying core CAD structures.

## 2. Source of Truth

- **Primary Code:** `src/ACadSharp/XData/ExtendedDataDictionary.cs` - Dictionary container indexed by AppId, stores ExtendedData instances.
- **Record Types:** `src/ACadSharp/XData/` directory contains 21 files implementing all ExtendedDataRecord types (ExtendedDataString, ExtendedDataInteger16, ExtendedDataReal, ExtendedDataCoordinate, ExtendedDataWorldCoordinate, ExtendedDataDisplacement, ExtendedDataDirection, ExtendedDataDistance, ExtendedDataScale, ExtendedDataHandle, ExtendedDataBinaryChunk, ExtendedDataLayer, ExtendedDataReference, ExtendedDataControlString).
- **Data Container:** `src/ACadSharp/XData/ExtendedData.cs` - Wraps List<ExtendedDataRecord>, manages control strings.
- **Record Base:** `src/ACadSharp/XData/ExtendedDataRecord.cs` (abstract) - Base class with Code (DxfCode) and RawValue properties; generic variant ExtendedDataRecord<T> provides typed Value property.
- **AppId Definition:** `src/ACadSharp/Tables/AppId.cs` - Table entry for registering application IDs (must exist in CadDocument.AppIds).
- **CadObject Integration:** `src/ACadSharp/CadObject.cs` - All CadObjects have ExtendedData property (ExtendedDataDictionary).
- **DXF Mapping:** DXF codes 1000-1071 reserved for XData serialization, mapped via ExtendedDataRecord.Code property.

## 3. Structural Layers

### Three-tier Hierarchy
```
CadObject
└── ExtendedDataDictionary (stores multiple AppIds)
    └── AppId → ExtendedData (List<ExtendedDataRecord>)
        └── ExtendedDataRecord[] (typed values)
```

### AppId Registration
- Must be added to document.AppIds table before attaching XData with that ID
- Built-in AppIds: "ACAD" (default), "AcDbBlockRepBTag" (dynamic blocks), "AcDbBlockRepETag"
- Custom AppIds created: `document.AppIds.Add(new AppId("MyCustomApp"))`

### ExtendedDataRecord Types (15 total)
1. **ExtendedDataString** - Text data (DxfCode 1)
2. **ExtendedDataInteger16** - 16-bit signed integer (DxfCode 70-78)
3. **ExtendedDataInteger32** - 32-bit signed integer (DxfCode 90-99)
4. **ExtendedDataReal** - Double precision float (DxfCode 40-59)
5. **ExtendedDataCoordinate** - 3D point in object coordinate system (DxfCode 10-39)
6. **ExtendedDataWorldCoordinate** - 3D point in world coordinate system
7. **ExtendedDataDisplacement** - 3D vector displacement
8. **ExtendedDataDirection** - 3D unit direction vector
9. **ExtendedDataDistance** - Distance scalar value
10. **ExtendedDataScale** - Scale factor
11. **ExtendedDataHandle** - Object reference by handle (resolvable to CadObject)
12. **ExtendedDataBinaryChunk** - Binary data array
13. **ExtendedDataLayer** - Layer reference
14. **ExtendedDataReference** - Generic reference
15. **ExtendedDataControlString** - Open/Close markers (ensures valid nesting)

## 4. Common Usage Patterns

**Adding XData to entity:**
```csharp
var appId = document.AppIds["ACAD"];
var xdata = new ExtendedData();
xdata.AddControlStrings();  // Ensures Open/Close
xdata.Records.Add(new ExtendedDataString("MyValue"));
entity.ExtendedData.Add(appId, xdata);
```

**Querying XData:**
```csharp
if (entity.ExtendedData.TryGet("ACAD", out var data))
{
    foreach (var record in data.Records)
    {
        if (record is ExtendedDataString str)
        {
            Console.WriteLine(str.Value);
        }
    }
}
```

**Special Use: Dynamic Blocks:**
- AppId "AcDbBlockRepBTag" stores source block reference in BlockRecord.Source
- Accessed via `entity.ExtendedData.Get("AcDbBlockRepBTag")`
- ExtendedDataHandle in records points to source block

## 5. Technical Details

- **Control Strings:** Each ExtendedData automatically wrapped by ExtendedDataControlString("(") and ExtendedDataControlString(")") via AddControlStrings()
- **Serialization:** DXF codes 1000-1071 map to specific record types during write
- **Document Association:** AppId must be in CadDocument.AppIds for XData attachment to be valid
- **Null Safety:** ExtendedDataDictionary handles missing AppIds gracefully; TryGet returns false if not present
- **Immutability:** ExtendedDataRecord instances are typically immutable; modify by replacing records in ExtendedData.Records list

## 6. Related Architecture

- `/llmdoc/architecture/tables-system.md` - AppId table management
- `/llmdoc/architecture/blocks-system.md` - Dynamic block XData storage via BlockRepBTag AppId
- `/llmdoc/reference/coding-conventions.md` - CAD object conventions

## 7. External References

- DXF Reference (Autodesk): Extended Data (Xdata) uses group codes 1000-1071
- CSAdSharp Source: All ExtendedData* classes implement DxfCode property for serialization
