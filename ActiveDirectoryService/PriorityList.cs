using System.Collections.Generic;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class PriorityList
    {
        private readonly Dictionary<string, int> paths = new Dictionary<string, int>();

        public PriorityList()
        {
        }

        private PriorityList(PriorityList list)
        {
            paths = new Dictionary<string, int>(list.paths);
        }

        internal PriorityList Clone()
        {
            return new PriorityList(this);
        }

        internal bool ContainsKey(string path)
        {
            return paths.ContainsKey(path);
        }

        internal void Promote(string path)
        {
            paths[path] = 1;
        }

        internal void Demote(string path)
        {
            if (!paths.ContainsKey(path))
            {
                paths[path] = 0;
            }
            paths[path] -= 1;
        }

        internal IEnumerable<string> GetPromoted()
        {
            return paths.Where(o => o.Value >= 0).Select(o => o.Key);
        }

        internal IEnumerable<string> GetDemoted()
        {
            return paths.Where(o => o.Value < 0).OrderByDescending(o => o.Value).Select(o => o.Key);
        }
    }
}