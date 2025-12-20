# Architecture of Blocks System

## 1. Identity

- **What it is:** A three-entity model for block definitions: `BlockRecord` (metadata table entry), `Block` (definition marker entity), and `BlockEnd` (termination entity).
- **Purpose:** Manages block geometry, insertion references, attribute definitions, and supports special block types (anonymous, XRef, dynamic).

## 2. Core Components

- `src/ACadSharp/Tables/BlockRecord.cs` (BlockRecord): Table entry inheriting from TableEntry, represents block metadata and entity container.
- `src/ACadSharp/Blocks/Block.cs` (Block): Entity inheriting from Entity, marks the start of a block definition with base point and flags.
- `src/ACadSharp/Blocks/BlockEnd.cs` (BlockEnd): Entity inheriting from Entity, marks the end of block definition section.
- `src/ACadSharp/Blocks/BlockTypeFlags.cs` (BlockTypeFlags): Enum defining block types (Anonymous, NonConstantAttributeDefinitions, XRef, XRefOverlay, XRefDependent, XRefResolved, Referenced).
- `src/ACadSharp/Entities/Insert.cs` (Insert): Entity inheriting from Entity, references a BlockRecord and allows placement with transformation, scaling, rotation, array configuration.
- `src/ACadSharp/Entities/AttributeDefinition.cs` (AttributeDefinition): Entity for defining attribute fields within a block.
- `src/ACadSharp/Entities/AttributeEntity.cs` (AttributeEntity): Entity for attribute values in Insert references.
- `src/ACadSharp/Tables/Collections/BlockRecordsTable.cs` (BlockRecordsTable): Concrete table implementation for managing all block records in a document.

## 3. Execution Flow (LLM Retrieval Map)

### Block-BlockRecord-BlockEnd Three-way Relationship

1. **BlockRecord Creation:** `new BlockRecord(name)` creates table entry - `src/ACadSharp/Tables/BlockRecord.cs:26-27`.
2. **Block Entity Assignment:** When BlockRecord is added to document, automatic Block entity created - `src/ACadSharp/Tables/BlockRecord.cs:95-100` (BlockEntity property).
3. **BlockEnd Entity Assignment:** When BlockRecord is added to document, automatic BlockEnd entity created - `src/ACadSharp/Tables/BlockRecord.cs:82-90` (BlockEnd property).
4. **Bidirectional Links:**
   - `BlockRecord.BlockEntity` → Block instance
   - `BlockRecord.BlockEnd` → BlockEnd instance
   - `Block.BlockOwner` → BlockRecord instance - `src/ACadSharp/Blocks/Block.cs:28`.
   - `BlockEnd.Owner` → BlockRecord instance (set in BlockEnd property setter).
5. **Document Registration:** BlockRecord added to `document.BlockRecords` table, which registers the Block and BlockEnd entities - `src/ACadSharp/Tables/Collections/BlockRecordsTable.cs`.

### Adding Entities to Blocks

1. **Entity Collection:** `BlockRecord.Entities` is a `CadObjectCollection<Entity>` - `src/ACadSharp/Tables/BlockRecord.cs`.
2. **Add Operation:** `blockRecord.Entities.Add(entity)` - entity's Owner automatically set to BlockRecord.
3. **Entity Ownership:** Once added to block, entity cannot be moved to different block (enforces single ownership).
4. **Attribute Definitions:** If entity is `AttributeDefinition`, it's accessible via `BlockRecord.AttributeDefinitions` property - `src/ACadSharp/Tables/BlockRecord.cs:71-77`.

### Insert Block Reference Creation

1. **Insert Creation:** `new Insert(blockRecord)` creates reference to block definition.
2. **Constructor Cloning:** If blockRecord belongs to document, automatic clone occurs to ensure proper ownership - `src/ACadSharp/Entities/Insert.cs:constructor`.
3. **Attribute Initialization:** Insert.Attributes initialized from BlockRecord.AttributeDefinitions via `UpdateAttributes()` - `src/ACadSharp/Entities/Insert.cs` (UpdateAttributes method).
4. **Transformation:** Insert.InsertPoint, Rotation, XScale, YScale, ZScale define block instance placement - `src/ACadSharp/Entities/Insert.cs:59-63`.
5. **Array Configuration:** RowCount, ColumnCount, RowSpacing, ColumnSpacing enable array insertions - `src/ACadSharp/Entities/Insert.cs:39-47`.
6. **Block Reference Link:** `Insert.Block` property references the BlockRecord - `src/ACadSharp/Entities/Insert.cs:34-35`.

### Special Block Types

1. **Model Space:** `BlockRecord.ModelSpace` static property - `src/ACadSharp/Tables/BlockRecord.cs:34-46`, always named "*Model_Space".
2. **Paper Space:** `BlockRecord.PaperSpace` static property - `src/ACadSharp/Tables/BlockRecord.cs:54-66`, always named "*Paper_Space".
3. **Anonymous Blocks:** Prefix "*A" set by BlockRecordsTable when adding unnamed blocks.
4. **XRef Blocks:** Flags field includes XRef, XRefOverlay flags; XRefPath stored in Block entity - `src/ACadSharp/Blocks/Block.cs`.
5. **Dynamic Blocks:** EvaluationGraph property (via XDictionary) stores dynamic block metadata; Source tracked via XData BlockRepBTag - `src/ACadSharp/Tables/BlockRecord.cs`.

### Attribute Management in Blocks

1. **Attribute Definition:** AttributeDefinition entities added to block definition define attribute fields - `src/ACadSharp/Entities/AttributeDefinition.cs`.
2. **Insert Attribute Creation:** When Insert is created/updated, AttributeEntity instances created for each AttributeDefinition via `UpdateAttributes()` - `src/ACadSharp/Entities/Insert.cs`.
3. **Attribute Collection:** `Insert.Attributes` is a `SeqendCollection<AttributeEntity>` terminated by Seqend entity - `src/ACadSharp/Entities/Insert.cs:29`.

### Block Sorting

1. **Sorted Entities:** `BlockRecord.GetSortedEntities()` returns ordered entity list if SortEntitiesTable exists - `src/ACadSharp/Tables/BlockRecord.cs` (method).
2. **Sort Table Storage:** Stored via XDictionary "AcDbSortentsTable".
3. **Custom Order:** Allows custom entity ordering within block without changing logical Entities collection.

## 4. Design Rationale

**Three-entity Model:** Separates concerns: BlockRecord (metadata), Block (geometry marker), BlockEnd (termination). Matches DXF file structure where BLOCK/ENDBLK pair appears in BLOCKS section.

**Bidirectional Linking:** Ensures consistency—BlockRecord.BlockEntity can find the Block definition, Block.BlockOwner can find its metadata entry, preventing orphaned entities.

**Single Ownership Enforcement:** Each Entity can only belong to one BlockRecord or document. This prevents accidental entity duplication and ensures clear data structure.

**Insert as Reference:** Insert entities are lightweight references to BlockRecord, enabling efficient reuse of block geometry. Transformation properties (position, rotation, scale) applied during rendering/export.

**Attribute Propagation:** AttributeDefinitions in BlockRecord drive automatic creation of AttributeEntity instances in Insert, keeping attribute lists synchronized.

**Special Blocks Protection:** ModelSpace and PaperSpace cannot be deleted and are created by default, ensuring document always has these fundamental containers.

**Dynamic Block Support:** EvaluationGraph and Source properties support dynamic block features without changing core block structure, leveraging XDictionary and XData systems.
