using System;
using System.Collections.Generic;

namespace BorrowedHex.Core
{
    /// <summary>
    /// Delayed actions on gameplay time (echo volleys, delayed spawns, telegraph follow-ups).
    /// Because it is driven by the gameplay clock, pause freezes pending work for free, and
    /// CancelAll on death/restart guarantees nothing leaks into results or the next run.
    /// </summary>
    public sealed class GameplayScheduler
    {
        struct Item
        {
            public double Due;
            public long Seq;
            public Action Action;
        }

        readonly List<Item> items = new List<Item>();
        readonly List<Item> ready = new List<Item>();
        long seq;
        // Incremented by CancelAll so an action scheduled by a callback that is running
        // while the run ends cannot sneak back into the queue of the next run.
        int generation;

        public int PendingCount => items.Count;

        public void Schedule(double due, Action action)
        {
            if (action == null) return;
            items.Add(new Item { Due = due, Seq = seq++, Action = action });
        }

        public void RunDue(double now)
        {
            int gen = generation;
            // Loop because a due callback may schedule more work that is also already due.
            while (true)
            {
                ready.Clear();
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (items[i].Due <= now + 1e-9)
                    {
                        ready.Add(items[i]);
                        items.RemoveAt(i);
                    }
                }
                if (ready.Count == 0) return;
                // Deterministic order: by due time, then by scheduling order.
                ready.Sort((a, b) => a.Due != b.Due ? a.Due.CompareTo(b.Due) : a.Seq.CompareTo(b.Seq));
                foreach (var it in ready)
                {
                    if (gen != generation) return; // cancelled mid-flush (e.g. player died)
                    it.Action();
                }
            }
        }

        public void CancelAll()
        {
            items.Clear();
            ready.Clear();
            generation++;
        }
    }
}
