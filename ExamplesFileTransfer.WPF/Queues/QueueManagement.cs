﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public class QueueManagement
    {
        private GlobalQueue _gQueue;
        private LocalQueue _lQueue;
        private List<string> _jobNeedToHandler;
        public void AddTo_JobNeedToHandler(string jobName) { _jobNeedToHandler.Add(jobName); }
        private List<string> _gQueueLogJobReceive; // lưu lại các job nhận được từ global queue
        private const int _maxLog = 100;
        public GlobalQueue GQueue => _gQueue;
        public LocalQueue LQueue => _lQueue;
        private int _maxJobsInLocalQueue = 5;
        private CancellationTokenSource _cancellationTokenSource; // hủy task khi tắt 
        public CancellationTokenSource CancellationTokenSource { set => _cancellationTokenSource = value; }
        private Task _transferGlobalToLocalTask;
        private Task _transferLocalToGlobalTask;
        int _delay = 0;
        private string _gQueueLog = string.Empty;
        private string _lQueueLog = string.Empty;
        public string GQueueLog => _gQueueLog;
        public string LQueueLog => _lQueueLog;
        public int MaxClone => _gQueue.MaxCloneJob; // biến này để kiểm soát lượt gửi qua các máy khác nhau (chỉ áp dụng cho job chửa xử lý)
        private string _deviceId;
        public string DeviceId
        {
            set // lấy mã của thằng rsa để tăng tính ngẫu nhiên khi tạo id máy trên các cấu hình khác nhau 
            {
                using (RSA rsa = RSA.Create())
                {
                    byte[] randomBytes = new byte[32];
                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(randomBytes);
                    }
                    byte[] encryptedBytes = rsa.Encrypt(randomBytes, RSAEncryptionPadding.OaepSHA256);
                    _deviceId = Convert.ToBase64String(encryptedBytes);
                }
            }
            get => _deviceId;
        }

        public QueueManagement()
        {
            _gQueue = new GlobalQueue();
            _lQueue = new LocalQueue();
            _jobNeedToHandler = new List<string>();
            _gQueueLogJobReceive = new List<string>();
            _transferGlobalToLocalTask = Task.Run(() => TransferJobsFromGlobalToLocal(_cancellationTokenSource.Token));
            // Kiến trúc thay đổi được truyền thẳng từ local queue sang TCP sender
            //_transferLocalToGlobalTask = Task.Run(() => TransferJobsFromLocalToGlobal(_cancellationTokenSource.Token)); 
            _transferLocalToGlobalTask = Task.Run(() => ManagerSpawner(_cancellationTokenSource.Token));
            _transferLocalToGlobalTask = Task.Run(() => ManagerHandler(_cancellationTokenSource.Token));
        }

        private void TransferJobsFromGlobalToLocal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
            {
                if (_lQueue.Count() < _maxJobsInLocalQueue && _gQueue.Count() > 0)
                {
                    int count = _lQueue.Count();
                    var job = _gQueue.Dequeue();
                    if ((job?.IsHandled != null && !job.IsHandled) || !job.IsError)
                        _lQueue.Enqueue((Job)job.Clone()); // clone để không ảnh hưởng đến việc xóa trong việc xóa tập tin nhận được 
                    if (_delay != 0)
                        Task.Delay(_delay).Wait();
                    if (count != _lQueue.Count())
                        _lQueueLog = $"-->> (LocalQueue): {job.Name}";
                }
            }
        }

        private void TransferJobsFromLocalToGlobal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
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
        #region Queue Manager 
        /// <summary>
        /// Xử lý xong đẩy lại vào local queue theo kiến trúc mới 
        /// </summary>
        /// <param name="cancellationToken"></param>
        private void ManagerHandler(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
            {
                if (_lQueue.Count() != 0)
                {
                    if (_jobNeedToHandler.Count() != 0)
                    {
                        _lQueue.SetJobToFirst(_jobNeedToHandler.First());
                        _jobNeedToHandler.RemoveAt(0);
                    }
                    if (_lQueue.Peek().DeviceId != _deviceId) // cần tính tới tường hợp Job spawn ra xếp trước thì không xử lý được
                    {
                        Job job = _lQueue.Dequeue();
                        if (JobHandler(job))
                            job.StatusChange(JobStatus.Complete);
                        else
                            job.StatusChange(JobStatus.Error);
                        _lQueue.Enqueue(job);
                    }
                }
            }
        }
        /// <summary>
        /// Tạo job và đẩy vào local queue theo kiến trúc mới cho phép hoạt động độc lập thành 2 phiên bản
        /// </summary>
        /// <param name="cancellationToken"></param>
        private void ManagerSpawner(CancellationToken cancellationToken)
        {
            for (int i = 0; i < 5; i++)
            {
                _lQueue.Enqueue(JobSpawner(i.ToString()));
                Task.Delay(100).Wait(); // tránh spawn fff bị ghi đè job
            }
        }
        #endregion 

        #region Job Handler
        private bool JobHandler(Job job)
        {
            string data = job.Data;
            if (int.TryParse(data, out int result))
            {
                for (int i = 2; i <= Math.Sqrt(result); i++)
                    if (result % i == 0)
                        return false;
                return true;
            }
            return false;
        }
        private Job JobSpawner(string data)
        {
            Job job = new Job(data);
            job.DeviceId = _deviceId;
            job.StatusChange(JobStatus.Initial);
            return job;
        }
        #endregion

        public void TransferJobsFromGlobalToLocal()
        {// dùng cái này thì tắt task đi 
            if (_lQueue.Count() < _maxJobsInLocalQueue && _gQueue.Count() > 0)
            {
                int count = _lQueue.Count();
                var job = _gQueue.Dequeue();
                if ((job?.IsHandled != null && !job.IsHandled) || !job.IsError)
                    _lQueue.Enqueue(job);
                if (_delay != 0)
                    Task.Delay(_delay).Wait();
                if (count != _lQueue.Count())
                    _lQueueLog = $"-->> (LocalQueue): {job.Name}";
            }
        }
        public void TransferJobsFromLocalToGlobal()
        {// dùng cái này thì tắt task đi 
            if (_lQueue.Count() > 0)
            {
                var job = _lQueue.Dequeue();

                if ((job?.IsHandled != null || job.IsHandled) || job.IsError)
                    _gQueue.Enqueue(job);
                if (_delay != 0)
                    Task.Delay(_delay).Wait();
            }
        }

        public Job TransferJobToTCPSender_Job()
        {
            if (_lQueue.Count() > 0)
                return _lQueue.Dequeue();
            return null;
        }
        public (string, Job) TransferJobToTCPSender_Job_Name()
        {
            if (_lQueue.Count() > 0)
            {
                var job = _lQueue.Dequeue();
                return (job.Name, job);
            }
            return (string.Empty, null);
        }
        public (string, Stream) TransferJobToTCPSender_Job_Stream()
        {
            if (_lQueue.Count() > 0)
            {
                var job = _lQueue.Dequeue();
                return (job.Name, Job.Send(job));
            }
            return (string.Empty, null);
        }

        /// <summary>
        /// Cho job vào global queue và lưu lại tên job đã nhận, xóa tên job cũ nếu vượt quá giới hạn
        /// </summary>
        /// <param name="job"></param>
        public void TransferJobToGlobalQueue(Job job)
        {
            if (_gQueueLogJobReceive.Contains(job.Name)) return;
            if (!job.IsHandled && !job.IsError)
            {
                _gQueue.Enqueue(job);
                _gQueueLogJobReceive.Add(job.Name);
            }
            if (_gQueueLogJobReceive.Count() > _maxJobsInLocalQueue)
                _gQueueLogJobReceive.RemoveAt(0);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            Task.WaitAll(_transferGlobalToLocalTask, _transferLocalToGlobalTask);
        }
    }
}