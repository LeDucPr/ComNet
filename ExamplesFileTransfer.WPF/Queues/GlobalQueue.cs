using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public class GlobalQueue : QueueBasic
    {
        private const int maxCloneJob = 3; // số lượng job clone tối đa
        public int MaxCloneJob => maxCloneJob;
        public GlobalQueue() : base() { }
        public override Job Peek() => (Job)Peek().Clone();
        public override void Enqueue(Job job, bool isReplace = true)
        {
            // nếu đã tồn tại job này và đã xử lý hoặc lỗi xử lý thì không cho vào 
            if (GetIndexJobByName(job.Name) != -1 && !(job.IsHandled || job.IsError))
                return;
            base.Enqueue(job, isReplace);
        }
        public Action<Job> JobChange => job => // ghi đè các job đã hoàn thành 
        {
            int index = GetIndexJobByName(job.Name);
            if (index != -1)
            {
                this[index] = job;
                GC.Collect();
                GC.WaitForFullGCApproach();
            }
        };
        /// <summary>
        /// Tải lên hàng đợi từ global cho các queue khác
        /// </summary>
        public Action<Job, IQueueBasic> TransferJob => (job, targetQueue) =>
        {
            if (targetQueue == null)
                throw new ArgumentNullException(nameof(targetQueue));
            var jobToTransfer = Peek();
            if (jobToTransfer != null && jobToTransfer.Name == job.Name)
                targetQueue.Enqueue(jobToTransfer);
        };
    }
}
