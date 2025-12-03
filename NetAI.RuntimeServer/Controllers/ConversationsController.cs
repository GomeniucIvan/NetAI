using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetAI.RuntimeServer.Models;
using NetAI.RuntimeServer.Services;

namespace NetAI.RuntimeServer.Controllers
{
    [ApiController]
    [Route("api/conversations")]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationRuntime _runtime;

        public ConversationsController(IConversationRuntime runtime)
        {
            _runtime = runtime;
        }

        [HttpPost]
        public async Task<IActionResult> Initialize([FromBody] CreateConversationRequestDto request)
        {
            Console.WriteLine($"[RuntimeServer] POST /api/conversations - creating conversation for repo={request?.Name ?? "<none>"}");
            var result = await _runtime.InitializeAsync(request);
            Console.WriteLine($"[RuntimeServer] Conversation initialized with id={result.ConversationId}");
            return Ok(new
            {
                status = result.Status,
                conversation_id = result.ConversationId,
                conversation_status = result.ConversationStatus,
                runtime_status = result.RuntimeStatus,
                message = result.Message,
                session_api_key = result.SessionApiKey,
                runtime_id = result.RuntimeId,
                session_id = result.SessionId
            });
        }

        [HttpPost("{id}/start")]
        public async Task<IActionResult> Start(string id)
        {
            Console.WriteLine("ConversationController Start:{0}", id);
            Console.WriteLine($"[RuntimeServer] Enter Start for conversation {id}");

            var result = await _runtime.StartAsync(id);

            if (result is null)
            {
                return NotFound();
            }

            //TODO move to EXTENSION
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true // makes it pretty-printed
            });

            Console.WriteLine($"[RuntimeServer] POST /api/conversations/{id}/start\n{json}");
            Console.WriteLine($"[RuntimeServer] Exit Start for conversation {id} with status {result.Status}");

            return Ok(new
            {
                status = result.Status,
                conversation_id = id,
                conversation_status = result.ConversationStatus,
                runtime_status = result.RuntimeStatus,
                message = result.Message,
                session_api_key = result.SessionApiKey,
                runtime_id = result.RuntimeId,
                session_id = result.SessionId
            });
        }

        [HttpPost("{id}/stop")]
        public async Task<IActionResult> Stop(string id)
        {
            Console.WriteLine($"[RuntimeServer] Enter Stop for conversation {id}");
            var result = await _runtime.StopAsync(id);
            Console.WriteLine($"[RuntimeServer] Exit Stop for conversation {id} -> {(result is null ? "not found" : result.RuntimeStatus)}");
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("{id}/message")]
        public async Task<IActionResult> Message(string id, [FromBody] AppendMessageRequestDto request)
        {
            Console.WriteLine("ConversationController Message:{0},{1}", request?.Message, request?.Source);
            Console.WriteLine($"[RuntimeServer] Enter Message for conversation {id}; length={request?.Message?.Length ?? 0}; source={request?.Source ?? "<none>"}");
            if (request is null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required." });
            }

            var result = await _runtime.AppendMessageAsync(id, request.Message, request.Source);
            Console.WriteLine($"[RuntimeServer] Exit Message for conversation {id} -> {(result is null ? "not found" : result.Type)}");
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetConversation(string id)
        {
            Console.WriteLine("ConversationController GetConversation:{0}", id);

            var result = await _runtime.GetConversationAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/config")]
        public async Task<IActionResult> GetConfiguration(string id)
        {
            Console.WriteLine("ConversationController GetConfiguration:{0}", id);

            var result = await _runtime.GetConfigurationAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/vscode-url")]
        public async Task<IActionResult> GetVscodeUrl(string id)
        {
            Console.WriteLine("ConversationController GetVscodeUrl:{0}", id);

            var result = await _runtime.GetVscodeUrlAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/web-hosts")]
        public async Task<IActionResult> GetWebHosts(string id)
        {
            Console.WriteLine("ConversationController GetWebHosts:{0}", id);

            var result = await _runtime.GetWebHostsAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/microagents")]
        public async Task<IActionResult> GetMicroagents(string id)
        {
            Console.WriteLine("ConversationController GetMicroagents:{0}", id);

            var result = await _runtime.GetMicroagentsAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/events")]
        public async Task<IActionResult> GetEvents(
            string id,
            [FromQuery(Name = "start_id")] int startId = 0,
            [FromQuery(Name = "end_id")] int? endId = null,
            [FromQuery] bool reverse = false,
            [FromQuery] int? limit = null)
        {
            Console.WriteLine("ConversationController GetEvents:{0},{1},{2},{3}", startId, endId, reverse, limit);

            var result = await _runtime.GetEventsAsync(id, startId, endId, reverse, limit);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("{id}/events")]
        public async Task<IActionResult> AppendEvent(string id, [FromBody] JsonElement payload)
        {
            Console.WriteLine("ConversationController AppendEvent:{0}", id);

            var result = await _runtime.AppendEventAsync(id, payload);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/list-files")]
        public async Task<IActionResult> ListFiles(
            string id,
            [FromQuery] string? path,
            CancellationToken cancellationToken)
        {
            var result = await _runtime.ListFilesAsync(id, path, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/select-file")]
        public async Task<IActionResult> SelectFile(
            string id,
            [FromQuery(Name = "file")] string? file,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return BadRequest(new { error = "File path is required." });
            }

            var result = await _runtime.SelectFileAsync(id, file, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            if (result.IsBinary)
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
                {
                    error = result.Error ?? "Binary files cannot be previewed."
                });
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { code = result.Code ?? string.Empty });
        }

        [HttpPost("{id}/actions/file-edit")]
        public async Task<IActionResult> EditFile(
            string id,
            [FromBody] RuntimeFileEditRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _runtime.EditFileAsync(id, request, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorCode) || !string.IsNullOrWhiteSpace(result.Error))
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id}/upload-files")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadFiles(string id, CancellationToken cancellationToken)
        {
            if (!Request.HasFormContentType)
            {
                return BadRequest(new { error = "Multipart form data is required." });
            }

            IFormCollection form = await Request.ReadFormAsync(cancellationToken);
            if (form.Files.Count == 0)
            {
                return BadRequest(new { error = "At least one file must be provided." });
            }

            var uploads = new List<RuntimeUploadedFile>(form.Files.Count);
            try
            {
                foreach (IFormFile file in form.Files)
                {
                    Stream stream = file.OpenReadStream();
                    uploads.Add(new RuntimeUploadedFile(file.FileName, stream, file.ContentType));
                }

                var result = await _runtime.UploadFilesAsync(id, uploads, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            finally
            {
                foreach (RuntimeUploadedFile upload in uploads)
                {
                    upload.Content.Dispose();
                }
            }
        }

        [HttpGet("{id}/zip-directory")]
        public async Task<IActionResult> ZipDirectory(string id, CancellationToken cancellationToken)
        {
            var result = await _runtime.ZipWorkspaceAsync(id, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            result.Content.Position = 0;
            return File(result.Content, result.ContentType, result.FileName);
        }

        [HttpGet("{id}/git/changes")]
        public async Task<IActionResult> GetGitChanges(string id, CancellationToken cancellationToken)
        {
            var result = await _runtime.GetGitChangesAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id}/git/diff")]
        public async Task<IActionResult> GetGitDiff(
            string id,
            [FromQuery(Name = "path")] string? path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { error = "Path is required." });
            }

            var result = await _runtime.GetGitDiffAsync(id, path, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
