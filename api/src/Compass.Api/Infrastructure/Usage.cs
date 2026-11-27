using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;

namespace Compass.Api.Infrastructure;

public sealed record UsageEvent(string Event, Guid? SessionId, string PropsJson, DateTimeOffset At);

/// <summary>
/// Records anonymous product events without slowing the request: events go into a bounded in-memory queue
/// and a background writer batches them into PostgreSQL. If the queue is full or the write fails, events are dropped:
/// analytics must never break a student's request.
/// </summary>
public sealed class UsageTracker(TimeProvider time)
{
    public const string SessionHeader = "X-Session-Id";
    /// <summary>Load tests and monitoring checks send "X-Synthetic: 1" so their events are excluded from real metrics.</summary>
    public const string SyntheticHeader = "X-Synthetic";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Channel<UsageEvent> _queue = Channel.CreateBounded<UsageEvent>(
        new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<UsageEvent> Reader => _queue.Reader;

    public void Track(string evt, HttpContext http, object? props = null)
    {
        var json = JsonSerializer.SerializeToNode(props ?? new { }, Json) as JsonObject ?? new JsonObject();
        if (IsSynthetic(http)) json["synthetic"] = true;
        _queue.Writer.TryWrite(new UsageEvent(evt, SessionOf(http), json.ToJsonString(), time.GetUtcNow()));
    }

    public static bool IsSynthetic(HttpContext http) => http.Request.Headers[SyntheticHeader].ToString() == "1";

    public static Guid? SessionOf(HttpContext http) =>
        Guid.TryParse(http.Request.Headers[SessionHeader].ToString(), out var id) ? id : null;
}

public sealed class UsageWriter(UsageTracker tracker, NpgsqlDataSource db, ILogger<UsageWriter> log) : BackgroundService
{
    private const int MaxBatch = 200;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var batch = new List<UsageEvent>(MaxBatch);
        while (await tracker.Reader.WaitToReadAsync(stop))
        {
            while (batch.Count < MaxBatch && tracker.Reader.TryRead(out var e)) batch.Add(e);
            try
            {
                await using var conn = await db.OpenConnectionAsync(stop);
                await using var cmd = new NpgsqlBatch(conn);
                foreach (var e in batch)
                {
                    cmd.BatchCommands.Add(new NpgsqlBatchCommand(
                        "INSERT INTO usage_events (occurred_at, event, session_id, props) VALUES ($1, $2, $3, $4)")
                    {
                        Parameters =
                        {
                            new NpgsqlParameter { Value = e.At },
                            new NpgsqlParameter { Value = e.Event },
                            new NpgsqlParameter { Value = (object?)e.SessionId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                            new NpgsqlParameter { Value = e.PropsJson, NpgsqlDbType = NpgsqlDbType.Jsonb },
                        }
                    });
                }
                await cmd.ExecuteNonQueryAsync(stop);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                log.LogWarning(ex, "Dropped {Count} usage events.", batch.Count);
            }
            batch.Clear();
        }
    }
}
