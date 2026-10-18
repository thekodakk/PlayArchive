using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlayArchive.Data;
using PlayArchive.Models;

namespace PlayArchive.Services
{
    public class GameDatabaseService
    {
        private readonly PlayArchiveDbContext _context;

        public GameDatabaseService(PlayArchiveDbContext context)
        {
            _context = context;
        }

        public async Task InitializeDatabaseAsync()
        {
            await _context.Database.EnsureCreatedAsync();
            await CleanupOrphanSessionsAsync();
        }

        public async Task CleanupOrphanSessionsAsync()
        {
            try
            {
                var activeSessions = await _context.Sessions
                    .Include(s => s.GameItem)
                    .Where(s => s.IsActive || s.EndTime == null)
                    .ToListAsync();

                if (activeSessions.Any())
                {
                    DateTime now = DateTime.Now;
                    foreach (var s in activeSessions)
                    {
                        s.IsActive = false;
                        s.EndTime = now;
                        double dur = (s.EndTime.Value - s.StartTime).TotalMinutes;
                        s.DurationMinutes = Math.Max(0, dur);

                        if (s.GameItem != null)
                        {
                            s.GameItem.TotalPlayTimeMinutes += s.DurationMinutes;
                            s.GameItem.LastPlayed = s.EndTime;
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDatabaseService] Error cleaning orphan sessions: {ex.Message}");
            }
        }

        public async Task<List<GameItem>> GetAllGamesAsync()
        {
            return await _context.Games
                .Include(g => g.Sessions)
                .OrderByDescending(g => g.LastPlayed ?? g.CreatedAt)
                .ToListAsync();
        }

        public async Task<GameItem?> GetGameByIdAsync(int id)
        {
            return await _context.Games
                .Include(g => g.Sessions)
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<GameItem> AddGameAsync(GameItem game)
        {
            if (!game.ExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                game.ExecutableName += ".exe";
            }

            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task UpdateGameAsync(GameItem game)
        {
            _context.Games.Update(game);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteGameAsync(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game != null)
            {
                _context.Games.Remove(game);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<GameSession?> GetActiveSessionForGameAsync(int gameItemId)
        {
            return await _context.Sessions
                .FirstOrDefaultAsync(s => s.GameItemId == gameItemId && (s.IsActive || s.EndTime == null));
        }

        public async Task<GameSession> StartSessionAsync(int gameItemId)
        {
            // Ensure no duplicate active session is created
            var existingActive = await GetActiveSessionForGameAsync(gameItemId);
            if (existingActive != null)
            {
                return existingActive;
            }

            var session = new GameSession
            {
                GameItemId = gameItemId,
                StartTime = DateTime.Now,
                IsActive = true
            };

            _context.Sessions.Add(session);

            var game = await _context.Games.FindAsync(gameItemId);
            if (game != null)
            {
                game.LastPlayed = session.StartTime;
            }

            await _context.SaveChangesAsync();
            return session;
        }

        public async Task EndSessionAsync(int sessionId, string? notes = null)
        {
            var session = await _context.Sessions
                .Include(s => s.GameItem)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session != null && (session.IsActive || session.EndTime == null))
            {
                session.EndTime = DateTime.Now;
                session.IsActive = false;
                double dur = (session.EndTime.Value - session.StartTime).TotalMinutes;
                session.DurationMinutes = Math.Max(0, dur);

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    session.Notes = notes;
                }

                if (session.GameItem != null)
                {
                    session.GameItem.TotalPlayTimeMinutes += session.DurationMinutes;
                    session.GameItem.LastPlayed = session.EndTime;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<GameSession>> GetRecentSessionsAsync(int count = 20)
        {
            return await _context.Sessions
                .Include(s => s.GameItem)
                .OrderByDescending(s => s.StartTime)
                .Take(count)
                .ToListAsync();
        }

        public async Task<double> GetTotalPlaytimeMinutesAsync()
        {
            return await _context.Games.SumAsync(g => g.TotalPlayTimeMinutes);
        }
    }
}
