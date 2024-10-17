using System;
using System.Collections.Generic;
using System.Linq;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public interface IQueueBasic
    {
        void Enqueue(Job job, bool isReplace = true);
        Job Dequeue();
        Job Peek();
        int Count();
        void Clear();
    }
    public class QueueBasic : IQueueBasic
    {
        protected List<Job> _jobs;
        public List<Job> Jobs => _jobs;
        public QueueBasic() { _jobs = new List<Job>(); }
        public virtual void Enqueue(Job job, bool isReplace = true)
        {
            if (!isReplace && _jobs.Any(rs => rs.Name == job.Name))
                throw new InvalidOperationException($"'{job.Name}' đã tồn tại trong hàng đợi.");
            else if (isReplace)
            {
                int index = GetIndexJobByName(job.Name);
                if (index != -1) _jobs[index] = job;
                else _jobs.Add(job);
            }
        }
        public virtual Job Dequeue()
        {
            if (_jobs.Count == 0)
                throw new InvalidOperationException("Hàng đợi trống.");
            Job job = _jobs[0];
            _jobs.RemoveAt(0);
            return job;
        }
        public virtual Job Peek()
        {
            if (_jobs.Count == 0)
                throw new InvalidOperationException("Hàng đợi trống.");
            return _jobs[0];
        }
        public int Count() => _jobs.Count;
        public void Clear() => _jobs.Clear();
        public Job GetJobtByName(string name) => _jobs.FirstOrDefault(job => job.Name == name);
        public int GetIndexJobByName(string name) => _jobs.FindIndex(job => job.Name == name);
        public Job this[int index]
        {
            get => _jobs[index];
            protected set => _jobs[index] = value;
        }
    }
}
