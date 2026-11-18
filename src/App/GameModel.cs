using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Sokol.Engine;

namespace Sokol.App
{
    enum GameResult { None, WhiteWins, BlackWins, Draw }

    /// <summary>The game record: moves, positions (by hash), engine analyses and move reviews.</summary>
    sealed class Game
    {
        public readonly string StartFen = Board.StartFen;
        public readonly Board Current = new Board();
        public readonly List<int> Moves = new List<int>();
        /// <summary>Hashes[i] = position hash at ply i (0..Moves.Count).</summary>
        public readonly List<ulong> Hashes = new List<ulong>();
        public readonly List<string> SanIntl = new List<string>();
        public readonly List<PositionAnalysis> Analyses = new List<PositionAnalysis>();
        public readonly List<MoveReview> Reviews = new List<MoveReview>();
        public GameResult Result;
        public string ResultReason = "";
        public DateTime Started = DateTime.Now;

        public Game()
        {
            Current.SetFen(StartFen);
            Hashes.Add(Current.Hash);
            Analyses.Add(null);
        }

        public int Ply => Moves.Count;

        public void Push(int move)
        {
            SanIntl.Add(Notation.ToSan(Current, move, NotationStyle.International));
            Current.MakeMove(move);
            Moves.Add(move);
            Hashes.Add(Current.Hash);
            Analyses.Add(null);
            Reviews.Add(null);
        }

        public void Pop()
        {
            if (Moves.Count == 0) return;
            Current.UnmakeMove();
            int last = Moves.Count - 1;
            Moves.RemoveAt(last);
            SanIntl.RemoveAt(last);
            Reviews.RemoveAt(last);
            Hashes.RemoveAt(last + 1);
            Analyses.RemoveAt(last + 1);
            Result = GameResult.None;
            ResultReason = "";
        }

        public Board BoardAt(int ply)
        {
            var b = new Board(StartFen);
            for (int i = 0; i < ply && i < Moves.Count; i++) b.MakeMove(Moves[i]);
            return b;
        }

        /// <summary>Checks the current position for the end of the game.</summary>
        public GameResult DetectEnd(out string reason)
        {
            reason = "";
            var b = Current;
            if (!b.HasLegalMove())
            {
                if (b.InCheck())
                {
                    reason = "мат";
                    return b.SideToMove == Piece.White ? GameResult.BlackWins : GameResult.WhiteWins;
                }
                reason = "пат";
                return GameResult.Draw;
            }
            if (b.IsInsufficientMaterial()) { reason = "недостаточно материала"; return GameResult.Draw; }
            if (b.RepetitionCount() >= 2) { reason = "троекратное повторение"; return GameResult.Draw; }
            if (b.HalfmoveClock >= 100) { reason = "правило 50 ходов"; return GameResult.Draw; }
            return GameResult.None;
        }

        public string OpeningName(int uptoPly)
        {
            var b = new Board(StartFen);
            string name = OpeningBook.NameOf(b);
            for (int i = 0; i < uptoPly && i < Moves.Count; i++)
            {
                b.MakeMove(Moves[i]);
                string n = OpeningBook.NameOf(b);
                if (n != null) name = n;
            }
            return name;
        }

