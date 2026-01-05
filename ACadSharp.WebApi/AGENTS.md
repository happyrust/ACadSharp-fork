# ACadSharp Web API & Converter Knowledge Base

## OVERVIEW
Web API services and high-performance conversion logic for CAD document processing in browser environments.

## STRUCTURE
- **ACadSharp.WebApi/**: ASP.NET Core web host and API controllers.
  - `Controllers/`: RESTful endpoints for CAD operations (`CadController`).
- **ACadSharp.WebConverter/**: Core conversion logic and specialized models.
  - `Models/`: JSON-friendly representations of CAD documents and entities.
  - `CadWebConverter.cs`: Service for cross-format conversion and stream handling.
  - `CadJsonSerializer.cs`: Specialized logic for CAD-to-JSON serialization.

## WHERE TO LOOK
| Component | Path | Responsibility |
|-----------|------|----------------|
| API Entry | `ACadSharp.WebApi/Controllers/CadController.cs` | Request validation, logging, and response formatting. |
| Conversion | `ACadSharp.WebConverter/CadWebConverter.cs` | Core orchestration of readers, writers, and options. |
| JSON Models | `ACadSharp.WebConverter/Models/CadJsonModels.cs` | Schema definitions for Web-based CAD visualization. |
| Configuration | `ACadSharp.WebApi/Program.cs` | Middleware setup (CORS, file limits, routing). |

## CONVENTIONS
- **RESTful Patterns**: Use standard HTTP verbs (`POST` for processing, `GET` for status/metadata).
- **Asynchronous Execution**: All I/O and conversion tasks MUST use `async/await` to avoid thread blocking.
- **Error Handling**: Return `ProblemDetails` for 4xx/5xx responses with descriptive error messages.
- **Conversion Options**: Use `ConversionOptions` class to encapsulate output format, version, and binary settings.
- **Validation**: Centralize file validation (extension, size, null checks) before processing.
- **MIME Mapping**: Explicitly handle MIME types for `.dwg` (`application/dwg`) and `.dxf` (`application/dxf`).

## ANTI-PATTERNS
- **Controller Bloat**: Avoid implementing conversion logic directly in `CadController`.
- **Sync Over Async**: Using `.Result` or `.Wait()` on asynchronous conversion tasks.
- **Direct Entity Exposure**: Returning internal `CadObject` types directly in API responses (use `Models`).
- **Missing Cleanup**: Failing to dispose of `Stream` or `MemoryStream` objects after conversion.
- **Unbounded Uploads**: Omitting `RequestSizeLimit` or `MultipartBodyLengthLimit` for file uploads.
