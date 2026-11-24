using System.Collections.Generic;
using UnityEngine;

// Small collision grid for the narrow maze. No baked NavMesh is required.
public sealed class ElfMazeNavigation
{
    private const float Cell = .3f;
    private readonly Transform elf, player;
    private readonly Collider[] overlaps = new Collider[64];
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Dictionary<Vector2Int, bool> free = new Dictionary<Vector2Int, bool>();
    private readonly List<Vector3> route = new List<Vector3>();
    private float nextPlan;
    private int routeIndex;
    public ElfMazeNavigation(Transform elf, Transform player) { this.elf = elf; this.player = player; }
    public void ResetRoute() { route.Clear(); routeIndex = 0; nextPlan = 0; }
    public Vector3 AwayDirection(Vector3 threat)
    {
        Vector3 away = Vector3.ProjectOnPlane(elf.position - threat, Vector3.up).normalized;
        if (away.sqrMagnitude < .001f) away = -elf.forward;
        Vector3 best = Vector3.zero;
        float bestScore = -1;
        // Prefer straight away; use an open side of the corridor when a wall
        // blocks it, without selecting a step back toward the player.
        for (int i = 0; i < 13; i++)
        {
            float angle = i == 0 ? 0 : ((i + 1) / 2) * 15f * (i % 2 == 0 ? -1 : 1);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            float score = Vector3.Dot(direction, away);
            if (score <= bestScore || !Clear(elf.position, elf.position + direction * .25f)) continue;
            best = direction; bestScore = score;
        }
        return best;
    }
    private bool Ignore(Collider collider)
    {
        if (collider.transform.IsChildOf(elf) || collider.transform.IsChildOf(player)) return true;
        string name = collider.name.ToLowerInvariant();
        return name.Contains("blocker") || name.Contains("mail box must");
    }
    public bool Clear(Vector3 a, Vector3 b, float radius = .12f)
    {
        a.y = b.y = elf.position.y + .35f;
        Vector3 offset = b - a;
        if (offset.sqrMagnitude < .00001f) return true;
        int count = Physics.SphereCastNonAlloc(a, radius, offset.normalized, hits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) if (!Ignore(hits[i].collider) && hits[i].normal.y < .7f) return false;
        return true;
    }
    private Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.z / Cell));
    private Vector3 Position(Vector2Int key) => new Vector3(key.x * Cell, elf.position.y, key.y * Cell);
    private bool Free(Vector2Int key)
    {
        if (free.TryGetValue(key, out bool value)) return value;
        int count = Physics.OverlapSphereNonAlloc(Position(key) + Vector3.up * .35f, .13f, overlaps, ~0, QueryTriggerInteraction.Ignore);
        value = count < overlaps.Length;
        for (int i = 0; i < count && value; i++) if (!Ignore(overlaps[i])) value = false;
        free[key] = value;
        return value;
    }
    public Vector3 Direction(Vector3 target)
    {
        if (Clear(elf.position, target)) return target - elf.position;
        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + .7f;
            Plan(target);
        }
        while (routeIndex < route.Count && Vector3.ProjectOnPlane(route[routeIndex] - elf.position, Vector3.up).magnitude < .12f) routeIndex++;
        if (routeIndex >= route.Count) return Vector3.zero;
        while (routeIndex + 1 < route.Count && Clear(elf.position, route[routeIndex + 1])) routeIndex++;
        return route[routeIndex] - elf.position;
    }
    private void Plan(Vector3 target)
    {
        route.Clear(); routeIndex = 0; free.Clear();
        var start = Key(elf.position); var goal = Key(target);
        var open = new List<Vector2Int> { start };
        var costs = new Dictionary<Vector2Int, int> { [start] = 0 };
        var parents = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        var closest = start;
        int closestDistance = (start - goal).sqrMagnitude;
        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        for (int visited = 0; open.Count > 0 && visited < 5000; visited++)
        {
            int best = 0, score = int.MaxValue;
            for (int i = 0; i < open.Count; i++) { var d = open[i] - goal; int f = costs[open[i]] + Mathf.Abs(d.x) + Mathf.Abs(d.y); if (f < score) { best = i; score = f; } }
            var current = open[best]; open.RemoveAt(best);
            if ((current - goal).sqrMagnitude < closestDistance) { closest = current; closestDistance = (current - goal).sqrMagnitude; }
            if (current == goal || (current - goal).sqrMagnitude <= 1 && Clear(Position(current), target))
            {
                for (var node = current; node != start; node = parents[node]) route.Add(Position(node));
                route.Reverse(); return;
            }
            closed.Add(current);
            foreach (var d in directions)
            {
                var next = current + d;
                if (closed.Contains(next) || Mathf.Abs(next.x - start.x) > 45 || Mathf.Abs(next.y - start.y) > 45 || !Free(next)) continue;
                if (!Clear(Position(current), Position(next))) continue;
                int cost = costs[current] + 1;
                if (costs.TryGetValue(next, out int old) && old <= cost) continue;
                costs[next] = cost; parents[next] = current;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        // A retreat target can lie beyond a wall. Use the reachable part of
        // the route rather than keeping the elf stationary until it fades.
        for (var node = closest; node != start; node = parents[node]) route.Add(Position(node));
        route.Reverse();
    }
}
