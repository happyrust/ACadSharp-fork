# How to Extend DXF Mapping

A practical guide to adding DXF mapping support for new CAD object types or extending existing ones.

1. **Create or Modify the Entity Class**

   Define a class inheriting from `Entity`, `TableEntry`, or `CadObject`. Add the required class-level attributes:

   ```csharp
   [DxfName(DxfFileToken.EntityLine)]
   [DxfSubClass(DxfSubclassMarker.Line)]
   public class Line : Entity
   {
       // Properties defined next
   }
   ```

   Reference `/llmdoc/architecture/dxf-mapping-system.md` (Section 2) for the four attribute types.

2. **Annotate Properties with DXF Code Attributes**

   For each property that maps to a DXF code, add the `[DxfCodeValue(...)]` attribute with the corresponding code(s):

   ```csharp
   [DxfCodeValue(10, 20, 30)]
   public XYZ StartPoint { get; set; }

   [DxfCodeValue(11, 21, 31)]
   public XYZ EndPoint { get; set; }

   [DxfCodeValue(40)]
   public double Thickness { get; set; }
   ```

   - Single-code properties: `[DxfCodeValue(8)]` for LayerName.
   - Multi-code properties: `[DxfCodeValue(10, 20, 30)]` for XYZ coordinates (codes 10=X, 20=Y, 30=Z).
   - Refer to `src/ACadSharp/DxfCode.cs` for valid DXF codes.

3. **Handle Inherited Properties Correctly**

   If your class inherits from `Entity` or another base class, inherited properties are already mapped in the base class's subclass. Do **not** re-annotate them. Add only the subclass-specific properties in your class.

   Example: `Circle` inherits from `Entity`. The Entity subclass ("AcDbEntity") contains properties like LayerName (code 8), Color (code 62). Circle's subclass ("AcDbCircle") adds:

   ```csharp
   [DxfCodeValue(10, 20, 30)]
   public XYZ Center { get; set; }

   [DxfCodeValue(40)]
   public double Radius { get; set; }
   ```

4. **Verify Subclass Ordering**

   When writing the file, subclasses are output in inheritance order: base classes first, derived classes last. Ensure the `[DxfSubClass]` attributes are applied to each class in the hierarchy. The mapping system automatically reverses the collection to maintain correct order (`src/ACadSharp/DxfMap.cs:98-100`).

5. **Support Collection Properties (if needed)**

   For collections of repeated codes (e.g., vertex points in a polyline), use the `[DxfCollectionCodeValueAttribute]`:

   ```csharp
   [DxfCollectionCodeValueAttribute(10, 20, 30)]
   public List<XYZ> Vertices { get; set; }
   ```

   Refer to `src/ACadSharp/IO/DXF/DxfStreamReader/*Reader.cs` for how the reader handles collection iteration during parsing.

6. **Test Your Mapping**

   - Read a DXF file containing your entity type. The mapping should automatically deserialize all properties.
   - Write the document back. The mapping should automatically serialize all properties.
   - Verify the output DXF file opens correctly in AutoCAD or other DXF readers.
   - Use unit tests in `/src/ACadSharp.Tests/` as reference (e.g., `Entities/CircleTests.cs`).

7. **Handle Special Cases**

   **Multi-Code Properties:** If a single property needs multiple codes (e.g., XYZ with codes 10, 20, 30), the reader accumulates values across codes and constructs the property atomically. The system identifies this via `DxfProperty.GetCollectionCodes()` (`src/ACadSharp/DxfProperty.cs:43-46`). The value converter handles the reconstruction (`src/ACadSharp/DxfPropertyBase.cs:57-80+`).

   **Enum Properties:** If a property is an enum, `SetValue()` automatically converts numeric codes to enum values via reflection. No special annotation needed.

   **Custom Type Conversion:** For complex types (Color, Transparency, PaperMargin), `DxfPropertyBase.SetValue()` includes type-specific conversion logic. Leverage existing converters in `CSUtilities.Converters` namespace.

   **Dimension Special Case:** `DimensionStyle` uses code 105 instead of 5 for the handle. This is handled specially in `DxfMap.Create()` (`src/ACadSharp/DxfMap.cs:91-96`).

8. **Clear the Mapping Cache if Testing Dynamically**

   If you add new types at runtime or modify attribute metadata in tests, call `DxfMap.ClearCache()` to force re-mapping:

   ```csharp
   DxfMap.ClearCache();
   var map = DxfMap.Create<MyCustomType>();
   ```

   This is rarely needed in production but essential when dynamically extending the type system.

