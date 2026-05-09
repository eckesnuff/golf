using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        private readonly ClaudeService _claude;
        private readonly IMemoryCache _cache;

        public QueryController(ClaudeService claude, IMemoryCache cache)
        {
            _claude = claude;
            _cache = cache;
        }

        [HttpPost]
        public async Task<ActionResult<Result>> Post(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SessionToken) ||
                !_cache.TryGetValue(request.SessionToken, out GolferSession session))
                return Unauthorized(Result.Error("Session expired or invalid"));

            session.History.Add(new ClaudeMessage { Role = "user", Content = request.Message });

            try
            {
                var reply = await _claude.AskAsync(session.SystemContext, session.History);
                session.History.Add(new ClaudeMessage { Role = "assistant", Content = reply.Text });
                _cache.Set(request.SessionToken, session, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });
                return Result.OK().WithData(reply);
            }
            catch (Exception ex)
            {
                session.History.RemoveAt(session.History.Count - 1);
                return StatusCode(500, Result.Error(ex.Message));
            }
        }
    }
}
