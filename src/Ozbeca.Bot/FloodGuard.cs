namespace Ozbeca.Bot;

/// <summary>User boshiga daqiqasiga <paramref name="perMinute"/> ta (qat'iy 60 s oyna). Oqim-xavfsiz.</summary>
public sealed class FloodGuard(int perMinute = 30)
{
    private const long WindowMs = 60_000;
    private readonly object _lock = new();
    private readonly Dictionary<long, (long Start, int Count, bool Notified)> _users = new();

    internal Func<long> NowMs { get; set; } = () => Environment.TickCount64;

    public bool Allow(long userId) => Allow(userId, out _);

    /// <summary>notify: shu oynada birinchi rad etilishi — foydalanuvchiga bir marta ogohlantirish uchun.</summary>
    public bool Allow(long userId, out bool notify)
    {
        notify = false;
        lock (_lock)
        {
            var now = NowMs();
            if (_users.Count > 5000)
                foreach (var k in _users.Where(p => now - p.Value.Start >= WindowMs).Select(p => p.Key).ToList()) _users.Remove(k);

            var (start, count, notified) = _users.TryGetValue(userId, out var s) && now - s.Start < WindowMs ? s : (now, 0, false);
            if (count < perMinute)
            {
                _users[userId] = (start, count + 1, notified);
                return true;
            }
            notify = !notified;
            _users[userId] = (start, count, true);
            return false;
        }
    }
}
