using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public class QueueManagement
    {
        private GlobalQueue _gQueue;
        private LocalQueue _lQueue;
        public GlobalQueue GQueue => _gQueue;
        public LocalQueue LQueue => _lQueue;
        private int _maxJobsInLocalQueue = 5;
        private CancellationTokenSource _cancellationTokenSource; // hủy task khi tắt 
        private Task _transferGlobalToLocalTask;
        private Task _transferLocalToGlobalTask;
        int _delay = 0;
        public int MaxClone => _gQueue.MaxCloneJob; // biến này để kiểm soát lượt gửi qua các máy khác nhau (chỉ áp dụng cho job chửa xử lý)
        public QueueManagement()
        {
            _gQueue = new GlobalQueue();
            _lQueue = new LocalQueue();
            _transferGlobalToLocalTask = Task.Run(() => TransferJobsFromGlobalToLocal(_cancellationTokenSource.Token));
            _transferLocalToGlobalTask = Task.Run(() => TransferJobsFromLocalToGlobal(_cancellationTokenSource.Token));
        }

        private void TransferJobsFromGlobalToLocal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_lQueue.Count() < _maxJobsInLocalQueue && _gQueue.Count() > 0)
                {
                    var job = _gQueue.Dequeue();
                    if ((job?.IsHandled != null || !job.IsHandled) || !job.IsError)
                        _lQueue.Enqueue(job);
                    if (_delay != 0)
                        Task.Delay(_delay).Wait(); 
                }
            }
        }

        private void TransferJobsFromLocalToGlobal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_lQueue.Count() > 0)
                {
                    var job = _lQueue.Dequeue();
                    if ((job?.IsHandled != null || job.IsHandled) || job.IsError)
                        _gQueue.Enqueue(job);
                    if (_delay != 0)
                        Task.Delay(_delay).Wait(); 
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            Task.WaitAll(_transferGlobalToLocalTask, _transferLocalToGlobalTask);
        }
    }
}