        public string ToPgn(string white, string black)
        {
            string res = Result == GameResult.WhiteWins ? "1-0" : Result == GameResult.BlackWins ? "0-1" : Result == GameResult.Draw ? "1/2-1/2" : "*";
            var sb = new StringBuilder();
            sb.AppendLine("[Event \"Sokol Chess\"]");
            sb.AppendLine($"[Date \"{Started:yyyy.MM.dd}\"]");
            sb.AppendLine($"[White \"{white}\"]");
            sb.AppendLine($"[Black \"{black}\"]");
            sb.AppendLine($"[Result \"{res}\"]");
            sb.AppendLine();
            var line = new StringBuilder();
            for (int i = 0; i < SanIntl.Count; i++)
            {
                if (i % 2 == 0) line.Append(i / 2 + 1).Append(". ");
                line.Append(SanIntl[i]).Append(' ');
            }
            line.Append(res);
            sb.AppendLine(line.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Background engine analysis of every position of the game (full strength, MultiPV 2).
    /// Results are delivered on the UI thread.
    /// </summary>
    sealed class AnalysisService : IDisposable
    {
        sealed class Task
        {
            public int Ply;
            public ulong Hash;
            public int[] Moves;
            public string StartFen;
            public volatile bool Cancelled;
        }

        sealed class Worker
        {
            public Thread Thread;
            public Searcher Searcher;
            public Task Current;
            public volatile bool Exit;
        }

        readonly Control ui;
        readonly object sync = new object();
        readonly List<Worker> workers = new List<Worker>();
        /// <summary>(ply, hash) pairs already analyzed and posted to the UI.</summary>
        readonly HashSet<(int, ulong)> completed = new HashSet<(int, ulong)>();
        List<Task> pending = new List<Task>();
        int priorityPly = -1;
        bool disposed;

        public int TimeMs = 1000;
        public int MultiPv = 2;
        /// <summary>(ply, hash, analysis, isFinal) - raised on the UI thread.</summary>
        public event Action<int, ulong, PositionAnalysis, bool> Result;

        public AnalysisService(Control ui) { this.ui = ui; }

        public int WorkerCount
        {
            get { lock (sync) return workers.Count; }
            set
            {
                lock (sync)
                {
                    while (workers.Count < value)
                    {
                        var w = new Worker { Searcher = new Searcher(32) };
                        w.Thread = new Thread(() => Loop(w)) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "analysis" };
                        workers.Add(w);
                        w.Thread.Start();
                    }
                    while (workers.Count > value)
                    {
                        var w = workers[workers.Count - 1];
                        workers.RemoveAt(workers.Count - 1);
                        w.Exit = true;
                        w.Searcher.Stop();
                    }
                    Monitor.PulseAll(sync);
                }
            }
        }

        /// <summary>Synchronizes the task list with the game (call after every change).</summary>
        public void Update(Game game, int priority)
        {
            lock (sync)
            {
                priorityPly = priority;
                var running = new HashSet<int>();
                foreach (var w in workers)
                {
                    var t = w.Current;
                    if (t == null || t.Cancelled) continue;
                    bool valid = t.Ply < game.Hashes.Count && game.Hashes[t.Ply] == t.Hash && game.Analyses[t.Ply] == null;
                    if (!valid) { t.Cancelled = true; w.Searcher.Stop(); }
                    else running.Add(t.Ply);
                }
                var list = new List<Task>();
                for (int ply = 0; ply < game.Hashes.Count; ply++)
                {
                    if (game.Analyses[ply] != null || running.Contains(ply)) continue;
                    if (completed.Contains((ply, game.Hashes[ply]))) continue; // result is on its way to the UI
                    var moves = new int[ply];
                    game.Moves.CopyTo(0, moves, 0, ply);
                    list.Add(new Task { Ply = ply, Hash = game.Hashes[ply], Moves = moves, StartFen = game.StartFen });
                }
                pending = list;
                Monitor.PulseAll(sync);
            }
        }

        public int PendingCount { get { lock (sync) { int n = pending.Count; foreach (var w in workers) if (w.Current != null && !w.Current.Cancelled) n++; return n; } } }

        void Loop(Worker w)
        {
            while (true)
            {
                Task task;
                lock (sync)
                {
                    while (!w.Exit && !disposed && pending.Count == 0) Monitor.Wait(sync);
                    if (w.Exit || disposed) return;
                    int idx = pending.FindIndex(t => t.Ply == priorityPly);
                    if (idx < 0)
                    {
                        idx = 0;
                        for (int i = 1; i < pending.Count; i++) if (pending[i].Ply < pending[idx].Ply) idx = i;
                    }
                    task = pending[idx];
                    pending.RemoveAt(idx);
                    w.Current = task;
                }

                PositionAnalysis result = null;
                try { result = Analyze(w, task); }
                catch (Exception) { result = null; }

                bool ok = result != null && !task.Cancelled && !w.Exit;
                lock (sync)
                {
                    if (w.Current == task) w.Current = null;
                    if (ok) completed.Add((task.Ply, task.Hash));
                }
                if (ok) Post(task, result, true);
            }
        }

        PositionAnalysis Analyze(Worker w, Task task)
        {
            var b = new Board(task.StartFen);
            foreach (int m in task.Moves) b.MakeMove(m);
            var a = new PositionAnalysis { Hash = b.Hash };
            if (!b.HasLegalMove())
            {
                a.Terminal = true;
                a.TerminalScore = b.InCheck() ? -Score.Mate : 0;
                return a;
            }
            if (b.IsInsufficientMaterial() || b.RepetitionCount() >= 2 || b.HalfmoveClock >= 100)
            {
                a.Terminal = true;
                a.TerminalScore = 0;
                return a;
            }
            int lastPostDepth = 0;
            var info = w.Searcher.Search(b, new SearchLimits { TimeMs = TimeMs, MultiPV = MultiPv, MaxDepth = 40 }, i =>
            {
                // Live updates for the position the user is looking at.
                if (task.Ply == priorityPly && !task.Cancelled && i.Depth >= 6 && i.Depth > lastPostDepth)
                {
                    lastPostDepth = i.Depth;
                    Post(task, new PositionAnalysis { Hash = task.Hash, Depth = i.Depth, Lines = i.Lines }, false);
                }
            });
            a.Depth = info.Depth;
            a.Lines = info.Lines;
            return a.Lines.Count > 0 ? a : null;
        }

        void Post(Task task, PositionAnalysis a, bool final)
        {
            try
            {
                if (ui.IsHandleCreated && !ui.IsDisposed)
                    ui.BeginInvoke((Action)(() => Result?.Invoke(task.Ply, task.Hash, a, final)));
            }
            catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                foreach (var w in workers) { w.Exit = true; w.Searcher.Stop(); }
                Monitor.PulseAll(sync);
            }
        }
    }
}
