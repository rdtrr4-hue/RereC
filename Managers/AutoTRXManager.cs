using System;
using System.Collections.Generic;
using System.Linq;
using Rere.Models;

namespace Rere.Managers
{
    // ─── Auto TRX Manager ────────────────────────────────────────────────────────
    // Matches Swift's AutoTRXManager behavior: automatically queues TRX
    // for sessions that match configured auto-targets.
    public class AutoTRXManager
    {
        public static readonly AutoTRXManager Shared = new AutoTRXManager();

        private readonly List<AutoTRXRule> _rules = new();
        private readonly HashSet<string> _processedIPs = new();

        public Action<string, ToastType>? OnToast;

        private AutoTRXManager()
        {
            // Hook into SessionManager updates
            SessionManager.Shared.OnUpdate += OnSessionsUpdated;
        }

        // ─── Rule Management ─────────────────────────────────────────────────────

        public void AddRule(AutoTRXRule rule)
        {
            _rules.Add(rule);
        }

        public void RemoveRule(string id)
        {
            _rules.RemoveAll(r => r.Id == id);
        }

        public List<AutoTRXRule> GetRules() => _rules.ToList();

        // ─── Session Update Handler ──────────────────────────────────────────────

        private void OnSessionsUpdated(
            List<WireGuardSession> sessions,
            List<ActivityLog> logs,
            string potentialHost)
        {
            foreach (var session in sessions)
            {
                if (_processedIPs.Contains(session.Ip)) continue;

                foreach (var rule in _rules.Where(r => r.IsEnabled))
                {
                    if (MatchesRule(session, rule))
                    {
                        _processedIPs.Add(session.Ip);
                        TRXManager.Shared.AddToQueue(
                            session.Ip, session.Port,
                            session.PlayerName?.Name ?? session.Ip,
                            method: rule.Method,
                            customDuration: rule.Duration,
                            mode: rule.Mode);

                        OnToast?.Invoke(
                            $"🤖 Auto-TRX: {session.Ip} ({rule.Name})",
                            ToastType.Info);
                        break;
                    }
                }
            }

            // Clean up processed IPs that are no longer in sessions
            var activeIPs = sessions.Select(s => s.Ip).ToHashSet();
            _processedIPs.IntersectWith(activeIPs);
        }

        private bool MatchesRule(WireGuardSession session, AutoTRXRule rule)
        {
            if (!string.IsNullOrEmpty(rule.TargetIP) && session.Ip != rule.TargetIP)
                return false;
            if (!string.IsNullOrEmpty(rule.TargetCountry) &&
                !session.Country.Equals(rule.TargetCountry, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(rule.TargetCity) &&
                !session.City.Contains(rule.TargetCity, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
    }

    // ─── Auto TRX Rule ───────────────────────────────────────────────────────────
    public class AutoTRXRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public string? TargetIP { get; set; }
        public string? TargetCountry { get; set; }
        public string? TargetCity { get; set; }
        public string? Method { get; set; }
        public int? Duration { get; set; }
        public TRXMode Mode { get; set; } = TRXMode.Normal;
    }
}
